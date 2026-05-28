using Microsoft.AspNetCore.Mvc;
using System.IO.Ports;

namespace BakerScaleConnect.Controllers
{
    /// <summary>
    /// API controller for sending commands to a cash drawer via serial port.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class CashDrawerController(AppSettings settings) : ControllerBase
    {
        /// <summary>
        /// Sends a cash drawer kick command to the configured serial port.
        /// </summary>
        [HttpPost("kick")]
        public IActionResult Kick()
        {
            string portName = settings.CashDrawer.SerialPort;

            if (string.IsNullOrWhiteSpace(portName))
                return BadRequest(new { error = "No cash drawer serial port configured." });

            try
            {
                using var port = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
                port.Open();
                // Standard ESC/POS cash drawer kick: ESC p m t1 t2
                port.Write([0x1B, 0x70, 0x00, 0x19, 0xFA], 0, 5);
                port.Close();
                return Ok(new { success = true, port = portName });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
