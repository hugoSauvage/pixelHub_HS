using System.Text.Json;
using PixelHub.Api.Models;
using StackExchange.Redis;

namespace PixelHub.Api.Repositories;

public class CachedGameCatalog : IGameCatalog
{
    private const string KeyAll = "cache:catalogue:tous";
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private readonly IGameCatalog _source;
    private readonly IDatabase _cache;

    public CachedGameCatalog(IGameCatalog source, IConnectionMultiplexer redis)
    {
        _source = source;
        _cache = redis.GetDatabase();
    }

    public async Task<List<Game>> GetAllAsync()
    {
        var cached = await _cache.StringGetAsync(KeyAll);
        if (cached.HasValue)
        {
            return JsonSerializer.Deserialize<List<Game>>((string)cached!)
                ?? throw new JsonException("Le cache du catalogue contient une valeur JSON nulle.");
        }

        var games = await _source.GetAllAsync();
        await _cache.StringSetAsync(KeyAll, JsonSerializer.Serialize(games), Ttl);
        return games;
    }

    public Task<List<Game>> GetByGenreAsync(string genre) =>
        _source.GetByGenreAsync(genre);

    public Task<List<Game>> GetTopRatedAsync(int count) =>
        _source.GetTopRatedAsync(count);

    public Task<List<GenreStats>> GetStatsByGenreAsync() =>
        _source.GetStatsByGenreAsync();
}
