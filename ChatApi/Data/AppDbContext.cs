using ChatApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ChatApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<Usuario, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Cargo> Cargos => Set<Cargo>();
    public DbSet<Equipe> Equipes => Set<Equipe>();
    public DbSet<Sala> Salas => Set<Sala>();
    public DbSet<SalaUsuario> SalaUsuarios => Set<SalaUsuario>();
    public DbSet<Mensagem> Mensagens => Set<Mensagem>();
    public DbSet<Postagem> Postagens => Set<Postagem>();
    public DbSet<Ciencia> Ciencias => Set<Ciencia>();

    // Tamanho de campo fica no proprio modelo, via [MaxLength].
    // Aqui ficam so as regras que atributo nao expressa: chave composta,
    // indices e o que acontece ao apagar um registro relacionado.
    // (String nao anulavel ja vira NOT NULL: o projeto usa nullable enable.)
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Usuario>(e =>
        {
            // Apagar cargo ou equipe nao pode apagar o funcionario.
            e.HasOne(u => u.Cargo).WithMany(c => c.Usuarios)
                .HasForeignKey(u => u.CargoId).OnDelete(DeleteBehavior.SetNull);

            e.HasOne(u => u.Equipe).WithMany(t => t.Membros)
                .HasForeignKey(u => u.EquipeId).OnDelete(DeleteBehavior.SetNull);
        });

        // Segundo relacionamento entre Equipe e Usuario: sem isto o EF
        // confundiria o Supervisor com a colecao Membros.
        b.Entity<Equipe>()
            .HasOne(t => t.Supervisor).WithMany()
            .HasForeignKey(t => t.SupervisorId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<Sala>()
            .HasOne(s => s.Equipe).WithMany()
            .HasForeignKey(s => s.EquipeId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<SalaUsuario>(e =>
        {
            // Um participante por sala: a chave e o par.
            e.HasKey(su => new { su.SalaId, su.UsuarioId });

            e.HasOne(su => su.Sala).WithMany(s => s.Participantes)
                .HasForeignKey(su => su.SalaId).OnDelete(DeleteBehavior.Cascade);

            e.HasOne(su => su.Usuario).WithMany()
                .HasForeignKey(su => su.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Mensagem>(e =>
        {
            // Apagar a sala apaga as mensagens; apagar o autor, nao.
            e.HasOne(m => m.Sala).WithMany(s => s.Mensagens)
                .HasForeignKey(m => m.SalaId).OnDelete(DeleteBehavior.Cascade);

            e.HasOne(m => m.Autor).WithMany()
                .HasForeignKey(m => m.AutorId).OnDelete(DeleteBehavior.Restrict);

            // O historico e sempre lido por sala, em ordem cronologica.
            e.HasIndex(m => new { m.SalaId, m.EnviadaEm });
        });

        b.Entity<Postagem>(e =>
        {
            e.HasOne(p => p.Autor).WithMany()
                .HasForeignKey(p => p.AutorId).OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(p => p.PublicadaEm);
        });

        b.Entity<Ciencia>(e =>
        {
            e.HasOne(c => c.Postagem).WithMany(p => p.Ciencias)
                .HasForeignKey(c => c.PostagemId).OnDelete(DeleteBehavior.Cascade);

            e.HasOne(c => c.Usuario).WithMany()
                .HasForeignKey(c => c.UsuarioId).OnDelete(DeleteBehavior.Cascade);

            // Um "Ciente" por usuario por postagem.
            e.HasIndex(c => new { c.PostagemId, c.UsuarioId }).IsUnique();
        });
    }
}
