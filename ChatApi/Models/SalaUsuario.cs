namespace ChatApi.Models;

/// <summary>
/// Juncao entre sala e participante. Vale so para a conversa privada: o canal
/// de equipe nao tem linha aqui, porque participar dele e estar na equipe —
/// ver <see cref="Sala.Participantes"/>.
/// </summary>
public class SalaUsuario
{
    public Guid SalaId { get; set; }
    public Sala Sala { get; set; } = null!;

    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public DateTime EntrouEm { get; set; } = DateTime.UtcNow;
}
