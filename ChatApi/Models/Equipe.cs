using System.ComponentModel.DataAnnotations;

namespace ChatApi.Models;

public class Equipe
{
    public Guid Id { get; set; }

    [MaxLength(80)]
    public string Nome { get; set; } = string.Empty;

    /// <summary>Supervisor responsavel pela equipe.</summary>
    public Guid? SupervisorId { get; set; }
    public Usuario? Supervisor { get; set; }

    public ICollection<Usuario> Membros { get; set; } = new List<Usuario>();
}
