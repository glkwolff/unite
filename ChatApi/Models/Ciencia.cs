namespace ChatApi.Models;

/// <summary>Botao "Ciente": a pessoa confirmou que leu a postagem. Chave = (PostagemId, UsuarioId).</summary>
public class Ciencia
{
    public Guid PostagemId { get; set; }
    public Postagem Postagem { get; set; } = null!;

    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public DateTime ConfirmadaEm { get; set; } = DateTime.UtcNow;
}
