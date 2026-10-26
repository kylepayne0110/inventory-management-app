using System.ComponentModel.DataAnnotations;
using Inventory.Api.Models;

namespace Inventory.Api.Contracts;

// Shared by create and update. The SKU is only accepted on create; it cannot be changed afterwards.
public record CreateProductRequest(
    [Required, StringLength(50), RegularExpression(@"^[A-Za-z0-9][A-Za-z0-9._-]*$", ErrorMessage = "SKU may contain only letters, digits, '.', '_' and '-', and must start with a letter or digit.")]
    string? Sku,
    [Required, StringLength(200)] string? Name,
    [StringLength(1000)] string? Description,
    [StringLength(20)] string? UnitOfMeasure);

public record UpdateProductRequest(
    [Required, StringLength(200)] string? Name,
    [StringLength(1000)] string? Description,
    [StringLength(20)] string? UnitOfMeasure);

public record ProductResponse(
    int Id,
    string Sku,
    string Name,
    string? Description,
    string UnitOfMeasure,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static ProductResponse From(Product p) =>
        new(p.Id, p.Sku, p.Name, p.Description, p.UnitOfMeasure, p.IsActive, p.CreatedAt, p.UpdatedAt);
}
