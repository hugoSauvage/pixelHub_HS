using StackExchange.Redis;

namespace PixelHub.Api.Repositories;

public record GameSales(string Titre, long Ventes);

// Projection des ventes dans un sorted set Redis, reconstructible depuis PostgreSQL.
public class RedisSales
{
    private const string Key = "ventes:jeux";
    private readonly IDatabase _db;

    public RedisSales(IConnectionMultiplexer redis) => _db = redis.GetDatabase();

    public async Task RecordSaleAsync(string titre) =>
        await _db.SortedSetIncrementAsync(Key, titre, 1);

    public async Task<List<GameSales>> GetTopAsync(int count)
    {
        if (count <= 0)
            return [];

        var entries = await _db.SortedSetRangeByRankWithScoresAsync(
            Key, 0, count - 1, Order.Descending);

        return entries.Select(e => new GameSales(e.Element.ToString(), (long)e.Score)).ToList();
    }
}
