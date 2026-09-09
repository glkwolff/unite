using ChatApi.Dtos;
using ChatApi.Models;

namespace ChatApi.Services;

public static class MapeamentoOrganizacao
{
    /// <summary>Assume que Cargo ja foi incluido via Include/ThenInclude — nao
    /// faz consulta adicional aqui para nao gerar N+1 em listas grandes.</summary>
    public static MembroResumoDto ParaResumo(Usuario u) => new(
        u.Id,
        u.NomeCompleto,
        u.FotoUrl,
        u.Cargo?.Nome,
        u.Cargo?.Nivel);
}
