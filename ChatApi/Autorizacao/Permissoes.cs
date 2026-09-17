using ChatApi.Models;

namespace ChatApi.Autorizacao;

/// <summary>
/// As regras de quem pode o que, reunidas em um lugar so. Sao funcoes puras
/// (nivel + ids entram, bool sai) justamente para poderem ser lidas e
/// testadas sem subir a API.
/// </summary>
public static class Permissoes
{
    /// <summary>Cargos, equipes e lotacao de pessoas: da gerencia para cima.</summary>
    public static bool PodeAdministrar(NivelHierarquico? nivel) =>
        nivel is not null && nivel <= NivelHierarquico.Gerente;

    /// <summary>
    /// Entrar e sair de uma equipe: a gerencia faz em qualquer equipe; o
    /// supervisor, apenas na equipe que ele responde.
    /// </summary>
    public static bool PodeGerenciarMembros(NivelHierarquico? nivel, Guid usuarioId, Guid? supervisorDaEquipe) =>
        PodeAdministrar(nivel) ||
        (nivel == NivelHierarquico.Supervisor && supervisorDaEquipe == usuarioId);

    /// <summary>
    /// Promover alguem. Duas travas explicitas: ninguem promove alguem acima
    /// do proprio nivel (um gerente nao cria um diretor) e ninguem mexe no
    /// proprio cargo (se promover sozinho anularia as duas regras acima).
    /// </summary>
    public static bool PodeAtribuirCargo(
        NivelHierarquico? nivel,
        Guid usuarioId,
        Guid alvoId,
        NivelHierarquico nivelDoCargo) =>
        PodeAdministrar(nivel) && alvoId != usuarioId && nivelDoCargo >= nivel;
}
