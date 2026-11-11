using System.Globalization;
using AuditoriaNFe.Domain.Documentos;

namespace AuditoriaNFe.Domain.Calculos;

/// <summary>Somas determinísticas por campo do catálogo de extração (FR-031).</summary>
public static class CalculadoraBase
{
    public static RegistroCalculo SomarCampo(
        Guid idCalculo,
        string campo,
        IReadOnlyList<EntradaDocumentoCalculo> documentos,
        IReadOnlySet<string>? chavesAcessoFiltro,
        bool incluirComErroEstrutural,
        DateTimeOffset registradoEm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campo);
        ArgumentNullException.ThrowIfNull(documentos);

        var incluidos = new List<DocumentoIncluidoCalculo>();
        var excluidos = new List<DocumentoExcluidoCalculo>();
        decimal soma = 0;

        foreach (var documento in documentos)
        {
            if (documento.Situacao is MotivoExclusaoCalculo.Duplicado or MotivoExclusaoCalculo.NaoSuportado)
            {
                excluidos.Add(new DocumentoExcluidoCalculo(documento.ChaveAcesso, documento.Situacao.Value));
                continue;
            }

            if (chavesAcessoFiltro is not null && !chavesAcessoFiltro.Contains(documento.ChaveAcesso))
            {
                excluidos.Add(new DocumentoExcluidoCalculo(documento.ChaveAcesso, MotivoExclusaoCalculo.ForaDoFiltro));
                continue;
            }

            if (documento.Situacao == MotivoExclusaoCalculo.ErroEstrutural)
            {
                if (!incluirComErroEstrutural)
                {
                    excluidos.Add(new DocumentoExcluidoCalculo(documento.ChaveAcesso, MotivoExclusaoCalculo.ErroEstrutural));
                    continue;
                }

                if (!TentarObterDecimal(documento.Campos, campo, out var valor))
                {
                    excluidos.Add(new DocumentoExcluidoCalculo(documento.ChaveAcesso, MotivoExclusaoCalculo.ErroEstrutural));
                    continue;
                }

                soma += valor;
                incluidos.Add(new DocumentoIncluidoCalculo(documento.ChaveAcesso, ComRessalva: true));
                continue;
            }

            if (!TentarObterDecimal(documento.Campos, campo, out var valorValido))
            {
                excluidos.Add(new DocumentoExcluidoCalculo(documento.ChaveAcesso, MotivoExclusaoCalculo.NaoSuportado));
                continue;
            }

            soma += valorValido;
            incluidos.Add(new DocumentoIncluidoCalculo(documento.ChaveAcesso, ComRessalva: false));
        }

        return new RegistroCalculo(
            idCalculo,
            OperacaoCalculo.Soma,
            campo,
            incluidos,
            excluidos,
            soma,
            registradoEm);
    }

    private static bool TentarObterDecimal(IReadOnlyList<CampoExtraido> campos, string campo, out decimal valor)
    {
        valor = 0;
        var extraido = campos.FirstOrDefault(c => string.Equals(c.Caminho, campo, StringComparison.Ordinal));
        if (extraido is null)
        {
            return false;
        }

        return decimal.TryParse(extraido.Valor, NumberStyles.Number, CultureInfo.InvariantCulture, out valor)
            || decimal.TryParse(extraido.Valor, NumberStyles.Number, new CultureInfo("pt-BR"), out valor);
    }
}
