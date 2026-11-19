using System.ComponentModel.DataAnnotations;
using ChatApi.Models;

namespace ChatApi.Dtos;

public class CargoEntradaDto
{
    [Required(ErrorMessage = "Informe o nome do cargo.")]
    [MaxLength(80)]
    public string Nome { get; set; } = string.Empty;

    public NivelHierarquico Nivel { get; set; }
}

public record CargoDto(Guid Id, string Nome, NivelHierarquico Nivel, int TotalUsuarios)
{
    public static CargoDto De(Cargo c, int totalUsuarios) => new(c.Id, c.Nome, c.Nivel, totalUsuarios);
}

public class EquipeEntradaDto
{
    [Required(ErrorMessage = "Informe o nome da equipe.")]
    [MaxLength(80)]
    public string Nome { get; set; } = string.Empty;

    public Guid? SupervisorId { get; set; }
}

public class AdicionarMembroDto
{
    [Required(ErrorMessage = "Informe o usuario.")]
    public Guid UsuarioId { get; set; }
}

public record MembroResumoDto(Guid Id, string NomeCompleto, string? FotoUrl, string? Cargo, NivelHierarquico? Nivel)
{
    public static MembroResumoDto De(Usuario u) => new(u.Id, u.NomeCompleto, u.FotoUrl, u.Cargo?.Nome, u.Cargo?.Nivel);
}

public record PessoaDto(
    Guid Id,
    string NomeCompleto,
    string? FotoUrl,
    Guid? CargoId,
    string? Cargo,
    NivelHierarquico? Nivel,
    Guid? EquipeId,
    string? Equipe)
{
    public static PessoaDto De(Usuario u) => new(
        u.Id, u.NomeCompleto, u.FotoUrl, u.CargoId, u.Cargo?.Nome, u.Cargo?.Nivel, u.EquipeId, u.Equipe?.Nome);
}

public class AtribuirCargoDto
{
    public Guid? CargoId { get; set; }
}

public record EquipeDto(
    Guid Id,
    string Nome,
    MembroResumoDto? Supervisor,
    IEnumerable<MembroResumoDto> Membros)
{
    public static EquipeDto De(Equipe e) => new(
        e.Id,
        e.Nome,
        e.Supervisor is null ? null : MembroResumoDto.De(e.Supervisor),
        e.Membros.Select(MembroResumoDto.De));
}

public record ArvoreOrganizacionalDto(
    IEnumerable<MembroResumoDto> Lideranca,
    IEnumerable<EquipeDto> Equipes,
    IEnumerable<MembroResumoDto> SemEquipe);
