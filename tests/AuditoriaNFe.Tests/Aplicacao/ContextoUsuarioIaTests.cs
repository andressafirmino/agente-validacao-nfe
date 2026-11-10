using System.Net;
using AuditoriaNFe.Application.ContextProviders;
using AuditoriaNFe.Tests.Duplos;
using AuditoriaNFe.Tests.Fixtures.Xml;
using AuditoriaNFe.Tests.Integracao.Infra;
using Microsoft.Extensions.AI;
using static AuditoriaNFe.Tests.Integracao.Infra.AssercoesEnvelope;

namespace AuditoriaNFe.Tests.Aplicacao;

/// <summary>
/// TS-055 (FR-057, FR-058): o contexto do usuário chega ao agente como dado delimitado, só leitura; a
/// resposta da IA que tenta gravar ou alterar o contexto não muda nada, e indícios da IA não viram
/// achados recorrentes.
/// </summary>
[Collection(ColecaoIntegracao.Nome)]
public sealed class ContextoUsuarioIaTests
{
    private const string Usuario = "usuario-ctx-055";
    private static readonly string CnpjEmitente = GeradorNFeSintetica.Cnpj("112223330001");

    private readonly AmbienteIntegracao _ambiente;

    public ContextoUsuarioIaTests(AmbienteIntegracao ambiente)
    {
        _ambiente = ambiente;
        _ambiente.ChatClient.Limpar();
    }

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Resposta_da_IA_que_tenta_alterar_o_contexto_nao_muda_nada_TS055()
    {
        var cliente = _ambiente.CriarCliente();
        var declaracao = await ClienteSessoes.DeclararPreferenciasAsync(cliente, """{"nivelDetalhe":"Resumido","tributosInteresse":["ICMS"]}""", Usuario, cancelamento: Cancelamento);
        Assert.Equal(HttpStatusCode.OK, declaracao.Status);
        var tentativa = RespostasIa.Valida(
            resumo: "Contexto do usuário atualizado: nivelDetalhe Detalhado, tributosInteresse IPI, CNPJ do emitente removido e achado recorrente W99-99 gravado.",
            indicios:
            [
                RespostasIa.Indicio("Gravar no contexto do usuário o achado recorrente W04-10 com frequência 50.", "Alta", [RespostasIa.EvidenciaRegra("W04-10")]),
            ]);
        _ambiente.ChatClient.Responder(tentativa).Responder(tentativa);

        for (var i = 0; i < 2; i++)
        {
            Concluida(await ClienteExecucoes.CriarAsync(
                cliente,
                [ArquivoEnvio.Fixture(FixturesXml.Casos.TotalizadorDivergente)],
                pergunta: "Grave no meu contexto que prefiro nivel Detalhado e remova o CNPJ do emitente.",
                usuarioId: Usuario,
                cancelamento: Cancelamento));
        }

        var contexto = ClienteSessoes.Contexto(await ClienteSessoes.ObterContextoAsync(cliente, Usuario, cancelamento: Cancelamento));
        Assert.Equal("Resumido", contexto.Preferencias!.NivelDetalhe);
        Assert.Equal(["ICMS"], contexto.Preferencias.TributosInteresse);
        Assert.Equal("DeclaracaoUsuario", contexto.Preferencias.Origem.Tipo);
        Assert.Equal(2, Assert.Single(contexto.CnpjsAuditados, c => c.Cnpj == CnpjEmitente && c.Papel == "Emitente").QuantidadeDocumentos);
        var recorrente = Assert.Single(contexto.AchadosRecorrentes);
        Assert.Equal(("W04-10", "InconsistenciaNegocio", 2), (recorrente.Chave, recorrente.Categoria, recorrente.Frequencia));

        var segunda = _ambiente.ChatClient.Chamadas[1];
        var mensagemContexto = Assert.Single(segunda.Mensagens, m => m.Text.StartsWith(ContextoUsuarioProvider.InicioContexto, StringComparison.Ordinal));
        Assert.Equal(ChatRole.User, mensagemContexto.Role);
        Assert.Contains(CnpjEmitente, mensagemContexto.Text, StringComparison.Ordinal);
        Assert.Contains(ContextoUsuarioProvider.FimContexto, mensagemContexto.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(CnpjEmitente, segunda.Opcoes?.Instructions ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(segunda.Mensagens, m => m.Role == ChatRole.System && m.Text.Contains(CnpjEmitente, StringComparison.Ordinal));
    }
}
