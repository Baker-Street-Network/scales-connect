using Microsoft.AspNetCore.Mvc;
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
    public class PaxController(PaxService paxService) : ControllerBase
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
                // Validate request
                if (string.IsNullOrWhiteSpace(request.Amount))
                {
                    return BadRequest(new PaxCreditResponse
                    {
                        Success = false,
                        ErrorMessage = "Amount is required",
                        Timestamp = DateTime.UtcNow
                    });
                }

                if (string.IsNullOrWhiteSpace(request.EcrReferenceNumber))
                {
                    return BadRequest(new PaxCreditResponse
                    {
                        Success = false,
                        ErrorMessage = "ECR Reference Number is required",
                        Timestamp = DateTime.UtcNow
                    });
                }

                //convert amount to decimal
                var amount = decimal.Parse(request.Amount);
                amount *= 100;
                request.Amount = amount.ToString("F0");

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
    }
}
