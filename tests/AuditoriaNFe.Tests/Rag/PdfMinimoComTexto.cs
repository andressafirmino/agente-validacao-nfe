namespace AuditoriaNFe.Tests.Rag;

/// <summary>PDF mínimo com uma linha de texto (para testes de ingestão).</summary>
internal static class PdfMinimoComTexto
{
    public static byte[] Gerar(string linha)
    {
        var escapado = linha.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal);
        var stream = $"""
            BT /F1 12 Tf 72 720 Td ({escapado}) Tj ET
            """;
        var comprimento = stream.Length;
        var pdf = $"""
            %PDF-1.4
            1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj
            2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj
            3 0 obj<</Type/Page/MediaBox[0 0 612 792]/Parent 2 0 R/Contents 4 0 R/Resources<</Font<</F1 5 0 R>>>>>>endobj
            4 0 obj<</Length {comprimento}>>stream
            {stream}
            endstream
            endobj
            5 0 obj<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>endobj
            xref
            0 6
            0000000000 65535 f 
            0000000009 00000 n 
            0000000058 00000 n 
            0000000115 00000 n 
            0000000270 00000 n 
            0000000360 00000 n 
            trailer<</Size 6/Root 1 0 R>>
            startxref
            430
            %%EOF
            """;
        return System.Text.Encoding.ASCII.GetBytes(pdf);
    }
}
