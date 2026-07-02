using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace BakerScaleConnect.Services
{
    public class PhoneCollectService
    {
        private static readonly JsonSerializerOptions TriggerJsonOptions =
            new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

        private readonly ILogger<PhoneCollectService> _logger;
        private readonly AppSettings _settings;

        public PhoneCollectService(ILogger<PhoneCollectService> logger, AppSettings settings)
        {
            _logger = logger;
            _settings = settings;
        }

        /// <summary>
        /// Send TCP trigger to Aries 8 to open the Odoo customer display in GeckoView.
        /// Returns once the trigger is sent — Odoo handles the display session.
        /// </summary>
        public async Task ShowCustomerDisplayAsync(string orderId, string displayUrl, string? logoUrl, string? logoVersion, CancellationToken ct)
        {
            var aries = _settings.Aries;

            if (string.IsNullOrWhiteSpace(aries.TerminalIp))
                throw new InvalidOperationException("Aries terminal IP not configured in settings.");

            if (aries.PhonePort is < 1 or > 65535)
                throw new InvalidOperationException($"Aries PhonePort {aries.PhonePort} is not a valid TCP port (1-65535).");

            await SendCustomerDisplayTriggerAsync(orderId, displayUrl, logoUrl, logoVersion, aries.TerminalIp, aries.PhonePort, ct);
            _logger.LogInformation("Customer display trigger sent — order={OrderId}", orderId);
        }

        private async Task SendCustomerDisplayTriggerAsync(string orderId, string displayUrl, string? logoUrl, string? logoVersion, string ip, int port, CancellationToken ct)
        {
            var trigger = new
            {
                action = "show_customer_display",
                order_id = orderId,
                display_url = displayUrl,
                logo_url = logoUrl,
                logo_version = logoVersion
            };

            string json = JsonSerializer.Serialize(trigger, TriggerJsonOptions);
            byte[] data = Encoding.UTF8.GetBytes(json + "\n");

            using var client = new TcpClient();
            await client.ConnectAsync(ip, port).WaitAsync(ct);
            var stream = client.GetStream();
            await stream.WriteAsync(data, 0, data.Length, ct);
            await stream.FlushAsync(ct);
            // Hold connection open briefly so receiver can drain before TCP FIN arrives.
            await Task.Delay(1500, ct);
        }
    }
}
