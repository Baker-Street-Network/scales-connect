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

        // Pending requests waiting for terminal callback
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

            var tcs = new TaskCompletionSource<PhoneResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[orderId] = tcs;

            try
            {
                await SendTcpTriggerAsync(orderId, aries.TerminalIp, aries.PhonePort, aries.CallbackPort);

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(aries.TimeoutSeconds));

                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, timeoutCts.Token));

                if (completedTask == tcs.Task)
                    return await tcs.Task;

                // Timeout — return skipped result
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
                _logger.LogInformation("Phone result received — order={OrderId} skipped={Skipped} phone={Phone}",
                    result.OrderId, result.Skipped, result.Skipped ? "(none)" : result.Phone);
            }
            else
            {
                _logger.LogWarning("Received phone result for unknown/expired order {OrderId}", result.OrderId);
            }
        }

        /// <summary>
        /// Check if a result is available without blocking (used by polling endpoint).
        /// </summary>
        public bool TryGetPendingResult(string orderId, out PhoneResult? result)
        {
            if (_pending.TryGetValue(orderId, out var tcs) && tcs.Task.IsCompleted)
            {
                result = tcs.Task.Result;
                return true;
            }
            result = null;
            return false;
        }

        private async Task SendTcpTriggerAsync(string orderId, string ip, int port, int callbackPort)
        {
            var trigger = new
            {
                action = "collect_phone",
                order_id = orderId,
                callback_ip = GetLocalIp(),
                callback_port = callbackPort
            };

            string json = JsonSerializer.Serialize(trigger);
            byte[] data = Encoding.UTF8.GetBytes(json + "\n");

            using var client = new TcpClient();
            await client.ConnectAsync(ip, port);
            await client.GetStream().WriteAsync(data);

            _logger.LogInformation("TCP trigger sent to {Ip}:{Port} for order {OrderId}", ip, port, orderId);
        }

        private string GetLocalIp()
        {
            // Get the local IP that the terminal can reach back to
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 80);
            return (socket.LocalEndPoint as System.Net.IPEndPoint)!.Address.ToString();
        }
    }
}
