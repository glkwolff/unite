using System.ComponentModel.DataAnnotations;

namespace ChatApi.Dtos;

public class PostagemEntradaDto
{
    [Required(ErrorMessage = "Informe o titulo do aviso.")]
    [MaxLength(160, ErrorMessage = "O titulo tem no maximo 160 caracteres.")]
    public string Titulo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escreva o conteudo do aviso.")]
    [MaxLength(5000, ErrorMessage = "O conteudo tem no maximo 5000 caracteres.")]
    public string Conteudo { get; set; } = string.Empty;

    /// <summary>Aviso em nome da empresa: so gerente para cima.</summary>
    public bool Institucional { get; set; }
}

/// <summary>
/// Uma postagem como o feed desenha. Os campos "Pode..." ja vem calculados
/// pelo servidor: assim o front nao precisa replicar regras que dependem do
/// nivel do autor e do leitor.
/// </summary>
public record PostagemDto(
    Guid Id,
    string Titulo,
    string Conteudo,
    bool Institucional,
    DateTime PublicadaEm,
    MembroResumoDto Autor,
    bool PodeRemover,
    DateTime? CienteEm,      // quando o usuario logado marcou "Ciente"; nulo = ainda nao
    bool PodeMarcarCiente,   // falso so para o autor da postagem
    bool PodeVerCiencias,    // pode abrir a lista de quem marcou
    int? TotalCiencias);     // quantos ele enxerga nessa lista; nulo se nao pode ver

/// <summary>Uma linha da lista "quem marcou Ciente".</summary>
public record CienciaDto(MembroResumoDto Usuario, DateTime ConfirmadaEm);

/// <summary>
/// Uma pagina do feed. Para buscar a proxima, o front manda a data da ultima
/// postagem recebida em <c>?antes=</c>.
/// </summary>
public record PaginaFeedDto(IEnumerable<PostagemDto> Itens, bool TemMais);
