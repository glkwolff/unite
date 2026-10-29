using System.ComponentModel.DataAnnotations;

namespace ChatApi.Dtos;

/// <summary>So o nome; cargo e equipe sao atribuidos por quem administra.</summary>
public class AtualizarPerfilDto
{
    [Required(ErrorMessage = "Informe o nome completo.")]
    [MaxLength(120)]
    public string NomeCompleto { get; set; } = string.Empty;
}
