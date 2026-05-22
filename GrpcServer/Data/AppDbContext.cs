using GrpcServer.Models;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace GrpcServer.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

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
        });
    }
}