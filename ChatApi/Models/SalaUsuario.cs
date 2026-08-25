namespace ChatApi.Models;

/// <summary>
/// Juncao entre sala e participante. Sustenta tanto a conversa privada
/// (dois participantes) quanto o grupo (varios).
/// </summary>
public class SalaUsuario
{
    public Guid SalaId { get; set; }
    public Sala Sala { get; set; } = null!;

    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public DateTime EntrouEm { get; set; } = DateTime.UtcNow;
}
