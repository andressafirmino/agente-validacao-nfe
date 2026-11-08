using System.Net;
using AuditoriaNFe.Application.Contratos;
using AuditoriaNFe.Infrastructure.Persistencia.Postgres;
using AuditoriaNFe.Application.Configuracao;
using AuditoriaNFe.Tests.Fixtures.Xml;
using Microsoft.EntityFrameworkCore;

namespace AuditoriaNFe.Tests.Integracao.Infra;

/// <summary>Conferências comuns sobre o envelope v1 e o consumo das execuções.</summary>
public static class AssercoesEnvelope
{
    /// <summary>NF-e válida de T044 na vigência do PL_010e_v1.02, anterior à data do relógio de teste.</summary>
    public static string NFeValida => FixturesXml.Pacotes().Single(p => p.Id == FixturesXml.Casos.Pacote).ArquivoNFe;

    public static string NFeProcValida => FixturesXml.Pacotes().Single(p => p.Id == FixturesXml.Casos.Pacote).ArquivoNFeProc;

    public static EnvelopeV1 Concluida(RespostaExecucao resposta)
    {
        Assert.Equal(HttpStatusCode.OK, resposta.Status);
        var envelope = resposta.Envelope;
        Assert.Equal("Concluida", envelope.Estado);
        Assert.NotNull(envelope.Parecer);
        return envelope;
    }

    /// <summary>Rejeição do <c>POST /api/v1/execucoes</c>: envelope <c>Rejeitada</c>, sem parecer.</summary>
    public static EnvelopeV1 Rejeitada(RespostaExecucao resposta, HttpStatusCode status, string codigo)
    {
        Assert.Equal(status, resposta.Status);
        var envelope = resposta.Envelope;
        Assert.Equal("Rejeitada", envelope.Estado);
        Assert.Equal(codigo, envelope.Erro?.Codigo);
        Assert.Null(envelope.Parecer);
        return envelope;
    }

    /// <summary>
    /// Dependência indisponível (ERR-013 a ERR-015): execução concluída com o código no erro da
    /// execução, achado de falha técnica e revisão humana.
    /// </summary>
    public static void FalhaDeDependencia(EnvelopeV1 envelope, string codigo)
    {
        Assert.Equal("Concluida", envelope.Estado);
        Assert.Equal(codigo, envelope.Erro?.Codigo);
        Assert.Contains(envelope.Parecer!.Achados, a => a.Categoria == "FalhaTecnica");
        Assert.True(envelope.Parecer.RevisaoHumana.Necessaria);
    }

    public static EtapaV1 Etapa(EnvelopeV1 envelope, int numero) => Assert.Single(envelope.Etapas, e => e.Numero == numero);

    public static AchadoV1 AchadoDaRegra(EnvelopeV1 envelope, string regraId) =>
        Assert.Single(envelope.Parecer!.Achados, a => a.RegraId == regraId);

    public static async Task<List<ChamadaProvedor>> LinhasConsumoAsync(ContainerPostgres postgres, Guid execucaoId, CancellationToken cancelamento)
    {
        var builder = new DbContextOptionsBuilder<ConsumoDbContext>();
        ConsumoDbContext.Configurar(
            builder,
            postgres.ConnectionString,
            new PersistenciaOpcoes { TimeoutMongo = TimeSpan.FromSeconds(5), TimeoutPostgres = TimeSpan.FromSeconds(5) });
        await using var contexto = new ConsumoDbContext(builder.Options);
        return await contexto.ChamadasProvedor
            .Where(l => l.ExecucaoId == execucaoId)
            .OrderBy(l => l.IniciadaEm).ThenBy(l => l.Id)
            .ToListAsync(cancelamento);
    }
}
