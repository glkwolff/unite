using System.ComponentModel.DataAnnotations;

namespace ChatApi.Dtos;

/// <summary>O usuario so edita o proprio nome por aqui; cargo e equipe sao
/// atribuidos por quem administra a estrutura organizacional.</summary>
public class AtualizarPerfilDto
{
    [Required(ErrorMessage = "Informe o nome completo.")]
    [MaxLength(120)]
    public string NomeCompleto { get; set; } = string.Empty;
}
