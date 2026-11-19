using System.ComponentModel.DataAnnotations;

namespace ChatApi.Dtos;

public class AtualizarPerfilDto
{
    [Required(ErrorMessage = "Informe o nome completo.")]
    [MaxLength(120)]
    public string NomeCompleto { get; set; } = string.Empty;
}
