using Microsoft.EntityFrameworkCore;
using PixelHub.Api.Models;

namespace PixelHub.Api.Data;

public class PixelHubContext : DbContext
{
    public PixelHubContext(DbContextOptions<PixelHubContext> options)
        : base(options) { }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<Purchase> Purchases => Set<Purchase>();

    // Ces règles ne valent que pour une base créée par EnsureCreated.
    // Sur une base existante, elles doivent être ajoutées manuellement en SQL.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Player>()
            .ToTable(t => t.HasCheckConstraint("coins_positifs", "\"Coins\" >= 0"));

        modelBuilder.Entity<Purchase>(achat =>
        {
            achat.HasOne<Player>().WithMany().HasForeignKey(a => a.PlayerId);
            achat.HasIndex(a => new { a.PlayerId, a.Titre }).IsUnique();
            achat.ToTable(t => t.HasCheckConstraint("prix_paye_positif", "\"PrixPaye\" >= 0"));
        });
    }
}