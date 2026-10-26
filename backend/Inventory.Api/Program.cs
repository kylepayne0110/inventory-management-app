var builder = WebApplication.CreateBuilder(args);

// Generates the OpenAPI document that describes this API.
builder.Services.AddOpenApi();

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

app.Run();

// Makes Program visible to the test project so it can start the API in memory.
public partial class Program;
