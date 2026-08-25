using System.ComponentModel.DataAnnotations;
using ChatApi.Models;

namespace ChatApi.Dtos;

public class RegistrarDto
{
    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "E-mail invalido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    [MinLength(6, ErrorMessage = "A senha precisa ter ao menos 6 caracteres.")]
    public string Senha { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o nome completo.")]
    [MaxLength(120)]
    public string NomeCompleto { get; set; } = string.Empty;
}

public class LoginDto
{
    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "E-mail invalido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    public string Senha { get; set; } = string.Empty;
}

public record UsuarioDto(
    Guid Id,
    string Email,
    string NomeCompleto,
    string? FotoUrl,
    string? Cargo,
    string? Equipe,
    NivelHierarquico? Nivel);

public record AuthRespostaDto(string Token, DateTime ExpiraEm, UsuarioDto Usuario);
