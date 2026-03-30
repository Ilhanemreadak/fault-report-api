using LotusCode.Domain.Enums;

namespace LotusCode.Domain.Entities
{
    public sealed class FaultReport
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Location { get; set; } = string.Empty;

        public PriorityLevel Priority { get; set; }

        public FaultReportStatus Status { get; set; }

        public Guid CreatedByUserId { get; set; }

        public User CreatedByUser { get; set; } = default!;

        public DateTime CreatedAtUtc { get; set; }

        public DateTime UpdatedAtUtc { get; set; }
    }
}
