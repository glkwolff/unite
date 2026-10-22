namespace ChatApi.Models;

/// <summary>Participante de uma conversa privada (o canal de equipe nao usa esta tabela).</summary>
public class SalaUsuario
{
    public Guid SalaId { get; set; }
    public Sala Sala { get; set; } = null!;

    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
}
