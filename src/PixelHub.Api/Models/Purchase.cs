namespace PixelHub.Api.Models;

// La trace d'achat fait foi dans PostgreSQL ; le graphe et le classement des ventes sont des projections.
public class Purchase
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public string Titre { get; set; } = "";
    public int PrixPaye { get; set; }
    public DateTime Date { get; set; }
}
