using System.ComponentModel.DataAnnotations;

namespace ChatApi.Models;

public enum TipoSala
{
    Privada = 1,

    /// <summary>
    /// No Unite grupo e sempre o canal automatico de uma equipe: nao existe
    /// grupo com participantes escolhidos a mao.
    /// </summary>
    Grupo = 2
}

public class Sala
{
    public Guid Id { get; set; }

    [MaxLength(120)]
    public string Nome { get; set; } = string.Empty;

    public TipoSala Tipo { get; set; }

    /// <summary>Preenchido quando a sala representa o canal de uma equipe.</summary>
    public Guid? EquipeId { get; set; }
    public Equipe? Equipe { get; set; }

    public DateTime CriadaEm { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Participantes explicitos. Fica VAZIA no canal de equipe de proposito: la
    /// o participante e todo Usuario com EquipeId igual ao desta sala. Repetir a
    /// lista aqui criaria duas fontes da mesma verdade, que divergem no instante
    /// em que alguem entra ou sai da equipe — e o DELETE de equipe aplica o
    /// SetNull dentro do banco, onde sincronizacao do EF nenhuma alcancaria.
    /// </summary>
    public ICollection<SalaUsuario> Participantes { get; set; } = new List<SalaUsuario>();
    public ICollection<Mensagem> Mensagens { get; set; } = new List<Mensagem>();
}
