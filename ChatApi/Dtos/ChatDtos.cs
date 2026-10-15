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
/// Uma conversa na lista lateral, servindo os dois tipos de sala. O
/// <see cref="Titulo"/> e montado a cada resposta — nome do outro na privada,
/// nome da equipe no canal — porque guardado em Sala.Nome ficaria velho assim
/// que alguem editasse o perfil ou a equipe fosse renomeada.
///
/// <see cref="Outro"/> so existe na privada, onde sustenta a foto e o cargo no
/// cabecalho: canal de equipe nao tem "o outro". Nao ha contagem de
/// participantes porque ela custaria uma consulta por canal na listagem para
/// alimentar um subtitulo que o <see cref="Tipo"/> ja permite escrever.
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
