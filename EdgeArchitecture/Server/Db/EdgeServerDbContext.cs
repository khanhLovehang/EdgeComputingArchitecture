using Microsoft.EntityFrameworkCore;
using Server.Models;
using System;

namespace Server.Db
{
    public class EdgeServerDbContext : DbContext
    {
        public EdgeServerDbContext(DbContextOptions<EdgeServerDbContext> options) : base(options) { }

        public DbSet<DataRecord> DataRecords { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Optional: Configure indexes for faster querying
            modelBuilder.Entity<DataRecord>()
                .HasIndex(r => r.DeviceId);
            modelBuilder.Entity<DataRecord>()
                .HasIndex(r => r.ReceivedTimestamp);
        }
    }
}
