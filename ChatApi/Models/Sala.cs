using System.ComponentModel.DataAnnotations;

namespace ChatApi.Models;

public enum TipoSala
{
    Privada = 1,
    Grupo = 2
}

public class Sala
{
    public Guid Id { get; set; }

    [MaxLength(120)]
    public string Nome { get; set; } = string.Empty;

    public TipoSala Tipo { get; set; }

    /// <summary>Preenchido quando a sala representa o canal de uma equipe.</summary>
    public Guid? EquipeId { get; set; }
    public Equipe? Equipe { get; set; }

    public DateTime CriadaEm { get; set; } = DateTime.UtcNow;

    public ICollection<SalaUsuario> Participantes { get; set; } = new List<SalaUsuario>();
    public ICollection<Mensagem> Mensagens { get; set; } = new List<Mensagem>();
}
