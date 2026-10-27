using BakerScaleConnect.Controllers.Models;
using Microsoft.Extensions.Logging;
using POSLinkCore.CommunicationSetting;
using POSLinkSemiIntegration;
using POSLinkSemiIntegration.Batch;
using POSLinkSemiIntegration.Transaction;
using POSLinkUart;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BakerScaleConnect.Services
{
    /// <summary>
    /// Service for handling PAX credit card terminal operations.
    /// </summary>
    public class PaxService
    {
        private readonly ILogger<PaxService> _logger;
        private readonly PaxTerminalSettings _settings;
        private string _connectionMethod = "TCP";
        private string _serialPort = "";
        private Terminal? _activeTerminal;
        private readonly object _terminalLock = new object();

        // Init is a quick handshake, so don't wait the full transaction timeout for it.
        private const int InitTimeoutMs = 5000;

        // Last terminal identity read from the terminal, served while it is busy.
        private PaxTerminalInfo? _lastTerminalInfo;
        private DateTime? _lastBatchCloseUtc;

        public PaxService(ILogger<PaxService> logger)
        {
            _logger = logger;

            // Default settings - can be configured via appsettings or environment
            _settings = new PaxTerminalSettings
            {
                Ip = "127.0.0.1",
                Port = 10009,
                Timeout = 60000
            };
        }

        /// <summary>
        /// Process a credit card payment through the PAX terminal.
        /// </summary>
        public async Task<PaxCreditResponse> ProcessCreditPaymentAsync(PaxCreditRequest paymentRequest, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Processing credit transaction: Type={Type}, Amount={Amount}, EcrRef={EcrRef}, OrigRef={OrigRef}, CardPresent={CardPresent}, Method={Method}",
                    paymentRequest.TransactionType ?? "Sale", paymentRequest.Amount, paymentRequest.EcrReferenceNumber,
                    paymentRequest.OriginalReferenceNumber, paymentRequest.CardPresent, _connectionMethod);

                Terminal terminal = CreateTerminal(_settings.Timeout);

                // Store active terminal for cancellation
                lock (_terminalLock)
                {
                    _activeTerminal = terminal;
                }

                try
                {
                    DoCreditRequest request = BuildCreditRequest(paymentRequest);

                    // Execute the transaction with cancellation support
                    // Note: DoCredit is a blocking call, so we run it in a Task and monitor for cancellation
                    POSLinkAdmin.ExecutionResult result = null!;
                    DoCreditResponse creditResponse = null!;

                    var transactionTask = Task.Run(() =>
                    {
                        var execResult = terminal.Transaction.DoCredit(request, out DoCreditResponse response);
                        return (execResult, response);
                    }, cancellationToken);

                    try
                    {
                        var taskResult = await transactionTask;
                        result = taskResult.execResult;
                        creditResponse = taskResult.response;
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning("Transaction cancelled by user");
                        terminal.Cancel();

                        return new PaxCreditResponse
                        {
                            Success = false,
                            ResponseCode = "",
                            ResponseMessage = "",
                            EcrReferenceNumber = paymentRequest.EcrReferenceNumber,
                            ErrorMessage = "Transaction cancelled by user",
                            Timestamp = DateTime.UtcNow
                        };
                    }

                    // Build response
                    if (result.GetErrorCode() == POSLinkAdmin.ExecutionResult.Code.Ok)
                    {
                        _logger.LogInformation("Credit transaction answered: Code={Code}, Message={Message}, RefNum={RefNum}, Sn={Sn}",
                            creditResponse.ResponseCode, creditResponse.ResponseMessage,
                            creditResponse.TraceInformation?.ReferenceNumber, creditResponse.TraceInformation?.Sn);

                        RememberTerminalSn(creditResponse.TraceInformation?.Sn);

                        return new PaxCreditResponse
                        {
                            Success = true,
                            ResponseCode = creditResponse.ResponseCode ?? "",
                            ResponseMessage = creditResponse.ResponseMessage ?? "",
                            EcrReferenceNumber = paymentRequest.EcrReferenceNumber,
                            Timestamp = DateTime.UtcNow,

                            CardBin = creditResponse.AccountInformation?.CardBin,
                            AuthorizationCode = creditResponse.HostInformation?.AuthorizationCode,
                            BatchNumber = creditResponse.HostInformation?.BatchNumber,
                            ControlNumber = creditResponse.HostInformation?.ControlNumber,
                            GatewayTransactionId = creditResponse.HostInformation?.GatewayTransactionId,
                            HostDetailedMessage = creditResponse.HostInformation?.HostDetailedMessage,
                            HostReferenceNumber = creditResponse.HostInformation?.HostReferenceNumber,
                            ReferenceNumber = creditResponse.TraceInformation?.ReferenceNumber,
                            GlobalUid = creditResponse.TraceInformation?.GlobalUid,
                            TerminalSn = creditResponse.TraceInformation?.Sn,
                            TerminalTimestamp = creditResponse.TraceInformation?.TimeStamp,
                            ApprovedAmount = creditResponse.AmountInformation?.ApprovedAmount,
                            Token = NullIfBlank(creditResponse.CofInformation?.Token),
                            RawResponse = SerializeRaw(creditResponse)
                        };
                    }
                    else
                    {
                        _logger.LogError("Credit payment failed: ErrorCode={ErrorCode}", result.GetErrorCode());

                        return new PaxCreditResponse
                        {
                            Success = false,
                            ResponseCode = "",
                            ResponseMessage = "",
                            EcrReferenceNumber = paymentRequest.EcrReferenceNumber,
                            ErrorMessage = $"Transaction failed with error code: {result.GetErrorCode()}",
                            Timestamp = DateTime.UtcNow
                        };
                    }
                }
                finally
                {
                    // Clear active terminal
                    lock (_terminalLock)
                    {
                        _activeTerminal = null;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Transaction cancelled before completion");
                return new PaxCreditResponse
                {
                    Success = false,
                    ResponseCode = "",
                    ResponseMessage = "",
                    EcrReferenceNumber = paymentRequest.EcrReferenceNumber,
                    ErrorMessage = "Transaction cancelled",
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing credit payment");
                return new PaxCreditResponse
                {
                    Success = false,
                    ResponseCode = "",
                    ResponseMessage = "",
                    EcrReferenceNumber = paymentRequest.EcrReferenceNumber,
                    ErrorMessage = ex.Message,
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Update terminal connection settings.
        /// </summary>
        public void UpdateSettings(string connectionMethod, string ip, int port, int timeout, string serialPort)
        {
            _connectionMethod = connectionMethod;
            _settings.Ip = ip;
            _settings.Port = port;
            _settings.Timeout = timeout;
            _serialPort = serialPort;
            // The connection may now point at a different terminal.
            lock (_terminalLock)
            {
                _lastTerminalInfo = null;
            }
            _logger.LogInformation("PAX terminal settings updated: Method={Method}, IP={Ip}, Port={Port}, Timeout={Timeout}, SerialPort={SerialPort}", 
                connectionMethod, ip, port, timeout, serialPort);
        }

        /// <summary>
        /// Get current terminal settings.
        /// </summary>
        public (string ConnectionMethod, PaxTerminalSettings Settings, string SerialPort) GetSettings()
        {
            return (_connectionMethod, new PaxTerminalSettings
            {
                Ip = _settings.Ip,
                Port = _settings.Port,
                Timeout = _settings.Timeout
            }, _serialPort);
        }

        /// <summary>
        /// Cancel the current operation on the PAX terminal.
        /// This cancels any in-progress transaction, prompts, or other operations.
        /// </summary>
        public (bool Success, string Message) CancelCurrentOperation()
        {
            try
            {
                _logger.LogInformation("Attempting to cancel current PAX terminal operation");

                Terminal? terminal;
                lock (_terminalLock)
                {
                    terminal = _activeTerminal;
                }

                if (terminal == null)
                {
                    _logger.LogWarning("No active terminal operation to cancel");
                    return (false, "No active operation to cancel");
                }

                // Cancel the current operation on the active terminal
                terminal.Cancel();

                _logger.LogInformation("PAX terminal operation cancel signal sent successfully");
                return (true, "Cancel signal sent to terminal");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error canceling PAX terminal operation");
                return (false, $"Failed to cancel operation: {ex.Message}");
            }
        }

        public async Task<PaxBatchCloseResponse> ProcessBatchCloseAsync(PaxBatchCloseRequest batchRequest, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Processing batch close: EcrRef={EcrRef}, Method={Method}",
                    batchRequest.EcrReferenceNumber, _connectionMethod);

                // Read the serial first so the caller knows which terminal's batch closed.
                var terminalInfo = await GetTerminalInfoAsync(cancellationToken);

                Terminal terminal = CreateTerminal(_settings.Timeout);

                // Store active terminal for cancellation
                lock (_terminalLock)
                {
                    _activeTerminal = terminal;
                }

                try
                {
                    // Create the batch close request
                    var request = new BatchCloseRequest();

                    var result = terminal.Batch.BatchClose(request, out BatchCloseResponse response);

                    // Build response
                    if (result.GetErrorCode() == POSLinkAdmin.ExecutionResult.Code.Ok)
                    {
                        _logger.LogInformation("Batch close successful: Code={Code}, Message={Message}",
                            response.ResponseCode, response.ResponseMessage);

                        var closedAt = DateTime.UtcNow;
                        if (response.ResponseCode == "000000")
                        {
                            lock (_terminalLock)
                            {
                                _lastBatchCloseUtc = closedAt;
                            }
                        }

                        return new PaxBatchCloseResponse
                        {
                            Success = true,
                            ResponseCode = response.ResponseCode ?? "",
                            ResponseMessage = response.ResponseMessage ?? "",
                            EcrReferenceNumber = batchRequest.EcrReferenceNumber,
                            BatchNumber = response.HostInformation?.BatchNumber,
                            HostResponse = NullIfBlank(response.HostInformation?.HostResponseMessage),
                            TerminalSn = terminalInfo.Sn,
                            TerminalTimestamp = response.TimeStamp,
                            ClosedAtUtc = closedAt,
                            Tid = response.Tid,
                            Mid = response.Mid,
                            Timestamp = closedAt
                        };
                    }
                    else
                    {
                        _logger.LogError("Batch close failed: ErrorCode={ErrorCode}", result.GetErrorCode());

                        return new PaxBatchCloseResponse
                        {
                            Success = false,
                            ResponseCode = "",
                            ResponseMessage = "",
                            EcrReferenceNumber = batchRequest.EcrReferenceNumber,
                            ErrorMessage = $"Batch close failed with error code: {result.GetErrorCode()}",
                            Timestamp = DateTime.UtcNow
                        };
                    }
                }
                finally
                {
                    // Clear active terminal
                    lock (_terminalLock)
                    {
                        _activeTerminal = null;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Batch close cancelled before completion");
                return new PaxBatchCloseResponse
                {
                    Success = false,
                    ResponseCode = "",
                    ResponseMessage = "",
                    EcrReferenceNumber = batchRequest.EcrReferenceNumber,
                    ErrorMessage = "Batch close cancelled",
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (NotSupportedException ex)
            {
                _logger.LogError(ex, "Batch close not supported by SDK");
                return new PaxBatchCloseResponse
                {
                    Success = false,
                    ResponseCode = "",
                    ResponseMessage = "",
                    EcrReferenceNumber = batchRequest.EcrReferenceNumber,
                    ErrorMessage = ex.Message,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing batch close");
                return new PaxBatchCloseResponse
                {
                    Success = false,
                    ResponseCode = "",
                    ResponseMessage = "",
                    EcrReferenceNumber = batchRequest.EcrReferenceNumber,
                    ErrorMessage = ex.Message,
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Display items on the PAX terminal screen using the ShowItemRequest command.
        /// This communicates with the BroadPOS app on the payment terminal.
        /// Note: This requires PAX SDK support for ShowItemRequest. If not available in your SDK version,
        /// consider updating to the latest POSLink SDK from PAX.
        /// </summary>
        public async Task<PaxShowItemResponse> ShowItemsAsync(PaxShowItemRequest showItemRequest, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Showing items on PAX terminal: ItemCount={ItemCount}, EcrRef={EcrRef}",
                    showItemRequest.Items.Count, showItemRequest.EcrReferenceNumber);

                Terminal terminal = CreateTerminal(_settings.Timeout);

                // Store active terminal for cancellation
                lock (_terminalLock)
                {
                    _activeTerminal = terminal;
                }

                try
                {
                    // Note: The ShowItemRequest API is part of the PAX POSLink SDK for BroadPOS integration.
                    // If this method is not available in your SDK version, you may need to:
                    // 1. Update to the latest PAX POSLink SDK
                    // 2. Contact PAX support for the ShowItemRequest documentation
                    // 3. Use an alternative approach like sending custom messages

                    // For now, we'll use reflection to attempt to call the ShowItem method if it exists
                    var transactionType = terminal.Transaction.GetType();
                    var showItemMethod = transactionType.GetMethod("ShowItem");

                    if (showItemMethod == null)
                    {
                        _logger.LogWarning("ShowItem method not found in PAX SDK. SDK version may not support this feature.");
                        return new PaxShowItemResponse
                        {
                            Success = false,
                            ResponseCode = "",
                            ResponseMessage = "",
                            EcrReferenceNumber = showItemRequest.EcrReferenceNumber,
                            ErrorMessage = "ShowItem feature not available in current PAX SDK version. Please update to the latest POSLink SDK or contact PAX support.",
                            Timestamp = DateTime.UtcNow
                        };
                    }

                    // Dynamically create the request using reflection
                    var showItemRequestType = Type.GetType("POSLinkSemiIntegration.Transaction.ShowItemRequest, POSLinkSemiIntegration");
                    if (showItemRequestType == null)
                    {
                        _logger.LogWarning("ShowItemRequest type not found in PAX SDK.");
                        return new PaxShowItemResponse
                        {
                            Success = false,
                            ResponseCode = "",
                            ResponseMessage = "",
                            EcrReferenceNumber = showItemRequest.EcrReferenceNumber,
                            ErrorMessage = "ShowItemRequest not available in current PAX SDK version.",
                            Timestamp = DateTime.UtcNow
                        };
                    }

                    var request = Activator.CreateInstance(showItemRequestType);

                    // Get the Items property
                    var itemsProperty = showItemRequestType.GetProperty("Items");
                    if (itemsProperty != null)
                    {
                        var itemsList = itemsProperty.GetValue(request);
                        var addMethod = itemsList?.GetType().GetMethod("Add");

                        // Get ItemInformation type
                        var itemInfoType = showItemRequestType.GetNestedType("ItemInformation");

                        if (itemInfoType != null && addMethod != null)
                        {
                            // Add items to the request
                            foreach (var item in showItemRequest.Items)
                            {
                                var itemInfo = Activator.CreateInstance(itemInfoType);

                                itemInfoType.GetProperty("ItemName")?.SetValue(itemInfo, item.Name);
                                itemInfoType.GetProperty("ItemPrice")?.SetValue(itemInfo, item.Price);
                                itemInfoType.GetProperty("ItemQuantity")?.SetValue(itemInfo, item.Quantity.ToString());

                                if (!string.IsNullOrWhiteSpace(item.Sku))
                                {
                                    itemInfoType.GetProperty("ItemCode")?.SetValue(itemInfo, item.Sku);
                                }

                                addMethod.Invoke(itemsList, new[] { itemInfo });
                            }
                        }
                    }

                    // Set trace information if provided
                    if (!string.IsNullOrWhiteSpace(showItemRequest.EcrReferenceNumber))
                    {
                        var traceInfoProperty = showItemRequestType.GetProperty("TraceInformation");
                        if (traceInfoProperty != null)
                        {
                            var traceReq = new POSLinkSemiIntegration.Util.TraceRequest
                            {
                                EcrReferenceNumber = showItemRequest.EcrReferenceNumber
                            };
                            traceInfoProperty.SetValue(request, traceReq);
                        }
                    }

                    // Execute the show item request with cancellation support
                    var executeTask = Task.Run(() =>
                    {
                        var parameters = new object?[] { request, null };
                        var execResult = showItemMethod.Invoke(terminal.Transaction, parameters);
                        return (execResult, parameters[1]);
                    }, cancellationToken);

                    try
                    {
                        var taskResult = await executeTask;
                        var result = taskResult.execResult as POSLinkAdmin.ExecutionResult;
                        var showItemResponse = taskResult.Item2;

                        if (result != null && result.GetErrorCode() == POSLinkAdmin.ExecutionResult.Code.Ok && showItemResponse != null)
                        {
                            var responseType = showItemResponse.GetType();
                            var responseCode = responseType.GetProperty("ResponseCode")?.GetValue(showItemResponse) as string ?? "";
                            var responseMessage = responseType.GetProperty("ResponseMessage")?.GetValue(showItemResponse) as string ?? "";

                            _logger.LogInformation("Show item successful: Code={Code}, Message={Message}",
                                responseCode, responseMessage);

                            return new PaxShowItemResponse
                            {
                                Success = true,
                                ResponseCode = responseCode,
                                ResponseMessage = responseMessage,
                                EcrReferenceNumber = showItemRequest.EcrReferenceNumber,
                                Timestamp = DateTime.UtcNow
                            };
                        }
                        else
                        {
                            var errorCode = result?.GetErrorCode().ToString() ?? "Unknown";
                            _logger.LogError("Show item failed: ErrorCode={ErrorCode}", errorCode);

                            return new PaxShowItemResponse
                            {
                                Success = false,
                                ResponseCode = "",
                                ResponseMessage = "",
                                EcrReferenceNumber = showItemRequest.EcrReferenceNumber,
                                ErrorMessage = $"Show item failed with error code: {errorCode}",
                                Timestamp = DateTime.UtcNow
                            };
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning("Show item operation cancelled by user");
                        terminal.Cancel();

                        return new PaxShowItemResponse
                        {
                            Success = false,
                            ResponseCode = "",
                            ResponseMessage = "",
                            EcrReferenceNumber = showItemRequest.EcrReferenceNumber,
                            ErrorMessage = "Show item operation cancelled by user",
                            Timestamp = DateTime.UtcNow
                        };
                    }
                }
                finally
                {
                    // Clear active terminal
                    lock (_terminalLock)
                    {
                        _activeTerminal = null;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Show item operation cancelled before completion");
                return new PaxShowItemResponse
                {
                    Success = false,
                    ResponseCode = "",
                    ResponseMessage = "",
                    EcrReferenceNumber = showItemRequest.EcrReferenceNumber,
                    ErrorMessage = "Show item operation cancelled",
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing items on PAX terminal");
                return new PaxShowItemResponse
                {
                    Success = false,
                    ResponseCode = "",
                    ResponseMessage = "",
                    EcrReferenceNumber = showItemRequest.EcrReferenceNumber,
                    ErrorMessage = ex.Message,
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Read the connected terminal's serial number and model with POSLink Init.
        /// While another command is in progress, returns the last known values instead of
        /// talking to the terminal.
        /// </summary>
        public async Task<PaxTerminalInfo> GetTerminalInfoAsync(CancellationToken cancellationToken = default)
        {
            Terminal terminal;
            lock (_terminalLock)
            {
                if (_activeTerminal != null)
                {
                    return (_lastTerminalInfo ?? new PaxTerminalInfo()) with { Connected = true, Busy = true };
                }

                terminal = CreateTerminal(InitTimeoutMs);
                _activeTerminal = terminal;
            }

            try
            {
                var (result, response) = await Task.Run(() =>
                {
                    var execResult = terminal.Manage.Init(out POSLinkAdmin.Manage.InitResponse initResponse);
                    return (execResult, initResponse);
                }, cancellationToken);

                if (result.GetErrorCode() != POSLinkAdmin.ExecutionResult.Code.Ok || string.IsNullOrWhiteSpace(response?.Sn))
                {
                    _logger.LogWarning("PAX Init failed: ErrorCode={ErrorCode}, Code={Code}, Message={Message}",
                        result.GetErrorCode(), response?.ResponseCode, response?.ResponseMessage);
                    return new PaxTerminalInfo
                    {
                        Connected = false,
                        CheckedAtUtc = DateTime.UtcNow,
                        ErrorMessage = result.GetErrorCode() == POSLinkAdmin.ExecutionResult.Code.Ok
                            ? $"Terminal answered without a serial number: {response?.ResponseCode} {response?.ResponseMessage}".Trim()
                            : $"Terminal did not answer: {result.GetErrorCode()}"
                    };
                }

                var info = new PaxTerminalInfo
                {
                    Connected = true,
                    Sn = response.Sn,
                    ModelName = NullIfBlank(response.ModelName),
                    AppName = NullIfBlank(response.AppName),
                    AppVersion = NullIfBlank(response.AppVersion),
                    CheckedAtUtc = DateTime.UtcNow
                };

                lock (_terminalLock)
                {
                    _lastTerminalInfo = info;
                }
                return info;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Error reading PAX terminal info");
                return new PaxTerminalInfo
                {
                    Connected = false,
                    CheckedAtUtc = DateTime.UtcNow,
                    ErrorMessage = ex.Message
                };
            }
            finally
            {
                ReleaseTerminal(terminal);
            }
        }

        /// <summary>
        /// Last batch close run through this instance since it started, or null.
        /// Closes done on the terminal itself or by the host are not seen.
        /// </summary>
        public DateTime? GetLastBatchCloseUtc()
        {
            lock (_terminalLock)
            {
                return _lastBatchCloseUtc;
            }
        }

        /// <summary>
        /// Search the terminal's local report for an approved transaction with this ECR reference.
        /// <para>ResponseCode is "000000" when found, "NOT_FOUND" when the terminal reports no such record,
        /// and TERMINAL_BUSY, TERMINAL_UNREACHABLE or LOOKUP_FAILED when the outcome is unknown.</para>
        /// </summary>
        public async Task<PaxTransactionLookupResponse> LookupTransactionAsync(string ecrReferenceNumber, CancellationToken cancellationToken = default)
        {
            Terminal terminal;
            lock (_terminalLock)
            {
                if (_activeTerminal != null)
                {
                    return LookupFailure(ecrReferenceNumber, "TERMINAL_BUSY", "The terminal is running another command. Try again when it is idle.");
                }

                terminal = CreateTerminal(_settings.Timeout);
                _activeTerminal = terminal;
            }

            try
            {
                var request = new POSLinkSemiIntegration.Report.LocalDetailReportRequest
                {
                    EcrReferenceNumber = ecrReferenceNumber,
                    EdcType = POSLinkAdmin.Const.EdcType.All,
                    TransactionResultType = POSLinkSemiIntegration.Const.TransactionResultType.SuccessfulOnly
                };

                var (result, report) = await Task.Run(() =>
                {
                    var execResult = terminal.Report.LocalDetailReport(request, out POSLinkSemiIntegration.Report.LocalDetailReportResponse response);
                    return (execResult, response);
                }, cancellationToken);

                if (result.GetErrorCode() != POSLinkAdmin.ExecutionResult.Code.Ok)
                {
                    _logger.LogWarning("Local detail report failed: ErrorCode={ErrorCode}", result.GetErrorCode());
                    return LookupFailure(ecrReferenceNumber, "TERMINAL_UNREACHABLE", $"Terminal did not answer: {result.GetErrorCode()}");
                }

                _logger.LogInformation("Local detail report for {EcrRef}: Code={Code}, Message={Message}, TotalRecord={Total}",
                    ecrReferenceNumber, report.ResponseCode, report.ResponseMessage, report.TotalRecord);

                bool hasRecords = int.TryParse(report.TotalRecord, out var total) && total > 0;

                // 100023 is PAX's "transaction not found". Anything else unexpected stays unknown.
                if (report.ResponseCode == "100023" || (report.ResponseCode == "000000" && !hasRecords))
                {
                    return new PaxTransactionLookupResponse
                    {
                        Success = true,
                        Found = false,
                        ResponseCode = "NOT_FOUND",
                        ResponseMessage = report.ResponseMessage ?? "",
                        EcrReferenceNumber = ecrReferenceNumber,
                        TotalRecord = report.TotalRecord,
                        LastBatchCloseUtc = GetLastBatchCloseUtc(),
                        Timestamp = DateTime.UtcNow,
                        RawResponse = SerializeRaw(report)
                    };
                }

                if (report.ResponseCode != "000000")
                {
                    return LookupFailure(ecrReferenceNumber, "LOOKUP_FAILED",
                        $"Terminal could not search its report: {report.ResponseCode} {report.ResponseMessage}".Trim());
                }

                RememberTerminalSn(report.TraceInformation?.Sn);

                return new PaxTransactionLookupResponse
                {
                    Success = true,
                    Found = true,
                    ResponseCode = "000000",
                    ResponseMessage = report.ResponseMessage ?? "",
                    EcrReferenceNumber = ecrReferenceNumber,
                    Timestamp = DateTime.UtcNow,
                    CardBin = report.AccountInformation?.CardBin,
                    AuthorizationCode = report.HostInformation?.AuthorizationCode,
                    BatchNumber = report.HostInformation?.BatchNumber,
                    ControlNumber = report.HostInformation?.ControlNumber,
                    GatewayTransactionId = report.HostInformation?.GatewayTransactionId,
                    HostDetailedMessage = report.HostInformation?.HostDetailedMessage,
                    HostReferenceNumber = report.HostInformation?.HostReferenceNumber,
                    ReferenceNumber = report.TraceInformation?.ReferenceNumber,
                    GlobalUid = report.TraceInformation?.GlobalUid,
                    TerminalSn = report.TraceInformation?.Sn,
                    TerminalTimestamp = report.TraceInformation?.TimeStamp,
                    ApprovedAmount = report.AmountInformation?.ApprovedAmount,
                    TotalRecord = report.TotalRecord,
                    TransactionType = report.TransactionType.ToString(),
                    Last4 = Last4Of(report.AccountInformation?.Account),
                    LastBatchCloseUtc = GetLastBatchCloseUtc(),
                    RawResponse = SerializeRaw(report)
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error looking up transaction {EcrRef}", ecrReferenceNumber);
                return LookupFailure(ecrReferenceNumber, "LOOKUP_FAILED", ex.Message);
            }
            finally
            {
                ReleaseTerminal(terminal);
            }
        }

        /// <summary>The account's last 4 digits, or null. The terminal may report the full number or only the end.</summary>
        private static string? Last4Of(string? account)
        {
            var trimmed = account?.Trim();
            if (trimmed == null || trimmed.Length < 4)
                return null;

            var last4 = trimmed[^4..];
            return last4.All(char.IsAsciiDigit) ? last4 : null;
        }

        private PaxTransactionLookupResponse LookupFailure(string ecrReferenceNumber, string code, string message) => new()
        {
            Success = false,
            Found = false,
            ResponseCode = code,
            EcrReferenceNumber = ecrReferenceNumber,
            ErrorMessage = message,
            LastBatchCloseUtc = GetLastBatchCloseUtc(),
            Timestamp = DateTime.UtcNow,
            RawResponse = ""
        };

        private static DoCreditRequest BuildCreditRequest(PaxCreditRequest paymentRequest)
        {
            var transactionType = paymentRequest.TransactionType switch
            {
                "Return" => POSLinkAdmin.Const.TransactionType.Return,
                "Void" => POSLinkAdmin.Const.TransactionType.Void,
                _ => POSLinkAdmin.Const.TransactionType.Sale
            };

            DoCreditRequest request = new() { TransactionType = transactionType };

            // A Void always cancels the full original amount; the terminal rejects one that carries an amount.
            if (transactionType != POSLinkAdmin.Const.TransactionType.Void)
            {
                request.AmountInformation = new POSLinkAdmin.Util.AmountRequest
                {
                    TransactionAmount = paymentRequest.Amount,
                };
            }

            var traceReq = new POSLinkSemiIntegration.Util.TraceRequest
            {
                EcrReferenceNumber = paymentRequest.EcrReferenceNumber
            };
            request.TraceInformation = traceReq;

            var cofReq = new POSLinkSemiIntegration.Util.CofRequest();
            bool hasCof = false;

            if (transactionType == POSLinkAdmin.Const.TransactionType.Sale)
            {
                if (paymentRequest.RequestToken)
                {
                    cofReq.TokenRequestFlag = POSLinkSemiIntegration.Const.TokenRequestFlag.Yes;
                    hasCof = true;
                }
            }
            else
            {
                // Without OriginalReferenceNumber the terminal asks the cashier to key in the record number.
                traceReq.OriginalReferenceNumber = NullIfBlank(paymentRequest.OriginalReferenceNumber);

                // A Void needs only the record number, and every extra field is one the terminal app can
                // refuse. Returns keep the fields that some hosts use.
                if (transactionType == POSLinkAdmin.Const.TransactionType.Return)
                {
                    traceReq.OriginalEcrReferenceNumber = NullIfBlank(paymentRequest.OriginalEcrReferenceNumber);
                    traceReq.OriginalTransactionDate = NullIfBlank(paymentRequest.OriginalTransactionDate);
                    traceReq.OriginalTransactionTime = NullIfBlank(paymentRequest.OriginalTransactionTime);
                }

                // originalLast4 is never sent as AccountInformation.OriginalPan: the First Data BroadPOS app
                // rejects it with 100003 "ORIGPAN NOT SUPPORTED". It is still used by the pre-Void check.
            }

            if (transactionType == POSLinkAdmin.Const.TransactionType.Return && paymentRequest.CardPresent == false)
            {
                request.TransactionBehavior = new POSLinkSemiIntegration.Util.TransactionBehaviorRequest
                {
                    CardPresentMode = POSLinkSemiIntegration.Const.CardPresentMode.AssumeCardNotPresent
                };

                // First Data returned "0" when it had no reference, so "0" identifies nothing.
                var hostRef = NullIfBlank(paymentRequest.OriginalHostReferenceNumber);
                if (hostRef is not null && hostRef != "0")
                {
                    request.HostTraceInformation = new POSLinkSemiIntegration.Util.HostTraceRequest
                    {
                        HostReferenceNumber = hostRef
                    };
                }

                if (!string.IsNullOrWhiteSpace(paymentRequest.OriginalTransactionIdentifier))
                {
                    cofReq.OriginalTransactionIdentifier = paymentRequest.OriginalTransactionIdentifier;
                    hasCof = true;
                }

                if (!string.IsNullOrWhiteSpace(paymentRequest.Token))
                {
                    cofReq.Token = paymentRequest.Token;
                    hasCof = true;
                }
            }

            if (hasCof)
            {
                request.CofInformation = cofReq;
            }

            return request;
        }

        private Terminal CreateTerminal(int timeout)
        {
            POSLinkSemi poslinkSemi = POSLinkSemi.GetPOSLinkSemi();

            if (_connectionMethod == "USB")
            {
                UartSetting uartSetting = new()
                {
                    BaudRate = 115200,
                    Timeout = timeout,
                    SerialPortName = _serialPort,
                };
                return poslinkSemi.GetTerminal(uartSetting);
            }

            TcpSetting tcpSetting = new()
            {
                Ip = _settings.Ip,
                Port = _settings.Port,
                Timeout = timeout
            };
            return poslinkSemi.GetTerminal(tcpSetting);
        }

        private void ReleaseTerminal(Terminal terminal)
        {
            lock (_terminalLock)
            {
                if (ReferenceEquals(_activeTerminal, terminal))
                {
                    _activeTerminal = null;
                }
            }
        }

        private void RememberTerminalSn(string? sn)
        {
            if (string.IsNullOrWhiteSpace(sn))
            {
                return;
            }

            lock (_terminalLock)
            {
                if (_lastTerminalInfo?.Sn != sn)
                {
                    // A different terminal answered; its model and app are no longer known.
                    _lastTerminalInfo = new PaxTerminalInfo { Connected = true, Sn = sn, CheckedAtUtc = DateTime.UtcNow };
                }
            }
        }

        private static readonly JsonSerializerOptions RawJsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private static string SerializeRaw(object response) => JsonSerializer.Serialize(response, RawJsonOptions);

        private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// Identity of the connected PAX terminal, as reported by POSLink Init.
    /// </summary>
    public record PaxTerminalInfo
    {
        public bool Connected { get; init; }
        public bool Busy { get; init; }
        public string? Sn { get; init; }
        public string? ModelName { get; init; }
        public string? AppName { get; init; }
        public string? AppVersion { get; init; }
        public DateTime? CheckedAtUtc { get; init; }
        public string? ErrorMessage { get; init; }
    }
}
