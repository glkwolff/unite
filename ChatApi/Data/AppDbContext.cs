using ChatApi.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ChatApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityUserContext<Usuario, Guid>(options)
{
    public DbSet<Cargo> Cargos => Set<Cargo>();
    public DbSet<Equipe> Equipes => Set<Equipe>();
    public DbSet<Sala> Salas => Set<Sala>();
    public DbSet<SalaUsuario> SalaUsuarios => Set<SalaUsuario>();
    public DbSet<Mensagem> Mensagens => Set<Mensagem>();
    public DbSet<Postagem> Postagens => Set<Postagem>();
    public DbSet<Ciencia> Ciencias => Set<Ciencia>();

    // SQLite nao guarda fuso: tudo e gravado com UtcNow, entao na leitura vira UTC (JSON com "Z").
    protected override void ConfigureConventions(ModelConfigurationBuilder c) =>
        c.Properties<DateTime>().HaveConversion<ConversorUtc>();

    private class ConversorUtc() : ValueConverter<DateTime, DateTime>(
        v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    // Tamanho de campo fica no modelo ([MaxLength]); aqui so chaves, indices e o que acontece no delete.
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

        // Segundo vinculo Equipe-Usuario: sem isto o EF confunde Supervisor com Membros.
        b.Entity<Equipe>()
            .HasOne(t => t.Supervisor).WithMany()
            .HasForeignKey(t => t.SupervisorId).OnDelete(DeleteBehavior.Restrict);

        // Apagar a equipe apaga o canal (e, pelo cascade da Sala, as mensagens).
        b.Entity<Sala>()
            .HasOne(s => s.Equipe).WithMany()
            .HasForeignKey(s => s.EquipeId).OnDelete(DeleteBehavior.Cascade);

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
            // Um "Ciente" por usuario por postagem: o par e a chave.
            e.HasKey(c => new { c.PostagemId, c.UsuarioId });

            e.HasOne(c => c.Postagem).WithMany(p => p.Ciencias)
                .HasForeignKey(c => c.PostagemId).OnDelete(DeleteBehavior.Cascade);

            e.HasOne(c => c.Usuario).WithMany()
                .HasForeignKey(c => c.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
