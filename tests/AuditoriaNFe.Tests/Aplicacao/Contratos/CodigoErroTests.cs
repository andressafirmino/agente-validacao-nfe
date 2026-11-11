using AuditoriaNFe.Application.Contratos;

namespace AuditoriaNFe.Tests.Aplicacao.Contratos;

public sealed class CodigoErroTests
{
    [Fact]
    public void Codigos_vao_de_ERR_001_a_ERR_027_sem_lacunas()
    {
        var esperados = Enumerable.Range(1, 27).Select(n => $"ERR-{n:000}");

        Assert.Equal(esperados, CodigoErro.Todos);
    }

    [Fact]
    public void Todo_codigo_tem_mensagem()
    {
        Assert.All(CodigoErro.Todos, codigo => Assert.False(string.IsNullOrWhiteSpace(MensagensErro.Para(codigo))));
        Assert.Throws<ArgumentOutOfRangeException>(() => MensagensErro.Para("ERR-999"));
    }

    [Fact]
    public void ERR_004_e_ERR_019_ficam_reservados_pelo_D05()
    {
        Assert.Equal(["ERR-004", "ERR-019"], CodigoErro.Reservados.Order());
    }

    [Fact]
    public void Mensagens_com_parametros_informam_limites()
    {
        Assert.Contains("5.242.880", MensagensErro.TamanhoExcedido(5_242_880, 6_000_000));
        Assert.Contains("20", MensagensErro.QuantidadeArquivosExcedida(20));
        Assert.Contains("42", MensagensErro.LimiteAtingido(42));
    }
}
