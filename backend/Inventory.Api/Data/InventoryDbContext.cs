using Inventory.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Data;

// The API's gateway to SQL Server.
public class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    public DbSet<Warehouse> Warehouses => Set<Warehouse>();

    public DbSet<ReceiptBatch> ReceiptBatches => Set<ReceiptBatch>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(e =>
        {
            e.Property(p => p.Sku).HasMaxLength(50).IsRequired();
            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
            e.Property(p => p.Description).HasMaxLength(1000);
            e.Property(p => p.UnitOfMeasure).HasMaxLength(20).IsRequired();
            e.Property(p => p.ReorderThreshold).HasPrecision(18, 3).HasDefaultValue(0m);
            e.HasIndex(p => p.Sku).IsUnique();
            e.ToTable(t => t.HasCheckConstraint("CK_Products_ReorderThreshold", "[ReorderThreshold] >= 0"));
        });

        modelBuilder.Entity<Warehouse>(e =>
        {
            e.Property(w => w.Code).HasMaxLength(20).IsRequired();
            e.Property(w => w.Name).HasMaxLength(200).IsRequired();
            e.Property(w => w.Location).HasMaxLength(500);
            e.HasIndex(w => w.Code).IsUnique();
        });

        // Products, warehouses, and batches are never deleted, so deletes that would orphan history are blocked.
        modelBuilder.Entity<ReceiptBatch>(e =>
        {
            e.Property(b => b.QuantityReceived).HasPrecision(18, 3);
            e.Property(b => b.QuantityOnHand).HasPrecision(18, 3);
            e.Property(b => b.Reference).HasMaxLength(100);
            e.Property(b => b.RowVersion).IsRowVersion();
            e.HasOne(b => b.Product).WithMany().HasForeignKey(b => b.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(b => b.Warehouse).WithMany().HasForeignKey(b => b.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(b => new { b.ProductId, b.WarehouseId });
            e.ToTable(t => t.HasCheckConstraint("CK_ReceiptBatches_Quantities", "[QuantityOnHand] >= 0 AND [QuantityOnHand] <= [QuantityReceived]"));
        });

        modelBuilder.Entity<StockMovement>(e =>
        {
            e.Property(m => m.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(m => m.QuantityChange).HasPrecision(18, 3);
            e.Property(m => m.Reference).HasMaxLength(100);
            e.Property(m => m.Reason).HasMaxLength(200);
            e.HasOne(m => m.Product).WithMany().HasForeignKey(m => m.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.Warehouse).WithMany().HasForeignKey(m => m.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.ReceiptBatch).WithMany().HasForeignKey(m => m.ReceiptBatchId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(m => m.ReceiptBatchId);
            e.HasIndex(m => m.TransferId);
        });
    }
}
