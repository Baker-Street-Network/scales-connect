using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace BakerScaleConnect.Services
{
    public class PhoneResult
    {
        public string OrderId { get; set; } = "";
        public string Phone { get; set; } = "";
        public bool Skipped { get; set; }
    }

    public class PhoneCollectService
    {
        private readonly ILogger<PhoneCollectService> _logger;
        private readonly AppSettings _settings;

        private readonly ConcurrentDictionary<string, TaskCompletionSource<PhoneResult>> _pending = new();

        public PhoneCollectService(ILogger<PhoneCollectService> logger, AppSettings settings)
        {
            _logger = logger;
            _settings = settings;
        }

        /// <summary>
        /// Send TCP trigger to Aries 8 and wait for the phone result callback.
        /// Called by PaxController when Odoo starts a checkout.
        /// </summary>
        public async Task<PhoneResult> RequestPhoneAsync(string orderId, CancellationToken ct)
        {
            var aries = _settings.Aries;

            if (string.IsNullOrWhiteSpace(aries.TerminalIp))
                throw new InvalidOperationException("Aries terminal IP not configured in settings.");

            if (aries.PhonePort is < 1 or > 65535)
                throw new InvalidOperationException($"Aries PhonePort {aries.PhonePort} is not a valid TCP port (1-65535).");

            if (aries.CallbackPort is < 1 or > 65535)
                throw new InvalidOperationException($"Aries CallbackPort {aries.CallbackPort} is not a valid TCP port (1-65535).");

            if (aries.TimeoutSeconds <= 0)
                throw new InvalidOperationException("Aries TimeoutSeconds must be greater than 0.");

            // Reuse existing TCS if a request for this order is already in flight
            var tcs = _pending.GetOrAdd(orderId,
                _ => new TaskCompletionSource<PhoneResult>(TaskCreationOptions.RunContinuationsAsynchronously));

            // If already completed (e.g. result arrived before we awaited), return immediately
            if (tcs.Task.IsCompleted)
                return await tcs.Task;

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(aries.TimeoutSeconds));

                await SendTcpTriggerAsync(orderId, aries.TerminalIp, aries.PhonePort, aries.CallbackPort, timeoutCts.Token);

                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, timeoutCts.Token));

                if (completedTask == tcs.Task)
                    return await tcs.Task;

                _logger.LogWarning("Phone collect timed out for order {OrderId}", orderId);
                return new PhoneResult { OrderId = orderId, Phone = "", Skipped = true };
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Phone collect cancelled for order {OrderId}", orderId);
                return new PhoneResult { OrderId = orderId, Phone = "", Skipped = true };
            }
            finally
            {
                _pending.TryRemove(orderId, out _);
            }
        }

        /// <summary>
        /// Called by PaxController when terminal POSTs back the phone result.
        /// </summary>
        public void ResolveResult(PhoneResult result)
        {
            if (_pending.TryGetValue(result.OrderId, out var tcs))
            {
                tcs.TrySetResult(result);
                // Log masked phone — last 4 digits only to avoid PII in logs
                var masked = result.Skipped ? "(none)" : MaskPhone(result.Phone);
                _logger.LogInformation("Phone result received — order={OrderId} skipped={Skipped} phone={Phone}",
                    result.OrderId, result.Skipped, masked);
            }
            else
            {
                _logger.LogWarning("Received phone result for unknown/expired order {OrderId}", result.OrderId);
            }
        }

        private async Task SendTcpTriggerAsync(string orderId, string ip, int port, int callbackPort, CancellationToken ct)
        {
            var callbackIp = GetLocalIpTowards(ip);

            var trigger = new
            {
                action = "collect_phone",
                order_id = orderId,
                callback_ip = callbackIp,
                callback_port = callbackPort
            };

            string json = JsonSerializer.Serialize(trigger);
            byte[] data = Encoding.UTF8.GetBytes(json + "\n");

            using var client = new TcpClient();
            await client.ConnectAsync(ip, port).WaitAsync(ct);
            await client.GetStream().WriteAsync(data, 0, data.Length, ct);

            _logger.LogInformation("TCP trigger sent to {Ip}:{Port} for order {OrderId}", ip, port, orderId);
        }

        /// <summary>
        /// Returns the local IP address that would be used to reach the given destination.
        /// Ensures callback_ip is reachable from the terminal on the same network segment.
        /// </summary>
        private static string GetLocalIpTowards(string destinationIp)
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(destinationIp, 80);
            return ((System.Net.IPEndPoint)socket.LocalEndPoint!).Address.ToString();
        }

        private static string MaskPhone(string phone)
        {
            if (string.IsNullOrEmpty(phone) || phone.Length < 4)
                return "****";
            return new string('*', phone.Length - 4) + phone[^4..];
        }
    }
}
