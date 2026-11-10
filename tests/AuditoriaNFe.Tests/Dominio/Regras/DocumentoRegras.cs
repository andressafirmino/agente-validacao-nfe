using System.Globalization;
using AuditoriaNFe.Domain.Documentos;
using AuditoriaNFe.Domain.Regras;

namespace AuditoriaNFe.Tests.Dominio.Regras;

/// <summary>Monta os <see cref="DadosDocumento"/> das regras no padrão de caminhos relativo a <c>infNFe</c>.</summary>
internal sealed class DocumentoRegras(DateOnly dataReferencia)
{
    public const string Arquivo = "nfe-regras.xml";
    public const string Chave = "35261112345678000195550010000001231000001230";

    private readonly List<CampoExtraido> _campos = [];
    private int _itens;

    public DocumentoRegras Campo(string caminho, string valor)
    {
        _campos.Add(new CampoExtraido(caminho, valor));
        return this;
    }

    public DocumentoRegras Campo(string caminho, decimal valor) => Campo(caminho, Texto(valor));

    /// <summary>Campo do grupo <c>total</c>, por exemplo <c>ICMSTot/vNF</c>.</summary>
    public DocumentoRegras Total(string caminho, decimal valor) => Campo("total/" + caminho, valor);

    public DocumentoRegras Item(Action<ItemRegras> configurar)
    {
        _itens++;
        configurar(new ItemRegras(this, _itens));
        return this;
    }

    public bool Tem(string caminho) => _campos.Any(c => c.Caminho == caminho);

    public DadosDocumento Construir() => new(Arquivo, Chave, dataReferencia, _campos);

    public static string Texto(decimal valor) => valor.ToString(CultureInfo.InvariantCulture);
}

internal sealed class ItemRegras(DocumentoRegras documento, int numero)
{
    public int Numero => numero;

    public ItemRegras Campo(string caminho, string valor)
    {
        documento.Campo($"det[{numero}]/{caminho}", valor);
        return this;
    }

    public ItemRegras Campo(string caminho, decimal valor) => Campo(caminho, DocumentoRegras.Texto(valor));

    public ItemRegras Prod(string campo, decimal valor) => Campo("prod/" + campo, valor);

    public ItemRegras Prod(string campo, string valor) => Campo("prod/" + campo, valor);

    /// <summary>Grupo de ICMS do item (por exemplo <c>ICMS00</c>) com o CST.</summary>
    public ItemRegras Icms(string grupo, string cst, params (string Campo, decimal Valor)[] valores)
    {
        Campo($"imposto/ICMS/{grupo}/CST", cst);
        foreach (var (campo, valor) in valores)
        {
            Campo($"imposto/ICMS/{grupo}/{campo}", valor);
        }

        return this;
    }

    public ItemRegras Issqn(params (string Campo, decimal Valor)[] valores)
    {
        Campo("imposto/ISSQN/cListServ", "01.01");
        foreach (var (campo, valor) in valores)
        {
            Campo($"imposto/ISSQN/{campo}", valor);
        }

        return this;
    }

    /// <summary>Campo do grupo <c>imposto/IBSCBS</c> do item, com CST e cClassTrib.</summary>
    public ItemRegras IbsCbs(string caminho, decimal valor)
    {
        if (!TemCampo("imposto/IBSCBS/CST"))
        {
            Campo("imposto/IBSCBS/CST", "000");
            Campo("imposto/IBSCBS/cClassTrib", "000001");
        }

        return Campo("imposto/IBSCBS/" + caminho, valor);
    }

    public ItemRegras Is(decimal vIS)
    {
        Campo("imposto/IS/CSTIS", "000");
        Campo("imposto/IS/cClassTribIS", "000001");
        return Campo("imposto/IS/vIS", vIS);
    }

    private bool TemCampo(string caminho) => documento.Tem($"det[{numero}]/{caminho}");
}
