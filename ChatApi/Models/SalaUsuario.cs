namespace ChatApi.Models;

public class SalaUsuario
{
    public Guid SalaId { get; set; }
    public Sala Sala { get; set; } = null!;

    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
}
