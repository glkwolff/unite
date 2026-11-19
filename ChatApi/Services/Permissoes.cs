using ChatApi.Data;
using ChatApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatApi.Services;

public class Permissoes(AppDbContext db, IHttpContextAccessor http)
{
    private Usuario? _usuario;
    private bool _buscou;

    public Guid? IdLogado =>
        Guid.TryParse(http.HttpContext?.User.FindFirst("sub")?.Value, out var id) ? id : null;

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

    public Task<bool> PodeGerenciarCargosAsync() => TemNivelAsync(NivelHierarquico.Diretor);

    public Task<bool> PodeGerenciarEquipesAsync() => TemNivelAsync(NivelHierarquico.Gerente);

    public async Task<bool> PodeGerenciarMembrosAsync(Guid equipeId)
    {
        if (await PodeGerenciarEquipesAsync()) return true;
        if (IdLogado is not Guid id) return false;

        return await db.Equipes.AnyAsync(e => e.Id == equipeId && e.SupervisorId == id);
    }

    public Task<bool> PodeAtribuirCargoAsync() => TemNivelAsync(NivelHierarquico.Gerente);

    public async Task<bool> PodeAtribuirAsync(Cargo? novoCargo, Usuario alvo)
    {
        if (await PodeGerenciarCargosAsync()) return true;
        if (!await PodeAtribuirCargoAsync()) return false;

        var nivelAlvo = alvo.Cargo?.Nivel;
        if (nivelAlvo is not null && nivelAlvo <= NivelHierarquico.Gerente) return false;

        return novoCargo is null || novoCargo.Nivel >= NivelHierarquico.Supervisor;
    }

    public Task<bool> PodePublicarAsync() => TemNivelAsync(NivelHierarquico.Supervisor);

    public Task<bool> PodePublicarInstitucionalAsync() => TemNivelAsync(NivelHierarquico.Gerente);

    public async Task<bool> PodeRemoverPostagemAsync(Postagem postagem) =>
        postagem.AutorId == IdLogado || await PodePublicarInstitucionalAsync();

    public bool PodeMarcarCiente(Postagem postagem) => postagem.AutorId != IdLogado;

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

    public async Task<bool> PodeVerLeitorAsync(NivelHierarquico? nivelDoLeitor) =>
        await NivelAsync() != NivelHierarquico.Supervisor
        || NivelEfetivo(nivelDoLeitor) == NivelHierarquico.Funcionario;

    public static bool ExigeDoisFatores(NivelHierarquico? nivel) =>
        nivel is not null && nivel <= NivelHierarquico.Gerente;

    private static NivelHierarquico NivelEfetivo(NivelHierarquico? nivel) => nivel ?? NivelHierarquico.Funcionario;

    private async Task<bool> TemNivelAsync(NivelHierarquico minimo) =>
        await NivelAsync() is NivelHierarquico nivel && nivel <= minimo;
}
