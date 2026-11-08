using AuditoriaNFe.Application.Configuracao;
using AuditoriaNFe.Application.Consumo;
using AuditoriaNFe.Domain.Identificacao;
using AuditoriaNFe.Domain.Sessoes;
using AuditoriaNFe.Infrastructure.Persistencia.Postgres;
using AuditoriaNFe.Tests.Integracao.Infra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AuditoriaNFe.Tests.Integracao;

/// <summary>Registro de consumo pelo host contra o PostgreSQL real (NFR-009, P14, constitution 5.4).</summary>
[Collection(ColecaoIntegracao.Nome)]
public sealed class ConsumoTests
{
    private static readonly string Chave = IdentificacaoSolicitante.Criar("usuario-consumo", "teste-consumo").ChaveIsolamento;

    private readonly AmbienteIntegracao ambiente;

    public ConsumoTests(AmbienteIntegracao ambiente)
    {
        this.ambiente = ambiente;
        ambiente.ChatClient.Limpar();
    }

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private IChatClient Cliente => ambiente.Fabrica.Services.GetRequiredService<IChatClient>();

    [Fact]
    public async Task Grava_uma_linha_por_chamada_inclusive_voltas_de_ferramenta_e_retentativas()
    {
        ambiente.ChatClient
            .ResponderComUso("chamada de ferramenta", tokensEntrada: 1000, tokensSaida: 50, tokensEntradaCache: 200)
            .ResponderComUso("saida invalida", tokensEntrada: 1200, tokensSaida: 300)
            .ResponderComUso("saida valida", tokensEntrada: 1300, tokensSaida: 320);
        var execucaoId = Guid.CreateVersion7();

        using (var escopo = EscopoConsumo.Iniciar(execucaoId, "corr-consumo", Chave, "AnalisarNFe"))
        {
            await Cliente.GetResponseAsync("pergunta", cancellationToken: Cancelamento);
            ambiente.Relogio.Avancar(TimeSpan.FromSeconds(1));
            await Cliente.GetResponseAsync("pergunta com resultado da ferramenta", cancellationToken: Cancelamento);
            ambiente.Relogio.Avancar(TimeSpan.FromSeconds(1));
            escopo.DefinirTentativa(2);
            await Cliente.GetResponseAsync("nova tentativa", cancellationToken: Cancelamento);

            var resumo = escopo.Resumir();
            Assert.True(resumo.RegistroCompleto);
            Assert.Equal(3500, resumo.TokensEntrada);
            Assert.Equal(670, resumo.TokensSaida);
        }

        var linhas = await Linhas(execucaoId);
        Assert.Equal([1, 1, 2], linhas.Select(l => l.Tentativa));
        Assert.All(linhas, l =>
        {
            Assert.Equal("Sucesso", l.Status);
            Assert.Equal("Chat", l.TipoChamada);
            Assert.Equal(AmbienteIntegracao.ProvedorRoteirizado, l.Provedor);
            Assert.Equal("gpt-5-mini", l.Modelo);
            Assert.Equal("corr-consumo", l.CorrelacaoId);
            Assert.Equal(Chave, l.ChaveIsolamento);
            Assert.Equal("InformadoProvedor", l.OrigemValores);
            Assert.Equal("precos-openai.v1", l.VersaoTabelaPrecos);
        });
        // gpt-5-mini: 800 × 0,25 + 200 × 0,025 por milhão na entrada e 50 × 2,00 por milhão na saída.
        Assert.Equal(0.000205m, linhas[0].CustoEntrada);
        Assert.Equal(0.0001m, linhas[0].CustoSaida);
        Assert.Equal(0.000305m, linhas[0].CustoTotal);
        Assert.Equal("USD", linhas[0].Moeda);
    }

    [Fact]
    public async Task Tokens_ausentes_sao_gravados_como_NULL()
    {
        ambiente.ChatClient.Responder("sem usage");
        var execucaoId = Guid.CreateVersion7();

        using (EscopoConsumo.Iniciar(execucaoId, null, Chave, "AnalisarNFe"))
        {
            await Cliente.GetResponseAsync("pergunta", cancellationToken: Cancelamento);
        }

        var linha = Assert.Single(await Linhas(execucaoId));
        Assert.Null(linha.TokensEntrada);
        Assert.Null(linha.TokensEntradaCache);
        Assert.Null(linha.TokensSaida);
        Assert.Null(linha.CustoTotal);
        Assert.Null(linha.Moeda);
    }

