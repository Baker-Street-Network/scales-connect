using Microsoft.AspNetCore.Mvc;
using BakerScaleConnect.Controllers.Models;
using BakerScaleConnect.Services;

namespace BakerScaleConnect.Controllers
{
    /// <summary>
    /// API controller for batch closing operations on PAX terminal.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ClosingController(PaxService paxService) : ControllerBase
    {
        /// <summary>
        /// Process a batch close operation to settle all transactions.
        /// </summary>
        /// <param name="request">Batch close request details including reference number.</param>
        /// <param name="cancellationToken">Cancellation token to abort the operation.</param>
        /// <returns>Batch close operation result.</returns>
        [HttpPost("batch")]
        public async Task<ActionResult<PaxBatchCloseResponse>> ProcessBatchClose(
            [FromBody] PaxBatchCloseRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                // Validate request
                if (string.IsNullOrWhiteSpace(request.EcrReferenceNumber))
                {
                    return BadRequest(new PaxBatchCloseResponse
                    {
                        Success = false,
                        ErrorMessage = "ECR Reference Number is required",
                        Timestamp = DateTime.UtcNow
                    });
                }

                // Process the batch close with cancellation support
                var response = await paxService.ProcessBatchCloseAsync(request, cancellationToken);

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
                return StatusCode(499, new PaxBatchCloseResponse
                {
                    Success = false,
                    ErrorMessage = "Request cancelled by client",
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new PaxBatchCloseResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Health check endpoint for closing service availability.
        /// </summary>
        /// <returns>Health status.</returns>
        [HttpGet("health")]
        public ActionResult GetHealth() =>
            Ok(new { status = "healthy", service = "closing", timestamp = DateTime.UtcNow });
    }
}
