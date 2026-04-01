using LotusCode.Domain.Enums;

namespace LotusCode.Application.Common
{
    /// <summary>
    /// Provides centralized normalization and parsing helpers for fault report query/input values.
    /// Ensures validators and services apply the same acceptance rules.
    /// </summary>
    public static class FaultReportQueryParsing
    {
        private static readonly string[] AllowedSortBy = ["createdat", "priority"];
        private static readonly string[] AllowedSortDirection = ["asc", "desc"];

        /// <summary>
        /// Normalizes the provided text by trimming whitespace and converting empty values to <see langword="null"/>.
        /// </summary>
        public static string? NormalizeNullable(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        /// <summary>
        /// Determines whether the value is a valid fault report status string.
        /// </summary>
        public static bool IsValidStatus(string? value)
        {
            var normalized = NormalizeNullable(value);
            return normalized is null || Enum.TryParse<FaultReportStatus>(normalized, ignoreCase: true, out _);
        }

        /// <summary>
        /// Determines whether the value is a valid priority string.
        /// </summary>
        public static bool IsValidPriority(string? value)
        {
            var normalized = NormalizeNullable(value);
            return normalized is null || Enum.TryParse<PriorityLevel>(normalized, ignoreCase: true, out _);
        }

        /// <summary>
        /// Determines whether the value is a valid sort field.
        /// </summary>
        public static bool IsValidSortBy(string? value)
        {
            var normalized = NormalizeNullable(value)?.ToLowerInvariant();
            return normalized is null || AllowedSortBy.Contains(normalized);
        }

        /// <summary>
        /// Determines whether the value is a valid sort direction.
        /// </summary>
        public static bool IsValidSortDirection(string? value)
        {
            var normalized = NormalizeNullable(value)?.ToLowerInvariant();
            return normalized is null || AllowedSortDirection.Contains(normalized);
        }

        /// <summary>
        /// Tries to parse the given status value.
        /// </summary>
        public static bool TryParseStatus(string? value, out FaultReportStatus status)
        {
            return Enum.TryParse(NormalizeNullable(value), ignoreCase: true, out status);
        }

        /// <summary>
        /// Tries to parse the given priority value.
        /// </summary>
        public static bool TryParsePriority(string? value, out PriorityLevel priority)
        {
            return Enum.TryParse(NormalizeNullable(value), ignoreCase: true, out priority);
        }

        /// <summary>
        /// Normalizes sort field value and falls back to <c>createdat</c>.
        /// </summary>
        public static string NormalizeSortBy(string? value)
        {
            var normalized = NormalizeNullable(value)?.ToLowerInvariant();
            return normalized is not null && AllowedSortBy.Contains(normalized)
                ? normalized
                : "createdat";
        }

        /// <summary>
        /// Normalizes sort direction value and falls back to <c>desc</c>.
        /// </summary>
        public static string NormalizeSortDirection(string? value)
        {
            var normalized = NormalizeNullable(value)?.ToLowerInvariant();
            return normalized is not null && AllowedSortDirection.Contains(normalized)
                ? normalized
                : "desc";
        }
    }
}