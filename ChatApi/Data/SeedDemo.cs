using ChatApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ChatApi.Data;

/// <summary>
/// Empresa ficticia para a apresentacao: organograma, equipes, feed com "Ciente" e conversas.
/// So roda com <c>dotnet run -- --seed-demo</c> (base sem usuarios) ou <c>--reset-demo</c>
/// (apaga a base antes). O startup normal e os testes nao passam por aqui.
///
/// Diretor e gerentes ficam sem o 2FA configurado de proposito: o primeiro login mostra o
/// QR code, que e justamente o que se quer demonstrar.
/// </summary>
public static class SeedDemo
{
    public const string Senha = "unite123";
    private const string Dominio = "unite.demo";

    // Cargos alem dos 4 padrao, para o organograma nao ficar so com "Funcionario".
    private static readonly (string Nome, NivelHierarquico Nivel)[] CargosExtras =
    [
        ("Executivo de Vendas", NivelHierarquico.Funcionario),
        ("Analista de Suporte", NivelHierarquico.Funcionario),
        ("Desenvolvedor", NivelHierarquico.Funcionario)
    ];

    private record Pessoa(string Login, string Nome, string Cargo, string? Equipe = null);

    private static readonly Pessoa[] Pessoas =
    [
        new("teste", "Usuário Teste", "Diretor"),
        new("helena.duarte", "Helena Duarte", "Diretor"),
        new("ricardo.almeida", "Ricardo Almeida", "Gerente"),
        new("patricia.nogueira", "Patrícia Nogueira", "Gerente"),

        new("bruno.carvalho", "Bruno Carvalho", "Supervisor", "Vendas"),
        new("camila.rocha", "Camila Rocha", "Executivo de Vendas", "Vendas"),
        new("diego.martins", "Diego Martins", "Executivo de Vendas", "Vendas"),
        new("larissa.teixeira", "Larissa Teixeira", "Executivo de Vendas", "Vendas"),

        new("juliana.freitas", "Juliana Freitas", "Supervisor", "Suporte ao Cliente"),
        new("mateus.ribeiro", "Mateus Ribeiro", "Analista de Suporte", "Suporte ao Cliente"),
        new("natalia.gomes", "Natália Gomes", "Analista de Suporte", "Suporte ao Cliente"),
        new("otavio.barbosa", "Otávio Barbosa", "Analista de Suporte", "Suporte ao Cliente"),

        new("marcelo.pires", "Marcelo Pires", "Supervisor", "Desenvolvimento"),
        new("priscila.andrade", "Priscila Andrade", "Desenvolvedor", "Desenvolvimento"),
        new("thiago.moreira", "Thiago Moreira", "Desenvolvedor", "Desenvolvimento"),
        new("vanessa.cardoso", "Vanessa Cardoso", "Desenvolvedor", "Desenvolvimento"),

        // Recem-contratado sem cargo nem equipe: para mostrar a atribuicao ao vivo.
        new("lucas.fernandes", "Lucas Fernandes", "")
    ];

    /// <summary>Supervisor de cada equipe (tambem e membro, senao fica fora do canal).</summary>
    private static readonly Dictionary<string, string> Equipes = new()
    {
        ["Vendas"] = "bruno.carvalho",
        ["Suporte ao Cliente"] = "juliana.freitas",
        ["Desenvolvimento"] = "marcelo.pires"
    };

    private record Aviso(string Autor, string Titulo, string Conteudo, bool Institucional, TimeSpan Ha);

