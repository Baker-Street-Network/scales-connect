using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using BakerScaleConnect.Controllers.Models;
using BakerScaleConnect.Services;
using POSLinkAdmin.Util;

namespace BakerScaleConnect.Controllers
{
    /// <summary>
    /// API controller for PAX credit card terminal operations.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class PaxController(PaxService paxService, PhoneCollectService phoneCollectService, AppSettings appSettings, ILogger<PaxController> logger) : ControllerBase
    {
        /// <summary>
        /// Process a credit card payment transaction.
        /// </summary>
        /// <param name="request">Payment request details including amount and reference number.</param>
        /// <param name="cancellationToken">Cancellation token to abort the transaction.</param>
        /// <returns>Payment transaction result.</returns>
        [HttpPost("credit")]
        public async Task<ActionResult<PaxCreditResponse>> ProcessCredit(
            [FromBody] PaxCreditRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var validationError = ValidateCreditRequest(request);
                if (validationError != null)
                {
                    return BadRequest(CreditError("", validationError, request.EcrReferenceNumber));
                }

                var guardResult = await ApplyTerminalGuardAsync(request, cancellationToken);
                if (guardResult != null)
                {
                    return guardResult;
                }

                var originalCheckResult = await CheckVoidOriginalAsync(request, cancellationToken);
                if (originalCheckResult != null)
                {
                    return originalCheckResult;
                }

                // Process the payment with cancellation support
                var response = await paxService.ProcessCreditPaymentAsync(request, cancellationToken);

                if (response.Success)
                {
                    return Ok(response);
                }
                else
                {
                    return StatusCode(502, response); // Bad Gateway - terminal error
                }
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499, new PaxCreditResponse
                {
                    Success = false,
                    ErrorMessage = "Request cancelled by client",
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new PaxCreditResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Get current PAX terminal settings.
        /// </summary>
        /// <returns>Terminal connection settings.</returns>
        [HttpGet("settings")]
        public ActionResult<PaxTerminalSettings> GetSettings()
        {
            try
            {
                var settings = paxService.GetSettings();
                return Ok(settings);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Update PAX terminal connection settings.
        /// </summary>
        /// <param name="settings">New terminal settings.</param>
        /// <returns>Confirmation of updated settings.</returns>
        [HttpPost("settings")]
        public ActionResult<PaxTerminalSettings> UpdateSettings([FromBody] PaxTerminalSettings settings)
        {
            try
            {
                // Validate settings
                if (string.IsNullOrWhiteSpace(settings.Ip))
                {
                    return BadRequest(new { error = "IP address is required" });
                }

                if (settings.Port <= 0 || settings.Port > 65535)
                {
                    return BadRequest(new { error = "Port must be between 1 and 65535" });
                }

                if (settings.Timeout < 1000)
                {
                    return BadRequest(new { error = "Timeout must be at least 1000 milliseconds" });
                }

                paxService.UpdateSettings(
                    settings.ConnectionMethod,
                    settings.Ip,
                    settings.Port,
                    settings.Timeout,
                    settings.SerialPort);
                return Ok(settings);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Cancel the current operation on the PAX terminal.
        /// This will cancel any in-progress transaction, prompt, or other operation.
        /// </summary>
        /// <returns>Result of the cancel operation.</returns>
        [HttpPost("cancel")]
        public ActionResult CancelOperation()
        {
            try
            {
                var (success, message) = paxService.CancelCurrentOperation();

                if (success)
                {
                    return Ok(new
                    {
                        success = true,
                        message = message,
                        timestamp = DateTime.UtcNow
                    });
                }
                else
                {
                    return StatusCode(502, new
                    {
                        success = false,
                        error = message,
                        timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Display items on the PAX terminal screen.
        /// This uses the ShowItemRequest command to communicate with the BroadPOS app.
        /// </summary>
        /// <param name="request">Items to display on the terminal.</param>
        /// <param name="cancellationToken">Cancellation token to abort the operation.</param>
        /// <returns>Result of the show item operation.</returns>
        [HttpPost("showitem")]
        public async Task<ActionResult<PaxShowItemResponse>> ShowItems(
            [FromBody] PaxShowItemRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                // Validate request
                if (request.Items == null || request.Items.Count == 0)
                {
                    return BadRequest(new PaxShowItemResponse
                    {
                        Success = false,
                        ErrorMessage = "At least one item is required",
                        Timestamp = DateTime.UtcNow
                    });
                }

                // Validate each item
                foreach (var item in request.Items)
                {
                    if (string.IsNullOrWhiteSpace(item.Name))
                    {
                        return BadRequest(new PaxShowItemResponse
                        {
                            Success = false,
                            ErrorMessage = "Item name is required for all items",
                            Timestamp = DateTime.UtcNow
                        });
                    }

                    if (string.IsNullOrWhiteSpace(item.Price))
                    {
                        return BadRequest(new PaxShowItemResponse
                        {
                            Success = false,
                            ErrorMessage = "Item price is required for all items",
                            Timestamp = DateTime.UtcNow
                        });
                    }

                    // Convert price to cents format (multiply by 100)
                    if (decimal.TryParse(item.Price, out var price))
                    {
                        price *= 100;
                        item.Price = price.ToString("F0");
                    }
                    else
                    {
                        return BadRequest(new PaxShowItemResponse
                        {
                            Success = false,
                            ErrorMessage = $"Invalid price format for item: {item.Name}",
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }

                // Show items on the terminal with cancellation support
                var response = await paxService.ShowItemsAsync(request, cancellationToken);

                if (response.Success)
                {
                    return Ok(response);
                }
                else
                {
                    return StatusCode(502, response); // Bad Gateway - terminal error
                }
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499, new PaxShowItemResponse
                {
                    Success = false,
                    ErrorMessage = "Request cancelled by client",
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new PaxShowItemResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Health check endpoint for PAX terminal availability.
        /// </summary>
        /// <returns>Health status.</returns>
        [HttpGet("health")]
        public ActionResult GetHealth() =>
            Ok(new { status = "healthy", service = "pax", timestamp = DateTime.UtcNow });

        /// <summary>
        /// Capabilities that callers can check before sending newer request fields.
        /// </summary>
        public static readonly string[] Features =
        [
            "void_by_reference",
            "void_original_check",
            "terminal_sn_guard",
            "return_original_fields",
            "card_not_present_return",
            "sale_token_request",
            "transaction_lookup",
            "batch_close_details"
        ];

        /// <summary>
        /// Scales Connect version and the identity of the connected terminal.
        /// While a transaction is in progress the terminal is not contacted and the
        /// last known details are returned with busy = true.
        /// </summary>
        [HttpGet("status")]
        public async Task<ActionResult<PaxTerminalStatusResponse>> GetStatus(CancellationToken cancellationToken)
        {
            var info = await paxService.GetTerminalInfoAsync(cancellationToken);

            return Ok(new PaxTerminalStatusResponse
            {
                Version = Form1.GetDisplayVersion(),
                Features = Features,
                Connected = info.Connected,
                Busy = info.Busy,
                TerminalSn = info.Sn,
                TerminalModel = info.ModelName,
                TerminalAppName = info.AppName,
                TerminalAppVersion = info.AppVersion,
                DemoMode = null,
                LastBatchCloseUtc = paxService.GetLastBatchCloseUtc(),
                CheckedAtUtc = info.CheckedAtUtc,
                ErrorMessage = info.ErrorMessage
            });
        }

        /// <summary>
        /// Look up an approved transaction in the terminal's local report by its ECR reference,
        /// to settle the outcome of a call whose response was lost.
        /// </summary>
        [HttpGet("transaction")]
        public async Task<ActionResult<PaxTransactionLookupResponse>> LookupTransaction(
            [FromQuery] string? ecrReferenceNumber,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(ecrReferenceNumber))
            {
                return BadRequest(CreditError("", "ecrReferenceNumber is required", ecrReferenceNumber));
            }

            var response = await paxService.LookupTransactionAsync(ecrReferenceNumber.Trim(), cancellationToken);

            return response.ResponseCode switch
            {
                "TERMINAL_BUSY" => StatusCode(409, response),
                "TERMINAL_UNREACHABLE" => StatusCode(503, response),
                "LOOKUP_FAILED" => StatusCode(502, response),
                _ => Ok(response)
            };
        }

        private static readonly System.Text.RegularExpressions.Regex Digits1To4 = new("^[0-9]{1,4}$");
        private static readonly System.Text.RegularExpressions.Regex Digits4 = new("^[0-9]{4}$");
        private static readonly System.Text.RegularExpressions.Regex Digits6 = new("^[0-9]{6}$");
        private static readonly System.Text.RegularExpressions.Regex Digits8 = new("^[0-9]{8}$");

        /// <summary>
        /// Validates and normalizes a credit request in place. Returns an error message, or null when valid.
        /// Converts the amount from dollars to cents.
        /// </summary>
        private static string? ValidateCreditRequest(PaxCreditRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.EcrReferenceNumber))
                return "ECR Reference Number is required";

            // An unrecognized type used to fall through to Sale, which would charge the customer.
            string? type = string.IsNullOrWhiteSpace(request.TransactionType) ? "Sale" : request.TransactionType.Trim().ToLowerInvariant() switch
            {
                "sale" => "Sale",
                "return" => "Return",
                "void" => "Void",
                _ => null
            };
            if (type == null)
                return $"transactionType must be \"Sale\", \"Return\" or \"Void\", not \"{request.TransactionType}\"";
            request.TransactionType = type;

            if (type == "Void")
            {
                // All three are needed to confirm the record is the sale being voided (CheckVoidOriginalAsync).
                if (string.IsNullOrWhiteSpace(request.OriginalReferenceNumber))
                    return "originalReferenceNumber is required for a Void";
                if (string.IsNullOrWhiteSpace(request.OriginalEcrReferenceNumber))
                    return "originalEcrReferenceNumber is required for a Void";
                if (string.IsNullOrWhiteSpace(request.OriginalTerminalSn))
                    return "originalTerminalSn is required for a Void";
            }

            if (type == "Void")
            {
                // A Void always cancels the full original amount, so any amount sent is ignored.
                request.Amount = "";

                // originalAmount is only checked against the terminal's record, which is in cents.
                if (!string.IsNullOrWhiteSpace(request.OriginalAmount))
                {
                    var originalAmountError = DollarsToCents(request.OriginalAmount, "originalAmount", out var originalCents);
                    if (originalAmountError != null)
                        return originalAmountError;
                    request.OriginalAmount = originalCents;
                }
                else
                {
                    request.OriginalAmount = null;
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(request.Amount))
                    return "Amount is required";

                var amountError = DollarsToCents(request.Amount, "Amount", out var cents);
                if (amountError != null)
                    return amountError;
                request.Amount = cents;
            }

            if (request.OriginalReferenceNumber != null && !Digits1To4.IsMatch(request.OriginalReferenceNumber.Trim()))
                return "originalReferenceNumber must be 1 to 4 digits";

            if (request.OriginalTransactionDate != null &&
                (!Digits8.IsMatch(request.OriginalTransactionDate) ||
                 !DateTime.TryParseExact(request.OriginalTransactionDate, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture,
                     System.Globalization.DateTimeStyles.None, out _)))
                return "originalTransactionDate must be a date in YYYYMMDD format";

            if (request.OriginalTransactionTime != null &&
                (!Digits6.IsMatch(request.OriginalTransactionTime) ||
                 !DateTime.TryParseExact(request.OriginalTransactionTime, "HHmmss", System.Globalization.CultureInfo.InvariantCulture,
                     System.Globalization.DateTimeStyles.None, out _)))
                return "originalTransactionTime must be a time in HHMMSS format";

            if (request.OriginalLast4 != null && !Digits4.IsMatch(request.OriginalLast4))
                return "originalLast4 must be exactly 4 digits";

            if (request.CardPresent == false && type != "Return")
                return "cardPresent: false is only allowed on a Return";

            if (request.RequestToken && type != "Sale")
                return "requestToken is only allowed on a Sale";

            return null;
        }

        /// <summary>Converts a dollar string such as "4.36" to whole cents ("436"). Returns an error message, or null when valid.</summary>
        private static string? DollarsToCents(string dollars, string fieldName, out string cents)
        {
            cents = "";

            if (!decimal.TryParse(dollars, System.Globalization.NumberStyles.AllowDecimalPoint,
                    System.Globalization.CultureInfo.InvariantCulture, out var amount) || amount <= 0)
                return $"{fieldName} must be a positive dollar amount such as \"4.36\", not \"{dollars}\"";

            var value = amount * 100;
            if (value != decimal.Truncate(value))
                return $"{fieldName} has more than two decimal places: \"{dollars}\"";

            cents = value.ToString("F0", System.Globalization.CultureInfo.InvariantCulture);
            return null;
        }

        /// <summary>
        /// Record numbers are per terminal and every terminal starts at 1, so a reference to record "1"
        /// sent to the wrong terminal points at a different customer's sale. Checks the connected
        /// terminal's serial before anything that uses originalReferenceNumber. Returns an error result
        /// to send back, or null to go ahead (possibly with the record reference removed).
        /// </summary>
        private async Task<ActionResult?> ApplyTerminalGuardAsync(PaxCreditRequest request, CancellationToken cancellationToken)
        {
            bool isVoid = request.TransactionType == "Void";
            bool isCardNotPresentReturn = request.TransactionType == "Return" && request.CardPresent == false;
            bool isReferencedReturn = request.TransactionType == "Return" && !string.IsNullOrWhiteSpace(request.OriginalReferenceNumber);

            if (!isVoid && !isCardNotPresentReturn && !isReferencedReturn)
                return null;

            var expectedSn = request.OriginalTerminalSn?.Trim();

            if (string.IsNullOrEmpty(expectedSn))
            {
                // A Void without originalTerminalSn was already rejected by ValidateCreditRequest.
                if (isCardNotPresentReturn)
                    return BadRequest(CreditError("", "originalTerminalSn is required for a card-not-present Return", request.EcrReferenceNumber));

                // Card-present Return: the record reference only helps on the original terminal, so drop it.
                logger.LogInformation("Return {EcrRef}: dropping originalReferenceNumber because originalTerminalSn was not sent", request.EcrReferenceNumber);
                request.OriginalReferenceNumber = null;
                request.OriginalEcrReferenceNumber = null;
                return null;
            }

            var info = await paxService.GetTerminalInfoAsync(cancellationToken);

            if (info.Busy)
                return StatusCode(409, CreditError("TERMINAL_BUSY", "The terminal is running another transaction. Try again when it is idle.", request.EcrReferenceNumber));

            if (!info.Connected || string.IsNullOrWhiteSpace(info.Sn))
                return StatusCode(503, CreditError("TERMINAL_SN_UNAVAILABLE",
                    $"Could not read the connected terminal's serial number, so the transaction was not sent. {info.ErrorMessage}".Trim(),
                    request.EcrReferenceNumber));

            if (string.Equals(info.Sn.Trim(), expectedSn, StringComparison.OrdinalIgnoreCase))
                return null;

            if (isVoid || isCardNotPresentReturn)
            {
                logger.LogWarning("Refusing {Type} {EcrRef}: sale was on terminal {Expected}, connected terminal is {Actual}",
                    request.TransactionType, request.EcrReferenceNumber, expectedSn, info.Sn);
                return StatusCode(409, CreditError("TERMINAL_MISMATCH",
                    $"This sale was made on terminal {expectedSn}; this register is connected to {info.Sn}.",
                    request.EcrReferenceNumber));
            }

            // Card-present Return on another terminal still works; it just can't point at the original record.
            logger.LogInformation("Return {EcrRef}: sale was on terminal {Expected}, connected terminal is {Actual}; dropping record references",
                request.EcrReferenceNumber, expectedSn, info.Sn);
            request.OriginalReferenceNumber = null;
            request.OriginalEcrReferenceNumber = null;
            return null;
        }

        /// <summary>
        /// A Void picks its sale by record number alone, and record numbers can restart after any batch close,
        /// including the terminal's own auto-close, so "record 1" may be a different customer's sale. Before a
        /// Void, finds the sale in the terminal's local report by its ECR reference and checks that it is the
        /// record being voided. Refuses whenever that can't be confirmed: a Return is always safe, a wrong Void isn't.
        /// Returns an error result to send back, or null to go ahead.
        /// </summary>
        private async Task<ActionResult?> CheckVoidOriginalAsync(PaxCreditRequest request, CancellationToken cancellationToken)
        {
            if (request.TransactionType != "Void")
                return null;

            var originalEcrRef = request.OriginalEcrReferenceNumber!.Trim();

            // Early sales were all sent with "/", so it can't single one out. Amount and last 4 aren't enough:
            // a regular paying the same amount with the same card twice in one batch is realistic.
            if (originalEcrRef == "/")
                return StatusCode(422, CreditError("ORIGINAL_UNVERIFIABLE",
                    "This sale was sent without a unique ECR reference, so the void can't be checked and was not sent. Use a Return.",
                    request.EcrReferenceNumber));

            var original = await paxService.LookupTransactionAsync(originalEcrRef, cancellationToken);

            if (original.ResponseCode == "TERMINAL_BUSY")
                return StatusCode(409, CreditError("TERMINAL_BUSY", "The terminal is running another transaction. Try again when it is idle.", request.EcrReferenceNumber));

            if (original.ResponseCode == "NOT_FOUND")
                return StatusCode(409, CreditError("ORIGINAL_NOT_FOUND",
                    $"Sale {originalEcrRef} is not in this terminal's open batch (the batch may have closed), so the void was not sent. Use a Return.",
                    request.EcrReferenceNumber));

            if (!original.Found)
                return StatusCode(503, CreditError("ORIGINAL_CHECK_FAILED",
                    $"Could not check sale {originalEcrRef} on the terminal, so the void was not sent. {original.ResponseCode}: {original.ErrorMessage}".Trim(),
                    request.EcrReferenceNumber));

            var mismatch = FindOriginalMismatch(request, original);
            if (mismatch != null)
            {
                logger.LogWarning("Refusing Void {EcrRef}: sale {OriginalEcrRef} {Mismatch}", request.EcrReferenceNumber, originalEcrRef, mismatch);
                return StatusCode(409, CreditError("ORIGINAL_MISMATCH",
                    $"The terminal's record for sale {originalEcrRef} doesn't match: {mismatch}. The void was not sent. Use a Return.",
                    request.EcrReferenceNumber));
            }

            return null;
        }

        /// <summary>Describes how the terminal's record differs from the sale Odoo asked to void, or null when it matches.</summary>
        private static string? FindOriginalMismatch(PaxCreditRequest request, PaxTransactionLookupResponse original)
        {
            if (!int.TryParse(original.TotalRecord, out var total) || total != 1)
                return $"the terminal reported {original.TotalRecord ?? "an unknown number of"} records with this ECR reference";

            if (original.TransactionType != "Sale")
                return $"it is a {original.TransactionType}, not a Sale";

            if (!int.TryParse(original.ReferenceNumber, out var recordNumber) || recordNumber != int.Parse(request.OriginalReferenceNumber!.Trim()))
                return $"it is record {original.ReferenceNumber}, not record {request.OriginalReferenceNumber!.Trim()}";

            if (!string.IsNullOrWhiteSpace(original.TerminalSn) &&
                !string.Equals(original.TerminalSn.Trim(), request.OriginalTerminalSn!.Trim(), StringComparison.OrdinalIgnoreCase))
                return $"it was made on terminal {original.TerminalSn}, not {request.OriginalTerminalSn!.Trim()}";

            // originalAmount and last 4 are optional, but when sent the record must show the same value.
            // Both sides are in cents here: ValidateCreditRequest converted originalAmount from dollars.
            if (request.OriginalAmount != null &&
                (!long.TryParse(original.ApprovedAmount, out var approvedCents) || approvedCents != long.Parse(request.OriginalAmount)))
                return $"its approved amount is {original.ApprovedAmount ?? "not reported"} cents, not {request.OriginalAmount}";

            if (request.OriginalLast4 != null && original.Last4 != request.OriginalLast4)
                return $"its card ends in {original.Last4 ?? "(not reported)"}, not {request.OriginalLast4}";

            return null;
        }

        private static PaxCreditResponse CreditError(string responseCode, string message, string? ecrReferenceNumber) => new()
        {
            Success = false,
            ResponseCode = responseCode,
            ErrorMessage = message,
            EcrReferenceNumber = ecrReferenceNumber,
            Timestamp = DateTime.UtcNow,
            RawResponse = ""
        };

        /// <summary>
        /// Called by Odoo on first item scan. Sends TCP trigger to open the Odoo
        /// customer display in GeckoView on the Aries 8.
        /// </summary>
        [HttpPost("phonecollect")]
        public async Task<ActionResult> PhoneCollect(
            [FromBody] PhoneCollectRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.DisplayUrl))
                return BadRequest(new { error = "display_url is required" });

            if (!IsAbsoluteHttpUrl(request.DisplayUrl))
                return BadRequest(new { error = "display_url must be an absolute http or https URL" });

            if (request.LogoUrl != null && !IsAbsoluteHttpUrl(request.LogoUrl))
                return BadRequest(new { error = "logo_url must be an absolute http or https URL" });

            if (request.LogoVersion != null && !Sha1Hex.IsMatch(request.LogoVersion))
                return BadRequest(new { error = "logo_version must be a 40-character hex SHA1" });

            try
            {
                await phoneCollectService.ShowCustomerDisplayAsync(
                    request.OrderId,
                    request.DisplayUrl,
                    request.LogoUrl,
                    request.LogoVersion,
                    cancellationToken);
                return Ok(new { order_id = request.OrderId });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(503, new { error = ex.Message });
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499, new { error = "Request cancelled by client" });
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                var target = $"{appSettings.Aries.TerminalIp}:{appSettings.Aries.PhonePort}";
                logger.LogError(ex, "Could not reach Aries terminal at {Target} for order {OrderId}", target, request.OrderId);
                return StatusCode(502, new
                {
                    error = $"Could not reach Aries terminal at {target}: {ex.Message}",
                    socket_error = ex.SocketErrorCode.ToString(),
                    target,
                    order_id = request.OrderId
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error in PhoneCollect for order {OrderId}", request.OrderId);
                return StatusCode(500, new
                {
                    error = $"Unexpected error sending customer display trigger: {ex.Message}",
                    exception = ex.GetType().Name,
                    inner = ex.InnerException?.Message,
                    order_id = request.OrderId
                });
            }
        }

        private static readonly System.Text.RegularExpressions.Regex Sha1Hex =
            new("^[0-9a-fA-F]{40}$", System.Text.RegularExpressions.RegexOptions.Compiled);

        private static bool IsAbsoluteHttpUrl(string value) =>
            Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    public record PhoneCollectRequest(
        [property: System.Text.Json.Serialization.JsonPropertyName("order_id")] string OrderId,
        [property: System.Text.Json.Serialization.JsonPropertyName("display_url")] string? DisplayUrl,
        [property: System.Text.Json.Serialization.JsonPropertyName("logo_url")] string? LogoUrl = null,
        [property: System.Text.Json.Serialization.JsonPropertyName("logo_version")] string? LogoVersion = null);
}
