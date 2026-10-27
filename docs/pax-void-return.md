# Scales Connect: PAX Voids, Returns and Transaction Lookup

How Odoo reverses a card payment through Scales Connect, checks which terminal a register is connected to, and recovers the outcome of a call whose response was lost. Scales Connect is a Windows app that sits between the POS and a PAX payment terminal (PAX POSLink 2 Semi-Integration SDK, v2.02.00). Each register PC runs its own Scales Connect at `http://localhost:5000`, connected to its own terminal.

This document builds on `pax-credit-response.md`, which describes the Sale call and the response format. Everything there still applies.

## 1. Detecting the update

Registers will not all update at once. Before sending any field from this document to a register, call `GET /api/pax/status` (section 8) and check the `features` array.

| Feature | Enables |
|---|---|
| `void_by_reference` | Void with no amount, identified by `originalReferenceNumber` (section 4). **Don't send a Void on this alone.** |
| `void_original_check` | Before a Void, Scales Connect confirms the record really is that sale (section 4.1). **Send a Void only to a register that reports this.** Otherwise use a Return. |
| `terminal_sn_guard` | `originalTerminalSn` check (section 3.4). |
| `return_original_fields` | `originalReferenceNumber`, `originalTransactionDate`, `originalTransactionTime`, `originalLast4` on a Return (section 5). |
| `card_not_present_return` | `cardPresent: false` on a Return (section 6). Accepted, **not yet verified on First Data**. |
| `sale_token_request` | `requestToken: true` on a Sale, and `token` in responses (section 6.3). |
| `transaction_lookup` | `GET /api/pax/transaction` (section 9). |
| `batch_close_details` | Terminal serial, batch number and close time from batch close (section 10). |

Check features, not `version`. `version` is informational only. If `/api/pax/status` returns 404, the register is on an older build: send only a plain card-present Return with an amount (`transactionType`, `ecrReferenceNumber`, `amount`), which works on every version.

## 2. Void or Return?

| | **Void** | **Return** |
|---|---|---|
| What it does | Cancels the original transaction. The customer is never charged. | Issues a new credit to a card. The charge and the refund both appear. |
| When it works | Only while the original sale is in the terminal's **open batch**. | Any time. |
| Amount | **None.** Always the full original amount. | Required. Full or partial. |
| Card needed? | **No.** | **Yes**, unless a card-not-present Return works for this processor (section 6). |
| Which terminal | **Only the terminal that ran the sale.** Record numbers are per terminal, and every terminal starts at 1. | Any terminal. |

**Rule for Odoo:**

1. Full refund, register reports `void_original_check`, sale made on this register's terminal (`terminalSn` from status equals the sale's stored serial), the sale has a real ECR reference (not `"/"`), and no known batch close on that terminal since the sale (section 10) → **Void**.
2. Partial refund → **Return**.
3. Otherwise, or if the Void is refused or not approved → **Return**.

The batch-close condition in rule 1 is only a hint, because Odoo can't see the terminal's auto-close. The pre-Void check (4.1) is what actually makes a Void safe after an unseen close.

## 3. Request: common rules

All payment calls use `POST http://localhost:5000/api/pax/credit` with `Content-Type: application/json`. Field names are camelCase, and matching is case-insensitive.

### 3.1 `transactionType`

`"Sale"`, `"Return"` or `"Void"`, case-insensitive. Omitted or empty means `"Sale"`. Any other value is rejected with HTTP 400. (Older builds treated an unrecognized value as a Sale.)

### 3.2 `ecrReferenceNumber`

Required on every call. Use a new, unique value per call. Odoo's format (`S-`/`R-`/`V-`/`B-` plus 10 uppercase letters and digits, 12 characters) fits PAX's limit of 32 characters. PAX documents the value as echoed back unchanged, both in the top-level `ecrReferenceNumber` and in `TraceInformation.EcrReferenceNumber`. It is also the search key for the lookup (section 9).

### 3.3 `amount`

Dollars as a decimal string with at most two decimal places, for example `"4.36"`. It must be greater than 0. Scales Connect converts it to cents. The amount is required on Sale and Return, and **ignored on a Void**. A Void can instead carry `originalAmount` (dollars, same format), which is checked against the sale (4.1) and never sent to the terminal. Invalid amounts get HTTP 400; older builds answered HTTP 500.

