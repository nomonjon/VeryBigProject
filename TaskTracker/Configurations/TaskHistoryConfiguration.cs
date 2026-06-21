using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskTracker.Models;

namespace TaskTracker.Configurations;

public class TaskHistoryConfiguration : IEntityTypeConfiguration<TaskHistory>
{
    public void Configure(EntityTypeBuilder<TaskHistory> builder)
    {
        builder.HasKey(th => th.Id);

        builder.Property(th => th.FieldName).IsRequired().HasMaxLength(100);
        builder.Property(th => th.OldValue).HasMaxLength(500);
        builder.Property(th => th.NewValue).HasMaxLength(500);

        builder.HasOne(th => th.WorkTask)
            .WithMany(wt => wt.History)
            .HasForeignKey(th => th.WorkTaskId);

        builder.HasOne(th => th.User)
            .WithMany()
            .HasForeignKey(th => th.UserId);
    }
}
