using System.Collections.Concurrent;
using AuditoriaNFe.Application.Abstracoes;

namespace AuditoriaNFe.Tests.Duplos;

/// <summary>Base de conhecimento habilitada que devolve os trechos dados ou lança o erro dado.</summary>
public sealed class ConsultaManuaisFalsa(IReadOnlyList<TrechoManual>? trechos = null, Exception? erro = null) : IConsultaManuais
{
    private readonly ConcurrentQueue<ConsultaManuais> _consultas = new();

    public IReadOnlyList<ConsultaManuais> Consultas => [.. _consultas];

    public bool Habilitada => true;

    /// <summary>Trecho sintético: não reproduz nem cita manual oficial.</summary>
    public static TrechoManual Trecho(string chunkId = "manual-sintetico-1.0-001") =>
        new(chunkId, "MANUAL-SINTETICO", "1.0", new DateOnly(2017, 12, 4), null,
            "Trecho sintético de teste sobre totalizadores.", Secao: "Seção 1", Pagina: 1, Pontuacao: 0.9);

    public Task<ResultadoConsultaManuais> ConsultarAsync(ConsultaManuais consulta, CancellationToken cancellationToken)
    {
        _consultas.Enqueue(consulta);
        return erro is null
            ? Task.FromResult(new ResultadoConsultaManuais(trechos ?? []))
            : Task.FromException<ResultadoConsultaManuais>(erro);
    }
}
