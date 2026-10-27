# Scales Connect: PAX Credit Transaction Response

How to read the result of a card transaction run through Scales Connect. Scales Connect is a Windows app that sits between the POS and a PAX payment terminal (PAX POSLink 2 Semi-Integration SDK, v2.02.00).

## 1. The call

`POST http://localhost:5000/api/pax/credit`

Request body:

```json
{
  "amount": "10.50",
  "ecrReferenceNumber": "SO-00042",
  "transactionType": "Sale"
}
```

| Field | Meaning |
|---|---|
| `amount` | Required. Dollars as a decimal string. Scales Connect converts it to cents before sending it to the terminal. |
| `ecrReferenceNumber` | Required. Your own reference (for example, the Odoo order name). It is echoed back. |
| `transactionType` | `"Sale"` (default), `"Return"` or `"Void"`. |

The call blocks until the customer finishes on the terminal or it times out (60 s by default). Cancelling the HTTP request cancels the transaction on the terminal.

## 2. HTTP status codes

| Status | Meaning |
|---|---|
| `200` | The terminal answered. **This does not mean the card was approved.** See section 4. |
| `400` | Bad request: `amount` or `ecrReferenceNumber` is missing. |
| `499` | The caller cancelled the request. |
| `502` | Communication with the terminal failed, the user cancelled, or a terminal-side error occurred. Read `errorMessage`. |
| `500` | Unexpected error inside Scales Connect. Read `errorMessage`. |

## 3. Top-level response body (camelCase JSON)

```json
{
  "success": true,
  "responseCode": "000000",
  "responseMessage": "OK",
  "ecrReferenceNumber": "SO-00042",
  "errorMessage": null,
  "timestamp": "2026-10-26T15:04:05.123Z",
  "cardBin": "411111",
  "authorizationCode": "A1B2C3",
  "batchNumber": "17",
  "controlNumber": null,
  "gatewayTransactionId": "…",
  "hostDetailedMessage": "APPROVAL",
  "hostReferenceNumber": "…",
  "rawResponse": "{ …escaped JSON string, see section 5… }"
}
```

The values above are illustrative.

| Field | Type | Meaning |
|---|---|---|
| `success` | bool | `true` means the terminal finished the exchange and returned a response. It is **not** an approval flag. |
| `responseCode` | string | The terminal's result code. `"000000"` means approved. Any other value means declined or error. |
| `responseMessage` | string | Text that goes with `responseCode` (for example `"OK"`, `"DECLINE"`). |
| `ecrReferenceNumber` | string | Echo of the reference from the request. |
| `errorMessage` | string or null | Set only when `success` is false. |
| `timestamp` | ISO-8601 UTC | When Scales Connect built the response. This is not the processor's timestamp. |
| `cardBin` | string or null | First 6–8 digits of the card (identifies the issuing bank and brand). |
| `authorizationCode` | string or null | Approval code from the processor. Print it on the receipt. |
| `batchNumber` | string or null | The processor's settlement batch. |
| `controlNumber` | string or null | Processor tax-related control number. Usually empty. |
| `gatewayTransactionId` | string or null | Gateway's ID for this transaction. |
| `hostDetailedMessage` | string or null | Extra text from the processor. |
| `hostReferenceNumber` | string or null | Processor's reference for this transaction. Use it for voids, refunds and reconciliation. |
| `rawResponse` | string or null | The full SDK response object, serialized as a JSON string. Only present when `success` is true. |

On failure, only `success`, `ecrReferenceNumber`, `errorMessage`, `timestamp` and empty `responseCode`/`responseMessage` are set. The card and host fields are null.

## 4. How to decide the outcome

1. HTTP status is not 200 → the transaction did not complete. Show `errorMessage`.
2. `success == true` and `responseCode == "000000"` → **approved**. Record the payment.
3. `success == true` and any other `responseCode` → **declined or rejected**. Show `responseMessage`, plus `HostInformation.HostResponseMessage` from `rawResponse` if present.
4. For partial approvals, compare `AmountInformation.ApprovedAmount` (in `rawResponse`, in cents) with the amount requested. A non-zero `AmountInformation.AmountDue` means a balance is still owed.

