using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskTracker.Models;

namespace TaskTracker.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.FullName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.Email).IsRequired();
        builder.Property(u => u.Position).HasMaxLength(100);


        builder.HasMany(u => u.WorkTasks)
                .WithOne(wt => wt.Assignee)
                .HasForeignKey(wt => wt.AssigneeId);
    }
}
