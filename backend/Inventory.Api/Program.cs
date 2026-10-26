using Inventory.Api.Data;
using Inventory.Api.Endpoints;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Generates the OpenAPI document that describes this API.
builder.Services.AddOpenApi();

// Checks incoming requests against the validation attributes (Required, StringLength, ...) and answers 400 when they fail.
builder.Services.AddValidation();

// The connection string is read from configuration (User Secrets or environment variables), never from appsettings files.
// Key: ConnectionStrings:InventoryDb. Environment variable form: ConnectionStrings__InventoryDb.
builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("InventoryDb")));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // The OpenAPI document is served at /openapi/v1.json.
    app.MapOpenApi();

    // Swagger UI is a web page for reading and trying the API. It is served at /swagger.
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Inventory API v1"));
}

// A simple endpoint to confirm the API is running.
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
    .WithName("GetHealth");

// Reports whether SQL Server is reachable. Failure details are deliberately not returned,
// because they can contain server names or credentials.
app.MapGet("/health/db", async (IServiceProvider services, CancellationToken cancellationToken) =>
{
    try
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        // Resolved inside the try so a missing connection string reports Unhealthy instead of throwing.
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        if (await db.Database.CanConnectAsync(timeout.Token))
        {
            return Results.Ok(new { status = "Healthy" });
        }
    }
    catch (Exception)
    {
        // Intentionally swallowed: the response must not expose exception details.
    }

    return Results.Json(new { status = "Unhealthy" }, statusCode: StatusCodes.Status503ServiceUnavailable);
}).WithName("GetDatabaseHealth");

app.MapProductEndpoints();
app.MapWarehouseEndpoints();

app.Run();

// Makes Program visible to the test project so it can start the API in memory.
public partial class Program;
