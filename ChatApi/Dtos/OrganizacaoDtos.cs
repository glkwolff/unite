using System.ComponentModel.DataAnnotations;
using ChatApi.Models;

namespace ChatApi.Dtos;

// ------------------------------------------------------------------ cargos

public class CargoEntradaDto
{
    [Required(ErrorMessage = "Informe o nome do cargo.")]
    [MaxLength(80)]
    public string Nome { get; set; } = string.Empty;

    public NivelHierarquico Nivel { get; set; }
}

public record CargoDto(Guid Id, string Nome, NivelHierarquico Nivel, int TotalUsuarios);

// ----------------------------------------------------------------- equipes

public class EquipeEntradaDto
{
    [Required(ErrorMessage = "Informe o nome da equipe.")]
    [MaxLength(80)]
    public string Nome { get; set; } = string.Empty;

    /// <summary>Opcional: equipe pode ficar sem supervisor definido ainda.</summary>
    public Guid? SupervisorId { get; set; }
}

public class AdicionarMembroDto
{
    [Required(ErrorMessage = "Informe o usuario.")]
    public Guid UsuarioId { get; set; }
}

/// <summary>Recorte minimo de usuario, usado dentro de listas e da arvore —
/// nunca expoe e-mail/senha alem do estritamente necessario para a UI.</summary>
public record MembroResumoDto(Guid Id, string NomeCompleto, string? FotoUrl, string? Cargo, NivelHierarquico? Nivel);

/// <summary>Recorte da tela de administracao de pessoas: alem do resumo, traz
/// os ids de cargo e equipe, que a tela precisa para montar o select e para
/// saber o que ja esta selecionado. Continua sem expor e-mail.</summary>
public record PessoaDto(
    Guid Id,
    string NomeCompleto,
    string? FotoUrl,
    Guid? CargoId,
    string? Cargo,
    NivelHierarquico? Nivel,
    Guid? EquipeId,
    string? Equipe);

public class AtribuirCargoDto
{
    /// <summary>Nulo tira o cargo da pessoa.</summary>
    public Guid? CargoId { get; set; }
}

public record EquipeDto(
    Guid Id,
    string Nome,
    MembroResumoDto? Supervisor,
    IEnumerable<MembroResumoDto> Membros);

// ---------------------------------------------------------- arvore (organograma)

/// <summary>
/// Estrutura pronta para desenhar o organograma no front: diretoria e gerencia
/// no topo (sem vinculo formal de equipe — enxergam tudo), depois cada equipe
/// com seu supervisor e membros, e por fim quem ainda nao foi alocado.
/// </summary>
public record ArvoreOrganizacionalDto(
    IEnumerable<MembroResumoDto> Lideranca,
    IEnumerable<EquipeDto> Equipes,
    IEnumerable<MembroResumoDto> SemEquipe);
