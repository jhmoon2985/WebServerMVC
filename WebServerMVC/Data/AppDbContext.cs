using WebServerMVC.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace WebServerMVC.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Client> Clients { get; set; }
        public DbSet<ClientMatch> Matches { get; set; }

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
        }
    }
}