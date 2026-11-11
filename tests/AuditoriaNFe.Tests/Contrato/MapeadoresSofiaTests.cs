using System.Text;
using System.Text.Json;
using Api.Agente.A2A;
using Api.Agente.A2A.Sofia;
using AuditoriaNFe.Application.Configuracao;
using AuditoriaNFe.Application.Contratos;
using AuditoriaNFe.Domain.Identificacao;
using AuditoriaNFe.Domain.Sessoes;
using AuditoriaNFe.Tests.Arquitetura;
using Json.Schema;
using Microsoft.Extensions.Options;

namespace AuditoriaNFe.Tests.Contrato;

/// <summary>Mapeadores SOFIA ↔ domínio (T141, data-model §18).</summary>
public sealed class MapeadoresSofiaTests
{
    [Fact]
    public void Solicitacao_mapeia_idDocumento_bytes_e_nome_normalizado()
    {
        var mapeador = CriarEntrada();
        var sofia = new SolicitacaoAnaliseSofia(
            ContratoSofiaV1.Versao,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            new UsuarioSofia("  João  DA  Silva  "),
            "  Pergunta?  ",
            [new DocumentoSofia("doc-1", "nfe.xml", "conteúdo", "text/plain")]);

        var (auditoria, dados) = mapeador.ParaCasoDeUso(sofia);

        Assert.Equal("doc-1", auditoria.Arquivos[0].IdDocumento);
        Assert.Equal("conteúdo", Encoding.UTF8.GetString(auditoria.Arquivos[0].Conteudo));
        Assert.Equal("Pergunta?", auditoria.Pergunta);
        Assert.Equal(CanalExecucao.A2A, auditoria.Canal);
        Assert.Equal(dados.IdSolicitacao, sofia.IdSolicitacao);
        Assert.StartsWith("nome-sha256:", auditoria.Identificacao.UsuarioId, StringComparison.Ordinal);
        Assert.Equal("João  DA  Silva", auditoria.Identificacao.UsuarioNome);
    }

    [Fact]
    public void Resultado_de_envelope_com_erro_de_contrato_valida_no_schema()
    {
        var idExec = Guid.CreateVersion7();
        var idSol = Guid.CreateVersion7();
        var envelope = MontadorEnvelope.Rejeitada(
            Guid.Empty,
            IdentificacaoSolicitante.Criar("u", "sofia"),
            CanalExecucao.A2A,
            DateTimeOffset.UtcNow,
            CodigoErro.QuantidadeArquivosExcedida,
            MensagensErro.Para(CodigoErro.QuantidadeArquivosExcedida),
            correlacaoId: idSol.ToString("D"));
        var resultado = CriarSaida().DeEnvelope(envelope, idExec, idSol);
        Assert.Equal("erro", resultado.Status);
        Assert.Equal(idExec, resultado.IdExecucao);
        Assert.Equal(idSol, resultado.IdSolicitacao);
        Assert.NotNull(resultado.Erro);
        ValidarSchema(resultado);
    }

    [Fact]
    public void Resultado_de_envelope_em_andamento_nao_expoe_id_interno()
    {
        var envelope = MontadorEnvelope.Rejeitada(
            Guid.CreateVersion7(),
            IdentificacaoSolicitante.Criar("u", "sofia"),
            CanalExecucao.A2A,
            DateTimeOffset.UtcNow,
            CodigoErro.SolicitacaoEmAndamento,
            MensagensErro.Para(CodigoErro.SolicitacaoEmAndamento));
        var resultado = CriarSaida().DeEnvelope(envelope, Guid.CreateVersion7(), Guid.CreateVersion7(), incluirIdInterno: false);
        Assert.Equal("erro", resultado.Status);
        Assert.DoesNotContain(resultado.Observacoes ?? [], o => o.Contains("Identificador interno", StringComparison.Ordinal));
    }

    private static MapeadorSolicitacaoSofia CriarEntrada() =>
        new(Options.Create(new A2AOpcoes { UrlPublica = "http://localhost", IdAgente = "agente-auditoria-nfe", ChamadorPadrao = "sofia" }));

    private static MapeadorResultadoSofia CriarSaida() =>
        new(Options.Create(new A2AOpcoes { UrlPublica = "http://localhost", IdAgente = "agente-auditoria-nfe", ChamadorPadrao = "sofia" }));

    private static void ValidarSchema(ResultadoAnaliseSofia resultado)
    {
        var json = JsonSerializer.Serialize(resultado, ContratoSofiaV1.Json);
        using var elemento = JsonDocument.Parse(json);
        var esquema = EsquemasSofiaTeste.ResultadoAnalise;
        var avaliacao = esquema.Evaluate(elemento.RootElement);
        Assert.True(avaliacao.IsValid, string.Join("; ", avaliacao.Errors?.Select(e => e.Value) ?? []));
    }
}
