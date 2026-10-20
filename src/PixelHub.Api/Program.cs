using Microsoft.EntityFrameworkCore;
using PixelHub.Api.Data;
using PixelHub.Api.Models;
using MongoDB.Driver;
using PixelHub.Api.Repositories;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<PixelHubContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddSingleton<IMongoClient>(_ =>
new MongoClient(builder.Configuration.GetConnectionString("Mongo")));

builder.Services.AddSingleton<IMongoDatabase>(sp =>
    sp.GetRequiredService<IMongoClient>()
    .GetDatabase(builder.Configuration["Mongo:Database"]));

builder.Services.AddSingleton<MongoGameCatalog>();
builder.Services.AddSingleton<IGameCatalog>(sp =>
    new CachedGameCatalog(
        sp.GetRequiredService<MongoGameCatalog>(),
        sp.GetRequiredService<IConnectionMultiplexer>()));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")
        ?? throw new InvalidOperationException("La chaîne de connexion Redis est manquante.")));
builder.Services.AddSingleton<ILeaderboard, RedisLeaderboard>();
builder.Services.AddSingleton<RedisPresence>();

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

app.MapGet("/games", async (IGameCatalog catalog) =>
    await catalog.GetAllAsync());

app.MapGet("/games/genre/{genre}", async (string genre, IGameCatalog catalog) =>
    await catalog.GetByGenreAsync(genre));

app.MapGet("/games/top/{count:int}", async (int count, IGameCatalog catalog) =>
    await catalog.GetTopRatedAsync(count));

app.MapGet("/games/stats", async (IGameCatalog catalog) =>
    await catalog.GetStatsByGenreAsync());

app.MapGet("/games/timed", async (IGameCatalog catalog) =>
{
    var chrono = System.Diagnostics.Stopwatch.StartNew();
    var jeux = await catalog.GetAllAsync();
    chrono.Stop();
    return new { nombre = jeux.Count, millisecondes = chrono.Elapsed.TotalMilliseconds };
});

app.MapPost("/leaderboard/{pseudo}/points/{points:double}",
    async (string pseudo, double points, ILeaderboard leaderboard) =>
    {
        await leaderboard.AddPointsAsync(pseudo, points);
        return Results.Ok();
    });

app.MapGet("/leaderboard/top/{count:int}",
    async (int count, ILeaderboard leaderboard) =>
        await leaderboard.GetTopAsync(count));

app.MapGet("/leaderboard/rank/{pseudo}",
    async (string pseudo, ILeaderboard leaderboard) =>
        await leaderboard.GetRankAsync(pseudo));

app.MapPost("/online/{pseudo}", async (string pseudo, RedisPresence presence) =>
{
    await presence.TouchAsync(pseudo);
    return Results.Ok();
});

app.MapGet("/online/count", async (RedisPresence presence) =>
    await presence.CountOnlineAsync());

app.Run();