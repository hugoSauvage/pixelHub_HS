using StackExchange.Redis;

namespace PixelHub.Api.Repositories;

public class RedisPresence
{
    private const string Key = "online:joueurs";
    private static readonly TimeSpan OnlineWindow = TimeSpan.FromSeconds(60);
    private readonly IDatabase _db;

    public RedisPresence(IConnectionMultiplexer redis) =>
        _db = redis.GetDatabase();

    public async Task TouchAsync(string pseudo)
    {
        var lastSeen = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await _db.SortedSetAddAsync(Key, pseudo, lastSeen);
    }

    public async Task<long> CountOnlineAsync()
    {
        var cutoff = DateTimeOffset.UtcNow.Subtract(OnlineWindow).ToUnixTimeMilliseconds();
        await _db.SortedSetRemoveRangeByScoreAsync(Key, double.NegativeInfinity, cutoff);
        return await _db.SortedSetLengthAsync(Key);
    }
}