### 3.4 `originalTerminalSn` (terminal guard)

The serial number of the terminal that ran the original sale. Store it from the sale's `terminalSn` (top-level, see section 7), or from `TraceInformation.Sn` in `rawResponse`.

When `originalTerminalSn` is sent, Scales Connect reads the connected terminal's serial number (POSLink Init, a short handshake that runs no transaction) **before** sending the transaction:

| Situation | Void | Card-not-present Return | Card-present Return |
|---|---|---|---|
| Serials match | Sent | Sent | Sent with record references |
| Serials differ | **Refused: `TERMINAL_MISMATCH`, HTTP 409. Transaction not sent.** | **Refused: `TERMINAL_MISMATCH`, HTTP 409** | Sent **without** `originalReferenceNumber`/`originalEcrReferenceNumber` (they would point at another customer's record) |
| Serial can't be read | Refused: `TERMINAL_SN_UNAVAILABLE`, HTTP 503 | Refused: `TERMINAL_SN_UNAVAILABLE`, HTTP 503 | Refused: `TERMINAL_SN_UNAVAILABLE`, HTTP 503 |
| Terminal is busy | Refused: `TERMINAL_BUSY`, HTTP 409 | Refused: `TERMINAL_BUSY`, HTTP 409 | Refused: `TERMINAL_BUSY`, HTTP 409 |
| `originalTerminalSn` not sent | Refused: HTTP 400 | Refused: HTTP 400 | Sent **without** record references |

**Always send `originalTerminalSn` on a Void and on any Return that carries `originalReferenceNumber`.** Mismatch response:

```json
{
  "success": false,
  "responseCode": "TERMINAL_MISMATCH",
  "errorMessage": "This sale was made on terminal 1440102625; this register is connected to 1440102999.",
  "ecrReferenceNumber": "V-MVQ2K7D1A4",
  ...
}
```

`TERMINAL_MISMATCH`, `TERMINAL_SN_UNAVAILABLE` and `TERMINAL_BUSY` are stable codes. In each case nothing was sent to the terminal and no money moved. For a refused Void, offer a Return instead.

### 3.5 Format checks (HTTP 400 when wrong)

| Field | Format |
|---|---|
| `originalReferenceNumber` | 1–4 digits |
| `originalTransactionDate` | `YYYYMMDD`, a real date |
| `originalTransactionTime` | `HHMMSS`, a real time |
| `originalLast4` | Exactly 4 digits |
| `cardPresent: false` | Return only |
| `requestToken: true` | Sale only |

A 400 response has `success: false`, an empty `responseCode` and the reason in `errorMessage`. The `original*` fields are ignored on a Sale.

## 4. Void

```json
{
  "transactionType": "Void",
  "ecrReferenceNumber": "V-MVQ2K7D1A4",
  "originalReferenceNumber": "1",
  "originalEcrReferenceNumber": "S-MVPXP9J8N8",
  "originalTerminalSn": "1440102625",
  "originalAmount": "4.36",
  "originalLast4": "6167"
}
```

| Field | Required | Value |
|---|---|---|
| `transactionType` | yes | `"Void"` |
| `ecrReferenceNumber` | yes | New reference for the void. |
| `originalReferenceNumber` | **yes** (400 without it) | The sale's `referenceNumber` (terminal record number). |
| `originalEcrReferenceNumber` | **yes** (400 without it) | The sale's ECR reference. `"/"` is refused (4.1). |
| `originalTerminalSn` | **yes** (400 without it) | The sale's `terminalSn`. See 3.4. |
| `originalAmount` | no, recommended | The sale's amount in dollars, e.g. `"4.36"`. Scales Connect converts it to cents and compares it with the record's `ApprovedAmount` (cents, e.g. `"436"`). Never sent to the terminal. |
| `originalLast4` | no, recommended | Last 4 of the sale's card. Checked against the terminal's record. |
| `amount` | no | Ignored on a Void. |

Scales Connect sends the terminal a PAX Void that carries **only** the record number (`originalReferenceNumber`), so the terminal doesn't ask the cashier for it. No amount and no card are needed. The other `original*` fields are used only by the pre-Void check. The terminal app rejected `OriginalPan` (the last 4) on a Void with `100003 ORIGPAN NOT SUPPORTED`, and it may reject other optional fields the same way.

### 4.1 Pre-Void check (`void_original_check`)

PAX picks the sale to void by record number alone. Record numbers are small and can start over after any batch close, including the terminal's own auto-close, which Odoo can't see. So "record 1" could be a different customer's sale in a new batch. Before sending a Void, Scales Connect therefore:

1. Runs the terminal guard (3.4).
2. Looks the sale up by `originalEcrReferenceNumber` in the terminal's local report (as in section 9). A batch close clears that report.
3. Sends the Void only if exactly one approved record is found, and that record:
   - is a Sale,
   - has record number `originalReferenceNumber`,
   - was made on `originalTerminalSn` (when the record reports a serial),
   - has approved amount `originalAmount` (if sent; compared in cents),
   - has a card ending in `originalLast4` (if sent).

   If `originalAmount` or `originalLast4` is sent but the terminal's record doesn't report that value, the Void is refused.

   The comparison is against the terminal's **approved** amount. If the terminal ever adds something itself (tip, cashback, surcharge), the approved amount is higher than what Odoo stored, and the sale gets `ORIGINAL_MISMATCH`, so it falls back to a Return. On the live sale all of those were 0. Revisit this if cashback or tipping is turned on at the terminal.

If anything can't be confirmed, the Void is refused with an HTTP error. The terminal is never asked to void, and no money moves:

| `responseCode` | HTTP | Meaning |
|---|---|---|
| `ORIGINAL_UNVERIFIABLE` | 422 | `originalEcrReferenceNumber` is `"/"`. Early sales all used `"/"`, so it can't identify one sale. Amount and last 4 alone aren't enough, because the same card paying the same amount twice in a batch is realistic. |
| `ORIGINAL_NOT_FOUND` | 409 | The sale isn't in the terminal's current batch. Usually the batch has closed, perhaps by auto-close. |
| `ORIGINAL_MISMATCH` | 409 | A record was found but something differs (record number, type, terminal, amount, last 4), or more than one record matched. Details in `errorMessage`. |
| `TERMINAL_BUSY` | 409 | The terminal was running something else. |
| `ORIGINAL_CHECK_FAILED` | 503 | The terminal couldn't be reached or couldn't search its report. |
| `TERMINAL_MISMATCH`, `TERMINAL_SN_UNAVAILABLE` | 409, 503 | From the guard (3.4). |

**All of these are definite "not voided" results, never "unknown".** Offer a Return. (`TERMINAL_BUSY` and `ORIGINAL_CHECK_FAILED` can also be retried as a Void after a moment.)

There is a short gap between the check and the Void itself. A batch close landing exactly in that gap would still let the Void hit the wrong record. That is very unlikely, but it is why rule 1 in section 2 still avoids Voids after a known close.

**Result:** `responseCode == "000000"` means voided. Store the void's own `referenceNumber`, `globalUid` and `authorizationCode`. Any other code means the void didn't happen: offer a Return. Don't branch on specific codes; a closed batch or an already-voided sale can each produce different codes depending on the processor.

## 5. Return (card present)

```json
{
  "transactionType": "Return",
  "ecrReferenceNumber": "R-MVQ2K7D1A5",
  "amount": "4.36",
  "originalReferenceNumber": "1",
  "originalTerminalSn": "1440102625",
  "originalTransactionDate": "20261026",
  "originalTransactionTime": "194900",
  "originalLast4": "6167"
}
```

| Field | Required | Value |
|---|---|---|
| `transactionType` | yes | `"Return"` |
| `ecrReferenceNumber` | yes | New reference. |
| `amount` | yes | Refund in dollars. Odoo caps it at the original amount minus earlier refunds. |
| `originalTransactionDate` / `originalTransactionTime` | no, send when known | From the sale's `terminalTimestamp` (`YYYYMMDDhhmmss`): first 8 characters, then last 6. PAX documents these as required by *some* hosts; without them those hosts get today's date. Hosts that don't use them are expected to ignore them (not verified on First Data). |
| `originalLast4` | no | Accepted but **not sent to the terminal**: this First Data app rejects it (`ORIGPAN NOT SUPPORTED`). Use it only for the after-approval card comparison below. |
| `originalReferenceNumber` + `originalTerminalSn` | no | Links the Return to the sale. Dropped automatically on a different terminal (3.4). |

The terminal shows the amount and asks for a card. Tell the cashier: **"Ask the customer to tap or insert their card on the terminal."** After it's approved, compare the last 4 in the return's `rawResponse.AccountInformation.Account` (only if it is 4 digits) with the sale's, and warn the cashier if they differ.

## 6. Return (card not present)

### 6.1 Status: accepted, not verified

Scales Connect accepts `cardPresent: false` and passes the original transaction's identifiers to the terminal. **Neither PAX's SDK documentation nor public First Data documentation says which of these First Data Nashville or Omaha needs for a referenced return, or whether the BroadPOS app must be configured for it.** Treat this as unverified until live test 5 passes. Ask the merchant's First Data/PAX reseller whether "return by reference" (or a TransArmor token return) is enabled in the terminal's BroadPOS app, and what it requires.

### 6.2 Request

```json
{
  "transactionType": "Return",
  "ecrReferenceNumber": "R-MVQ2K7D1A6",
  "amount": "4.36",
  "cardPresent": false,
  "originalTerminalSn": "1440102625",
  "originalReferenceNumber": "1",
  "originalTransactionDate": "20261026",
  "originalTransactionTime": "194900",
  "originalLast4": "6167",
  "originalHostReferenceNumber": "0",
  "originalTransactionIdentifier": "586299857548691"
}
```

What Scales Connect does with each field:

| Field | Sent to the terminal as |
|---|---|
| `cardPresent: false` | `TransactionBehavior.CardPresentMode = AssumeCardNotPresent` |
| `originalReferenceNumber` | `TraceInformation.OriginalReferenceNumber` (terminal record; requires a matching `originalTerminalSn`) |
| `originalTransactionDate` / `Time` | `TraceInformation.OriginalTransactionDate` / `Time` |
| `originalLast4` | Not sent (`ORIGPAN NOT SUPPORTED` on this terminal app) |
| `originalHostReferenceNumber` | `HostTraceInformation.HostReferenceNumber`, **only if not empty and not `"0"`** |
| `originalTransactionIdentifier` | `CofInformation.OriginalTransactionIdentifier` |
| `token` | `CofInformation.Token` (6.3) |

`originalTerminalSn` is **required** with `cardPresent: false` (400 without it), and the serial must match (3.4). The terminal record is only meaningful on the terminal that ran the sale, and a wrong match would refund a different customer's card. So a card-not-present Return can only run on the register whose terminal made the sale.

`originalAuthorizationCode` and `originalGlobalUid` are accepted but **not forwarded**. PAX's only request field for an authorization code is for forced (voice-authorized) sales, and the request `GlobalUid` is not documented as a reference to an earlier transaction.

### 6.3 Tokens

Send `"requestToken": true` on a Sale to ask the processor for a card token. When the processor returns one, it appears as top-level `token` in the response. Store it (it is not the card number) and send it as `token` on a card-not-present Return. PAX notes that the host decides whether a token comes back, with or without the flag. If `token` stays empty on approved sales, this processor/app setup doesn't return tokens.

### 6.4 Result

- `responseCode == "000000"` → refunded. Record it as in 5.
- Anything else → not refunded. Ask the cashier for the card and send a card-present Return.
- If the terminal can't do card-not-present returns, it will most likely prompt for a card or reject the request. Scales Connect can't tell this apart in advance, so there is no dedicated "not supported" code. Until live test 5 passes, show the cashier: "If the terminal asks for a card, have the customer present it."

## 7. New top-level response fields

Sale, Return and Void responses (and lookup results, section 9) now include these top-level values, so Odoo doesn't need to parse `rawResponse` for them:

| Field | Source | Store on the payment for |
|---|---|---|
| `referenceNumber` | `TraceInformation.ReferenceNumber` | Void (`originalReferenceNumber`) |
| `terminalSn` | `TraceInformation.Sn` | Void/Return (`originalTerminalSn`) |
| `globalUid` | `TraceInformation.GlobalUid` | Audit |
| `terminalTimestamp` | `TraceInformation.TimeStamp` (`YYYYMMDDhhmmss`, terminal local time) | Return date/time; Void-or-Return choice |
| `approvedAmount` | `AmountInformation.ApprovedAmount` (cents) | Refund cap |
| `token` | `CofInformation.Token`, or null | Card-not-present Return (6.3) |

Existing fields are unchanged. Approval is still decided only by `responseCode == "000000"`. Codes added by Scales Connect (`TERMINAL_MISMATCH`, `TERMINAL_SN_UNAVAILABLE`, `TERMINAL_BUSY`, `ORIGINAL_UNVERIFIABLE`, `ORIGINAL_NOT_FOUND`, `ORIGINAL_MISMATCH`, `ORIGINAL_CHECK_FAILED`, `NOT_FOUND`, `TERMINAL_UNREACHABLE`, `LOOKUP_FAILED`) are never `"000000"`.

## 8. Status: `GET /api/pax/status`

```json
{
  "version": "1.0.9",
  "features": ["void_by_reference", "void_original_check", "terminal_sn_guard", "return_original_fields", "card_not_present_return",
               "sale_token_request", "transaction_lookup", "batch_close_details"],
  "connected": true,
  "busy": false,
  "terminalSn": "1440102625",
  "terminalModel": "A35",
  "terminalAppName": "BroadPOS ...",
  "terminalAppVersion": "...",
  "demoMode": null,
  "lastBatchCloseUtc": null,
  "checkedAtUtc": "2026-10-27T15:04:05Z",
  "errorMessage": null
}
```

(Example values; `terminalModel` and the app fields are whatever the terminal reports.)

- Always HTTP 200. `connected: false` with `errorMessage` means the terminal didn't answer; warn before a refund.
- `busy: true`: a transaction is running. The terminal is **not** contacted, and the terminal fields are the last known values. Never treat a busy status as proof of which terminal is connected; the guard (3.4) refuses while busy for this reason.
- The answer takes up to about 5 seconds when the terminal is unreachable.
- `demoMode` is always `null`: POSLink has no documented way to read it. The terminal is live per the merchant.
- `lastBatchCloseUtc` only covers batch closes run through this Scales Connect since it last started (section 10). It does **not** include the terminal's auto-close, host-initiated closes or closes from the terminal menu, and it resets to `null` when Scales Connect restarts. Use it as a hint, never as proof that no close happened.

## 9. Lookup: `GET /api/pax/transaction?ecrReferenceNumber=R-MVPXP9J8N8`

Searches the terminal's local transaction report (POSLink LocalDetailReport) for **successful** transactions with that ECR reference.

| Result | HTTP | Body |
|---|---|---|
| Found | 200 | `found: true`, `responseCode: "000000"`, plus the same top-level fields as the original call's response (section 7, `authorizationCode`, `hostReferenceNumber`, etc.), and `transactionType` (`"Sale"`, `"Return"`, ...) and `last4` (null if the terminal doesn't report it). `rawResponse` holds the report record, not a DoCredit response, so its shape differs. |
| Not found | 200 | `found: false`, `responseCode: "NOT_FOUND"` |
| Terminal busy | 409 | `found: false`, `responseCode: "TERMINAL_BUSY"` |
| Terminal unreachable | 503 | `found: false`, `responseCode: "TERMINAL_UNREACHABLE"` |
| Terminal couldn't search | 502 | `found: false`, `responseCode: "LOOKUP_FAILED"`, reason in `errorMessage` |

