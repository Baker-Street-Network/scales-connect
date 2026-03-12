using BakerScaleConnect.Controllers.Models;
using Microsoft.Extensions.Logging;
using POSLinkCore.CommunicationSetting;
using POSLinkSemiIntegration;
using POSLinkSemiIntegration.Transaction;

namespace BakerScaleConnect.Services
{
    /// <summary>
    /// Service for handling PAX credit card terminal operations.
    /// </summary>
    public class PaxService
    {
        private readonly ILogger<PaxService> _logger;
        private readonly PaxTerminalSettings _settings;

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
                _logger.LogInformation("Processing credit payment: Amount={Amount}, EcrRef={EcrRef}", 
                    paymentRequest.Amount, paymentRequest.EcrReferenceNumber);

                // Configure TCP settings
                TcpSetting tcpSetting = new()
                {
                    Ip = _settings.Ip,
                    Port = _settings.Port,
                    Timeout = _settings.Timeout
                };

                // Get POSLink instance and terminal
                POSLinkSemi poslinkSemi = POSLinkSemi.GetPOSLinkSemi();
                Terminal terminal = poslinkSemi.GetTerminal(tcpSetting);

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
        public void UpdateSettings(string ip, int port, int timeout)
        {
            _settings.Ip = ip;
            _settings.Port = port;
            _settings.Timeout = timeout;
            _logger.LogInformation("PAX terminal settings updated: IP={Ip}, Port={Port}, Timeout={Timeout}", 
                ip, port, timeout);
        }

        /// <summary>
        /// Get current terminal settings.
        /// </summary>
        public PaxTerminalSettings GetSettings()
        {
            return new PaxTerminalSettings
            {
                Ip = _settings.Ip,
                Port = _settings.Port,
                Timeout = _settings.Timeout
            };
        }
    }
}
