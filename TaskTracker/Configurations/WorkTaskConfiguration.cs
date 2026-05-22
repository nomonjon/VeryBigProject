using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskTracker.Models;

namespace TaskTracker.Configurations;

public class WorkTaskConfiguration : IEntityTypeConfiguration<WorkTask>
{
    public void Configure(EntityTypeBuilder<WorkTask> builder)
    {
        builder.HasKey(wt => wt.Id);

        builder.Property(wt => wt.Name)
           .IsRequired()
           .HasMaxLength(200);

        builder.Property(wt => wt.Description)
            .HasMaxLength(500);

        builder.Property(wt => wt.Status).HasDefaultValue(Status.InProgress);


        builder.HasIndex(wt => wt.Name);
        builder.HasIndex(wt => wt.Status);

        builder.HasOne(wt => wt.Assignee)
                .WithMany(u => u.WorkTasks)
                .HasForeignKey(wt => wt.AssigneeId);

        builder.HasOne(wt => wt.Project)
                .WithMany(p => p.WorkTasks)
                .HasForeignKey(wt => wt.ProjectId);
    }
}