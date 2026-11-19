using System.ComponentModel.DataAnnotations;
using ChatApi.Models;
using ChatApi.Services;

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

    public string? Codigo { get; set; }
}

public record UsuarioDto(
    Guid Id,
    string Email,
    string NomeCompleto,
    string? FotoUrl,
    string? Cargo,
    string? Equipe,
    NivelHierarquico? Nivel,
    bool DoisFatoresAtivo,
    bool DoisFatoresObrigatorio)
{
    public static UsuarioDto De(Usuario u) => new(
        u.Id,
        u.Email ?? string.Empty,
        u.NomeCompleto,
        u.FotoUrl,
        u.Cargo?.Nome,
        u.Equipe?.Nome,
        u.Cargo?.Nivel,
        u.TwoFactorEnabled,
        Permissoes.ExigeDoisFatores(u.Cargo?.Nivel));
}

public record AuthRespostaDto(string Token, DateTime ExpiraEm, UsuarioDto Usuario);

public record SegundaEtapaDto(string Etapa, string? Chave, string? Uri);

public record ChaveAuthenticatorDto(string Chave, string Uri);

public class CodigoDto
{
    [Required(ErrorMessage = "Informe o codigo do aplicativo.")]
    public string Codigo { get; set; } = string.Empty;
}
