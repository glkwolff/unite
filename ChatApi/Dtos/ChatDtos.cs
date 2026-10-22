using System.ComponentModel.DataAnnotations;
using ChatApi.Models;

namespace ChatApi.Dtos;

// ------------------------------------------------------------------- salas

public class AbrirPrivadaDto
{
    [Required(ErrorMessage = "Informe com quem voce quer conversar.")]
    public Guid UsuarioId { get; set; }
}

/// <summary>
/// Uma conversa na lista lateral. Titulo = nome do outro (privada) ou da equipe (canal),
/// montado a cada resposta. Outro so existe na privada.
/// </summary>
public record SalaResumoDto(
    Guid Id,
    TipoSala Tipo,
    string Titulo,
    MembroResumoDto? Outro,
    string? UltimaMensagem,
    DateTime? UltimaEm);

// --------------------------------------------------------------- mensagens

public class EnviarMensagemDto
{
    [Required(ErrorMessage = "A mensagem nao pode ficar vazia.")]
    [MaxLength(4000)]
    public string Texto { get; set; } = string.Empty;
}

public record MensagemDto(
    Guid Id,
    Guid SalaId,
    Guid AutorId,
    string AutorNome,
    string Texto,
    DateTime EnviadaEm);
