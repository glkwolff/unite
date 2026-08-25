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

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Cargo>(e =>
        {
            e.Property(c => c.Nome).HasMaxLength(80).IsRequired();
        });

        b.Entity<Usuario>(e =>
        {
            e.Property(u => u.NomeCompleto).HasMaxLength(120).IsRequired();
            e.Property(u => u.FotoUrl).HasMaxLength(300);

            e.HasOne(u => u.Cargo)
                .WithMany(c => c.Usuarios)
                .HasForeignKey(u => u.CargoId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(u => u.Equipe)
                .WithMany(t => t.Membros)
                .HasForeignKey(u => u.EquipeId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Equipe>(e =>
        {
            e.Property(t => t.Nome).HasMaxLength(80).IsRequired();

            // Segundo relacionamento entre Equipe e Usuario: precisa ser explicito
            // para o EF nao confundir com Equipe.Membros.
            e.HasOne(t => t.Supervisor)
                .WithMany()
                .HasForeignKey(t => t.SupervisorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Sala>(e =>
        {
            e.Property(s => s.Nome).HasMaxLength(120).IsRequired();

            e.HasOne(s => s.Equipe)
                .WithMany()
                .HasForeignKey(s => s.EquipeId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<SalaUsuario>(e =>
        {
            e.HasKey(su => new { su.SalaId, su.UsuarioId });

            e.HasOne(su => su.Sala)
                .WithMany(s => s.Participantes)
                .HasForeignKey(su => su.SalaId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(su => su.Usuario)
                .WithMany()
                .HasForeignKey(su => su.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Mensagem>(e =>
        {
            e.Property(m => m.Texto).HasMaxLength(4000).IsRequired();

            e.HasOne(m => m.Sala)
                .WithMany(s => s.Mensagens)
                .HasForeignKey(m => m.SalaId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(m => m.Autor)
                .WithMany()
                .HasForeignKey(m => m.AutorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Historico da sala e sempre lido em ordem cronologica.
            e.HasIndex(m => new { m.SalaId, m.EnviadaEm });
        });

        b.Entity<Postagem>(e =>
        {
            e.Property(p => p.Titulo).HasMaxLength(160).IsRequired();
            e.Property(p => p.Conteudo).IsRequired();

            e.HasOne(p => p.Autor)
                .WithMany()
                .HasForeignKey(p => p.AutorId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(p => p.PublicadaEm);
        });

        b.Entity<Ciencia>(e =>
        {
            e.HasOne(c => c.Postagem)
                .WithMany(p => p.Ciencias)
                .HasForeignKey(c => c.PostagemId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(c => c.Usuario)
                .WithMany()
                .HasForeignKey(c => c.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);

            // Um "Ciente" por usuario por postagem.
            e.HasIndex(c => new { c.PostagemId, c.UsuarioId }).IsUnique();
        });
    }
}
