using LotusCode.Domain.Enums;

namespace LotusCode.Domain.Entities
{
    public sealed class User
    {
        public Guid Id { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public UserRole Role { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public ICollection<FaultReport> FaultReports { get; set; } = new List<FaultReport>();
    }
}