using Microsoft.EntityFrameworkCore;

namespace DevicesDashboard.Models
{
    public class EdgeDevicesDbContext : DbContext
    {
        public EdgeDevicesDbContext(DbContextOptions<EdgeDevicesDbContext> options) : base(options)
        {

        }

        // OnConfiguring method is used to select and config the data source
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }

        // Adding db set

        public DbSet<Device> Devices { get; set; }
    }
}
