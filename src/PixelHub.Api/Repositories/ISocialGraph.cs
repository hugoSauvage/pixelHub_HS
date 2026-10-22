namespace PixelHub.Api.Repositories;

public interface ISocialGraph
{
    Task<List<string>> GetFriendsAsync(string pseudo);
    Task<List<GameRecommendation>> RecommendGamesAsync(string pseudo, int profondeur = 2);
    Task<List<FriendSuggestion>> SuggestFriendsAsync(string pseudo);
    Task AddOwnershipAsync(string pseudo, string titre, int annee);
}

public sealed record GameRecommendation(string Titre, int NbAmis);

public sealed record FriendSuggestion(string Pseudo, int AmisCommuns);
