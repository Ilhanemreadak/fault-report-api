namespace LotusCode.Application.Common
{
    /// <summary>
    /// Provides a single normalization strategy for fault report location values.
    /// </summary>
    public static class FaultReportLocationNormalizer
    {
        /// <summary>
        /// Normalizes location text with trim + lower-invariant rules.
        /// </summary>
        public static string Normalize(string value)
        {
            return value.Trim().ToLowerInvariant();
        }
    }
}