using System.Diagnostics.Metrics;
using AuditoriaNFe.Application.Abstracoes;
using AuditoriaNFe.Application.Configuracao;
using AuditoriaNFe.Application.Consumo;
using AuditoriaNFe.Domain.Sessoes;
using AuditoriaNFe.Infrastructure.Ia;
using AuditoriaNFe.Infrastructure.Persistencia.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AuditoriaNFe.Tests.Aplicacao.Consumo;

/// <summary>
/// Comportamento do registro sem PostgreSQL disponível; a gravação real fica em
/// <c>Integracao/ConsumoTests</c> (T043).
/// </summary>
[Collection(ColecaoSemParalelismo.Nome)]
public sealed class RegistroConsumoChatClientTests
{
    private const string Chave = "f483983d00000000000000000000000000000000000000000000000000008fe5";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task Falha_ao_gravar_nao_altera_a_resposta_e_marca_registro_incompleto()
    {
        var esperado = new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"))
        {
            ModelId = "gpt-4.1-mini-2025-04-14",
            Usage = new UsageDetails { InputTokenCount = 1000, CachedInputTokenCount = 200, OutputTokenCount = 500 },
        };
        using var medidores = new FabricaMedidores();
        using var falhas = new ContadorFalhas(medidores);
        using var cliente = Criar(new ChatClientFixo(_ => esperado), medidores);
        using var escopo = EscopoConsumo.Iniciar(Guid.NewGuid(), "corr-1", Chave, "AnalisarNFe");

        var inicio = DateTime.UtcNow;
        var resposta = await cliente.GetResponseAsync("pergunta", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Same(esperado, resposta);
        Assert.True(DateTime.UtcNow - inicio < Timeout + TimeSpan.FromSeconds(3));
        var resumo = escopo.Resumir();
        Assert.False(resumo.RegistroCompleto);
        Assert.Equal(1000, resumo.TokensEntrada);
        Assert.Equal(0.00114m, resumo.CustoTotal);
        Assert.Equal(OrigemValoresConsumo.InformadoProvedor, resumo.Origem);
        Assert.Equal(1, falhas.Total);
    }

    [Fact]
    public async Task Excecao_do_provedor_e_propagada_sem_alteracao()
    {
        var erro = new InvalidOperationException("provedor indisponível");
        using var medidores = new FabricaMedidores();
        using var cliente = Criar(new ChatClientFixo(_ => throw erro), medidores);
        using var escopo = EscopoConsumo.Iniciar(Guid.NewGuid(), null, Chave, "AnalisarNFe");

        var lancada = await Assert.ThrowsAsync<InvalidOperationException>(
            () => cliente.GetResponseAsync("pergunta", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Same(erro, lancada);
        Assert.Equal(new ConsumoResumo(null, null, null, null, null, OrigemValoresConsumo.InformadoProvedor, false), escopo.Resumir());
    }

    [Fact]
    public async Task Resposta_sem_usage_gera_tokens_e_custo_nulos()
    {
        using var medidores = new FabricaMedidores();
        using var cliente = Criar(new ChatClientFixo(_ => new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")) { ModelId = "gpt-5-mini" }), medidores);
        using var escopo = EscopoConsumo.Iniciar(Guid.NewGuid(), null, Chave, "AnalisarNFe");

        await cliente.GetResponseAsync("pergunta", cancellationToken: TestContext.Current.CancellationToken);

        var resumo = escopo.Resumir();
        Assert.Null(resumo.TokensEntrada);
        Assert.Null(resumo.TokensSaida);
        Assert.Null(resumo.CustoTotal);
    }

    [Fact]
    public async Task Chamada_sem_escopo_nao_e_registrada_e_conta_na_metrica()
    {
        using var medidores = new FabricaMedidores();
        using var falhas = new ContadorFalhas(medidores);
        using var cliente = Criar(new ChatClientFixo(_ => new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"))), medidores);

        await cliente.GetResponseAsync("pergunta", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, falhas.Total);
    }

    private static RegistroConsumoChatClient Criar(IChatClient interno, IMeterFactory medidores)
    {
        var opcoes = new PersistenciaOpcoes { TimeoutMongo = Timeout, TimeoutPostgres = Timeout };
        var builder = new DbContextOptionsBuilder<ConsumoDbContext>();
        ConsumoDbContext.Configurar(builder, "Host=127.0.0.1;Port=1;Database=auditoria_agente;Username=teste", opcoes);
        var tabela = TabelaPrecos.Carregar(Path.Combine(AppContext.BaseDirectory, "Catalogos", "precos-openai.v1.json"));

        var gravador = new GravadorConsumo(
            new FabricaContexto(builder.Options),
            new CalculadoraCusto(tabela),
            new RelogioFixo(),
            new IdsSequenciais(),
            Options.Create(opcoes),
            medidores,
            NullLogger<GravadorConsumo>.Instance);

        return new RegistroConsumoChatClient(interno, gravador, "OpenAI", OrigemValoresConsumo.InformadoProvedor);
    }

    private sealed class ChatClientFixo(Func<IEnumerable<ChatMessage>, ChatResponse> responder) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(responder(messages));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed class FabricaContexto(DbContextOptions<ConsumoDbContext> opcoes) : IDbContextFactory<ConsumoDbContext>
    {
        public ConsumoDbContext CreateDbContext() => new(opcoes);
    }

    private sealed class RelogioFixo : IRelogio
    {
        public DateTimeOffset Agora { get; } = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class IdsSequenciais : IGeradorIds
    {
        public Guid Novo() => Guid.CreateVersion7();
    }

    private sealed class FabricaMedidores : IMeterFactory
    {
        private readonly List<Meter> _medidores = [];

        public IReadOnlyList<Meter> Medidores => _medidores;

        public Meter Create(MeterOptions options)
        {
            var medidor = new Meter(options);
            _medidores.Add(medidor);
            return medidor;
        }

        public void Dispose() => _medidores.ForEach(m => m.Dispose());
    }

    private sealed class ContadorFalhas : IDisposable
    {
        private readonly MeterListener _ouvinte = new();
        private long _total;

        public ContadorFalhas(FabricaMedidores medidores)
        {
            _ouvinte.InstrumentPublished = (instrumento, ouvinte) =>
            {
                if (medidores.Medidores.Contains(instrumento.Meter) && instrumento.Name == GravadorConsumo.NomeContadorFalhas)
                {
                    ouvinte.EnableMeasurementEvents(instrumento);
                }
            };
            _ouvinte.SetMeasurementEventCallback<long>((_, valor, _, _) => Interlocked.Add(ref _total, valor));
            _ouvinte.Start();
        }

        public long Total => Interlocked.Read(ref _total);

        public void Dispose() => _ouvinte.Dispose();
    }
}

/// <summary>
/// Testes que medem tempo de parede: sob a carga das coleções de integração em paralelo, o pool de
/// threads atrasa o disparo do timeout além da tolerância.
/// </summary>
[CollectionDefinition(Nome, DisableParallelization = true)]
public sealed class ColecaoSemParalelismo
{
    public const string Nome = "SemParalelismo";
}
