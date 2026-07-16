using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GrpcServer.Data;

// Used only by `dotnet ef` at design time; avoids running Program.cs,
// which requires a live RabbitMQ connection. 5431 is the host-mapped
// port of the product.database container (see docker-compose.yml).
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5431;Database=productdb;Username=postgres;Password=postgres")
            .Options;

        return new AppDbContext(options);
    }
}
