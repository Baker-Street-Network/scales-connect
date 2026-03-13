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
                _logger.LogInformation("Processing credit payment: Amount={Amount}, EcrRef={EcrRef}, Method={Method}", 
                    paymentRequest.Amount, paymentRequest.EcrReferenceNumber, _connectionMethod);

                // Get POSLink instance
                POSLinkSemi poslinkSemi = POSLinkSemi.GetPOSLinkSemi();
                Terminal terminal;

                // Configure connection based on method
                if (_connectionMethod == "USB")
                {
                    UartSetting uartSetting = new()
                    {
                        BaudRate = 115200,
                        Timeout = _settings.Timeout,
                        SerialPortName = _serialPort,
                    };
                    terminal = poslinkSemi.GetTerminal(uartSetting);
                }
                else
                {
                    // Configure TCP settings
                    TcpSetting tcpSetting = new()
                    {
                        Ip = _settings.Ip,
                        Port = _settings.Port,
                        Timeout = _settings.Timeout
                    };
                    terminal = poslinkSemi.GetTerminal(tcpSetting);
                }

                // Store active terminal for cancellation
                lock (_terminalLock)
                {
                    _activeTerminal = terminal;
                }

                try
                {
                    // Create the credit request
                    DoCreditRequest request = new();

                    // Set amount information
                    POSLinkAdmin.Util.AmountRequest amountReq = new()
                    {
                        TransactionAmount = paymentRequest.Amount,
                    };
                    request.AmountInformation = amountReq;

                    // Set trace information
                    POSLinkSemiIntegration.Util.TraceRequest traceReq = new()
                    {
                        EcrReferenceNumber = paymentRequest.EcrReferenceNumber
                    };
                    request.TraceInformation = traceReq;

                    // Set transaction type (default to Sale if not specified)
                    request.TransactionType = paymentRequest.TransactionType switch
                    {
                        "Return" => POSLinkAdmin.Const.TransactionType.Return,
                        "Void" => POSLinkAdmin.Const.TransactionType.Void,
                        _ => POSLinkAdmin.Const.TransactionType.Sale
                    };

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
                        _logger.LogInformation("Credit payment successful: Code={Code}, Message={Message}",
                            creditResponse.ResponseCode, creditResponse.ResponseMessage);

                        
                        return new PaxCreditResponse
                        {
                            Success = true,
                            ResponseCode = creditResponse.ResponseCode ?? "",
                            ResponseMessage = creditResponse.ResponseMessage ?? "",
                            EcrReferenceNumber = paymentRequest.EcrReferenceNumber,
                            Timestamp = DateTime.UtcNow,
                            
                            CardBin = creditResponse.AccountInformation.CardBin,
                            AuthorizationCode = creditResponse.HostInformation.AuthorizationCode,
                            BatchNumber = creditResponse.HostInformation.BatchNumber,
                            ControlNumber = creditResponse.HostInformation.ControlNumber,
                            GatewayTransactionId = creditResponse.HostInformation.GatewayTransactionId,
                            HostDetailedMessage = creditResponse.HostInformation.HostDetailedMessage,
                            HostReferenceNumber = creditResponse.HostInformation.HostReferenceNumber,
                            RawResponse = JsonSerializer.Serialize(
                                creditResponse,
                                new JsonSerializerOptions
                                {
                                    WriteIndented = true,
                                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                                }
                            )
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

                // Get POSLink instance
                POSLinkSemi poslinkSemi = POSLinkSemi.GetPOSLinkSemi();
                Terminal terminal;

                // Configure connection based on method
                if (_connectionMethod == "USB")
                {
                    UartSetting uartSetting = new()
                    {
                        BaudRate = 115200,
                        Timeout = _settings.Timeout,
                        SerialPortName = _serialPort,
                    };
                    terminal = poslinkSemi.GetTerminal(uartSetting);
                }
                else
                {
                    // Configure TCP settings
                    TcpSetting tcpSetting = new()
                    {
                        Ip = _settings.Ip,
                        Port = _settings.Port,
                        Timeout = _settings.Timeout
                    };
                    terminal = poslinkSemi.GetTerminal(tcpSetting);
                }

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

                        return new PaxBatchCloseResponse
                        {
                            Success = true,
                            ResponseCode = response.ResponseCode ?? "",
                            ResponseMessage = response.ResponseMessage ?? "",
                            EcrReferenceNumber = batchRequest.EcrReferenceNumber,
                            Timestamp = DateTime.UtcNow
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
    }
}