    private static readonly Aviso[] Avisos =
    [
        new("helena.duarte", "Boas-vindas ao Unite",
            "A partir de hoje todos os comunicados oficiais da empresa serão publicados aqui no feed.\n\n" +
            "Sempre que ler um aviso, clique em \"Ciente\" — assim a liderança sabe que a informação chegou a todos.",
            true, TimeSpan.FromDays(13)),
        new("patricia.nogueira", "Nova política de home office",
            "A partir de dezembro, cada equipe poderá trabalhar remotamente até 3 dias por semana.\n\n" +
            "Combinem a escala com o seu supervisor até o dia 25. Reuniões presenciais continuam às terças.",
            true, TimeSpan.FromDays(10)),
        new("ricardo.almeida", "Resultado do 3º trimestre",
            "Fechamos o trimestre 18% acima da meta, puxados pela renovação dos contratos anuais.\n\n" +
            "Obrigado a todos pelo esforço. A apresentação completa está na pasta compartilhada do Comercial.",
            true, TimeSpan.FromDays(7)),
        new("juliana.freitas", "Escala de plantão do suporte — novembro",
            "Plantões de fim de semana:\n" +
            "• 15–16: Mateus\n• 22–23: Natália\n• 29–30: Otávio\n\n" +
            "Trocas devem ser combinadas entre vocês e avisadas no canal da equipe.",
            false, TimeSpan.FromDays(6)),
        new("marcelo.pires", "Deploy da versão 2.4 na quinta",
            "A janela de manutenção será quinta-feira, das 22h às 23h. O sistema pode ficar instável nesse período.\n\n" +
            "Suporte: o roteiro de respostas para os clientes já está no canal de vocês.",
            false, TimeSpan.FromDays(4)),
        new("helena.duarte", "Recesso de fim de ano",
            "O escritório estará fechado entre 24/12 e 01/01. O suporte manterá plantão reduzido, conforme escala a ser divulgada.\n\n" +
            "Boas festas a todos!",
            true, TimeSpan.FromDays(3)),
        new("bruno.carvalho", "Meta da semana: 40 propostas",
            "Pessoal, estamos em 31 de 40 propostas enviadas. Foco nos leads quentes da feira — a lista está no CRM.",
            false, TimeSpan.FromDays(1.5)),
        new("patricia.nogueira", "Treinamento obrigatório de segurança da informação",
            "Todos devem concluir o treinamento online de segurança até sexta-feira. Leva cerca de 40 minutos.\n\n" +
            "O link foi enviado por e-mail. Dúvidas, falem com o seu supervisor.",
            true, TimeSpan.FromHours(5))
    ];

    /// <summary>Conta "de apresentacao" do funcionario: os avisos recentes ficam para ela clicar "Ciente".</summary>
    private const string ContaApresentacao = "camila.rocha";

    private record Fala(string Autor, string Texto);

    private static readonly Dictionary<string, Fala[]> Canais = new()
    {
        ["Vendas"] =
        [
            new("bruno.carvalho", "Bom dia, time! Lembrando da reunião de pipeline às 10h."),
            new("camila.rocha", "Bom dia! Vou levar a atualização dos leads da feira."),
            new("diego.martins", "Fechei com a Transportadora Horizonte ontem à noite 🎉"),
            new("bruno.carvalho", "Excelente, Diego! Já atualiza no CRM, por favor."),
            new("larissa.teixeira", "Alguém tem o modelo novo de proposta? O da pasta ainda é o antigo."),
            new("camila.rocha", "Tenho sim, te mando no privado."),
            new("bruno.carvalho", "Vou trocar o arquivo da pasta hoje. Obrigado por avisar, Larissa."),
            new("diego.martins", "Faltam 9 propostas pra meta. Bora!")
        ],
        ["Suporte ao Cliente"] =
        [
            new("juliana.freitas", "Pessoal, a escala de plantão de novembro está no feed."),
            new("mateus.ribeiro", "Vi! Fico com o dia 15, sem problema."),
            new("natalia.gomes", "Chegou um chamado sobre lentidão no login, alguém mais viu isso hoje?"),
            new("otavio.barbosa", "Dois clientes reclamaram. Já abri um ticket para o Desenvolvimento."),
            new("juliana.freitas", "Ótimo. Natália, acompanha com o Marcelo e atualiza aqui."),
            new("natalia.gomes", "Combinado. Era cache, já normalizou ✅")
        ],
        ["Desenvolvimento"] =
        [
            new("marcelo.pires", "Code freeze da 2.4 amanhã às 18h."),
            new("priscila.andrade", "O PR do relatório de vendas está pronto pra revisão."),
            new("thiago.moreira", "Reviso depois do almoço."),
            new("vanessa.cardoso", "Corrigi o cache do login que o Suporte reportou. Subindo em homologação."),
            new("marcelo.pires", "Boa, Vanessa! Avisa a Natália quando estiver no ar."),
            new("thiago.moreira", "PR aprovado, Priscila. Só um comentário sobre nomes de variáveis."),
            new("priscila.andrade", "Ajustado e mergeado. Obrigada!")
        ]
    };

