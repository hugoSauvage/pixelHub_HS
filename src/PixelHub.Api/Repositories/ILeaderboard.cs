namespace PixelHub.Api.Repositories;

public record LeaderboardEntry(string Pseudo, double Score, long Rang);

public interface ILeaderboard
{
    Task AddPointsAsync(string pseudo, double points);
    Task<List<LeaderboardEntry>> GetTopAsync(int count);
    Task<long?> GetRankAsync(string pseudo);
}
