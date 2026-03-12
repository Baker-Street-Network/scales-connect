using BakerScaleConnect.Controllers.Models;
using Microsoft.Extensions.Logging;
using POSLinkCore.CommunicationSetting;
using POSLinkSemiIntegration;
using POSLinkSemiIntegration.Transaction;
using POSLinkUart;

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
        public PaxCreditResponse ProcessCreditPayment(PaxCreditRequest paymentRequest)
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

                // Execute the transaction
                POSLinkAdmin.ExecutionResult result = terminal.Transaction.DoCredit(request, out DoCreditResponse response);

                // Build response
                if (result.GetErrorCode() == POSLinkAdmin.ExecutionResult.Code.Ok)
                {
                    _logger.LogInformation("Credit payment successful: Code={Code}, Message={Message}",
                        response.ResponseCode, response.ResponseMessage);

                    return new PaxCreditResponse
                    {
                        Success = true,
                        ResponseCode = response.ResponseCode ?? "",
                        ResponseMessage = response.ResponseMessage ?? "",
                        EcrReferenceNumber = paymentRequest.EcrReferenceNumber,
                        Timestamp = DateTime.UtcNow
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
                _logger.LogInformation("Canceling current PAX terminal operation: Method={Method}", _connectionMethod);

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

                // Cancel the current operation
                terminal.Cancel();

                _logger.LogInformation("PAX terminal operation canceled successfully");
                return (true, "Operation canceled successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error canceling PAX terminal operation");
                return (false, $"Failed to cancel operation: {ex.Message}");
            }
        }
    }
}
