using ChatApi.Data;
using ChatApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatApi.Services;

/// <summary>
/// Quem pode o que no Unite (RF11). Esta classe e a unica fonte da regra —
/// os controllers so perguntam, nunca decidem por conta propria.
///
/// <code>
///                  cargos   equipes    membros           atribuir cargo
/// Diretor           CRUD     CRUD      qualquer equipe   qualquer nivel
/// Gerente           ler      CRUD      qualquer equipe   ate Supervisor
/// Supervisor        ler      ler       so a sua equipe   nao
/// Funcionario       ler      ler       nao               nao
/// </code>
///
/// Ler continua liberado para qualquer autenticado: o organograma e publico
/// dentro da empresa. O que a hierarquia restringe e escrever.
///
/// O enum <see cref="NivelHierarquico"/> ja esta ordenado por poder
/// (Diretor = 0 ... Funcionario = 3), entao "no minimo Gerente" se escreve
/// literalmente como <c>nivel &lt;= Gerente</c> — sem tabela de pesos.
/// </summary>
public class Permissoes(AppDbContext db, IHttpContextAccessor http)
{
    private Usuario? _usuario;
    private bool _buscou;

    /// <summary>Id do usuario logado, lido do claim "sub" do JWT.</summary>
    public Guid? IdLogado =>
        Guid.TryParse(http.HttpContext?.User.FindFirst("sub")?.Value, out var id) ? id : null;

    /// <summary>
    /// O usuario logado, com cargo e equipe. O resultado e guardado porque
    /// uma mesma requisicao costuma perguntar isso mais de uma vez.
    /// </summary>
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

    /// <summary>
    /// O nivel vem do banco, nao do claim "nivel" do token. Se um diretor
    /// rebaixa alguem, a perda de poder vale na proxima requisicao — e nao
    /// daqui a 8 horas, quando o token da vitima expirar.
    /// </summary>
    public async Task<NivelHierarquico?> NivelAsync() => (await UsuarioAtualAsync())?.Cargo?.Nivel;

    /// <summary>Cargos sao o vocabulario da hierarquia: so o diretor mexe.</summary>
    public Task<bool> PodeGerenciarCargosAsync() => TemNivelAsync(NivelHierarquico.Diretor);

    /// <summary>Criar, renomear e excluir equipes: diretor e gerente.</summary>
    public Task<bool> PodeGerenciarEquipesAsync() => TemNivelAsync(NivelHierarquico.Gerente);

    /// <summary>
    /// Mover pessoas para dentro/fora de uma equipe. Gerente para cima mexe em
    /// qualquer equipe; o supervisor mexe apenas na equipe que ele lidera.
    /// </summary>
    public async Task<bool> PodeGerenciarMembrosAsync(Guid equipeId)
    {
        if (await PodeGerenciarEquipesAsync()) return true;
        if (IdLogado is not Guid id) return false;

        return await db.Equipes.AnyAsync(e => e.Id == equipeId && e.SupervisorId == id);
    }

    /// <summary>Existe alguma atribuicao de cargo que este usuario possa fazer?
    /// Usado so para decidir se a tela /pessoas e oferecida.</summary>
    public Task<bool> PodeAtribuirCargoAsync() => TemNivelAsync(NivelHierarquico.Gerente);

    /// <summary>
    /// Dar a <paramref name="alvo"/> o cargo <paramref name="novoCargo"/>
    /// (nulo = tirar o cargo). O diretor faz qualquer troca. O gerente so
    /// distribui cargos de Supervisor para baixo, e nao encosta em quem ja e
    /// diretor ou gerente — senao um gerente promoveria a si mesmo por tabela.
    /// </summary>
    public async Task<bool> PodeAtribuirAsync(Cargo? novoCargo, Usuario alvo)
    {
        if (await PodeGerenciarCargosAsync()) return true;
        if (!await PodeAtribuirCargoAsync()) return false;

        var nivelAlvo = alvo.Cargo?.Nivel;
        if (nivelAlvo is not null && nivelAlvo <= NivelHierarquico.Gerente) return false;

        return novoCargo is null || novoCargo.Nivel >= NivelHierarquico.Supervisor;
    }

    // ------------------------------------------------------------------ feed
    //
    //                  publicar   aviso institucional   remover postagem
    // Diretor           sim        sim                   qualquer uma
    // Gerente           sim        sim                   qualquer uma
    // Supervisor        sim        nao                   so as proprias
    // Funcionario       nao        nao                   -
    //
    // Ler o feed continua liberado para qualquer autenticado.

    /// <summary>Publicar no feed: supervisor para cima. O funcionario le e
    /// (a partir de 22/10) confirma ciencia, mas nao publica.</summary>
    public Task<bool> PodePublicarAsync() => TemNivelAsync(NivelHierarquico.Supervisor);

    /// <summary>Aviso institucional fala em nome da empresa inteira, entao fica
    /// com quem esta acima das equipes: gerente e diretor.</summary>
    public Task<bool> PodePublicarInstitucionalAsync() => TemNivelAsync(NivelHierarquico.Gerente);

    /// <summary>
    /// O autor remove a propria postagem; gerente para cima remove qualquer
    /// uma (moderacao). Quem foi rebaixado a funcionario ainda consegue tirar
    /// o que ele mesmo publicou — apagar o proprio texto nao da poder novo.
    /// </summary>
    public async Task<bool> PodeRemoverPostagemAsync(Postagem postagem) =>
        postagem.AutorId == IdLogado || await PodePublicarInstitucionalAsync();

    // ---------------------------------------------------------------- ciente
    //
    // Marcar "Ciente": qualquer autenticado, em qualquer postagem que nao seja
    // a propria (o autor ja sabe o que escreveu).
    //
    // Ver QUEM marcou:
    //                  em quais postagens                     quais leitores
    // Diretor           todas                                  todos
    // Gerente           de supervisores e funcionarios         todos
    //                   + as proprias
    // Supervisor        so as proprias                         so funcionarios
    // Funcionario       nenhuma                                -
    //
    // Pessoa sem cargo conta como funcionario, nos dois papeis (autor e leitor).

    public bool PodeMarcarCiente(Postagem postagem) => postagem.AutorId != IdLogado;

    /// <summary>Exige <c>postagem.Autor.Cargo</c> carregado: a regra do gerente
    /// depende do nivel atual de quem publicou.</summary>
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

    /// <summary>Dentro de uma lista que o usuario ja pode ver, este leitor
    /// aparece? So o supervisor tem a lista recortada (ve apenas funcionarios).</summary>
    public async Task<bool> PodeVerLeitorAsync(NivelHierarquico? nivelDoLeitor) =>
        await NivelAsync() != NivelHierarquico.Supervisor
        || NivelEfetivo(nivelDoLeitor) == NivelHierarquico.Funcionario;

    private static NivelHierarquico NivelEfetivo(NivelHierarquico? nivel) => nivel ?? NivelHierarquico.Funcionario;

    /// <summary>Nivel numericamente menor ou igual = poder maior ou igual.</summary>
    private async Task<bool> TemNivelAsync(NivelHierarquico minimo) =>
        await NivelAsync() is NivelHierarquico nivel && nivel <= minimo;
}
