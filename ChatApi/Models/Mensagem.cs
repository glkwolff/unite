using System.ComponentModel.DataAnnotations;

namespace ChatApi.Models;

public class Mensagem
{
    public Guid Id { get; set; }

    public Guid SalaId { get; set; }
    public Sala Sala { get; set; } = null!;

    public Guid AutorId { get; set; }
    public Usuario Autor { get; set; } = null!;

    [MaxLength(4000)]
    public string Texto { get; set; } = string.Empty;

    public DateTime EnviadaEm { get; set; } = DateTime.UtcNow;
}
