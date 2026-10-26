using Inventory.Api.Contracts;
using Inventory.Api.Data;
using Inventory.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Endpoints;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        // There is deliberately no DELETE endpoint: products are deactivated, not removed.
        var group = app.MapGroup("/api/products").WithTags("Products");

        group.MapPost("/", async (CreateProductRequest request, InventoryDbContext db, CancellationToken ct) =>
        {
            var sku = request.Sku!.Trim().ToUpperInvariant();

            if (await db.Products.AnyAsync(p => p.Sku == sku, ct))
            {
                return SkuConflict(sku);
            }

            var now = DateTimeOffset.UtcNow;
            var product = new Product
            {
                Sku = sku,
                Name = request.Name!.Trim(),
                Description = Clean(request.Description),
                UnitOfMeasure = Clean(request.UnitOfMeasure) ?? "each",
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.Products.Add(product);

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
            {
                // Another request created the same SKU between the check above and the save.
                return SkuConflict(sku);
            }

            return Results.Created($"/api/products/{product.Id}", ProductResponse.From(product));
        }).WithName("CreateProduct");

        // ?isActive=true or ?isActive=false filters the list; omit it to list everything.
        group.MapGet("/", async (bool? isActive, InventoryDbContext db, CancellationToken ct) =>
        {
            var query = db.Products.AsNoTracking();
            if (isActive is not null)
            {
                query = query.Where(p => p.IsActive == isActive);
            }

            var products = await query.OrderBy(p => p.Sku).ToListAsync(ct);
            return Results.Ok(products.Select(ProductResponse.From));
        }).WithName("ListProducts");

        group.MapGet("/{id:int}", async (int id, InventoryDbContext db, CancellationToken ct) =>
            await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct) is { } product
                ? Results.Ok(ProductResponse.From(product))
                : Results.NotFound()).WithName("GetProduct");

        group.MapPut("/{id:int}", async (int id, UpdateProductRequest request, InventoryDbContext db, CancellationToken ct) =>
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (product is null)
            {
                return Results.NotFound();
            }

            product.Name = request.Name!.Trim();
            product.Description = Clean(request.Description);
            product.UnitOfMeasure = Clean(request.UnitOfMeasure) ?? "each";
            product.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            return Results.Ok(ProductResponse.From(product));
        }).WithName("UpdateProduct");

        // Safe to repeat: deactivating an already inactive product changes nothing.
        group.MapPost("/{id:int}/deactivate", async (int id, InventoryDbContext db, CancellationToken ct) =>
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (product is null)
            {
                return Results.NotFound();
            }

            if (product.IsActive)
            {
                product.IsActive = false;
                product.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
            }

            return Results.Ok(ProductResponse.From(product));
        }).WithName("DeactivateProduct");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IResult SkuConflict(string sku) =>
        Results.Conflict(new { error = $"A product with SKU '{sku}' already exists." });
}
