using System.Text;
using System.Text.Json.Serialization;
using ChatApi.Data;
using ChatApi.Hubs;
using ChatApi.Models;
using ChatApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

string[] flagsDemo = ["--seed-demo", "--reset-demo"];
var semearDemo = args.Any(flagsDemo.Contains);
var recriarBase = args.Contains("--reset-demo");

var builder = WebApplication.CreateBuilder(args.Except(flagsDemo).ToArray());

const string PoliticaCors = "front";

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Padrao") ?? "Data Source=chat.db"));

builder.Services
    .AddIdentityCore<Usuario>(o =>
    {
        o.User.RequireUniqueEmail = true;
        o.Password.RequiredLength = 6;
        o.Password.RequireNonAlphanumeric = false;
        o.Password.RequireUppercase = false;
        o.Password.RequireDigit = false;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddTokenProvider<AuthenticatorTokenProvider<Usuario>>(TokenOptions.DefaultAuthenticatorProvider);

var chaveJwt = builder.Configuration["Jwt:Chave"];
if (string.IsNullOrWhiteSpace(chaveJwt))
{
    throw new InvalidOperationException(
        "Jwt:Chave nao configurada. Rode dentro de ChatApi/:\n" +
        "  dotnet user-secrets set \"Jwt:Chave\" \"<chave longa e aleatoria>\"");
}

var emissor = builder.Configuration["Jwt:Emissor"] ?? "unite";

builder.Services.AddSingleton<TokenService>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Permissoes>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;

        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = emissor,
            ValidateAudience = true,
            ValidAudience = emissor,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chaveJwt)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name"
        };

        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) &&
                    ctx.HttpContext.Request.Path.StartsWithSegments("/chat"))
                {
                    ctx.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(o => o.AddPolicy(PoliticaCors, p => p
    .WithOrigins(builder.Configuration["Front:Origem"] ?? "http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, ProvedorIdUsuario>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var escopo = app.Services.CreateScope())
{
    var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
    if (recriarBase) ApagarBase(db);
    db.Database.Migrate();
    db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
    await SeedCargos.GarantirAsync(db);

    if (semearDemo) await SeedDemo.ExecutarAsync(escopo.ServiceProvider);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads", "perfis"));

app.UseCors(PoliticaCors);
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ChatHub>("/chat");

app.Run();

static void ApagarBase(AppDbContext db)
{
    var arquivo = new SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource;
    db.Database.EnsureDeleted();
    SqliteConnection.ClearAllPools();
    foreach (var sufixo in new[] { "-wal", "-shm" })
        File.Delete(arquivo + sufixo);
}

public partial class Program { }
