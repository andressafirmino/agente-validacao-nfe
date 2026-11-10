using AuditoriaNFe.Application.Abstracoes;
using AuditoriaNFe.Application.Configuracao;
using AuditoriaNFe.Domain.ContextoUsuario;
using AuditoriaNFe.Domain.Documentos;
using AuditoriaNFe.Domain.Sessoes;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AuditoriaNFe.Infrastructure.Persistencia.Mongo;

/// <summary>
/// Acesso ao banco <c>auditoria_agente</c> (data-model, persistência M). <c>Persistencia:TimeoutMongo</c>
/// limita a seleção de servidor, a conexão, o socket e cada operação; indisponibilidade ou tempo
/// esgotado viram <see cref="ArmazenamentoIndisponivelException"/> (ERR-025; constitution 5.4).
/// </summary>
public sealed class ContextoMongo
{
    public const string NomeConnectionString = "MongoDb";
    public const string BancoPadrao = "auditoria_agente";

    public ContextoMongo(string connectionString, IOptions<PersistenciaOpcoes> opcoes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        MapeamentoMongo.Registrar();

        Timeout = opcoes.Value.TimeoutMongo
            ?? throw new InvalidOperationException("Persistencia:TimeoutMongo é obrigatório.");

        var url = MongoUrl.Create(connectionString);
        var configuracao = MongoClientSettings.FromUrl(url);
        configuracao.ServerSelectionTimeout = Timeout;
        configuracao.ConnectTimeout = Timeout;
        configuracao.SocketTimeout = Timeout;
        configuracao.WaitQueueTimeout = Timeout;

        Cliente = new MongoClient(configuracao);
        Banco = Cliente.GetDatabase(string.IsNullOrEmpty(url.DatabaseName) ? BancoPadrao : url.DatabaseName);
    }

    public TimeSpan Timeout { get; }

    public IMongoClient Cliente { get; }

    public IMongoDatabase Banco { get; }

    public IMongoCollection<Sessao> Sessoes => Banco.GetCollection<Sessao>("sessoes");

    public IMongoCollection<Execucao> Execucoes => Banco.GetCollection<Execucao>("execucoes");

    public IMongoCollection<DocumentoNFe> Documentos => Banco.GetCollection<DocumentoNFe>("documentos");

    public IMongoCollection<ContextoUsuario> ContextosUsuario => Banco.GetCollection<ContextoUsuario>("contextos_usuario");

    public async Task<T> ExecutarAsync<T>(Func<CancellationToken, Task<T>> operacao, CancellationToken cancellationToken)
    {
        using var prazo = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        prazo.CancelAfter(Timeout);
        try
        {
            return await operacao(prazo.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException excecao) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ArmazenamentoIndisponivelException(excecao);
        }
        catch (TimeoutException excecao)
        {
            throw new ArmazenamentoIndisponivelException(excecao);
        }
        catch (MongoException excecao) when (excecao is not MongoWriteException and not MongoBulkWriteException)
        {
            throw new ArmazenamentoIndisponivelException(excecao);
        }
    }

    public Task ExecutarAsync(Func<CancellationToken, Task> operacao, CancellationToken cancellationToken) =>
        ExecutarAsync<bool>(
            async token =>
            {
                await operacao(token).ConfigureAwait(false);
                return true;
            },
            cancellationToken);
}
