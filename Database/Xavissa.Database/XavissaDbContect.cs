using Microsoft.EntityFrameworkCore;
using Xavissa.Database.Models;

namespace Xavissa.Database
{
    public class XavissaDbContext : DbContext
    {
        public XavissaDbContext(DbContextOptions<XavissaDbContext> options)
            : base(options) { }

        public DbSet<Product> Products { get; set; }
        public DbSet<StandardProduct> StandardProducts { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<SaleItem> SaleItems { get; set; }
        public DbSet<DeletedSale> DeletedSales { get; set; }
        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Unique constraint for barcode
            modelBuilder.Entity<Product>()
                .HasIndex(p => p.Barcode)
                .IsUnique();
        }
    }
}
