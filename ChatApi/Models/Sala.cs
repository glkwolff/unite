namespace ChatApi.Models;

public enum TipoSala
{
    Privada = 1,

    /// <summary>Grupo e sempre o canal automatico de uma equipe.</summary>
    Grupo = 2
}

public class Sala
{
    public Guid Id { get; set; }

    public TipoSala Tipo { get; set; }

    /// <summary>So no canal de equipe.</summary>
    public Guid? EquipeId { get; set; }
    public Equipe? Equipe { get; set; }

    /// <summary>So na conversa privada. No canal, participante e quem tem Usuario.EquipeId == EquipeId.</summary>
    public ICollection<SalaUsuario> Participantes { get; set; } = new List<SalaUsuario>();
    public ICollection<Mensagem> Mensagens { get; set; } = new List<Mensagem>();
}
