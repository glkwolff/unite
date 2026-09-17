using ChatApi.Data;
using ChatApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ChatApi.Autorizacao;

/// <summary>
/// Barra a acao quando o cargo do usuario logado e mais baixo que o exigido.
/// Foi escrito como filtro proprio, e nao como policy do ASP.NET, porque a
/// regra e uma comparacao de niveis que precisa ser lida em uma linha:
/// <c>[ExigeNivel(NivelHierarquico.Gerente)]</c> em cima da acao.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ExigeNivelAttribute(NivelHierarquico minimo) : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext contexto)
    {
        var db = contexto.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var nivel = await UsuarioLogado.NivelAsync(contexto.HttpContext.User, db);

        // Diretor = 0 e Funcionario = 3: quanto MENOR o numero, mais alto o
        // cargo. Por isso o teste e ">" e nao "<".
        if (nivel is null || nivel > minimo)
        {
            contexto.Result = new ObjectResult(new
            {
                erro = $"Acao restrita ao nivel {minimo} ou superior."
            })
            { StatusCode = StatusCodes.Status403Forbidden };
        }
    }
}
