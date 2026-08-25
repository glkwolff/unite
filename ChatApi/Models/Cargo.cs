namespace ChatApi.Models;

/// <summary>Niveis da arvore de gerenciamento do Unite.</summary>
public enum NivelHierarquico
{
    Gerente = 1,
    Supervisor = 2,
    Funcionario = 3
}

public class Cargo
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public NivelHierarquico Nivel { get; set; }

    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
}
