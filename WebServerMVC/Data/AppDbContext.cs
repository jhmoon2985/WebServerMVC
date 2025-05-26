using WebServerMVC.Models;
using Microsoft.EntityFrameworkCore;

namespace WebServerMVC.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Client> Clients { get; set; }
        public DbSet<ClientMatch> Matches { get; set; }
        public DbSet<InAppPurchase> InAppPurchases { get; set; } // 추가

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Client 엔티티 구성
            modelBuilder.Entity<Client>()
                .HasKey(c => c.ClientId);

            modelBuilder.Entity<Client>()
                .HasIndex(c => c.ConnectionId);

            // ClientMatch 엔티티 구성
            modelBuilder.Entity<ClientMatch>()
                .HasKey(m => m.Id);

            modelBuilder.Entity<ClientMatch>()
                .HasIndex(m => new { m.ClientId1, m.ClientId2 });

            modelBuilder.Entity<ClientMatch>()
                .HasIndex(m => m.MatchedAt);

            // InAppPurchase 엔티티 구성 (추가)
            modelBuilder.Entity<InAppPurchase>()
                .HasKey(p => p.Id);

            modelBuilder.Entity<InAppPurchase>()
                .HasIndex(p => p.ClientId);

            modelBuilder.Entity<InAppPurchase>()
                .HasIndex(p => p.PurchaseToken)
                .IsUnique();

            modelBuilder.Entity<InAppPurchase>()
                .HasIndex(p => p.Status);

            modelBuilder.Entity<InAppPurchase>()
                .HasIndex(p => p.PurchasedAt);

            modelBuilder.Entity<InAppPurchase>()
                .Property(p => p.Amount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<InAppPurchase>()
                .Property(p => p.Status)
                .HasConversion<string>();
        }
    }
}