using Api.Agente.Identificacao;
using AuditoriaNFe.Domain.Identificacao;
using Microsoft.AspNetCore.Http;

namespace AuditoriaNFe.Tests.Contrato;

public sealed class LeitorIdentificacaoHttpTests
{
    [Fact]
    public void Sem_cabecalhos_usa_anonimo_e_nao_informado()
    {
        Assert.True(LeitorIdentificacaoHttp.TentarLer(Requisicao(), out var identificacao));

        Assert.Equal(IdentificacaoSolicitante.UsuarioAnonimo, identificacao.UsuarioId);
        Assert.Equal(IdentificacaoSolicitante.ChamadorNaoInformado, identificacao.ChamadorId);
        Assert.False(identificacao.UsuarioIdentificado);
    }

    [Fact]
    public void Le_usuario_e_chamador_informados()
    {
        var requisicao = Requisicao(("X-Usuario-Id", "usuario.teste@exemplo"), ("X-Chamador-Id", "sofia"));

        Assert.True(LeitorIdentificacaoHttp.TentarLer(requisicao, out var identificacao));

        Assert.Equal("usuario.teste@exemplo", identificacao.UsuarioId);
        Assert.Equal("sofia", identificacao.ChamadorId);
        Assert.True(identificacao.UsuarioIdentificado);
    }

    [Fact]
    public void Com_nome_sem_id_usa_hash_como_no_A2A()
    {
        const string Nome = "Maria Silva";
        var requisicao = Requisicao(
            (LeitorIdentificacaoHttp.CabecalhoUsuarioNome, Nome),
            (LeitorIdentificacaoHttp.CabecalhoChamador, "teste-vercel"));

        Assert.True(LeitorIdentificacaoHttp.TentarLer(requisicao, out var identificacao));
        Assert.True(IdentificacaoSolicitante.TentarCriarPorNome(Nome, "teste-vercel", out var esperada));
        Assert.Equal(esperada.UsuarioId, identificacao.UsuarioId);
        Assert.Equal(esperada.ChaveIsolamento, identificacao.ChaveIsolamento);
        Assert.Equal(Nome.Trim(), identificacao.UsuarioNome);
        Assert.True(identificacao.UsuarioIdentificado);
    }

    [Fact]
    public void Id_informado_prevalece_sobre_nome()
    {
        var requisicao = Requisicao(
            (LeitorIdentificacaoHttp.CabecalhoUsuario, "homolog-01"),
            (LeitorIdentificacaoHttp.CabecalhoUsuarioNome, "Maria Silva"),
            (LeitorIdentificacaoHttp.CabecalhoChamador, "teste-vercel"));

        Assert.True(LeitorIdentificacaoHttp.TentarLer(requisicao, out var identificacao));
        Assert.Equal("homolog-01", identificacao.UsuarioId);
        Assert.Null(identificacao.UsuarioNome);
    }

    [Theory]
    [InlineData("X-Usuario-Id", "usuário com espaço")]
    [InlineData("X-Chamador-Id", "sofia/producao")]
    [InlineData("X-Usuario-Id", "<script>")]
    public void Formato_invalido_e_rejeitado(string cabecalho, string valor)
    {
        Assert.False(LeitorIdentificacaoHttp.TentarLer(Requisicao((cabecalho, valor)), out _));
    }

    [Fact]
    public void Identificador_acima_de_128_caracteres_e_rejeitado()
    {
        Assert.False(LeitorIdentificacaoHttp.TentarLer(Requisicao(("X-Usuario-Id", new string('a', 129))), out _));
    }

    [Fact]
    public void Cabecalho_repetido_e_rejeitado()
    {
        var requisicao = new DefaultHttpContext().Request;
        requisicao.Headers.Append("X-Usuario-Id", new[] { "usuario-1", "usuario-2" });

        Assert.False(LeitorIdentificacaoHttp.TentarLer(requisicao, out _));
    }

    private static HttpRequest Requisicao(params (string Nome, string Valor)[] cabecalhos)
    {
        var requisicao = new DefaultHttpContext().Request;
        foreach (var (nome, valor) in cabecalhos)
        {
            requisicao.Headers[nome] = valor;
        }

        return requisicao;
    }
}
