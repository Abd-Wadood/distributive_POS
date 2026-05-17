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
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Category>(entity =>
        {
            entity.HasIndex(x => x.Name).IsUnique().HasDatabaseName("UX_Categories_Name");
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
        });

        builder.Entity<Branch>(entity =>
        {
            entity.HasIndex(x => x.BranchCode).IsUnique().HasDatabaseName("UX_Branches_BranchCode");
            entity.Property(x => x.BranchCode).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Address).HasMaxLength(300);
            entity.Property(x => x.Phone).HasMaxLength(40);
        });

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.HasIndex(x => x.BranchId);
            entity.HasIndex(x => new { x.BranchId, x.IsActive }).HasDatabaseName("IX_AspNetUsers_BranchId_IsActive");
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
            entity.HasIndex(x => new { x.BranchId, x.CurrentQuantity }).HasDatabaseName("IX_Inventories_BranchId_CurrentQuantity");
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
            entity.HasIndex(x => x.PublicId).IsUnique().HasDatabaseName("UX_InventoryTransactions_PublicId");
            entity.HasIndex(x => x.IdempotencyKey)
                .IsUnique()
                .HasFilter("\"IdempotencyKey\" IS NOT NULL")
                .HasDatabaseName("UX_InventoryTransactions_IdempotencyKey");
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
            entity.HasIndex(x => x.PublicId).IsUnique().HasDatabaseName("UX_Purchases_PublicId");
            entity.HasIndex(x => x.IdempotencyKey)
                .IsUnique()
                .HasFilter("\"IdempotencyKey\" IS NOT NULL")
                .HasDatabaseName("UX_Purchases_IdempotencyKey");
            entity.HasIndex(x => new { x.SupplierId, x.InvoiceNumber })
                .IsUnique()
                .HasFilter("\"InvoiceNumber\" IS NOT NULL")
                .HasDatabaseName("UX_Purchases_SupplierId_InvoiceNumber");
            entity.HasIndex(x => x.BranchId);
            entity.HasIndex(x => x.TerminalId);
            entity.HasIndex(x => x.UserSessionId);
            entity.HasIndex(x => x.CreatedAt);
            entity.Property(x => x.TerminalCode).HasMaxLength(40).IsRequired();
            entity.Property(x => x.IdempotencyKey).HasMaxLength(120);
            entity.Property(x => x.InvoiceNumber).HasMaxLength(80);
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
            entity.HasIndex(x => new { x.BranchId, x.PhoneNumber }).IsUnique().HasDatabaseName("UX_Customers_BranchId_PhoneNumber");
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
            entity.HasIndex(x => x.PublicId).IsUnique().HasDatabaseName("UX_Orders_PublicId");
            entity.HasIndex(x => x.IdempotencyKey)
                .IsUnique()
                .HasFilter("\"IdempotencyKey\" IS NOT NULL")
                .HasDatabaseName("UX_Orders_IdempotencyKey");
            entity.HasIndex(x => new { x.BranchId, x.OrderNumber }).IsUnique().HasDatabaseName("UX_Orders_BranchId_OrderNumber");
            entity.HasIndex(x => x.BranchId);
            entity.HasIndex(x => x.TerminalId);
            entity.HasIndex(x => x.UserSessionId);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => new { x.BranchId, x.CompletedAt, x.OrderStatus }).HasDatabaseName("IX_Orders_BranchId_CompletedAt_OrderStatus");
            entity.HasIndex(x => x.OrderStatus);
            entity.Property(x => x.OrderNumber).HasMaxLength(40).IsRequired();
            entity.Property(x => x.IdempotencyKey).HasMaxLength(120);
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
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_UserSessions_OpeningCashAmount_NonNegative", "\"OpeningCashAmount\" >= 0");
                t.HasCheckConstraint("CK_UserSessions_CountedClosingCash_NonNegative", "\"CountedClosingCash\" IS NULL OR \"CountedClosingCash\" >= 0");
            });
            entity.HasIndex(x => x.PublicId).IsUnique().HasDatabaseName("UX_UserSessions_PublicId");
            entity.HasIndex(x => x.IdempotencyKey)
                .IsUnique()
                .HasFilter("\"IdempotencyKey\" IS NOT NULL")
                .HasDatabaseName("UX_UserSessions_IdempotencyKey");
            entity.HasIndex(x => x.CloseIdempotencyKey)
                .IsUnique()
                .HasFilter("\"CloseIdempotencyKey\" IS NOT NULL")
                .HasDatabaseName("UX_UserSessions_CloseIdempotencyKey");
            entity.HasIndex(x => x.SessionCode).IsUnique().HasDatabaseName("UX_UserSessions_SessionCode");
            entity.HasIndex(x => new { x.UserId, x.Status });
            entity.HasIndex(x => new { x.Status, x.StartedAt }).HasDatabaseName("IX_UserSessions_Status_StartedAt");
            entity.HasIndex(x => new { x.BranchId, x.Status }).HasDatabaseName("IX_UserSessions_BranchId_Status");
            entity.HasIndex(x => x.TerminalId);
            entity.HasIndex(x => x.UserId)
                .IsUnique()
                .HasFilter("\"Status\" IN ('Active', 'Reopened', 'ClosingPending')")
                .HasDatabaseName("UX_UserSessions_UserId_Active");
            entity.HasIndex(x => x.TerminalId)
                .IsUnique()
                .HasFilter("\"Status\" IN ('Active', 'Reopened', 'ClosingPending')")
                .HasDatabaseName("UX_UserSessions_TerminalId_Active");
            entity.HasIndex(x => new { x.UserId, x.BranchId })
                .IsUnique()
                .HasFilter("\"Status\" IN ('Active', 'Reopened', 'ClosingPending')")
                .HasDatabaseName("UX_UserSessions_UserId_BranchId_Active");
            entity.HasIndex(x => x.BranchId);
            entity.Property(x => x.SessionCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.IdempotencyKey).HasMaxLength(120);
            entity.Property(x => x.CloseIdempotencyKey).HasMaxLength(120);
            entity.Property(x => x.RoleName).HasMaxLength(40).IsRequired();
            entity.Property(x => x.TerminalName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.TerminalCode).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Property(x => x.OpeningCashAmount).HasPrecision(18, 2);
            entity.Property(x => x.CountedClosingCash).HasPrecision(18, 2);
            entity.Property(x => x.ExpectedClosingCash).HasPrecision(18, 2);
            entity.Property(x => x.CashDifference).HasPrecision(18, 2);
            entity.Property(x => x.ReopenReason).HasMaxLength(500);
            entity.HasOne(x => x.User)
                .WithMany(x => x.UserSessions)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ClosedByUser)
                .WithMany()
                .HasForeignKey(x => x.ClosedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.ReopenedByUser)
                .WithMany()
                .HasForeignKey(x => x.ReopenedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
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
            entity.HasIndex(x => x.TerminalCode).IsUnique().HasDatabaseName("UX_Terminals_TerminalCode");
            entity.HasIndex(x => x.BranchId);
            entity.HasIndex(x => new { x.BranchId, x.IsActive }).HasDatabaseName("IX_Terminals_BranchId_IsActive");
            entity.Property(x => x.TerminalCode).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.IpAddress).HasMaxLength(60);
            entity.Property(x => x.TerminalTokenHash).HasMaxLength(128);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TerminalHeartbeat>(entity =>
        {
            entity.HasIndex(x => x.TerminalId).IsUnique().HasDatabaseName("UX_TerminalHeartbeats_TerminalId");
            entity.HasIndex(x => x.BranchId);
            entity.HasIndex(x => new { x.BranchId, x.LastSeenAt }).HasDatabaseName("IX_TerminalHeartbeats_BranchId_LastSeenAt");
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
            entity.HasIndex(x => x.UserSessionId).IsUnique().HasDatabaseName("UX_UserSessionHeartbeats_UserSessionId");
            entity.Property(x => x.TerminalName).HasMaxLength(120);
            entity.HasOne(x => x.UserSession)
                .WithMany()
                .HasForeignKey(x => x.UserSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => new { x.EntityName, x.EntityId });
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => new { x.EventType, x.CreatedAt }).HasDatabaseName("IX_AuditLogs_EventType_CreatedAt");
            entity.HasIndex(x => new { x.IpAddress, x.CreatedAt }).HasDatabaseName("IX_AuditLogs_IpAddress_CreatedAt");
            entity.HasIndex(x => new { x.UserId, x.CreatedAt }).HasDatabaseName("IX_AuditLogs_UserId_CreatedAt");
            entity.HasIndex(x => new { x.Severity, x.CreatedAt }).HasDatabaseName("IX_AuditLogs_Severity_CreatedAt");
            entity.Property(x => x.Action).HasMaxLength(120).IsRequired();
            entity.Property(x => x.EventType).HasMaxLength(120);
            entity.Property(x => x.Severity).HasMaxLength(40);
            entity.Property(x => x.Message).HasMaxLength(500);
            entity.Property(x => x.AttemptedUserName).HasMaxLength(256);
            entity.Property(x => x.EntityName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.EntityId).HasMaxLength(80);
            entity.Property(x => x.OldValues).HasColumnType("jsonb");
            entity.Property(x => x.NewValues).HasColumnType("jsonb");
            entity.Property(x => x.IpAddress).HasMaxLength(80);
            entity.Property(x => x.UserAgent).HasMaxLength(500);
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Terminal)
                .WithMany()
                .HasForeignKey(x => x.TerminalId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<IdempotencyRecord>(entity =>
        {
            entity.HasIndex(x => x.IdempotencyKey).IsUnique().HasDatabaseName("UX_IdempotencyRecords_IdempotencyKey");
            entity.HasIndex(x => new { x.OperationType, x.IdempotencyKey }).IsUnique().HasDatabaseName("UX_IdempotencyRecords_OperationType_IdempotencyKey");
            entity.HasIndex(x => new { x.Status, x.CreatedAt }).HasDatabaseName("IX_IdempotencyRecords_Status_CreatedAt");
            entity.HasIndex(x => x.ExpiresAt).HasDatabaseName("IX_IdempotencyRecords_ExpiresAt");
            entity.Property(x => x.IdempotencyKey).HasMaxLength(120).IsRequired();
            entity.Property(x => x.OperationType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.RequestHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ResourceType).HasMaxLength(80);
            entity.Property(x => x.ResponseBodySummary).HasMaxLength(500);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Terminal)
                .WithMany()
                .HasForeignKey(x => x.TerminalId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.HasSequence<long>("SessionCodeSequence");

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
