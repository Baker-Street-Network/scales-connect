using Microsoft.AspNetCore.Mvc;
using BakerScaleConnect.Services;

namespace BakerScaleConnect.Controllers
{
    /// <summary>
    /// API controller for checking scanner connectivity status.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ConnectivityController(ConnectivityService connectivityService) : ControllerBase
    {
        /// <summary>
        /// Gets the overall connectivity status.
        /// </summary>
        /// <returns>Connectivity status information</returns>
        [HttpGet("status")]
        public ActionResult<ConnectivityStatusResponse> GetStatus()
        {
            try
            {
                var status = connectivityService.GetConnectivityStatus();
                return Ok(status);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Gets detailed information about all discovered scanners.
        /// </summary>
        /// <returns>List of scanner information</returns>
        [HttpGet("scanners")]
        public ActionResult<List<ScannerResponse>> GetScanners()
        {
            try
            {
                var scanners = connectivityService.GetScanners();
                return Ok(scanners);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Basic health check endpoint.
        /// </summary>
        /// <returns>Health status</returns>
        [HttpGet("/health")]
        public ActionResult GetHealth() =>
            Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
}
