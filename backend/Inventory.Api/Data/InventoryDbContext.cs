using Inventory.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Data;

// The API's gateway to SQL Server.
public class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    public DbSet<Warehouse> Warehouses => Set<Warehouse>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(e =>
        {
            e.Property(p => p.Sku).HasMaxLength(50).IsRequired();
            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
            e.Property(p => p.Description).HasMaxLength(1000);
            e.Property(p => p.UnitOfMeasure).HasMaxLength(20).IsRequired();
            e.HasIndex(p => p.Sku).IsUnique();
        });

        modelBuilder.Entity<Warehouse>(e =>
        {
            e.Property(w => w.Code).HasMaxLength(20).IsRequired();
            e.Property(w => w.Name).HasMaxLength(200).IsRequired();
            e.Property(w => w.Location).HasMaxLength(500);
            e.HasIndex(w => w.Code).IsUnique();
        });
    }
}
