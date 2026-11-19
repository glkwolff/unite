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

    public bool Institucional { get; set; }
}

public record PostagemDto(
    Guid Id,
    string Titulo,
    string Conteudo,
    bool Institucional,
    DateTime PublicadaEm,
    MembroResumoDto Autor,
    bool PodeRemover,
    DateTime? CienteEm,
    bool PodeMarcarCiente,
    bool PodeVerCiencias,
    int? TotalCiencias);

public record CienciaDto(MembroResumoDto Usuario, DateTime ConfirmadaEm);

public record PaginaFeedDto(IEnumerable<PostagemDto> Itens, bool TemMais);
