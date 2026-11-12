using Api.RAG.Servicos;

namespace AuditoriaNFe.Tests.Rag;

public sealed class ExtraidorTextoPdfTests
{
    [Fact]
    public void Bytes_vazios_retorna_null()
    {
        var extrator = new ExtraidorTextoPdf();
        Assert.Null(extrator.Extrair([]));
    }

    [Fact]
    public void Pdf_invalido_retorna_null()
    {
        var extrator = new ExtraidorTextoPdf();
        Assert.Null(extrator.Extrair([0x25, 0x50, 0x44, 0x46, 0x00]));
    }

    [Fact]
    public void Pdf_minimo_extrai_texto()
    {
        var bytes = PdfMinimoComTexto.Gerar("ICMSTot vNF W16-10");
        var extrator = new ExtraidorTextoPdf();
        var texto = extrator.Extrair(bytes);
        Assert.NotNull(texto);
        Assert.Contains("W16-10", texto, StringComparison.Ordinal);
    }
}
