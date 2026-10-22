using System.ComponentModel.DataAnnotations;
using ChatApi.Models;

namespace ChatApi.Dtos;

// ---- salas

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
    DateTime? UltimaEm)
{
    /// <summary>Exige Participantes.Usuario.Cargo carregados. Nulo se o outro lado nao existe mais.</summary>
    public static SalaResumoDto? Privada(Sala sala, Guid meuId, Mensagem? ultima)
    {
        var outro = sala.Participantes.FirstOrDefault(p => p.UsuarioId != meuId)?.Usuario;
        if (outro is null) return null;

        return new(sala.Id, TipoSala.Privada, outro.NomeCompleto, MembroResumoDto.De(outro),
            ultima?.Texto, ultima?.EnviadaEm);
    }

    /// <summary>Exige Equipe carregada.</summary>
    public static SalaResumoDto Canal(Sala sala, Mensagem? ultima) =>
        new(sala.Id, TipoSala.Grupo, sala.Equipe!.Nome, null, ultima?.Texto, ultima?.EnviadaEm);
}

// ---- mensagens

public class EnviarMensagemDto
{
    [Required(ErrorMessage = "A mensagem nao pode ficar vazia.")]
    [MaxLength(4000)]
    public string Texto { get; set; } = string.Empty;
}

/// <summary>Exige Autor carregado.</summary>
public record MensagemDto(
    Guid Id,
    Guid SalaId,
    Guid AutorId,
    string AutorNome,
    string Texto,
    DateTime EnviadaEm)
{
    public static MensagemDto De(Mensagem m) => new(m.Id, m.SalaId, m.AutorId, m.Autor.NomeCompleto, m.Texto, m.EnviadaEm);
}

/// <summary>Uma pagina do historico, em ordem de leitura. Para a anterior: ?antesDe= a data da primeira.</summary>
public record PaginaMensagensDto(IEnumerable<MensagemDto> Itens, bool TemMais);