The meaning of `"000000"` comes from the PAX protocol. Confirm it against a real decline before relying on it.

## 5. `rawResponse` contents

`rawResponse` is a **string** that holds JSON. Parse it a second time. The inner JSON differs from the outer body:

- **Property names are PascalCase** (`HostInformation`, not `hostInformation`).
- **Enums are integers** (for example `"EntryMode": 5`). Value tables are in section 6.
- **Null fields are omitted.** Enum fields are never null, so unset ones appear as `0` (NotSet). Nested objects with nothing filled in may appear as `{}`.
- **Amounts are strings in cents** (`"1050"` = $10.50).
- **Dates are strings**: `YYYYMMDDhhmmss` unless noted otherwise.
- Which fields are filled in depends on the processor and the terminal's configuration. Most fields are usually absent.

### Fields that matter for a POS/ERP

**Top level**
- `ResponseCode`, `ResponseMessage`: same as the outer values.
- `TransactionType` (enum): 2 = Sale, 3 = Return, 17 = Void.
- `EdcType` (string): payment type actually used, for example `"CREDIT"` or `"DEBIT"`.

**`HostInformation`** (the processor's answer)
- `HostResponseCode`, `HostResponseMessage`: the processor's approve/decline code and text.
- `AuthorizationCode`, `HostReferenceNumber`, `GatewayTransactionId`, `BatchNumber`, `RetrievalReferenceNumber`: transaction references.
- `IssuerResponseCode`: the card issuer's code. Useful for explaining a decline.
- `PaymentAccountReferenceId`: **PAR**. A non-sensitive identifier tied to the card, the same every time that card is used. Only present if the processor returns it. This is the preferred way to recognize repeat customers.
- `PrintLine1`–`PrintLine5`: extra receipt lines from the processor.
- `HostEchoData`: the processor's raw response. **Do not store.**
- `AdditionalResponseData`: card-network details (Visa TransID, Mastercard Banknet data, and so on). Usually not needed.

**`AmountInformation`** (cents)
- `ApprovedAmount`: amount actually approved.
- `AmountDue`: remaining balance after a partial approval.
- `TipAmount` / `ApprovedTipAmount`, `CashBackAmount` / `ApprovedCashBackAmount`, `TaxAmount` / `ApprovedTaxAmount`, `MerchantFee` / `ApprovedMerchantFee` (surcharge), `ServiceFee`, `DiscountAmount`.
- `Balance1`, `Balance2`: balances for gift, EBT and prepaid cards.

**`AccountInformation`** (the card)
- `CardType` (enum): brand. Values in section 6.
- `EntryMode` (enum): how the card was read. Values in section 6.
- `CardBin`: first 6–8 digits.
- `HostAccount`: last 4 digits, as reported by the processor.
- `Account`: card number. It is normally masked or the last 4 only, **but the terminal can be configured to return the full number. Never store this field as-is.**
- `CardHolder`: name on the card. Personal data.
- `ExpireDate`: `MMYY`. Do not store.
- `CardPresentIndicator` (enum): 1 = card present, 2 = card not present.
- `CardUniqueIdentifier`: an identifier with no documented meaning. Whether it stays the same across transactions is unverified.
- `Track1Data`, `Track2Data`, `Track3Data`: **never store.**
- `CvdApprovalCode`, `CvdMessage`: CVV check result (not the CVV itself).

**`TraceInformation`**
- `EcrReferenceNumber`: echo of your reference.
- `ReferenceNumber`: the terminal's own record number. Print it on the receipt.
- `TimeStamp`: terminal date and time.
- `Sn`: terminal serial number.
- `GlobalUid`: unique ID for **this transaction**. It is not a customer ID.
- `InvoiceNumber`, `SettlementDate`.

**`CofInformation`** (card-on-file / tokens)
- `Token`: processor token for the card. Only returned if the processor supports tokens, usually only when requested. Whether the same card always gets the same token depends on the processor.
- `TokenSerialNumber`: the processor's vault slot.
- `TransactionIdentifier`: card-network transaction ID. Different for every transaction.

**`HostTraceInformation`**
- `HostTimeStamp`, `HostReferenceNumber`, `HostTraceNumber` (STAN), `EcrTransactionId`.

**`PaymentEmvTag`** (chip and contactless only)
- `AppLabel` / `AppPreferName`: card app name (for example `"VISA CREDIT"`). Goes on EMV receipts.
- `Aid`, `Tvr`, `Tsi`, `Tc` / `Arqc`, `Cvm` (enum): EMV receipt and dispute data. Values in section 6.
- `PanSequenceNumber`: tells apart cards that share a card number. It does not identify a customer.

**`SignatureInformation`**
- `SignatureStatus` (enum), `SignatureData`: captured signature, if any.

**Other objects, usually empty for this merchant:** `AvsInformation` (address check), `CommercialInformation` (PO/customer code for corporate cards), `MotoECommerceInformation`, `Restaurant`, `FleetCard`, `TaxDetails[]`, `VasInformation` (loyalty/wallet passes), `TorInformation` (reversal details), `HostCredentialInformation`, `TransactionBehavior`, `FpsInformation`, `DynamicCurrencyConversion`, `MultiMerchant`, and `PayloadData` (processor-specific free text).

**Deprecated duplicates, ignore:** `PaymentTransactionInformation` and `CardInformation`. Their contents are repeated in the sections above.

## 6. Enum values used in `rawResponse`

**EntryMode:** 0 NotSet, 1 Manual (keyed), 2 Swipe, 3 Contactless (tap), 4 LaserScanner, 5 Chip, 6 ChipFallBackSwipe, 7 FrontCamera, 8 RearCamera

**CardType:** 0 NotSet, 1 Visa, 2 MasterCard, 3 Amex, 4 Discover, 5 DinerClub, 6 EnRoute, 7 Jcb, 8 RevolutionCard, 9 VisaFleet, 10 MasterCardFleet, 11 FleetOne, 12 Fleetwide, 13 Fuelman, 14 Gascard, 15 Voyager, 16 WrightExpress, 17 Interac, 18 Cup (UnionPay), 19 Maestro, 20 Sinclair, 21 MotorCard, 22 PrivateCard, 23 Peaje1, 24 Kress, 25 Prepagopr, 26 Adsg, 27 Aee, 28–31 FleetWay 1–4, 32 Dna, 33 Other

**Cvm (cardholder verification):** 0 NotSet, 1 FailCvmProcessing, 2 PlaintextOfflinePin, 3 OnlinePin, 4 PlaintextOfflinePinAndSignature, 5 EncipheredOfflinePin, 6 EncipheredOfflinePinAndSignature, 7 Signature, 8 NoCvmRequired, 9 OnDeviceCvm (phone/watch)

**TransactionType** (subset): 2 Sale, 3 Return, 4 Authorization, 5 PostAuthorization, 17 Void, 18 VoidSale, 19 VoidReturn, 33 Tokenize, 46 Reversal

## 7. Handling rules

- **Never store or log** `Account`, `CurrentAccountNumber`, `Track1Data`/`Track2Data`/`Track3Data`, `ExpireDate`, `HostEchoData` or `SignatureData`, and never store the whole `rawResponse` string. Pull out the fields you need and throw the rest away.
- Safe to store on the payment record: `responseCode`, `responseMessage`, `authorizationCode`, `hostReferenceNumber`, `gatewayTransactionId`, `batchNumber`, `ecrReferenceNumber`, `ApprovedAmount`, `CardType`, `EntryMode`, `HostAccount` (last 4), `cardBin`, `AppLabel`, `TraceInformation.ReferenceNumber`, `TraceInformation.TimeStamp`.
- **Recognizing a repeat customer:** use `HostInformation.PaymentAccountReferenceId` (PAR) if it is present, and store it hashed (for example HMAC-SHA256 with a secret key). `CofInformation.Token` is the fallback, if this processor returns the same token for the same card. Do **not** match customers on BIN, last 4, cardholder name or expiry: different cards can share these values, and they change when a card is reissued.
- Scales Connect does not currently request a token, so `CofInformation.Token` will usually be absent until that is turned on.