    private static readonly (string A, string B, Fala[] Falas)[] Privadas =
    [
        ("helena.duarte", "ricardo.almeida",
        [
            new("helena.duarte", "Ricardo, parabéns pelo trimestre. Consegue apresentar os números na reunião de diretoria?"),
            new("ricardo.almeida", "Claro, Helena. Preparo os slides até quarta."),
            new("helena.duarte", "Perfeito. Inclua a projeção para o primeiro trimestre também."),
            new("ricardo.almeida", "Pode deixar.")
        ]),
        ("patricia.nogueira", "juliana.freitas",
        [
            new("patricia.nogueira", "Juliana, como ficou o plantão do recesso?"),
            new("juliana.freitas", "Estou fechando com a equipe. Te mando até sexta."),
            new("patricia.nogueira", "Ótimo, obrigada!")
        ]),
        ("bruno.carvalho", "camila.rocha",
        [
            new("bruno.carvalho", "Camila, bom trabalho na feira! Quantos leads você trouxe?"),
            new("camila.rocha", "Foram 23, sendo 8 bem quentes."),
            new("bruno.carvalho", "Excelente. Prioriza esses 8 essa semana."),
            new("camila.rocha", "Já estou montando as propostas."),
            new("bruno.carvalho", "👏")
        ]),
        ("mateus.ribeiro", "priscila.andrade",
        [
            new("mateus.ribeiro", "Priscila, o relatório de vendas novo já vai na 2.4?"),
            new("priscila.andrade", "Vai sim! Sai na quinta à noite."),
            new("mateus.ribeiro", "Show, vários clientes estavam pedindo.")
        ])
    ];

    /// <summary>Semeia a empresa de demonstracao. Devolve false (sem tocar em nada) se ja existem usuarios.</summary>
    public static async Task<bool> ExecutarAsync(IServiceProvider servicos)
    {
        var db = servicos.GetRequiredService<AppDbContext>();
        var usuarios = servicos.GetRequiredService<UserManager<Usuario>>();
        var log = servicos.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(SeedDemo));

        if (await db.Users.AnyAsync())
        {
            log.LogWarning("Seed de demonstracao ignorado: a base ja tem usuarios. Use --reset-demo para recriar.");
            return false;
        }

        await SeedCargos.GarantirAsync(db);
        var agora = DateTime.UtcNow;
        var rng = new Random(2026); // fixo: toda demo sai igual

        // ---- cargos
        foreach (var (nome, nivel) in CargosExtras)
        {
            if (!await db.Cargos.AnyAsync(c => c.Nome == nome))
                db.Cargos.Add(new Cargo { Nome = nome, Nivel = nivel });
        }
        await db.SaveChangesAsync();
        var cargos = await db.Cargos.ToDictionaryAsync(c => c.Nome);

        // ---- equipes (supervisor entra depois, quando a pessoa existir)
        var equipes = Equipes.Keys.ToDictionary(n => n, n => new Equipe { Nome = n });
        db.Equipes.AddRange(equipes.Values);
        await db.SaveChangesAsync();

