namespace ChatApi.Models;

public class Postagem
{
    public Guid Id { get; set; }

    public Guid AutorId { get; set; }
    public Usuario Autor { get; set; } = null!;

    public string Titulo { get; set; } = string.Empty;
    public string Conteudo { get; set; } = string.Empty;

    /// <summary>Aviso institucional: exige permissao para publicar.</summary>
    public bool Institucional { get; set; }

    public DateTime PublicadaEm { get; set; } = DateTime.UtcNow;

    public ICollection<Ciencia> Ciencias { get; set; } = new List<Ciencia>();
}
