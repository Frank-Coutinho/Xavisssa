using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Xavissa.Database.Models;
using Xavissa.Database.Security;

namespace Xavissa.Database;

public class XavissaDbContext : DbContext
{
    private readonly IRequestContext? _requestContext;

    public XavissaDbContext(DbContextOptions<XavissaDbContext> options)
        : base(options) { }

    public XavissaDbContext(
        DbContextOptions<XavissaDbContext> options,
        IRequestContext requestContext
    )
        : base(options)
    {
        _requestContext = requestContext;
    }

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductStoreAssignment> ProductStoreAssignments => Set<ProductStoreAssignment>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<StoreSellableVariantView> StoreSellableVariants => Set<StoreSellableVariantView>();
    public DbSet<CashRegisterSessionSummaryView> CashRegisterSessionSummaries => Set<CashRegisterSessionSummaryView>();
    public DbSet<UserRolesNormalizedView> UserRolesNormalized => Set<UserRolesNormalizedView>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<SalePayment> SalePayments => Set<SalePayment>();
    public DbSet<CashRegisterSession> CashRegisterSessions => Set<CashRegisterSession>();
    public DbSet<CashRegisterCashMovement> CashRegisterCashMovements => Set<CashRegisterCashMovement>();
    public DbSet<StoreOperationalSetting> StoreOperationalSettings => Set<StoreOperationalSetting>();
    public DbSet<SyncConflict> SyncConflicts => Set<SyncConflict>();
    public DbSet<StandardProduct> StandardProducts => Set<StandardProduct>();
    public DbSet<StockLevel> StockLevels => Set<StockLevel>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<StockTransferItem> StockTransferItems => Set<StockTransferItem>();
    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();
    public DbSet<StockAdjustmentItem> StockAdjustmentItems => Set<StockAdjustmentItem>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<StorePrintingSetting> StorePrintingSettings => Set<StorePrintingSetting>();
    public DbSet<DemoTemplate> DemoTemplates => Set<DemoTemplate>();
    public DbSet<DemoSession> DemoSessions => Set<DemoSession>();
    public DbSet<DemoSessionEvent> DemoSessionEvents => Set<DemoSessionEvent>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantPrintingSetting> TenantPrintingSettings => Set<TenantPrintingSetting>();
    public DbSet<TenantUser> TenantUsers => Set<TenantUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserStoreRole> UserStoreRoles => Set<UserStoreRole>();

