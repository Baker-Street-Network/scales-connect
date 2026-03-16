using Microsoft.AspNetCore.Mvc;

namespace BakerScaleConnect.Controllers
{
    /// <summary>
    /// API controller for reading scale weight from the embedded scanner scale.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ScaleController(ScaleWeightCache weightCache) : ControllerBase
    {
        /// <summary>
        /// Gets the last recorded weight. The reading is cached for 3 seconds;
        /// after expiry a fresh reading is taken from the scanner.
        /// </summary>
        /// <returns>Scale weight information or error details.</returns>
        [HttpGet("weight")]
        public ActionResult<ScaleWeightResponse> GetWeight()
        {
            try
            {
                var response = weightCache.GetLatestWeight();

                if (!response.HasWeight)
                    return StatusCode(503, response); // Service Unavailable — no weight available

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
