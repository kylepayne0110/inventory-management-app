using Inventory.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Inventory.Api.Tests;

// Starts the API in memory with an in-memory database instead of SQL Server, so tests need no running database.
// Each factory instance gets its own empty database.
public class InventoryApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:InventoryDb", "unused");
        builder.ConfigureServices(services =>
        {
            // Remove the SQL Server registration before adding the in-memory one.
            services.RemoveAll<DbContextOptions<InventoryDbContext>>();
            // EF Core 10 also keeps the provider setup in an internal IDbContextOptionsConfiguration<InventoryDbContext> service.
            services.RemoveAll(typeof(DbContextOptions<InventoryDbContext>).Assembly
                .GetType("Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration`1")!
                .MakeGenericType(typeof(InventoryDbContext)));
            services.AddDbContext<InventoryDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }
}
