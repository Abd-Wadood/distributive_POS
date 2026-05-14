using Microsoft.EntityFrameworkCore;
using BranchPOS.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace BranchPOS.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductIngredient> ProductIngredients => Set<ProductIngredient>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<UserSessionHeartbeat> UserSessionHeartbeats => Set<UserSessionHeartbeat>();
    public DbSet<Terminal> Terminals => Set<Terminal>();
    public DbSet<TerminalHeartbeat> TerminalHeartbeats => Set<TerminalHeartbeat>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Category>(entity =>
        {
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
        });

        builder.Entity<Branch>(entity =>
        {
            entity.HasIndex(x => x.BranchCode).IsUnique();
            entity.Property(x => x.BranchCode).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Address).HasMaxLength(300);
            entity.Property(x => x.Phone).HasMaxLength(40);
        });

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.HasIndex(x => x.BranchId);
            entity.Property(x => x.FullName).HasMaxLength(160);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Ingredient>(entity =>
        {
            entity.HasIndex(x => new { x.BranchId, x.Name }).IsUnique();
            entity.HasIndex(x => x.BranchId);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.UnitType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.MinimumStockLevel).HasPrecision(18, 3);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Product>(entity =>
        {
            entity.HasIndex(x => new { x.BranchId, x.Name });
            entity.HasIndex(x => x.BranchId);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Price).HasPrecision(18, 2);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Category)
                .WithMany(x => x.Products)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProductIngredient>(entity =>
        {
            entity.HasIndex(x => new { x.ProductId, x.IngredientId }).IsUnique();
            entity.Property(x => x.QuantityRequired).HasPrecision(18, 3);
            entity.HasOne(x => x.Product)
                .WithMany(x => x.ProductIngredients)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Ingredient)
                .WithMany(x => x.ProductIngredients)
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Inventory>(entity =>
        {
            entity.ToTable(t => t.HasCheckConstraint("CK_Inventories_CurrentQuantity_NonNegative", "\"CurrentQuantity\" >= 0"));
            entity.HasIndex(x => new { x.BranchId, x.IngredientId }).IsUnique();
            entity.HasIndex(x => x.BranchId);
            entity.Property(x => x.CurrentQuantity).HasPrecision(18, 3);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Ingredient)
                .WithOne(x => x.Inventory)
                .HasForeignKey<Inventory>(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<InventoryTransaction>(entity =>
        {
            entity.HasIndex(x => x.PublicId).IsUnique();
            entity.HasIndex(x => x.BranchId);
            entity.HasIndex(x => x.TerminalId);
            entity.HasIndex(x => x.UserSessionId);
            entity.HasIndex(x => new { x.IngredientId, x.CreatedAt });
            entity.HasIndex(x => new { x.TransactionType, x.ReferenceId });
            entity.Property(x => x.QuantityChanged).HasPrecision(18, 3);
            entity.Property(x => x.TerminalCode).HasMaxLength(40).IsRequired();
            entity.Property(x => x.TransactionType).HasConversion<string>().HasMaxLength(40);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.UserSession)
                .WithMany()
                .HasForeignKey(x => x.UserSessionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PerformedByUser)
                .WithMany()
                .HasForeignKey(x => x.PerformedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Terminal)
                .WithMany()
                .HasForeignKey(x => x.TerminalId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Ingredient)
                .WithMany(x => x.InventoryTransactions)
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Supplier>(entity =>
        {
            entity.HasIndex(x => x.Name);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(40);
        });

        builder.Entity<Purchase>(entity =>
        {
            entity.HasIndex(x => x.PublicId).IsUnique();
            entity.HasIndex(x => x.BranchId);
            entity.HasIndex(x => x.TerminalId);
            entity.HasIndex(x => x.UserSessionId);
            entity.HasIndex(x => x.CreatedAt);
            entity.Property(x => x.TerminalCode).HasMaxLength(40).IsRequired();
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.UserSession)
                .WithMany()
                .HasForeignKey(x => x.UserSessionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PerformedByUser)
                .WithMany()
                .HasForeignKey(x => x.PerformedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Terminal)
                .WithMany()
                .HasForeignKey(x => x.TerminalId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Supplier)
                .WithMany(x => x.Purchases)
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PurchaseItem>(entity =>
        {
            entity.HasIndex(x => x.BranchId);
            entity.HasIndex(x => new { x.PurchaseId, x.IngredientId });
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitCost).HasPrecision(18, 2);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Purchase)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.PurchaseId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Ingredient)
                .WithMany(x => x.PurchaseItems)
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Customer>(entity =>
        {
            entity.HasIndex(x => new { x.BranchId, x.PhoneNumber }).IsUnique();
            entity.HasIndex(x => x.BranchId);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.PhoneNumber).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Address).HasMaxLength(300);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Order>(entity =>
        {
            entity.HasIndex(x => x.PublicId).IsUnique();
            entity.HasIndex(x => new { x.BranchId, x.OrderNumber }).IsUnique();
            entity.HasIndex(x => x.BranchId);
            entity.HasIndex(x => x.TerminalId);
            entity.HasIndex(x => x.UserSessionId);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.OrderStatus);
            entity.Property(x => x.OrderNumber).HasMaxLength(40).IsRequired();
            entity.Property(x => x.OrderType).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.OrderStatus).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Subtotal).HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.TableNumber).HasMaxLength(40);
            entity.Property(x => x.TerminalName).HasMaxLength(120);
            entity.Property(x => x.TerminalCode).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.UserSession)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.UserSessionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Customer)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Cashier)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.CashierId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Terminal)
                .WithMany()
                .HasForeignKey(x => x.TerminalId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OrderItem>(entity =>
        {
            entity.HasIndex(x => x.BranchId);
            entity.HasIndex(x => new { x.OrderId, x.ProductId });
            entity.Property(x => x.ProductNameSnapshot).HasMaxLength(160).IsRequired();
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.LineTotal).HasPrecision(18, 2);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Order)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Product)
                .WithMany(x => x.OrderItems)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<UserSession>(entity =>
        {
            entity.HasIndex(x => x.PublicId).IsUnique();
            entity.HasIndex(x => x.SessionCode).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.Status });
            entity.HasIndex(x => x.UserId)
                .IsUnique()
                .HasFilter("\"Status\" = 'Active'");
            entity.HasIndex(x => x.BranchId);
            entity.HasIndex(x => x.TerminalId);
            entity.Property(x => x.SessionCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.RoleName).HasMaxLength(40).IsRequired();
            entity.Property(x => x.TerminalName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.TerminalCode).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.HasOne(x => x.User)
                .WithMany(x => x.UserSessions)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Terminal)
                .WithMany()
                .HasForeignKey(x => x.TerminalId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Terminal>(entity =>
        {
            entity.HasIndex(x => x.TerminalCode).IsUnique();
            entity.HasIndex(x => x.BranchId);
            entity.Property(x => x.TerminalCode).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.IpAddress).HasMaxLength(60);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TerminalHeartbeat>(entity =>
        {
            entity.HasIndex(x => x.TerminalId).IsUnique();
            entity.HasIndex(x => x.BranchId);
            entity.Property(x => x.TerminalCode).HasMaxLength(40).IsRequired();
            entity.HasOne(x => x.Terminal)
                .WithMany()
                .HasForeignKey(x => x.TerminalId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CurrentUser)
                .WithMany()
                .HasForeignKey(x => x.CurrentUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CurrentSession)
                .WithMany()
                .HasForeignKey(x => x.CurrentSessionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<UserSessionHeartbeat>(entity =>
        {
            entity.HasIndex(x => x.UserSessionId).IsUnique();
            entity.Property(x => x.TerminalName).HasMaxLength(120);
            entity.HasOne(x => x.UserSession)
                .WithMany()
                .HasForeignKey(x => x.UserSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Branch>().HasData(new Branch
        {
            Id = 1,
            BranchCode = "MAIN",
            Name = "Main Branch",
            Address = "Local Branch",
            Phone = "",
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        builder.Entity<Category>().HasData(
            new Category { Id = 1, Name = "Food", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Category { Id = 2, Name = "Beverages", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Category { Id = 3, Name = "Desserts", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) });

        builder.Entity<Terminal>().HasData(new Terminal
        {
            Id = 1,
            BranchId = 1,
            TerminalCode = "MAIN-01",
            Name = "Main Terminal",
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }

    public override int SaveChanges()
    {
        ApplyTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyTimestamps()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is InventoryTransaction)
            {
                continue;
            }

            if (entry.State == EntityState.Added && entry.Properties.Any(x => x.Metadata.Name == "CreatedAt"))
            {
                entry.Property("CreatedAt").CurrentValue = now;
            }

            if ((entry.State == EntityState.Added || entry.State == EntityState.Modified) &&
                entry.Properties.Any(x => x.Metadata.Name == "UpdatedAt"))
            {
                entry.Property("UpdatedAt").CurrentValue = now;
            }
        }
    }
}
