using Microsoft.EntityFrameworkCore;
using PixelHub.Api.Data;
using PixelHub.Api.Models;
using PixelHub.Api.Services;
using MongoDB.Driver;
using PixelHub.Api.Repositories;
using StackExchange.Redis;
using Neo4j.Driver;

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
builder.Services.AddSingleton<RedisSales>();
builder.Services.AddScoped<ShopService>();

builder.Services.AddSingleton<IDriver>(_ =>
    GraphDatabase.Driver(
        builder.Configuration["Neo4j:Uri"]
            ?? throw new InvalidOperationException("L'URI Neo4j est manquante."),
        AuthTokens.Basic(
            builder.Configuration["Neo4j:User"]
                ?? throw new InvalidOperationException("L'utilisateur Neo4j est manquant."),
            builder.Configuration["Neo4j:Password"]
                ?? throw new InvalidOperationException("Le mot de passe Neo4j est manquant."))));
builder.Services.AddSingleton<ISocialGraph, Neo4jSocialGraph>();

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

app.MapGet("/social/{pseudo}/friends",
    async (string pseudo, ISocialGraph graph) => await graph.GetFriendsAsync(pseudo));

app.MapGet("/social/{pseudo}/recommendations",
    async (string pseudo, int? depth, ISocialGraph graph) =>
        await graph.RecommendGamesAsync(pseudo, depth ?? 2));

app.MapGet("/social/{pseudo}/friend-suggestions",
    async (string pseudo, ISocialGraph graph) => await graph.SuggestFriendsAsync(pseudo));

// ---------- Séance 4 bis — la boutique (mission 2) ----------
app.MapPost("/shop/{pseudo}/buy/{titre}", async (string pseudo, string titre, ShopService shop) =>
{
    var r = await shop.BuyAsync(pseudo, titre);
    return r.Statut switch
    {
        AchatStatut.Ok => Results.Ok(r.Recu),
        AchatStatut.JeuInconnu => Results.NotFound(new { erreur = $"Jeu inconnu : {titre}" }),
        AchatStatut.JoueurInconnu => Results.NotFound(new { erreur = $"Joueur inconnu : {pseudo}" }),
        AchatStatut.SoldeInsuffisant => Results.Conflict(new { erreur = "Solde insuffisant" }),
        AchatStatut.DejaPossede => Results.Conflict(new { erreur = "Jeu déjà acheté" }),
        _ => Results.StatusCode(500)
    };
});

app.MapGet("/shop/bestsellers", async (RedisSales ventes) => await ventes.GetTopAsync(3));

app.Run();