    private bool HasAuthenticatedRequest => _requestContext?.IsAuthenticated == true;
    private bool CanBypassFilters => _requestContext?.IsPlatformAdmin == true;
    private int? CurrentSelectedTenantId => _requestContext?.SelectedTenantId;
    private int? CurrentSelectedStoreId => _requestContext?.SelectedStoreId;
    private int?[] CurrentAllowedTenantIds =>
        _requestContext?.AllowedTenantIds.Select(id => (int?)id).ToArray() ?? Array.Empty<int?>();
    private int[] CurrentAllowedStoreIds =>
        _requestContext?.AllowedStoreIds.ToArray() ?? Array.Empty<int>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.Property(x => x.OldValuesJson).HasColumnType("jsonb");
            entity.Property(x => x.NewValuesJson).HasColumnType("jsonb");
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasFilter("\"Code\" IS NOT NULL");
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId);
            entity.HasQueryFilter(x =>
                !HasAuthenticatedRequest
                || CanBypassFilters
                || CurrentSelectedTenantId == null
                || x.TenantId == CurrentSelectedTenantId
                || CurrentAllowedTenantIds.Contains(x.TenantId)
            );
        });
        ConfigureSyncMetadata<Category>(modelBuilder);

        modelBuilder.Entity<Product>(entity =>
        {
            entity
                .HasOne(x => x.CategoryNavigation)
                .WithMany(x => x.Products)
                .HasForeignKey(x => x.CategoryId);
            entity
                .HasMany(x => x.StoreAssignments)
                .WithOne(x => x.Product)
                .HasForeignKey(x => x.ProductId);
            entity.Ignore(x => x.Variants);
            entity.Property(x => x.Description).IsRequired(false);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.TenantId, x.UpdatedAt });
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.HasQueryFilter(x =>
                !HasAuthenticatedRequest
                || CanBypassFilters
                || CurrentSelectedTenantId == null
                || x.TenantId == CurrentSelectedTenantId
                || CurrentAllowedTenantIds.Contains(x.TenantId)
            );
        });
        ConfigureSyncMetadata<Product>(modelBuilder);

        modelBuilder.Entity<ProductVariant>(entity =>
        {
            entity.Property(x => x.AttributesJson).HasColumnType("jsonb");
            entity.Ignore(x => x.CostPrice);
            entity.Ignore(x => x.ProductId);
            entity.Ignore(x => x.Product);
            entity.Ignore(x => x.StoreId);
            entity.Property(x => x.Description).IsRequired(false);
            entity.Property(x => x.Price).HasPrecision(18, 2);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.TenantId, x.Barcode }).IsUnique().HasFilter("\"Barcode\" IS NOT NULL");
            entity.HasIndex(x => new { x.TenantId, x.SKU }).IsUnique().HasFilter("\"SKU\" IS NOT NULL");
            entity.HasIndex(x => new { x.TenantId, x.UpdatedAt });
            entity
                .HasOne(x => x.ProductStoreAssignment)
                .WithMany(x => x.Variants)
                .HasForeignKey(x => x.ProductStoreAssignmentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(x =>
                !HasAuthenticatedRequest
                || CanBypassFilters
                || (
                    (
                        CurrentSelectedTenantId == null
                        || x.TenantId == CurrentSelectedTenantId
                        || CurrentAllowedTenantIds.Contains(x.TenantId)
                    )
                    && (
                        CurrentSelectedStoreId == null
                        || (
                            x.ProductStoreAssignment != null
                            && (
                                x.ProductStoreAssignment.StoreId == CurrentSelectedStoreId
                                || CurrentAllowedStoreIds.Contains(x.ProductStoreAssignment.StoreId)
                            )
                        )
                    )
                )
            );
        });
        ConfigureSyncMetadata<ProductVariant>(modelBuilder);

        modelBuilder.Entity<ProductStoreAssignment>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.StoreId, x.ProductId }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.StoreId, x.UpdatedAt });
            entity.Ignore(x => x.Price);
            entity.Ignore(x => x.CreatedAt);
            entity.Ignore(x => x.CreatedBy);
            entity.Ignore(x => x.UpdatedBy);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity
                .HasOne(x => x.Product)
                .WithMany(x => x.StoreAssignments)
                .HasForeignKey(x => x.ProductId);
            entity
                .HasOne(x => x.Store)
                .WithMany(x => x.ProductAssignments)
                .HasForeignKey(x => x.StoreId);
            entity.HasQueryFilter(x =>
                !HasAuthenticatedRequest
                || CanBypassFilters
                || (
                    (
                        CurrentSelectedTenantId == null
                        || x.TenantId == CurrentSelectedTenantId
                        || CurrentAllowedTenantIds.Contains(x.TenantId)
                    )
                    && (
                        CurrentSelectedStoreId == null
                        || x.StoreId == CurrentSelectedStoreId
                        || CurrentAllowedStoreIds.Contains(x.StoreId)
                    )
                )
            );
        });
        ConfigureSyncMetadata<ProductStoreAssignment>(modelBuilder);

        modelBuilder.Entity<Sale>(entity =>
        {
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.Discount).HasPrecision(18, 2);
            entity.Property(x => x.ChangeGiven).HasPrecision(18, 2);
            entity.Property(x => x.PaymentStatus).HasDefaultValue("Paid");
            entity.Property(x => x.Status).HasDefaultValue("Completed");
            entity.Property(x => x.HasUntrackedCashPayment).HasDefaultValue(false);
            entity.Property(x => x.IsVoided).HasDefaultValue(false);
            entity.Property(x => x.CashRegisterTrackingMode).IsRequired(false);
            entity.Property(x => x.VoidReason).IsRequired(false);
            entity.ToTable(t =>
                t.HasCheckConstraint(
                    "CK_Sales_Discount_NotGreaterThanTotalAmount",
                    "\"Discount\" IS NULL OR \"Discount\" <= \"TotalAmount\""
                )
            );
            entity.HasIndex(x => new { x.TenantId, x.StoreId, x.UpdatedAt });
            entity
                .HasOne<CashRegisterSession>()
                .WithMany()
                .HasForeignKey(x => x.CashRegisterSessionId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasQueryFilter(x =>
                !HasAuthenticatedRequest
                || CanBypassFilters
                || (
                    (
                        x.TenantId == null
                        || CurrentSelectedTenantId == null
                        || x.TenantId == CurrentSelectedTenantId
                        || CurrentAllowedTenantIds.Contains(x.TenantId.Value)
                    )
                    && (
                        CurrentSelectedStoreId == null
                        || x.StoreId == CurrentSelectedStoreId
                        || CurrentAllowedStoreIds.Contains(x.StoreId)
                    )
                )
            );
        });
        ConfigureSyncMetadata<Sale>(modelBuilder);

        modelBuilder.Entity<SaleItem>(entity =>
        {
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.StoreId, x.UpdatedAt });
            entity.HasOne(x => x.Sale).WithMany(x => x.SaleItems).HasForeignKey(x => x.SaleId);
            entity
                .HasOne(x => x.Variant)
                .WithMany(x => x.SaleItems)
                .HasForeignKey(x => x.VariantId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x =>
                !HasAuthenticatedRequest
                || CanBypassFilters
                || (
                    (
                        CurrentSelectedTenantId == null
                        || x.TenantId == CurrentSelectedTenantId
                        || CurrentAllowedTenantIds.Contains(x.TenantId)
                    )
                    && (
                        CurrentSelectedStoreId == null
                        || x.StoreId == CurrentSelectedStoreId
                        || CurrentAllowedStoreIds.Contains(x.StoreId)
                    )
                )
            );
        });
        ConfigureSyncMetadata<SaleItem>(modelBuilder);

        modelBuilder.Entity<StockLevel>(entity =>
        {
            entity.Ignore(x => x.CreatedAt);
            entity.Ignore(x => x.CreatedBy);
            entity
                .HasIndex(x => new
                {
                    x.TenantId,
                    x.StoreId,
                    x.VariantId,
                })
                .IsUnique();
            entity.HasIndex(x => new { x.StoreId, x.UpdatedAt });
            entity.HasQueryFilter(x =>
                !HasAuthenticatedRequest
                || CanBypassFilters
                || (
                    (
                        CurrentSelectedTenantId == null
                        || x.TenantId == CurrentSelectedTenantId
                        || CurrentAllowedTenantIds.Contains(x.TenantId)
                    )
                    && (
                        CurrentSelectedStoreId == null
                        || x.StoreId == CurrentSelectedStoreId
                        || CurrentAllowedStoreIds.Contains(x.StoreId)
                    )
                )
            );
        });
        ConfigureSyncMetadata<StockLevel>(modelBuilder);

        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity
                .HasOne(x => x.Variant)
                .WithMany(x => x.StockMovements)
                .HasForeignKey(x => x.VariantId);
            entity.HasQueryFilter(x =>
                !HasAuthenticatedRequest
                || CanBypassFilters
                || (
                    (
                        CurrentSelectedTenantId == null
                        || x.TenantId == CurrentSelectedTenantId
                        || CurrentAllowedTenantIds.Contains(x.TenantId)
                    )
                    && (
                        CurrentSelectedStoreId == null
                        || x.StoreId == CurrentSelectedStoreId
                        || CurrentAllowedStoreIds.Contains(x.StoreId)
                    )
                )
            );
        });
        ConfigureSyncMetadata<StockMovement>(modelBuilder);

        modelBuilder.Entity<StandardProduct>(entity =>
        {
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity.HasQueryFilter(x =>
                !HasAuthenticatedRequest
                || CanBypassFilters
                || (CurrentSelectedStoreId != null && x.StoreId == CurrentSelectedStoreId)
            );
        });

        modelBuilder.Entity<Store>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity.HasQueryFilter(x =>
                !HasAuthenticatedRequest
                || CanBypassFilters
                || (
                    (
                        CurrentSelectedTenantId == null
                        || x.TenantId == CurrentSelectedTenantId
                        || CurrentAllowedTenantIds.Contains(x.TenantId)
                    )
                    && (
                        CurrentSelectedStoreId == null
                        || x.Id == CurrentSelectedStoreId
                        || CurrentAllowedStoreIds.Contains(x.Id)
                    )
                )
            );
        });

        modelBuilder.Entity<StorePrintingSetting>(entity =>
        {
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
            entity.HasQueryFilter(x =>
                !HasAuthenticatedRequest
                || CanBypassFilters
                || (
                    (
                        CurrentSelectedTenantId == null
                        || x.TenantId == CurrentSelectedTenantId
                        || CurrentAllowedTenantIds.Contains(x.TenantId)
                    )
                    && (
                        CurrentSelectedStoreId == null
                        || x.StoreId == CurrentSelectedStoreId
                        || CurrentAllowedStoreIds.Contains(x.StoreId)
                    )
                )
            );
        });

        modelBuilder.Entity<StoreOperationalSetting>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.StoreId }).IsUnique();
            entity.Property(x => x.CashRegisterMode).HasDefaultValue(CashRegisterModes.Disabled);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
            entity.Ignore(x => x.CreatedBy);
            entity.Ignore(x => x.UpdatedBy);
            entity
                .HasOne(x => x.Store)
                .WithOne(x => x.OperationalSetting)
                .HasForeignKey<StoreOperationalSetting>(x => x.StoreId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(x =>
                !HasAuthenticatedRequest
                || CanBypassFilters
                || (
                    (
                        CurrentSelectedTenantId == null
                        || x.TenantId == CurrentSelectedTenantId
                        || CurrentAllowedTenantIds.Contains(x.TenantId)
                    )
                    && (
                        CurrentSelectedStoreId == null
                        || x.StoreId == CurrentSelectedStoreId
                        || CurrentAllowedStoreIds.Contains(x.StoreId)
                    )
                )
            );
        });

        modelBuilder.Entity<SalePayment>(entity =>
        {
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity
                .HasOne(x => x.Sale)
                .WithMany(x => x.Payments)
                .HasForeignKey(x => x.SaleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne<CashRegisterSession>()
                .WithMany(x => x.SalePayments)
                .HasForeignKey(x => x.CashRegisterSessionId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasQueryFilter(x =>
                !HasAuthenticatedRequest
                || CanBypassFilters
                || (
                    (
                        CurrentSelectedTenantId == null
                        || x.TenantId == CurrentSelectedTenantId
                        || CurrentAllowedTenantIds.Contains(x.TenantId)
                    )
                    && (
                        CurrentSelectedStoreId == null
                        || x.StoreId == CurrentSelectedStoreId
                        || CurrentAllowedStoreIds.Contains(x.StoreId)
                    )
                )
            );
        });
        ConfigureSyncMetadata<SalePayment>(modelBuilder);

        modelBuilder.Entity<CashRegisterSession>(entity =>
        {
            entity.Property(x => x.OpeningCashAmount).HasPrecision(18, 2);
            entity.Property(x => x.ExpectedCashAmount).HasPrecision(18, 2);
            entity.Property(x => x.CountedCashAmount).HasPrecision(18, 2);
            entity.Property(x => x.DifferenceAmount).HasPrecision(18, 2);
            entity.Property(x => x.OpenedAt).HasDefaultValueSql("now()");
            entity.Property(x => x.Status).HasDefaultValue("Open");
            entity.Ignore(x => x.CreatedBy);
            entity.Ignore(x => x.UpdatedBy);
            entity
                .HasOne(x => x.Store)
                .WithMany()
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x =>
                !HasAuthenticatedRequest
                || CanBypassFilters
                || (
                    (
                        CurrentSelectedTenantId == null
                        || x.TenantId == CurrentSelectedTenantId
                        || CurrentAllowedTenantIds.Contains(x.TenantId)
                    )
                    && (
                        CurrentSelectedStoreId == null
                        || x.StoreId == CurrentSelectedStoreId
                        || CurrentAllowedStoreIds.Contains(x.StoreId)
                    )
                )
            );
        });
        ConfigureSyncMetadata<CashRegisterSession>(modelBuilder);

        modelBuilder.Entity<CashRegisterCashMovement>(entity =>
        {
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity
                .HasOne(x => x.CashRegisterSession)
                .WithMany(x => x.CashMovements)
                .HasForeignKey(x => x.CashRegisterSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(x =>
                !HasAuthenticatedRequest
                || CanBypassFilters
                || (
                    (
                        CurrentSelectedTenantId == null
                        || x.TenantId == CurrentSelectedTenantId
                        || CurrentAllowedTenantIds.Contains(x.TenantId)
                    )
                    && (
                        CurrentSelectedStoreId == null
                        || x.StoreId == CurrentSelectedStoreId
                        || CurrentAllowedStoreIds.Contains(x.StoreId)
                    )
                )
            );
        });
        ConfigureSyncMetadata<CashRegisterCashMovement>(modelBuilder);

        modelBuilder.Entity<SyncConflict>(entity =>
        {
            entity.Property(x => x.LocalPayloadJson).HasColumnType("jsonb");
            entity.Property(x => x.ServerPayloadJson).HasColumnType("jsonb");
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(x => x.ResolutionStatus).HasDefaultValue(SyncConflictResolutionStatuses.Open);
            entity.HasIndex(x => new { x.TenantId, x.StoreId, x.ResolutionStatus });
            entity.HasIndex(x => new { x.TenantId, x.StoreId, x.EntityName, x.EntitySyncId });
            entity.HasQueryFilter(x =>
                !HasAuthenticatedRequest
                || CanBypassFilters
                || (
                    (
                        CurrentSelectedTenantId == null
                        || x.TenantId == CurrentSelectedTenantId
                        || CurrentAllowedTenantIds.Contains(x.TenantId)
                    )
                    && (
                        x.StoreId == null
                        || CurrentSelectedStoreId == null
                        || x.StoreId == CurrentSelectedStoreId
                        || CurrentAllowedStoreIds.Contains(x.StoreId.Value)
                    )
                )
            );
        });

        modelBuilder.Entity<DemoTemplate>(entity =>
        {
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(x => x.SeedVersion).HasDefaultValue("v1");
            entity.Property(x => x.DefaultDurationMinutes).HasDefaultValue(1440);
            entity.Ignore(x => x.CreatedBy);
            entity.Ignore(x => x.UpdatedBy);
            entity
                .HasOne(x => x.TemplateTenant)
                .WithMany()
                .HasForeignKey(x => x.TemplateTenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x =>
                !HasAuthenticatedRequest
                || CanBypassFilters
                || CurrentSelectedTenantId == null
                || x.TemplateTenantId == CurrentSelectedTenantId
                || CurrentAllowedTenantIds.Contains(x.TemplateTenantId)
            );
        });

        modelBuilder.Entity<DemoSession>(entity =>
        {
            entity.HasIndex(x => x.DemoTokenHash).IsUnique();
            entity.Property(x => x.TenantId).IsRequired();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity
                .HasOne(x => x.DemoTemplate)
                .WithMany(x => x.DemoSessions)
                .HasForeignKey(x => x.DemoTemplateId)
                .OnDelete(DeleteBehavior.Restrict);
            entity
                .HasOne(x => x.Tenant)
                .WithMany(x => x.DemoSessions)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(x =>
                !HasAuthenticatedRequest
                || CanBypassFilters
                || CurrentSelectedTenantId == null
                || x.TenantId == CurrentSelectedTenantId
                || CurrentAllowedTenantIds.Contains(x.TenantId)
            );
        });

        modelBuilder.Entity<DemoSessionEvent>(entity =>
        {
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity
                .HasOne(x => x.DemoSession)
                .WithMany(x => x.Events)
                .HasForeignKey(x => x.DemoSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(x =>
                !HasAuthenticatedRequest
                || CanBypassFilters
                || CurrentSelectedTenantId == null
                || x.DemoSession.TenantId == CurrentSelectedTenantId
                || CurrentAllowedTenantIds.Contains(x.DemoSession.TenantId)
            );
        });

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity.HasQueryFilter(x =>
                !HasAuthenticatedRequest
                || CanBypassFilters
                || CurrentSelectedTenantId == null
                || x.Id == CurrentSelectedTenantId
                || CurrentAllowedTenantIds.Contains(x.Id)
            );
        });

        modelBuilder.Entity<TenantPrintingSetting>(entity =>
        {
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
            entity.HasQueryFilter(x =>
                !HasAuthenticatedRequest
                || CanBypassFilters
                || CurrentSelectedTenantId == null
                || x.TenantId == CurrentSelectedTenantId
                || CurrentAllowedTenantIds.Contains(x.TenantId)
            );
        });

        modelBuilder.Entity<TenantUser>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();
            entity.Ignore(x => x.Role);
            entity
                .HasOne(x => x.TenantRole)
                .WithMany(x => x.TenantUsers)
                .HasForeignKey(x => x.TenantRoleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x =>
                !HasAuthenticatedRequest
                || CanBypassFilters
                || CurrentSelectedTenantId == null
                || x.TenantId == CurrentSelectedTenantId
                || CurrentAllowedTenantIds.Contains(x.TenantId)
            );
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Ignore(x => x.PlatformRole);
            entity
                .HasOne(x => x.PlatformRoleNavigation)
                .WithMany(x => x.PlatformUsers)
                .HasForeignKey(x => x.PlatformRoleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x =>
                !HasAuthenticatedRequest
                || CanBypassFilters
                || x.TenantUsers.Any(tu => CurrentAllowedTenantIds.Contains(tu.TenantId))
            );
        });

        modelBuilder.Entity<UserStoreRole>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.StoreId, x.UserId }).IsUnique();
            entity.Ignore(x => x.Role);
            entity.HasOne(x => x.User).WithMany(x => x.UserStores).HasForeignKey(x => x.UserId);
            entity.HasOne(x => x.Store).WithMany(x => x.UserStores).HasForeignKey(x => x.StoreId);
            entity
                .HasOne(x => x.RoleNavigation)
                .WithMany(x => x.StoreRoleAssignments)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x =>
                !HasAuthenticatedRequest
                || CanBypassFilters
                || (
                    (
                        CurrentSelectedTenantId == null
                        || x.TenantId == CurrentSelectedTenantId
                        || CurrentAllowedTenantIds.Contains(x.TenantId)
                    )
                    && (
                        CurrentSelectedStoreId == null
                        || x.StoreId == CurrentSelectedStoreId
                        || CurrentAllowedStoreIds.Contains(x.StoreId)
                    )
                )
            );
        });

        modelBuilder.Entity<StoreSellableVariantView>(entity =>
        {
            entity.HasNoKey();
            entity.ToView("vw_store_sellable_variants");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasIndex(x => new { x.Scope, x.Code }).IsUnique();
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<StockTransfer>(entity =>
        {
            entity.Property(x => x.RequestedAt).HasDefaultValueSql("now()");
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity.Ignore(x => x.CreatedBy);
            entity.Ignore(x => x.UpdatedBy);
            entity.HasCheckConstraint("CK_StockTransfers_DifferentStores", "\"FromStoreId\" <> \"ToStoreId\"");
            entity.HasMany(x => x.Items).WithOne(x => x.StockTransfer).HasForeignKey(x => x.StockTransferId).OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(x =>
                !HasAuthenticatedRequest
                || CanBypassFilters
                || CurrentSelectedTenantId == null
                || x.TenantId == CurrentSelectedTenantId
                || CurrentAllowedTenantIds.Contains(x.TenantId)
            );
        });
        ConfigureSyncMetadata<StockTransfer>(modelBuilder);

        modelBuilder.Entity<StockTransferItem>(entity =>
        {
            entity.HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.StockTransferId);
        });
        ConfigureSyncMetadata<StockTransferItem>(modelBuilder);

        modelBuilder.Entity<StockAdjustment>(entity =>
        {
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity.Ignore(x => x.UpdatedBy);
            entity.HasMany(x => x.Items).WithOne(x => x.StockAdjustment).HasForeignKey(x => x.StockAdjustmentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(x =>
                !HasAuthenticatedRequest
                || CanBypassFilters
                || (
                    (
                        CurrentSelectedTenantId == null
                        || x.TenantId == CurrentSelectedTenantId
                        || CurrentAllowedTenantIds.Contains(x.TenantId)
                    )
                    && (
                        CurrentSelectedStoreId == null
                        || x.StoreId == CurrentSelectedStoreId
                        || CurrentAllowedStoreIds.Contains(x.StoreId)
                    )
                )
            );
        });
        ConfigureSyncMetadata<StockAdjustment>(modelBuilder);

        modelBuilder.Entity<StockAdjustmentItem>(entity =>
        {
            entity.HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.StockAdjustmentId);
        });
        ConfigureSyncMetadata<StockAdjustmentItem>(modelBuilder);

        modelBuilder.Entity<CashRegisterSessionSummaryView>(entity =>
        {
            entity.HasNoKey();
            entity.ToView("vw_cash_register_session_summary");
        });

        modelBuilder.Entity<UserRolesNormalizedView>(entity =>
        {
            entity.HasNoKey();
            entity.ToView("vw_user_roles_normalized");
        });
    }

    private static void ConfigureSyncMetadata<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, IOfflineSyncEntity
    {
        modelBuilder.Entity<TEntity>(entity =>
        {
            entity.HasIndex(x => x.SyncId).IsUnique();
            entity.Property(x => x.SyncId).IsRequired().HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.SourceDeviceId).HasMaxLength(128);
            entity.Property(x => x.ClientCreatedAt);
            entity.Property(x => x.ClientUpdatedAt);
            entity.Property(x => x.LastSyncedAt);
        });
    }

    public override int SaveChanges()
    {
        return SaveChangesWithAuditingAsync(false, CancellationToken.None).GetAwaiter().GetResult();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return SaveChangesWithAuditingAsync(true, cancellationToken);
    }

    private async Task<int> SaveChangesWithAuditingAsync(
        bool async,
        CancellationToken cancellationToken
    )
    {
        ApplySyncInfo();
        ApplyAuditInfo();
        ValidateStoreIntegrity();
        ValidateRoleScopes();
        var auditEntries = BuildAuditLogs();
        if (auditEntries.Count > 0)
            AuditLogs.AddRange(auditEntries);

        return async ? await base.SaveChangesAsync(cancellationToken) : base.SaveChanges();
    }

    private void ApplySyncInfo()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries().Where(x => x.Entity is IOfflineSyncEntity))
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
                continue;

            var entity = (IOfflineSyncEntity)entry.Entity;
            if (entity.SyncId == Guid.Empty)
                entity.SyncId = Guid.NewGuid();

            if (entry.State == EntityState.Added)
            {
                entity.ClientCreatedAt ??= now;
                entity.ClientUpdatedAt ??= entity.ClientCreatedAt;
            }
            else if (entry.State == EntityState.Modified)
            {
                entity.ClientUpdatedAt = now;
            }
        }
    }

    private void ApplyAuditInfo()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries().Where(x => x.Entity is IAuditableEntity))
        {
            var entity = (IAuditableEntity)entry.Entity;
            if (entry.State == EntityState.Added)
            {
                entity.CreatedAt ??= now;
                entity.UpdatedAt ??= now;
                if (_requestContext?.UserId != null)
                {
                    entity.CreatedBy ??= _requestContext.UserId;
                    entity.UpdatedBy ??= _requestContext.UserId;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                entity.UpdatedAt = now;
                if (_requestContext?.UserId != null)
                    entity.UpdatedBy = _requestContext.UserId;
            }
        }
    }

    private List<AuditLog> BuildAuditLogs()
    {
        if (_requestContext?.UserId == null)
            return new List<AuditLog>();

        var entries = ChangeTracker
            .Entries()
            .Where(entry =>
                entry.Entity is not AuditLog
                && entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
            )
            .ToList();

        var logs = new List<AuditLog>();
        foreach (var entry in entries)
        {
            var keyValues = entry
                .Properties.Where(p => p.Metadata.IsPrimaryKey())
                .ToDictionary(
                    p => p.Metadata.Name,
                    p => entry.State == EntityState.Added ? p.CurrentValue : p.OriginalValue
                );

            var oldValues = new Dictionary<string, object?>();
            var newValues = new Dictionary<string, object?>();

            foreach (var property in entry.Properties)
            {
                if (property.Metadata.IsPrimaryKey())
                    continue;

                switch (entry.State)
                {
                    case EntityState.Added:
                        newValues[property.Metadata.Name] = property.CurrentValue;
                        break;
                    case EntityState.Deleted:
                        oldValues[property.Metadata.Name] = property.OriginalValue;
                        break;
                    case EntityState.Modified when property.IsModified:
                        oldValues[property.Metadata.Name] = property.OriginalValue;
                        newValues[property.Metadata.Name] = property.CurrentValue;
                        break;
                }
            }

            var tenantId = ResolveTenantId(entry);
            var storeId = ResolveStoreId(entry);

            logs.Add(
                new AuditLog
                {
                    TenantId = IsKnownTenantId(tenantId) ? tenantId : null,
                    StoreId = IsKnownStoreId(storeId) ? storeId : null,
                    UserId = _requestContext.UserId,
                    EntityName = entry.Metadata.ClrType.Name,
                    EntityId = JsonSerializer.Serialize(keyValues),
                    ActionType = entry.State.ToString(),
                    OldValuesJson =
                        oldValues.Count == 0 ? null : JsonSerializer.Serialize(oldValues),
                    NewValuesJson =
                        newValues.Count == 0 ? null : JsonSerializer.Serialize(newValues),
                    Description = $"{entry.State} {entry.Metadata.ClrType.Name}",
                    IpAddress = _requestContext.IpAddress,
                    CreatedAt = DateTime.UtcNow,
                }
            );
        }

        return logs;
    }

    private bool IsKnownTenantId(int? tenantId)
    {
        if (!tenantId.HasValue || tenantId.Value <= 0)
            return false;

        if (
            ChangeTracker
                .Entries<Tenant>()
                .Any(x => x.Entity.Id == tenantId.Value && x.State != EntityState.Deleted)
        )
        {
            return true;
        }

        return Tenants.IgnoreQueryFilters().Any(x => x.Id == tenantId.Value);
    }

    private bool IsKnownStoreId(int? storeId)
    {
        if (!storeId.HasValue || storeId.Value <= 0)
            return false;

        if (
            ChangeTracker
                .Entries<Store>()
                .Any(x => x.Entity.Id == storeId.Value && x.State != EntityState.Deleted)
        )
        {
            return true;
        }

        return Stores.IgnoreQueryFilters().Any(x => x.Id == storeId.Value);
    }

    private static int? ResolveTenantId(EntityEntry entry)
    {
        int? tenantId = null;

        if (entry.Entity is ITenantScopedEntity tenantScoped)
        {
            tenantId = tenantScoped.TenantId;
        }
        else
        {
            tenantId = ReadIntProperty(entry, "TenantId");
        }

        return tenantId.HasValue && tenantId.Value > 0 ? tenantId : null;
    }

    private static int? ResolveStoreId(EntityEntry entry)
    {
        int? storeId = null;

        if (entry.Entity is IStoreScopedEntity storeScoped)
        {
            storeId = storeScoped.StoreId;
        }
        else
        {
            storeId = ReadIntProperty(entry, "StoreId");
        }

        return storeId.HasValue && storeId.Value > 0 ? storeId : null;
    }

    private static int? ReadIntProperty(EntityEntry entry, string propertyName)
    {
        var property = entry.Properties.FirstOrDefault(x => x.Metadata.Name == propertyName);
        if (property == null)
            return null;

        var value = entry.State == EntityState.Deleted ? property.OriginalValue : property.CurrentValue;

        if (value is int intValue)
            return intValue;

        if (value is long longValue && longValue is > 0 and <= int.MaxValue)
            return (int)longValue;

        return null;
    }

    private void ValidateStoreIntegrity()
    {
        foreach (
            var entry in ChangeTracker
                .Entries<ProductVariant>()
                .Where(x => x.State != EntityState.Unchanged)
        )
        {
            var assignment =
                entry.Entity.ProductStoreAssignment
                ?? ProductStoreAssignments
                    .IgnoreQueryFilters()
                    .FirstOrDefault(a => a.Id == entry.Entity.ProductStoreAssignmentId);

            if (assignment != null)
            {
                if (assignment.TenantId != entry.Entity.TenantId)
                    throw new InvalidOperationException(
                        "Product variant must belong to the same tenant as its assignment."
                    );
            }
        }

        foreach (
            var entry in ChangeTracker
                .Entries<SaleItem>()
                .Where(x => x.State != EntityState.Unchanged)
        )
        {
            var sale =
                entry.Entity.Sale
                ?? Sales.IgnoreQueryFilters().FirstOrDefault(s => s.Id == entry.Entity.SaleId);

            if (sale != null)
            {
                if (sale.StoreId != entry.Entity.StoreId)
                    throw new InvalidOperationException(
                        "Sale item must belong to the same store as its sale."
                    );
                if (sale.TenantId != entry.Entity.TenantId)
                    throw new InvalidOperationException(
                        "Sale item must belong to the same tenant as its sale."
                    );
            }

            var variant =
                entry.Entity.Variant
                ?? ProductVariants
                    .IgnoreQueryFilters()
                    .FirstOrDefault(v => v.Id == entry.Entity.VariantId);
            if (variant != null && variant.TenantId != entry.Entity.TenantId)
                throw new InvalidOperationException(
                    "Sale item variant must belong to the same tenant as the sale."
                );
        }

        foreach (
            var entry in ChangeTracker
                .Entries<StockMovement>()
                .Where(x => x.State != EntityState.Unchanged)
        )
        {
            var variant =
                entry.Entity.Variant
                ?? ProductVariants
                    .IgnoreQueryFilters()
                    .FirstOrDefault(v => v.Id == entry.Entity.VariantId);
            if (variant != null)
            {
                if (variant.TenantId != entry.Entity.TenantId)
                    throw new InvalidOperationException(
                        "Stock movement variant must belong to the same tenant."
                    );
            }
        }
    }

    private void ValidateRoleScopes()
    {
        var roleIds = ChangeTracker
            .Entries()
            .Where(x => x.State is EntityState.Added or EntityState.Modified)
            .SelectMany(entry =>
            {
                return entry.Entity switch
                {
                    User user when user.PlatformRoleId.HasValue => new[] { user.PlatformRoleId.Value },
                    TenantUser tenantUser when tenantUser.TenantRoleId.HasValue => new[] { tenantUser.TenantRoleId.Value },
                    UserStoreRole userStoreRole when userStoreRole.RoleId.HasValue => new[] { userStoreRole.RoleId.Value },
                    _ => Array.Empty<int>(),
                };
            })
            .Distinct()
            .ToList();

        if (roleIds.Count == 0)
            return;

        var roles = Roles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => roleIds.Contains(x.Id))
            .ToDictionary(x => x.Id, x => x.Scope);

        foreach (
            var entry in ChangeTracker
                .Entries()
                .Where(x => x.State is EntityState.Added or EntityState.Modified)
        )
        {
            switch (entry.Entity)
            {
                case User user when user.PlatformRoleId.HasValue:
                    EnsureRoleScope(roles, user.PlatformRoleId.Value, "Platform", "Users.PlatformRoleId");
                    break;
                case TenantUser tenantUser when tenantUser.TenantRoleId.HasValue:
                    EnsureRoleScope(roles, tenantUser.TenantRoleId.Value, "Tenant", "TenantUsers.TenantRoleId");
                    break;
                case UserStoreRole userStoreRole when userStoreRole.RoleId.HasValue:
                    EnsureRoleScope(roles, userStoreRole.RoleId.Value, "Store", "UserStoreRoles.RoleId");
                    break;
            }
        }
    }

    private static void EnsureRoleScope(
        IReadOnlyDictionary<int, string> roles,
        int roleId,
        string expectedScope,
        string fieldName)
    {
        if (!roles.TryGetValue(roleId, out var actualScope))
            throw new InvalidOperationException($"{fieldName} references a role that does not exist.");

        if (!string.Equals(actualScope, expectedScope, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{fieldName} must reference a {expectedScope} role.");
    }
}
