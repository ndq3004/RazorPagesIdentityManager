using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RazorPageShopManager.Databases.EntityTypeConfigurations;
using RazorPageShopManager.Entities;

namespace RazorPageShopManager.Databases
{
    public class AppDbContext : IdentityDbContext<ApplicaitonUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        //public DbSet<Product> Products { get; set; }
        //public DbSet<Category> Categories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfiguration(new ProductConfiguration());
        }
    }
}
