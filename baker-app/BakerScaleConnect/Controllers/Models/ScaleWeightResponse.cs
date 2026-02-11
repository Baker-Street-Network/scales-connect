namespace BakerScaleConnect
{
    /// <summary>
    /// Response model for scale weight readings.
    /// </summary>
    public class ScaleWeightResponse
    {
        /// <summary>Whether a valid weight reading is available.</summary>
        public bool HasWeight { get; set; }

        /// <summary>The weight value as a string (e.g. "1.234").</summary>
        public string Weight { get; set; } = "";

        /// <summary>The unit of the weight (e.g. "Kilograms", "Pounds").</summary>
        public string WeightUnit { get; set; } = "";

        /// <summary>Numeric scale status code from the SDK.</summary>
        public int ScaleStatus { get; set; }

        /// <summary>Human-readable scale status.</summary>
        public string ScaleStatusDescription { get; set; } = "";

        /// <summary>When this reading was taken (UTC).</summary>
        public DateTime ReadAt { get; set; }

        /// <summary>When this cached reading expires (UTC).</summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>Age of this reading in milliseconds.</summary>
        public int AgeMs { get; set; }

        /// <summary>Error message if HasWeight is false.</summary>
        public string? ErrorMessage { get; set; }
    }
}
