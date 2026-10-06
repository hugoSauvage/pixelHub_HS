using Microsoft.EntityFrameworkCore;
using PixelHub.Api.Models;

namespace PixelHub.Api.Data;

public class PixelHubContext : DbContext
{
    public PixelHubContext(DbContextOptions<PixelHubContext> options)
        : base(options) { }

    public DbSet<Player> Players => Set<Player>();
}