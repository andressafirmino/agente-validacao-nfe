using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AuditoriaNFe.Tests.Duplos;
using AuditoriaNFe.Tests.Fixtures.Xml;
using AuditoriaNFe.Tests.Integracao.Infra;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static AuditoriaNFe.Tests.Integracao.Infra.AssercoesEnvelope;

namespace AuditoriaNFe.Tests.Integracao;

/// <summary>
/// NFR-007 e P16: os logs de uma execução completa não levam XML, pergunta, resposta da IA, CPF nem
/// <c>usuarioId</c> em claro, e toda linha das categorias do agente tem <c>ExecucaoId</c>.
/// </summary>
[Collection(ColecaoIntegracao.Nome)]
public sealed partial class LogsSemConteudoTests
{
    private const string Pergunta = "PERGUNTA-SIGILOSA-LOGS: o total do lote está correto?";
    private const string ResumoIa = "RESPOSTA-SIGILOSA-LOGS sobre o lote analisado.";
    private const string UsuarioId = "usuario-sigiloso-logs-4821";
    private const string CpfDestinatario = "12345678909";

    private readonly AmbienteIntegracao _ambiente;

    public LogsSemConteudoTests(AmbienteIntegracao ambiente)
    {
        _ambiente = ambiente;
        _ambiente.ChatClient.Limpar();
    }

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Logs_de_execucao_completa_nao_tem_conteudo_e_tem_execucaoId()
    {
        var coletor = new ColetorLogs();
        var fabrica = _ambiente.CriarFabrica(
            configuracoes: new Dictionary<string, string?> { ["Logging:LogLevel:Default"] = "Debug" },
            servicos: s => s.AddSingleton<ILoggerProvider>(coletor));
        var cliente = fabrica.CreateClient();
        await cliente.GetAsync("/health/live", Cancelamento);
        coletor.Limpar();
        _ambiente.ChatClient.Responder(RespostasIa.Valida(resumo: ResumoIa));

        var envelope = Concluida(await ClienteExecucoes.CriarAsync(
            cliente,
            [ArquivoEnvio.Fixture(FixturesXml.Casos.TotalizadorDivergente), PessoaFisica()],
            pergunta: Pergunta,
            usuarioId: UsuarioId,
            cancelamento: Cancelamento));

        var linhas = coletor.Linhas();
        Assert.Contains(linhas, l => l.Agente);
        var proibidos = new[] { "<infNFe", "<?xml", "<nfeProc", "<NFe", "<det ", "PERGUNTA-SIGILOSA-LOGS", "RESPOSTA-SIGILOSA-LOGS", UsuarioId, CpfDestinatario };
        foreach (var linha in linhas)
        {
            foreach (var proibido in proibidos)
            {
                Assert.False(
                    linha.Texto.Contains(proibido, StringComparison.OrdinalIgnoreCase),
                    $"Log [{linha.Categoria}] contém \"{proibido}\": {linha.Texto}");
            }

            Assert.False(CpfFormatado().IsMatch(linha.Texto), $"Log [{linha.Categoria}] contém CPF: {linha.Texto}");
        }

        Assert.All(
            linhas.Where(l => l.Agente),
            l => Assert.True(l.TemExecucaoId, $"Log [{l.Categoria}] sem ExecucaoId: {l.Texto}"));
        Assert.Contains(linhas, l => l.Agente && l.Texto.Contains(envelope.ExecucaoId.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    private static ArquivoEnvio PessoaFisica() =>
        new("nfe-pessoa-fisica-logs.xml", GeradorNFeSintetica.GerarBytes(new OpcoesNFeSintetica
        {
            Numero = 951,
            Ajustar = documento =>
            {
                var ns = GeradorNFeSintetica.Nfe;
                var dest = documento.Descendants(ns + "dest").Single();
                dest.Element(ns + "CNPJ")!.ReplaceWith(new XElement(ns + "CPF", CpfDestinatario));
            },
        }));

    [GeneratedRegex(@"\d{3}\.\d{3}\.\d{3}-\d{2}")]
    private static partial Regex CpfFormatado();

    private sealed record LinhaLog(string Categoria, string Texto, bool TemExecucaoId)
    {
        public bool Agente => Categoria.StartsWith("AuditoriaNFe.", StringComparison.Ordinal)
            || Categoria.StartsWith("Api.Agente", StringComparison.Ordinal);
    }

    /// <summary>Guarda mensagem, valores do estado, escopos e exceção de cada log.</summary>
    private sealed class ColetorLogs : ILoggerProvider, ISupportExternalScope
    {
        private readonly ConcurrentQueue<LinhaLog> _linhas = new();
        private IExternalScopeProvider _escopos = new LoggerExternalScopeProvider();

        public ILogger CreateLogger(string categoryName) => new Registrador(this, categoryName);

        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _escopos = scopeProvider;

        public void Limpar() => _linhas.Clear();

        public List<LinhaLog> Linhas() => [.. _linhas];

        public void Dispose()
        {
        }

        private void Registrar<TState>(string categoria, TState estado, Exception? erro, string mensagem)
        {
            var texto = new StringBuilder(mensagem);
            var temExecucaoId = false;
            void Acumular(object? valor)
            {
                if (valor is IEnumerable<KeyValuePair<string, object?>> pares)
                {
                    foreach (var (chave, item) in pares)
                    {
                        texto.Append(" | ").Append(chave).Append('=').Append(item);
                        temExecucaoId |= string.Equals(chave, "ExecucaoId", StringComparison.OrdinalIgnoreCase) && item is not null;
                    }
                }
                else if (valor is not null)
                {
                    texto.Append(" | ").Append(valor);
                }
            }

            Acumular(estado);
            _escopos.ForEachScope((escopo, _) => Acumular(escopo), (object?)null);
            if (erro is not null)
            {
                texto.Append(" | ").Append(erro);
            }

            _linhas.Enqueue(new LinhaLog(categoria, texto.ToString(), temExecucaoId));
        }

        private sealed class Registrador(ColetorLogs coletor, string categoria) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => coletor._escopos.Push(state);

            public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                coletor.Registrar(categoria, state, exception, formatter(state, exception));
        }
    }
}
