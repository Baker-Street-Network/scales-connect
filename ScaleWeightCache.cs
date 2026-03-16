namespace BakerScaleConnect
{
    /// <summary>
    /// Thread-safe in-memory cache for the last scale weight reading.
    /// Readings expire after a configurable TTL (default 3 seconds).
    /// </summary>
    public class ScaleWeightCache
    {
        private readonly ScannerManager _scannerManager;
        private readonly TimeSpan _ttl;
        private readonly object _lock = new();

        private ScaleWeightResult? _cached;
        private DateTime _cachedAt;

        public ScaleWeightCache(ScannerManager scannerManager, TimeSpan? ttl = null)
        {
            _scannerManager = scannerManager;
            _ttl = ttl ?? TimeSpan.FromSeconds(3);
        }

        /// <summary>
        /// Gets the latest weight. Returns a fresh reading from the scanner
        /// and caches it. If a cached value exists and is still within the TTL,
        /// returns it immediately. Once expired, the cached value is discarded.
        /// </summary>
        public ScaleWeightResponse GetLatestWeight()
        {
            lock (_lock)
            {
                // If we have a cached value that hasn't expired, return it
                if (_cached != null && DateTime.UtcNow - _cachedAt < _ttl)
                {
                    return ToResponse(_cached, _cachedAt);
                }

                // Expired or no cache — read fresh from scanner
                var result = _scannerManager.ReadWeight();

                if (result.Success)
                {
                    _cached = result;
                    _cachedAt = DateTime.UtcNow;
                    return ToResponse(result, _cachedAt);
                }

                // Read failed — clear cache and return error
                _cached = null;
                return new ScaleWeightResponse
                {
                    HasWeight = false,
                    ErrorMessage = result.ErrorMessage
                };
            }
        }

        private ScaleWeightResponse ToResponse(ScaleWeightResult result, DateTime readAt)
        {
            var age = DateTime.UtcNow - readAt;
            return new ScaleWeightResponse
            {
                HasWeight = true,
                Weight = result.Weight,
                WeightUnit = result.WeightUnit,
                ScaleStatus = result.ScaleStatus,
                ScaleStatusDescription = result.ScaleStatusDescription,
                ReadAt = readAt,
                ExpiresAt = readAt + _ttl,
                AgeMs = (int)age.TotalMilliseconds
            };
        }
    }
}
