using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace ChatApi.Models;

public class Usuario : IdentityUser<Guid>
{
    [MaxLength(120)]
    public string NomeCompleto { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? FotoUrl { get; set; }

    public Guid? CargoId { get; set; }
    public Cargo? Cargo { get; set; }

    public Guid? EquipeId { get; set; }
    public Equipe? Equipe { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}