    [Fact]
    public async Task Modelo_sem_preco_tem_custo_NULL_e_mantem_os_tokens()
    {
        ambiente.ChatClient.Responder("ok", new UsageDetails { InputTokenCount = 100, OutputTokenCount = 10 }, modeloResposta: "modelo-sem-preco");
        var execucaoId = Guid.CreateVersion7();

        using (EscopoConsumo.Iniciar(execucaoId, null, Chave, "AnalisarNFe"))
        {
            await Cliente.GetResponseAsync("pergunta", cancellationToken: Cancelamento);
        }

        var linha = Assert.Single(await Linhas(execucaoId));
        Assert.Equal("modelo-sem-preco", linha.Modelo);
        Assert.Equal(100, linha.TokensEntrada);
        Assert.Null(linha.CustoEntrada);
        Assert.Null(linha.CustoSaida);
        Assert.Null(linha.CustoTotal);
    }

    [Fact]
    public async Task Falha_do_provedor_e_registrada_com_status_e_a_excecao_e_preservada()
    {
        ambiente.ChatClient.Lancar(new HttpRequestException("indisponível"));
        var execucaoId = Guid.CreateVersion7();

        using (EscopoConsumo.Iniciar(execucaoId, null, Chave, "AnalisarNFe"))
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => Cliente.GetResponseAsync("pergunta", cancellationToken: Cancelamento));
        }

        Assert.Equal("Falha", Assert.Single(await Linhas(execucaoId)).Status);
    }

    [Fact]
    public async Task PostgreSQL_parado_falha_o_registro_dentro_do_timeout_sem_alterar_o_resultado()
    {
        await using var postgres = new ContainerPostgres();
        await postgres.InitializeAsync();
        var fabrica = ambiente.CriarFabrica(configuracoes: new Dictionary<string, string?>
        {
            ["ConnectionStrings:Consumo"] = postgres.ConnectionString,
            ["Persistencia:TimeoutPostgres"] = "00:00:02",
        });
        var cliente = fabrica.Services.GetRequiredService<IChatClient>();
        var timeout = fabrica.Services.GetRequiredService<IOptions<PersistenciaOpcoes>>().Value.TimeoutPostgres!.Value;
        await postgres.PararAsync();
        ambiente.ChatClient.ResponderComUso("resultado da IA", tokensEntrada: 100, tokensSaida: 10);

        using var escopo = EscopoConsumo.Iniciar(Guid.CreateVersion7(), null, Chave, "AnalisarNFe");
        var inicio = DateTime.UtcNow;
        var resposta = await cliente.GetResponseAsync("pergunta", cancellationToken: Cancelamento);
        var duracao = DateTime.UtcNow - inicio;

        Assert.Equal("resultado da IA", resposta.Text);
        Assert.True(duracao < timeout + TimeSpan.FromSeconds(3), $"Registro levou {duracao.TotalSeconds:F1} s.");
        var resumo = escopo.Resumir();
        Assert.False(resumo.RegistroCompleto);
        Assert.Equal(100, resumo.TokensEntrada);
        Assert.Equal(OrigemValoresConsumo.InformadoProvedor, resumo.Origem);
    }

    private async Task<List<ChamadaProvedor>> Linhas(Guid execucaoId)
    {
        var builder = new DbContextOptionsBuilder<ConsumoDbContext>();
        ConsumoDbContext.Configurar(
            builder,
            ambiente.Postgres.ConnectionString,
            new PersistenciaOpcoes { TimeoutMongo = TimeSpan.FromSeconds(5), TimeoutPostgres = TimeSpan.FromSeconds(5) });
        await using var contexto = new ConsumoDbContext(builder.Options);
        return await contexto.ChamadasProvedor
            .Where(l => l.ExecucaoId == execucaoId)
            .OrderBy(l => l.IniciadaEm).ThenBy(l => l.Id)
            .ToListAsync(Cancelamento);
    }
}
