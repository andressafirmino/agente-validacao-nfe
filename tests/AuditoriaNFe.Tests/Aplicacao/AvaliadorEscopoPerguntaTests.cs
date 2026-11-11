using AuditoriaNFe.Application.Servicos;
using AuditoriaNFe.Domain.Sessoes;

namespace AuditoriaNFe.Tests.Aplicacao;

public sealed class AvaliadorEscopoPerguntaTests
{
    [Theory]
    [InlineData("Como fazer bolo?")]
    [InlineData("Qual a cotação do dólar hoje?")]
    [InlineData("Receita de brigadeiro")]
    public void Pergunta_generica_fora_do_escopo(string pergunta)
    {
        Assert.True(AvaliadorEscopoPergunta.DeveResponderForaDoEscopoDeterministico(TipoExecucao.Pergunta, pergunta));
    }

    [Theory]
    [InlineData("Qual o valor total da nota?")]
    [InlineData("Quais notas têm totalizador divergente?")]
    [InlineData("Há inconsistência de ICMS nesta NF-e?")]
    [InlineData("Validação do XML enviado")]
    public void Pergunta_sobre_nfe_permite_ia(string pergunta)
    {
        Assert.False(AvaliadorEscopoPergunta.DeveResponderForaDoEscopoDeterministico(TipoExecucao.Pergunta, pergunta));
    }

    [Fact]
    public void Analise_de_lote_nao_usa_resposta_deterministica()
    {
        Assert.False(AvaliadorEscopoPergunta.DeveResponderForaDoEscopoDeterministico(TipoExecucao.AnaliseLote, "Como fazer bolo?"));
    }
}
