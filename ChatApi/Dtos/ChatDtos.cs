using System.ComponentModel.DataAnnotations;

namespace ChatApi.Dtos;

// ------------------------------------------------------------------- salas

public class AbrirPrivadaDto
{
    [Required(ErrorMessage = "Informe com quem voce quer conversar.")]
    public Guid UsuarioId { get; set; }
}

/// <summary>
/// Uma conversa na lista lateral. Na sala privada o titulo e sempre o nome do
/// <see cref="Outro"/> participante, montado na hora: nome guardado no campo
/// Sala.Nome ficaria velho assim que a pessoa editasse o perfil.
/// </summary>
public record SalaResumoDto(
    Guid Id,
    MembroResumoDto Outro,
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
