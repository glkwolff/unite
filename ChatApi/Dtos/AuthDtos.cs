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
    /// <summary>Exige <c>Cargo</c> e <c>Equipe</c> carregados.</summary>
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

/// <summary>
/// Senha certa, mas falta o segundo fator. <c>Etapa</c> e "codigo" (ja usa o
/// Authenticator) ou "configurar" (o nivel exige e ainda nao foi ativado).
/// </summary>
public record DesafioDto(string Desafio, string Etapa);

public record ChaveAuthenticatorDto(string Chave, string Uri);

public class DesafioRequisicaoDto
{
    /// <summary>Vazio quando quem chama ja esta logado (ativacao pelo perfil).</summary>
    public string? Desafio { get; set; }
}

public class CodigoDto
{
    public string? Desafio { get; set; }

    [Required(ErrorMessage = "Informe o codigo do aplicativo.")]
    public string Codigo { get; set; } = string.Empty;
}
