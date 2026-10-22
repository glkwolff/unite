using ChatApi.Data;
using ChatApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatApi.Services;

/// <summary>
/// Todas as regras de quem pode o que. Os controllers so perguntam aqui.
/// O nivel e lido do banco a cada requisicao e o enum ja esta em ordem de poder
/// (Diretor = 0 ... Funcionario = 3): "pelo menos Gerente" e <c>nivel &lt;= Gerente</c>.
///
/// <code>
///              cargos  equipes  membros      atribuir cargo  publicar  institucional  remover post
/// Diretor      sim     sim      qualquer     qualquer        sim       sim            qualquer
/// Gerente      -       sim      qualquer     ate Supervisor  sim       sim            qualquer
/// Supervisor   -       -        so a sua     -               sim       -              so as suas
/// Funcionario  -       -        -            -               -         -              so as suas
/// </code>
///
/// Ler (feed, equipes, pessoas) e livre para qualquer logado. Sem cargo conta como Funcionario.
/// </summary>
public class Permissoes(AppDbContext db, IHttpContextAccessor http)
{
    private Usuario? _usuario;
    private bool _buscou;

    /// <summary>Id do usuario logado (claim "sub" do JWT).</summary>
    public Guid? IdLogado =>
        Guid.TryParse(http.HttpContext?.User.FindFirst("sub")?.Value, out var id) ? id : null;

    /// <summary>Usuario logado com cargo e equipe; busca uma vez por requisicao.</summary>
    public async Task<Usuario?> UsuarioAtualAsync()
    {
        if (_buscou) return _usuario;
        _buscou = true;

        if (IdLogado is not Guid id) return null;

        _usuario = await db.Users
            .Include(u => u.Cargo)
            .Include(u => u.Equipe)
            .FirstOrDefaultAsync(u => u.Id == id);

        return _usuario;
    }

    public async Task<NivelHierarquico?> NivelAsync() => (await UsuarioAtualAsync())?.Cargo?.Nivel;

    // ---- organizacao

    public Task<bool> PodeGerenciarCargosAsync() => TemNivelAsync(NivelHierarquico.Diretor);

    public Task<bool> PodeGerenciarEquipesAsync() => TemNivelAsync(NivelHierarquico.Gerente);

    /// <summary>Gerente para cima: qualquer equipe. Supervisor: so a que ele lidera.</summary>
    public async Task<bool> PodeGerenciarMembrosAsync(Guid equipeId)
    {
        if (await PodeGerenciarEquipesAsync()) return true;
        if (IdLogado is not Guid id) return false;

        return await db.Equipes.AnyAsync(e => e.Id == equipeId && e.SupervisorId == id);
    }

    public Task<bool> PodeAtribuirCargoAsync() => TemNivelAsync(NivelHierarquico.Gerente);

    /// <summary>
    /// Diretor faz qualquer troca. Gerente so da cargos de Supervisor para baixo e nao mexe
    /// em quem ja e gerente ou diretor (senao se promoveria por tabela).
    /// </summary>
    public async Task<bool> PodeAtribuirAsync(Cargo? novoCargo, Usuario alvo)
    {
        if (await PodeGerenciarCargosAsync()) return true;
        if (!await PodeAtribuirCargoAsync()) return false;

        var nivelAlvo = alvo.Cargo?.Nivel;
        if (nivelAlvo is not null && nivelAlvo <= NivelHierarquico.Gerente) return false;

        return novoCargo is null || novoCargo.Nivel >= NivelHierarquico.Supervisor;
    }

    // ---- feed

    public Task<bool> PodePublicarAsync() => TemNivelAsync(NivelHierarquico.Supervisor);

    public Task<bool> PodePublicarInstitucionalAsync() => TemNivelAsync(NivelHierarquico.Gerente);

    /// <summary>O autor remove a sua; gerente para cima remove qualquer uma.</summary>
    public async Task<bool> PodeRemoverPostagemAsync(Postagem postagem) =>
        postagem.AutorId == IdLogado || await PodePublicarInstitucionalAsync();

    // ---- ciente
    //
    // Marcar: qualquer um, menos o autor. Ver quem marcou:
    //              em quais postagens                    quais leitores
    // Diretor      todas                                 todos
    // Gerente      de supervisores e funcionarios + suas todos
    // Supervisor   so as suas                            so funcionarios
    // Funcionario  nenhuma                               -

    public bool PodeMarcarCiente(Postagem postagem) => postagem.AutorId != IdLogado;

    /// <summary>Exige postagem.Autor.Cargo carregado.</summary>
    public async Task<bool> PodeVerCienciasAsync(Postagem postagem)
    {
        var propria = postagem.AutorId == IdLogado;

        return await NivelAsync() switch
        {
            NivelHierarquico.Diretor => true,
            NivelHierarquico.Gerente => propria || NivelEfetivo(postagem.Autor.Cargo?.Nivel) >= NivelHierarquico.Supervisor,
            NivelHierarquico.Supervisor => propria,
            _ => false
        };
    }

    /// <summary>Dentro da lista que ele ja pode ver: o supervisor so enxerga funcionarios.</summary>
    public async Task<bool> PodeVerLeitorAsync(NivelHierarquico? nivelDoLeitor) =>
        await NivelAsync() != NivelHierarquico.Supervisor
        || NivelEfetivo(nivelDoLeitor) == NivelHierarquico.Funcionario;

    // ---- 2fa

    /// <summary>Gerente e Diretor sao obrigados; os demais ativam se quiserem.</summary>
    public static bool ExigeDoisFatores(NivelHierarquico? nivel) =>
        nivel is not null && nivel <= NivelHierarquico.Gerente;

    private static NivelHierarquico NivelEfetivo(NivelHierarquico? nivel) => nivel ?? NivelHierarquico.Funcionario;

    private async Task<bool> TemNivelAsync(NivelHierarquico minimo) =>
        await NivelAsync() is NivelHierarquico nivel && nivel <= minimo;
}
