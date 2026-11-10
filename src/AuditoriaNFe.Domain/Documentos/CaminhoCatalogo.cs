namespace AuditoriaNFe.Domain.Documentos;

/// <summary>
/// Caminhos relativos a <c>infNFe</c> no padrão dos catálogos: segmentos separados por <c>/</c>,
/// <c>*</c> para qualquer segmento (grupo de escolha, como <c>ICMS00</c> ou <c>PISAliq</c>) e
/// <c>nome[*]</c> para qualquer ocorrência indexada, como <c>det[*]</c> ou <c>cobr/dup[*]</c>.
/// </summary>
public static class CaminhoCatalogo
{
    public static bool Corresponde(string padrao, string caminho)
    {
        ArgumentNullException.ThrowIfNull(padrao);
        ArgumentNullException.ThrowIfNull(caminho);

        var segmentosPadrao = padrao.Split('/');
        var segmentos = caminho.Split('/');
        if (segmentosPadrao.Length != segmentos.Length)
        {
            return false;
        }

        for (var i = 0; i < segmentos.Length; i++)
        {
            if (!SegmentoCorresponde(segmentosPadrao[i], segmentos[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SegmentoCorresponde(string padrao, string segmento)
    {
        if (padrao == "*")
        {
            return true;
        }

        if (!padrao.EndsWith("[*]", StringComparison.Ordinal))
        {
            return string.Equals(padrao, segmento, StringComparison.Ordinal);
        }

        var nome = padrao[..^3];
        return segmento.Length > nome.Length + 2
            && segmento.StartsWith(nome + "[", StringComparison.Ordinal)
            && segmento[^1] == ']'
            && segmento[(nome.Length + 1)..^1].All(char.IsAsciiDigit);
    }
}
