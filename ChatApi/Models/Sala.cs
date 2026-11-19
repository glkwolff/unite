namespace ChatApi.Models;

public enum TipoSala
{
    Privada = 1,

    Grupo = 2
}

public class Sala
{
    public Guid Id { get; set; }

    public TipoSala Tipo { get; set; }

    public Guid? EquipeId { get; set; }
    public Equipe? Equipe { get; set; }

    public ICollection<SalaUsuario> Participantes { get; set; } = new List<SalaUsuario>();
    public ICollection<Mensagem> Mensagens { get; set; } = new List<Mensagem>();
}
