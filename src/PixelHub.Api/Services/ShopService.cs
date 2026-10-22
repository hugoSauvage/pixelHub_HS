using Microsoft.EntityFrameworkCore;
using Npgsql;
using PixelHub.Api.Data;
using PixelHub.Api.Models;
using PixelHub.Api.Repositories;

namespace PixelHub.Api.Services;

public enum AchatStatut { Ok, JeuInconnu, JoueurInconnu, SoldeInsuffisant, DejaPossede }

public record AchatRecu(string Pseudo, string Titre, int PrixPaye, int Solde, bool ProjectionsAJour);

public record AchatResultat(AchatStatut Statut, AchatRecu? Recu = null);

// PostgreSQL is the source of truth; MongoDB provides prices and Neo4j/Redis are projections.
public class ShopService
{
    private readonly PixelHubContext _db;
    private readonly IGameCatalog _catalog;
    private readonly ISocialGraph _graph;
    private readonly RedisSales _ventes;
    private readonly ILogger<ShopService> _log;

    public ShopService(
        PixelHubContext db,
        IGameCatalog catalog,
        ISocialGraph graph,
        RedisSales ventes,
        ILogger<ShopService> log)
    {
        _db = db;
        _catalog = catalog;
        _graph = graph;
        _ventes = ventes;
        _log = log;
    }

    public async Task<AchatResultat> BuyAsync(string pseudo, string titre)
    {
        var jeu = await _catalog.GetByTitleAsync(titre);
        if (jeu is null)
            return new(AchatStatut.JeuInconnu);

        var joueur = await _db.Players.SingleOrDefaultAsync(p => p.Pseudo == pseudo);
        if (joueur is null)
            return new(AchatStatut.JoueurInconnu);

        if (await _db.Purchases.AnyAsync(a => a.PlayerId == joueur.Id && a.Titre == jeu.Titre))
            return new(AchatStatut.DejaPossede);

        await using (var tx = await _db.Database.BeginTransactionAsync())
        {
            var lignes = await _db.Players
                .Where(p => p.Id == joueur.Id && p.Coins >= jeu.Prix)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Coins, p => p.Coins - jeu.Prix));
            if (lignes == 0)
                return new(AchatStatut.SoldeInsuffisant);

            _db.Purchases.Add(new Purchase
            {
                PlayerId = joueur.Id,
                Titre = jeu.Titre,
                PrixPaye = jeu.Prix,
                Date = DateTime.UtcNow
            });

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException e) when (
                e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return new(AchatStatut.DejaPossede);
            }

            await tx.CommitAsync();
        }

        var solde = await _db.Players
            .Where(p => p.Id == joueur.Id)
            .Select(p => p.Coins)
            .SingleAsync();

        await _graph.AddOwnershipAsync(pseudo, jeu.Titre, DateTime.UtcNow.Year);
        await _ventes.RecordSaleAsync(jeu.Titre);

        return new(AchatStatut.Ok, new AchatRecu(pseudo, jeu.Titre, jeu.Prix, solde, true));
    }
}
