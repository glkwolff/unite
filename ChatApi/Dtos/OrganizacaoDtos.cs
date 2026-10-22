using System.ComponentModel.DataAnnotations;
using ChatApi.Models;

namespace ChatApi.Dtos;

// ---- cargos

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

// ---- equipes

public class EquipeEntradaDto
{
    [Required(ErrorMessage = "Informe o nome da equipe.")]
    [MaxLength(80)]
    public string Nome { get; set; } = string.Empty;

    /// <summary>Opcional.</summary>
    public Guid? SupervisorId { get; set; }
}

public class AdicionarMembroDto
{
    [Required(ErrorMessage = "Informe o usuario.")]
    public Guid UsuarioId { get; set; }
}

/// <summary>Resumo de uma pessoa em listas e na arvore (sem e-mail). Exige Cargo carregado.</summary>
public record MembroResumoDto(Guid Id, string NomeCompleto, string? FotoUrl, string? Cargo, NivelHierarquico? Nivel)
{
    public static MembroResumoDto De(Usuario u) => new(u.Id, u.NomeCompleto, u.FotoUrl, u.Cargo?.Nome, u.Cargo?.Nivel);
}

/// <summary>Pessoa na tela de administracao: o resumo mais os ids de cargo e equipe. Exige Cargo e Equipe carregados.</summary>
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
    /// <summary>Nulo tira o cargo da pessoa.</summary>
    public Guid? CargoId { get; set; }
}

/// <summary>Exige Supervisor.Cargo e Membros.Cargo carregados.</summary>
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

// ---- arvore (organograma)

/// <summary>Organograma: diretoria e gerencia no topo, as equipes, e quem esta sem equipe.</summary>
public record ArvoreOrganizacionalDto(
    IEnumerable<MembroResumoDto> Lideranca,
    IEnumerable<EquipeDto> Equipes,
    IEnumerable<MembroResumoDto> SemEquipe);
