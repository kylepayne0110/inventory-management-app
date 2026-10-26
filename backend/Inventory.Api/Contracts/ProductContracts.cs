using System.ComponentModel.DataAnnotations;
using Inventory.Api.Models;

namespace Inventory.Api.Contracts;

// ReorderThreshold is optional on create (default 0) and on update (omit it to keep the current value); at most 3 decimal places.
// The SKU is only accepted on create; it cannot be changed afterwards.
public record CreateProductRequest(
    [Required, StringLength(50), RegularExpression(@"^[A-Za-z0-9][A-Za-z0-9._-]*$", ErrorMessage = "SKU may contain only letters, digits, '.', '_' and '-', and must start with a letter or digit.")]
    string? Sku,
    [Required, StringLength(200)] string? Name,
    [StringLength(1000)] string? Description,
    [StringLength(20)] string? UnitOfMeasure,
    [Range(0.0, 1000000000.0, ErrorMessage = "Reorder threshold cannot be negative.")] decimal? ReorderThreshold);

public record UpdateProductRequest(
    [Required, StringLength(200)] string? Name,
    [StringLength(1000)] string? Description,
    [StringLength(20)] string? UnitOfMeasure,
    [Range(0.0, 1000000000.0, ErrorMessage = "Reorder threshold cannot be negative.")] decimal? ReorderThreshold);

public record ProductResponse(
    int Id,
    string Sku,
    string Name,
    string? Description,
    string UnitOfMeasure,
    decimal ReorderThreshold,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static ProductResponse From(Product p) =>
        new(p.Id, p.Sku, p.Name, p.Description, p.UnitOfMeasure, p.ReorderThreshold, p.IsActive, p.CreatedAt, p.UpdatedAt);
}