`NOT_FOUND` comes back only when the terminal answers PAX code `100023` ("transaction not found"), or a successful search with zero records. Any other answer is reported as `LOOKUP_FAILED`, never as not found.

**When `NOT_FOUND` is safe to act on.** The local report normally covers only the current open batch, and a batch close clears it. So `NOT_FOUND` means "no approved record in the current batch". Treat it as "never completed, safe to retry" **only if no batch close has happened on that terminal since the attempt**. Check Odoo's own batch-close records (section 10), `lastBatchCloseUtc` in the response, and whether the terminal is set to auto-close (common on First Data setups). If a close may have happened, keep the refund marked as unknown and check the processor's records.

On `TERMINAL_BUSY`, `TERMINAL_UNREACHABLE` or `LOOKUP_FAILED`, keep the refund marked as unknown and try the lookup again later.

## 10. Batch close: `POST /api/closing/batch`

The request is unchanged. The response now also includes:

| Field | Meaning |
|---|---|
| `terminalSn` | Serial of the terminal whose batch closed (read just before closing; null if it couldn't be read). |
| `batchNumber` | `HostInformation.BatchNumber` from the close. |
| `terminalTimestamp` | Terminal's close time, `YYYYMMDDhhmmss`. |
| `closedAtUtc` | When Scales Connect received the result. |
| `tid`, `mid` | Processor terminal and merchant IDs, when returned. |
| `hostResponse` | Processor message, when returned. |

The close succeeded only if `responseCode == "000000"`. Record `terminalSn` + `closedAtUtc` per terminal, and offer a Void only for sales on that terminal after its last close. This doesn't see closes done from the terminal's own menu, by the terminal's auto-close, or by the host. The pre-Void check (4.1) is what protects a Void in those cases.

## 11. Answers to Odoo's questions

| # | Question | Answer |
|---|---|---|
| 1 | When does the update ship, and in which version? | It's in the next Scales Connect release. Detect it by `features` on `GET /api/pax/status` (section 1), not by version number. |
| 2 | `originalTerminalSn` guard and status endpoint? | Both done (3.4, 8). The guard reads the serial from the terminal with a handshake but sends no transaction on a mismatch. |
| 3 | Which identifiers does First Data need for a card-not-present return? | **Unknown.** Not in PAX's SDK docs or public First Data docs. All candidates are passed through (6.2). Confirm with the reseller and live test 5. |
| 4 | Terminal setting or token? | Unknown, same reason. Token requests are supported (6.3). If live test 5 fails, ask the reseller to enable return-by-reference or TransArmor tokens in BroadPOS. |
| 5 | Lookup by `ecrReferenceNumber`? | Done (section 9), with the batch-close caveat. |
| 6 | 12-character references accepted and echoed? | Within PAX's 32-character limit, and documented as echoed unchanged. Confirm in live test 1. |
| 7 | Original date/time harmless when unused? | PAX documents them as used only by hosts that need them; others are expected to ignore them. Not verified on First Data; covered by live test 4. |
| 8 | Batch close: serial, batch number, close time? | Done (section 10). |
| 9 | Void response fields and type? | The void response has the same `referenceNumber`, `globalUid` and `authorizationCode` fields as a sale, but whether First Data fills them, and whether `TransactionType` comes back 17 or 18, is unverified. Check in live test 1, and accept either value. |
| 10 | Where is First Data's own reference? | Not in the sample response: `HostReferenceNumber`, `BatchNumber` and `HostTraceNumber` were `"0"`; `GatewayTransactionId` and `RetrievalReferenceNumber` were empty. The only processor-side values were `AuthorizationCode` and the card network's `CofInformation.TransactionIdentifier`. Reconcile on authorization code + amount + date + last 4 + terminal serial and record number. Ask the reseller whether the BroadPOS app can be set to return the First Data reference. |

## 12. Live test notes

Odoo's test plan covers these. Additional checks:

- Test 1: confirm `terminalSn` from `/api/pax/status` equals the sale's `TraceInformation.Sn`. The guard compares the two, and they come from different PAX commands.
- Test 1: before the Void, run a lookup on the sale's `S-` reference. Confirm that `totalRecord` is `"1"`, `transactionType` is `"Sale"`, `referenceNumber` matches, `approvedAmount` is in cents (`"436"` for a $4.36 sale, the same as in the sale's own response), and `last4` is filled. Then void that sale with `originalAmount: "4.36"` and expect `"000000"`. If the lookup's `approvedAmount` is empty or in a different unit, or `last4` is empty, the pre-Void check refuses every Void that sends `originalAmount`/`originalLast4` (`ORIGINAL_MISMATCH`, amount and card details in `errorMessage`). That fails safe, but it blocks all Voids, so report it so the check can be adjusted.
- Pre-Void check: void a sale with a wrong `originalReferenceNumber` (expect 409 `ORIGINAL_MISMATCH`). Void a sale from before the last batch close (expect 409 `ORIGINAL_NOT_FOUND`). Send `originalEcrReferenceNumber: "/"` (expect 422 `ORIGINAL_UNVERIFIABLE`). The terminal should run nothing in any of these.
- Test 2: expect HTTP 409 `TERMINAL_MISMATCH`. The second register's terminal gets a status handshake but no transaction.
- Test 6: also run a lookup after a batch close, and expect `NOT_FOUND` even for a completed return. This is why section 9 requires the batch-close check.
