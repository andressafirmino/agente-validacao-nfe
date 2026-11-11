using System.Text;
using System.Text.Json;
using AuditoriaNFe.Application.Configuracao;
using AuditoriaNFe.Application.Contratos;
using Microsoft.Extensions.Options;

namespace Api.Agente.A2A.Sofia;

/// <summary>Validação do contrato SOFIA na borda (FR-043a; contrato §3.2).</summary>
public sealed class ValidadorSolicitacaoSofia(IOptions<LimitesOpcoes> limites)
{
    public ValidacaoSofia Validar(SolicitacaoAnaliseSofia solicitacao)
    {
        if (!string.Equals(solicitacao.VersaoContrato, ContratoSofiaV1.Versao, StringComparison.Ordinal))
        {
            return Invalida(CodigoErro.MensagemForaDoContrato);
        }

        if (string.IsNullOrWhiteSpace(solicitacao.Usuario.Nome) || solicitacao.Usuario.Nome.Trim().Length > 200)
        {
            return Invalida(CodigoErro.MensagemForaDoContrato);
        }

        if (string.IsNullOrWhiteSpace(solicitacao.Pergunta) || solicitacao.Pergunta.Length > 4_000)
        {
            return Invalida(CodigoErro.MensagemForaDoContrato);
        }

        var opcoes = limites.Value;
        if (solicitacao.Documentos.Count == 0)
        {
            return Invalida(CodigoErro.SolicitacaoSemConteudo);
        }

        if (solicitacao.Documentos.Count > opcoes.QuantidadeArquivos)
        {
            return Invalida(CodigoErro.QuantidadeArquivosExcedida);
        }

        long total = 0;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var doc in solicitacao.Documentos)
        {
            if (string.IsNullOrWhiteSpace(doc.IdDocumento) || doc.IdDocumento.Length > 128)
            {
                return Invalida(CodigoErro.MensagemForaDoContrato);
            }

            if (!ids.Add(doc.IdDocumento))
            {
                return Invalida(CodigoErro.MensagemForaDoContrato);
            }

            if (string.IsNullOrWhiteSpace(doc.Nome) || string.IsNullOrWhiteSpace(doc.Texto))
            {
                return Invalida(CodigoErro.MensagemForaDoContrato);
            }

            total += Encoding.UTF8.GetByteCount(doc.Texto);
        }

        if (total > opcoes.TamanhoTotalBytes)
        {
            return Invalida(CodigoErro.TamanhoExcedido);
        }

        return new ValidacaoSofia(true, null, null);
    }

    public static bool TentarDesserializar(string json, out SolicitacaoAnaliseSofia? solicitacao, out string? erro)
    {
        solicitacao = null;
        erro = null;
        try
        {
            solicitacao = JsonSerializer.Deserialize<SolicitacaoAnaliseSofia>(json, ContratoSofiaV1.Json);
            if (solicitacao is null)
            {
                erro = "JSON nulo.";
                return false;
            }

            return true;
        }
        catch (JsonException ex)
        {
            erro = ex.Message;
            return false;
        }
    }

    private static ValidacaoSofia Invalida(string codigo) => new(false, codigo, MensagensErro.Para(codigo));
}
