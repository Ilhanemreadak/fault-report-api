using LotusCode.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LotusCode.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Configures database mapping for the FaultReport entity.
    /// Defines property requirements, length constraints and relationship
    /// between fault reports and users.
    /// </summary>
    public sealed class FaultReportConfiguration : IEntityTypeConfiguration<FaultReport>
    {
        public void Configure(EntityTypeBuilder<FaultReport> builder)
        {
            builder.ToTable("FaultReports");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Title)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(x => x.Description)
                .IsRequired()
                .HasMaxLength(2000);

            builder.Property(x => x.Location)
                .IsRequired()
                .HasMaxLength(300);

            builder.Property(x => x.Priority)
                .IsRequired();

            builder.Property(x => x.Status)
                .IsRequired();

            builder.Property(x => x.CreatedAtUtc)
                .IsRequired();

            builder.Property(x => x.UpdatedAtUtc)
                .IsRequired();

            builder.HasOne(x => x.CreatedByUser)
                .WithMany(x => x.FaultReports)
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}