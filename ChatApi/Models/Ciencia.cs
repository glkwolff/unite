namespace ChatApi.Models;

/// <summary>
/// Registro do botao "Ciente": confirma que o colaborador leu a postagem.
/// Um usuario so pode estar ciente uma vez por postagem (indice unico).
/// </summary>
public class Ciencia
{
    public Guid Id { get; set; }

    public Guid PostagemId { get; set; }
    public Postagem Postagem { get; set; } = null!;

    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public DateTime ConfirmadaEm { get; set; } = DateTime.UtcNow;
}
