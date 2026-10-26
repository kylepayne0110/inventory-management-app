using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Inventory.Api.Tests;

public class DatabaseHealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    // Port 1 is closed, so the connection fails fast. The password is fake.
    private const string UnreachableConnection =
        "Server=127.0.0.1,1;Database=Missing;User Id=sa;Password=Fake_Test_Password_1;Connect Timeout=1;TrustServerCertificate=True";

    private readonly WebApplicationFactory<Program> _factory;

    public DatabaseHealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetDatabaseHealth_WhenSqlServerUnreachable_Returns503WithoutDetails()
    {
        var client = _factory
            .WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:InventoryDb", UnreachableConnection))
            .CreateClient();

        var response = await client.GetAsync("/health/db");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("Unhealthy", body);
        Assert.DoesNotContain("Fake_Test_Password_1", body);
        Assert.DoesNotContain("127.0.0.1", body);
    }
}
