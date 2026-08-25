using Microsoft.AspNetCore.Identity;

namespace ChatApi.Models;

public class Usuario : IdentityUser<Guid>
{
    public string NomeCompleto { get; set; } = string.Empty;
    public string? FotoUrl { get; set; }

    public Guid? CargoId { get; set; }
    public Cargo? Cargo { get; set; }

    public Guid? EquipeId { get; set; }
    public Equipe? Equipe { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}
