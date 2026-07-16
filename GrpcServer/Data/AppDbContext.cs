using GrpcServer.Models;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace GrpcServer.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductRule> ProductRules => Set<ProductRule>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Product>(x =>
        {
            x.HasKey(p => p.Id);

            x.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(200);

            x.Property(p => p.Price)
                .HasColumnType("decimal(18,2)");

            x.Property(p => p.StatusColor)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue(ProductColors.Default);
        });

        builder.Entity<ProductRule>(x =>
        {
            x.HasKey(r => r.Id);

            x.Property(r => r.Name)
                .IsRequired()
                .HasMaxLength(200);

            x.Property(r => r.Expression)
                .IsRequired()
                .HasMaxLength(1000);

            x.Property(r => r.Color)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue(ProductColors.Orange);
        });
    }
}