using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LotusCode.Application.DTOs.FaultReports
{
    public sealed class FaultReportListItemDto
    {
        public Guid Id { get; init; }

        public string Title { get; init; } = string.Empty;

        public string Location { get; init; } = string.Empty;

        public string Priority { get; init; } = string.Empty;

        public string Status { get; init; } = string.Empty;

        public string CreatedByFullName { get; init; } = string.Empty;

        public DateTime CreatedAtUtc { get; init; }

        public DateTime UpdatedAtUtc { get; init; }
    }
}
