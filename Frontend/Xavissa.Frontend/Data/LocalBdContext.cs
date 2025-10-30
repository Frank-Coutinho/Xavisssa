using Microsoft.EntityFrameworkCore;
using System;

namespace Xavissa.Frontend.Data
{
    public class LocalDbContext : DbContext
    {
        public DbSet<Product> Products { get; set; }
        public DbSet<SyncLog> SyncLogs { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=xavissa_local.db");
        }
    }

    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public DateTime LastModified { get; set; }
    }

    public class SyncLog
    {
        public int Id { get; set; }
        public string Table { get; set; }
        public string Operation { get; set; } // Insert/Update/Delete
        public string Payload { get; set; }   // JSON data
        public DateTime Timestamp { get; set; }
        public bool Synced { get; set; }
    }
}
