using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskTracker.Models;

namespace TaskTracker.Configurations;

public class TaskCommentConfiguration : IEntityTypeConfiguration<TaskComment>
{
    public void Configure(EntityTypeBuilder<TaskComment> builder)
    {
        builder.HasKey(tc => tc.Id);

        builder.Property(tc => tc.Content).IsRequired().HasMaxLength(1000);

        builder.HasOne(tc => tc.WorkTask)
            .WithMany(wt => wt.Comments)
            .HasForeignKey(tc => tc.WorkTaskId);

        builder.HasOne(tc => tc.User)
            .WithMany()
            .HasForeignKey(tc => tc.UserId);
    }
}
