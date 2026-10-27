using BakerScaleConnect.Services;
using Microsoft.AspNetCore.Mvc;

namespace BakerScaleConnect.Controllers
{
    /// <summary>
    /// API controller for opening the cash drawer through the receipt printer.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class CashDrawerController(AppSettings settings) : ControllerBase
    {
        /// <summary>
        /// Sends a cash drawer kick command to the configured printer or serial port.
        /// </summary>
        [HttpPost("kick")]
        public IActionResult Kick()
        {
            try
            {
                string target = CashDrawerService.Kick(settings.CashDrawer);
                return Ok(new { success = true, port = target });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