        // ---- pessoas
        var pessoas = new Dictionary<string, Usuario>();
        foreach (var p in Pessoas)
        {
            var usuario = new Usuario
            {
                UserName = $"{p.Login}@{Dominio}",
                Email = $"{p.Login}@{Dominio}",
                EmailConfirmed = true,
                NomeCompleto = p.Nome,
                CargoId = p.Cargo == "" ? null : cargos[p.Cargo].Id,
                EquipeId = p.Equipe is null ? null : equipes[p.Equipe].Id
            };

            var resultado = await usuarios.CreateAsync(usuario, Senha);
            if (!resultado.Succeeded)
                throw new InvalidOperationException(
                    $"Seed: falha ao criar {usuario.Email}: {string.Join(" ", resultado.Errors.Select(e => e.Description))}");

            pessoas[p.Login] = usuario;
        }

        foreach (var (nome, supervisor) in Equipes)
            equipes[nome].SupervisorId = pessoas[supervisor].Id;
        await db.SaveChangesAsync();

        // ---- feed e "Ciente"
        foreach (var a in Avisos)
        {
            var publicadaEm = agora - a.Ha;
            var postagem = new Postagem
            {
                AutorId = pessoas[a.Autor].Id,
                Titulo = a.Titulo,
                Conteudo = a.Conteudo,
                Institucional = a.Institucional,
                PublicadaEm = publicadaEm
            };

            // Quanto mais antigo, mais gente ja leu (de ~20% no mesmo dia a ~95% depois de 2 semanas).
            var chance = Math.Min(0.95, 0.2 + a.Ha.TotalDays * 0.06);
            var horasDesde = Math.Max(1, (agora - publicadaEm).TotalHours);

            foreach (var leitor in pessoas.Values.Where(u => u.Id != postagem.AutorId))
            {
                if (leitor.Email!.StartsWith(ContaApresentacao) && a.Ha < TimeSpan.FromDays(4)) continue;
                if (rng.NextDouble() > chance) continue;

                postagem.Ciencias.Add(new Ciencia
                {
                    UsuarioId = leitor.Id,
                    ConfirmadaEm = publicadaEm.AddHours(rng.NextDouble() * Math.Min(horasDesde, 48))
                });
            }

            db.Postagens.Add(postagem);
        }

        // ---- canais das equipes (conversa de ontem para hoje)
        foreach (var (nome, falas) in Canais)
        {
            var sala = new Sala { Tipo = TipoSala.Grupo, EquipeId = equipes[nome].Id };
            AdicionarFalas(sala, falas, pessoas, agora.AddHours(-20), rng);
            db.Salas.Add(sala);
        }

        // ---- conversas privadas
        var inicio = agora.AddHours(-30);
        foreach (var (a, b, falas) in Privadas)
        {
            var sala = new Sala
            {
                Tipo = TipoSala.Privada,
                Participantes =
                [
                    new SalaUsuario { UsuarioId = pessoas[a].Id },
                    new SalaUsuario { UsuarioId = pessoas[b].Id }
                ]
            };
            AdicionarFalas(sala, falas, pessoas, inicio, rng);
            db.Salas.Add(sala);
            inicio = inicio.AddHours(6);
        }

        await db.SaveChangesAsync();

        log.LogInformation(
            "Seed de demonstracao criado: {Pessoas} pessoas, {Equipes} equipes, {Avisos} avisos, {Salas} conversas. " +
            "Senha de todos: {Senha}\n{Logins}",
            pessoas.Count, equipes.Count, Avisos.Length, Canais.Count + Privadas.Length, Senha,
            string.Join("\n", Pessoas.Select(p =>
                $"  {p.Login + "@" + Dominio,-32} {(p.Cargo == "" ? "(sem cargo)" : p.Cargo),-20} {p.Equipe}")));

        return true;
    }

    /// <summary>Mensagens em sequencia a partir de <paramref name="inicio"/>, com 1 a 25 min entre elas.</summary>
    private static void AdicionarFalas(
        Sala sala, Fala[] falas, Dictionary<string, Usuario> pessoas, DateTime inicio, Random rng)
    {
        var quando = inicio;
        foreach (var f in falas)
        {
            quando = quando.AddMinutes(1 + rng.Next(25));
            sala.Mensagens.Add(new Mensagem { AutorId = pessoas[f.Autor].Id, Texto = f.Texto, EnviadaEm = quando });
        }
    }
}
