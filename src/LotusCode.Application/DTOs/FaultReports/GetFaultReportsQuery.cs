using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LotusCode.Application.DTOs.FaultReports
{
    public sealed class GetFaultReportsQuery
    {
        public string? Status { get; init; }

        public string? Priority { get; init; }

        public string? Location { get; init; }

        public int Page { get; init; } = 1;

        public int PageSize { get; init; } = 10;

        public string SortBy { get; init; } = "createdAt";

        public string SortDirection { get; init; } = "desc";
    }
}
