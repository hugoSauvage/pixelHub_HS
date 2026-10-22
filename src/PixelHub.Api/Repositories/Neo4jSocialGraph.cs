using Neo4j.Driver;

namespace PixelHub.Api.Repositories;

public class Neo4jSocialGraph : ISocialGraph
{
    private readonly IDriver _driver;

    public Neo4jSocialGraph(IDriver driver) => _driver = driver;

    public async Task<List<string>> GetFriendsAsync(string pseudo)
    {
        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                @"MATCH (:Joueur {pseudo: $pseudo})-[:AMI_DE]-(a:Joueur)
                  RETURN a.pseudo AS pseudo",
                new { pseudo });

            return await cursor.ToListAsync(r => r["pseudo"].As<string>());
        });
    }

    public async Task<List<GameRecommendation>> RecommendGamesAsync(string pseudo, int profondeur = 2)
    {
        if (profondeur < 1 || profondeur > 4)
            throw new ArgumentOutOfRangeException(nameof(profondeur), "Profondeur attendue entre 1 et 4.");

        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                $@"MATCH (moi:Joueur {{pseudo: $pseudo}})-[:AMI_DE*1..{profondeur}]-(ami:Joueur)
                         -[:POSSEDE]->(j:Jeu)
                   WHERE ami <> moi AND NOT (moi)-[:POSSEDE]->(j)
                   RETURN j.titre AS titre, count(DISTINCT ami) AS nbAmis
                   ORDER BY nbAmis DESC",
                new { pseudo });

            return await cursor.ToListAsync(r =>
                new GameRecommendation(r["titre"].As<string>(), r["nbAmis"].As<int>()));
        });
    }

    public async Task<List<FriendSuggestion>> SuggestFriendsAsync(string pseudo)
    {
        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                @"MATCH (moi:Joueur {pseudo: $pseudo})-[:AMI_DE]-(c:Joueur)-[:AMI_DE]-(s:Joueur)
                  WHERE s <> moi AND NOT (moi)-[:AMI_DE]-(s)
                  RETURN s.pseudo AS pseudo, count(DISTINCT c) AS communs
                  ORDER BY communs DESC",
                new { pseudo });

            return await cursor.ToListAsync(r =>
                new FriendSuggestion(r["pseudo"].As<string>(), r["communs"].As<int>()));
        });
    }

    public async Task AddOwnershipAsync(string pseudo, string titre, int annee)
    {
        await using var session = _driver.AsyncSession();

        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(
                @"MERGE (j:Joueur {pseudo: $pseudo})
                  MERGE (g:Jeu {titre: $titre})
                  MERGE (j)-[r:POSSEDE]->(g)
                  ON CREATE SET r.depuis = $annee",
                new { pseudo, titre, annee });
        });
    }
}
