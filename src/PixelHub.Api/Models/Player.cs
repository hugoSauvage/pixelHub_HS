namespace PixelHub.Api.Models;

public class Player
{
    public int Id { get; set; }
    public string Pseudo { get; set; } = "";
    public int Coins { get; set; }          // monnaie virtuelle
}

