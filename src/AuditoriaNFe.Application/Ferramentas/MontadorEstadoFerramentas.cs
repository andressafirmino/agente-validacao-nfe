using System.Globalization;
using AuditoriaNFe.Domain.Calculos;
using AuditoriaNFe.Domain.Documentos;
using AuditoriaNFe.Domain.Sessoes;
using AuditoriaNFe.Domain.Validacao;

namespace AuditoriaNFe.Application.Ferramentas;

/// <summary>Monta o estado exposto às ferramentas a partir da sessão e da execução em andamento.</summary>
public static class MontadorEstadoFerramentas
{
    public static FerramentasEstadoExecucao Montar(
        IReadOnlyList<DocumentoFerramenta> documentos,
        IReadOnlyList<Achado> achados,
        IReadOnlyList<RegistroCalculo> calculos) =>
        new() { Documentos = documentos, Achados = achados, Calculos = calculos };

    public static DocumentoFerramenta DeArquivo(
        ArquivoRecebido arquivo,
        IReadOnlyList<CampoExtraido> campos,
        MotivoExclusaoCalculo? situacao = null)
    {
        var layout = arquivo.ValidacaoLayout;
        var conteudo = arquivo.ValidacaoConteudo;
        return new DocumentoFerramenta(
            arquivo.ChaveAcesso ?? string.Empty,
            arquivo.Nome,
            layout.DataReferencia,
            layout.OrigemDataReferencia?.ToString(),
            layout.PacoteAplicado,
            layout.Situacao.ToString(),
            layout.Motivo,
            conteudo.Situacao.ToString(),
            conteudo.Motivo,
            arquivo.DuplicadoDe,
            situacao ?? MotivoDeArquivo(arquivo),
            campos);
    }

    public static DocumentoFerramenta DePersistido(DocumentoNFe documento, ArquivoRecebido? arquivoAtual = null)
    {
        if (arquivoAtual is not null)
        {
            return DeArquivo(arquivoAtual, documento.DadosExtraidos);
        }

        return new DocumentoFerramenta(
            documento.ChaveAcesso,
            documento.ArquivoOrigem,
            documento.DataReferencia,
            documento.OrigemDataReferencia.ToString(),
            documento.PacoteSchema,
            SituacaoValidacao.Realizada.ToString(),
            null,
            SituacaoValidacao.Realizada.ToString(),
            null,
            null,
            null,
            documento.DadosExtraidos);
    }

    private static MotivoExclusaoCalculo? MotivoDeArquivo(ArquivoRecebido arquivo)
    {
        if (arquivo.DuplicadoDe is not null)
        {
            return MotivoExclusaoCalculo.Duplicado;
        }

        return arquivo.ValidacaoLayout.Situacao == SituacaoValidacao.NaoRealizada
            ? MotivoExclusaoCalculo.ErroEstrutural
            : null;
    }
}
