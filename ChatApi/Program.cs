using System.Text;
using System.Text.Json.Serialization;
using ChatApi.Data;
using ChatApi.Models;
using ChatApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

const string PoliticaCors = "front";

// ---------------------------------------------------------------- banco
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Padrao") ?? "Data Source=chat.db"));

// ------------------------------------------------------------- identity
builder.Services
    .AddIdentityCore<Usuario>(o =>
    {
        o.User.RequireUniqueEmail = true;
        o.Password.RequiredLength = 6;
        o.Password.RequireNonAlphanumeric = false;
        o.Password.RequireUppercase = false;
        o.Password.RequireDigit = false;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>();

// ------------------------------------------------------------------ jwt
var chaveJwt = builder.Configuration["Jwt:Chave"];
if (string.IsNullOrWhiteSpace(chaveJwt))
{
    throw new InvalidOperationException(
        "Jwt:Chave nao configurada. Rode dentro de ChatApi/:\n" +
        "  dotnet user-secrets set \"Jwt:Chave\" \"<chave longa e aleatoria>\"");
}

var emissor = builder.Configuration["Jwt:Emissor"] ?? "unite";

builder.Services.AddSingleton<TokenService>();

// Quem pode o que (RF11). Scoped: guarda o usuario logado durante a requisicao.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Permissoes>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        // Sem o mapeamento legado os claims chegam com o nome que foram emitidos.
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
            NameClaimType = "name",
            RoleClaimType = "role"
        };

        // O WebSocket do SignalR nao envia header Authorization: o token vem
        // na query string. O hub entra em 01/10; a configuracao ja fica pronta.
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

// ----------------------------------------------------------------- cors
// AllowCredentials e incompativel com AllowAnyOrigin: a origem do front
// precisa ser explicita.
builder.Services.AddCors(o => o.AddPolicy(PoliticaCors, p => p
    .WithOrigins(builder.Configuration["Front:Origem"] ?? "http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services
    .AddControllers()
    // Sem isto o NivelHierarquico chega no front como 1/2/3 em vez de "Gerente".
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ------------------------------------------------- migrations e modo WAL
using (var escopo = app.Services.CreateScope())
{
    var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    // WAL: leitor nao bloqueia escritor. E persistente no arquivo do banco.
    db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
    // Base nova precisa ter os quatro cargos padrao para o primeiro usuario
    // poder virar diretor no registro.
    await SeedCargos.GarantirAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Fotos de perfil ficam em wwwroot/uploads e sao servidas como arquivo
// estatico direto — sem controller/stream proprio para isso.
Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads", "perfis"));

app.UseCors(PoliticaCors);
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Necessario para o WebApplicationFactory dos testes de integracao (05/11).
public partial class Program { }
