using Inventory.Api.Contracts;
using Inventory.Api.Data;
using Inventory.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Endpoints;

public static class WarehouseEndpoints
{
    public static void MapWarehouseEndpoints(this IEndpointRouteBuilder app)
    {
        // There is deliberately no DELETE endpoint: warehouses are deactivated, not removed.
        var group = app.MapGroup("/api/warehouses").WithTags("Warehouses");

        group.MapPost("/", async (CreateWarehouseRequest request, InventoryDbContext db, CancellationToken ct) =>
        {
            var code = request.Code!.Trim().ToUpperInvariant();

            if (await db.Warehouses.AnyAsync(w => w.Code == code, ct))
            {
                return CodeConflict(code);
            }

            var now = DateTimeOffset.UtcNow;
            var warehouse = new Warehouse
            {
                Code = code,
                Name = request.Name!.Trim(),
                Location = Clean(request.Location),
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.Warehouses.Add(warehouse);

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
            {
                // Another request created the same code between the check above and the save.
                return CodeConflict(code);
            }

            return Results.Created($"/api/warehouses/{warehouse.Id}", WarehouseResponse.From(warehouse));
        }).WithName("CreateWarehouse");

        // ?isActive=true or ?isActive=false filters the list; omit it to list everything.
        group.MapGet("/", async (bool? isActive, InventoryDbContext db, CancellationToken ct) =>
        {
            var query = db.Warehouses.AsNoTracking();
            if (isActive is not null)
            {
                query = query.Where(w => w.IsActive == isActive);
            }

            var warehouses = await query.OrderBy(w => w.Code).ToListAsync(ct);
            return Results.Ok(warehouses.Select(WarehouseResponse.From));
        }).WithName("ListWarehouses");

        group.MapGet("/{id:int}", async (int id, InventoryDbContext db, CancellationToken ct) =>
            await db.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id, ct) is { } warehouse
                ? Results.Ok(WarehouseResponse.From(warehouse))
                : Results.NotFound()).WithName("GetWarehouse");

        group.MapPut("/{id:int}", async (int id, UpdateWarehouseRequest request, InventoryDbContext db, CancellationToken ct) =>
        {
            var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == id, ct);
            if (warehouse is null)
            {
                return Results.NotFound();
            }

            warehouse.Name = request.Name!.Trim();
            warehouse.Location = Clean(request.Location);
            warehouse.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            return Results.Ok(WarehouseResponse.From(warehouse));
        }).WithName("UpdateWarehouse");

        // Safe to repeat: deactivating an already inactive warehouse changes nothing.
        group.MapPost("/{id:int}/deactivate", async (int id, InventoryDbContext db, CancellationToken ct) =>
        {
            var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == id, ct);
            if (warehouse is null)
            {
                return Results.NotFound();
            }

            if (warehouse.IsActive)
            {
                warehouse.IsActive = false;
                warehouse.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
            }

            return Results.Ok(WarehouseResponse.From(warehouse));
        }).WithName("DeactivateWarehouse");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IResult CodeConflict(string code) =>
        Results.Conflict(new { error = $"A warehouse with code '{code}' already exists." });
}
