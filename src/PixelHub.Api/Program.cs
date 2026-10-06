using Microsoft.EntityFrameworkCore;
using PixelHub.Api.Data;
using PixelHub.Api.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<PixelHubContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

var app = builder.Build();

// Crée la base et les tables au démarrage.
// Suffisant en TP ; dans un vrai projet, on utilise les migrations EF Core.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PixelHubContext>();
    db.Database.EnsureCreated();

    if (!db.Players.Any())
    {
        db.Players.AddRange(
            new Player { Pseudo = "Nova", Coins = 1200 },
            new Player { Pseudo = "Krayz", Coins = 350 },
            new Player { Pseudo = "Ombre", Coins = 90 }
        );
        db.SaveChanges();
    }
}

app.MapGet("/", () => "PixelHub API — séance 1 OK");

app.MapPost("/players", async (
    Player player,
    PixelHubContext db,
    CancellationToken cancellationToken) =>
{
    db.Players.Add(player);
    await db.SaveChangesAsync(cancellationToken);
    return Results.Created($"/players/{player.Id}", player);
});

app.MapGet("/players", async (PixelHubContext db) =>
    await db.Players.ToListAsync());

app.Run();