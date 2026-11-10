using System.Net;
using AuditoriaNFe.Application.ContextProviders;
using AuditoriaNFe.Application.Contratos;
using AuditoriaNFe.Domain.Identificacao;
using AuditoriaNFe.Domain.Parecer;
using AuditoriaNFe.Tests.Duplos;
using AuditoriaNFe.Tests.Fixtures.Xml;
using AuditoriaNFe.Tests.Integracao.Infra;
using MongoDB.Bson;
using MongoDB.Driver;
using static AuditoriaNFe.Tests.Integracao.Infra.AssercoesEnvelope;

namespace AuditoriaNFe.Tests.Integracao;

/// <summary>
/// Contexto do usuário pelo canal direto (FR-055 a FR-060): isolamento por usuário e chamador,
/// exclusão, prazo de inatividade, usuário não identificado e o <c>$jsonSchema</c> da coleção.
/// </summary>
[Collection(ColecaoIntegracao.Nome)]
public sealed class ContextoUsuarioTests
{
    private static readonly string CnpjEmitente = GeradorNFeSintetica.Cnpj("112223330001");
    private static readonly string CnpjDestinatario = GeradorNFeSintetica.Cnpj("445556660001");

    private readonly AmbienteIntegracao _ambiente;

    public ContextoUsuarioTests(AmbienteIntegracao ambiente)
    {
        _ambiente = ambiente;
        _ambiente.ChatClient.Limpar();
    }

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private async Task AnalisarAsync(HttpClient cliente, string? usuario, string? chamador = ClienteExecucoes.ChamadorPadrao)
    {
        _ambiente.ChatClient.Responder(RespostasIa.Valida());
        Concluida(await ClienteExecucoes.CriarAsync(cliente, [ArquivoEnvio.Fixture(NFeValida)], usuarioId: usuario, chamadorId: chamador, cancelamento: Cancelamento));
    }

    private bool UltimaChamadaTemContextoDoUsuario() =>
        _ambiente.ChatClient.Chamadas[^1].Mensagens.Any(m => m.Text.StartsWith(ContextoUsuarioProvider.InicioContexto, StringComparison.Ordinal));

    [Fact]
    public async Task Contexto_de_outro_usuario_ou_chamador_nao_e_revelado_TS054_SC012()
    {
        const string Dono = "usuario-ctx-054";
        var cliente = _ambiente.CriarCliente();
        await AnalisarAsync(cliente, Dono);

        var doDono = await ClienteSessoes.ObterContextoAsync(cliente, Dono, cancelamento: Cancelamento);
        var outroUsuario = await ClienteSessoes.ObterContextoAsync(cliente, "outro-usuario-054", cancelamento: Cancelamento);
        var outroChamador = await ClienteSessoes.ObterContextoAsync(cliente, Dono, "outro-chamador", Cancelamento);
        await ClienteSessoes.RemoverCnpjAsync(cliente, CnpjEmitente, null, Dono, "outro-chamador", Cancelamento);
        await ClienteSessoes.ExcluirContextoAsync(cliente, "outro-usuario-054", cancelamento: Cancelamento);
        await AnalisarAsync(cliente, Dono, "outro-chamador");
        var semVazamento = !UltimaChamadaTemContextoDoUsuario();

        Assert.Equal(HttpStatusCode.OK, doDono.Status);
        var contexto = ClienteSessoes.Contexto(doDono);
        Assert.Equal((Dono, ClienteExecucoes.ChamadorPadrao), (contexto.UsuarioId, contexto.ChamadorId));
        Assert.Equal(1, Assert.Single(contexto.CnpjsAuditados, c => c.Cnpj == CnpjEmitente && c.Papel == "Emitente").QuantidadeDocumentos);
        Assert.Contains(contexto.CnpjsAuditados, c => c.Cnpj == CnpjDestinatario && c.Papel == "Destinatario");
        Assert.All(contexto.CnpjsAuditados, c => Assert.Equal("Execucao", c.Origem.Tipo));
        foreach (var alheio in new[] { outroUsuario, outroChamador })
        {
            Assert.Equal(HttpStatusCode.NotFound, alheio.Status);
            Assert.Equal(CodigoErro.SessaoNaoEncontrada, alheio.ErroResposta.Erro.Codigo);
            Assert.Equal(MensagensErro.ContextoUsuarioNaoEncontrado, alheio.ErroResposta.Erro.Mensagem);
        }

        Assert.True(semVazamento);
        var depois = ClienteSessoes.Contexto(await ClienteSessoes.ObterContextoAsync(cliente, Dono, cancelamento: Cancelamento));
        Assert.Equal(contexto.CnpjsAuditados, depois.CnpjsAuditados);
    }

    [Fact]
    public async Task Exclusao_pelo_canal_direto_deixa_o_contexto_indisponivel_TS056()
    {
        const string Usuario = "usuario-ctx-056";
        var cliente = _ambiente.CriarCliente();
        await AnalisarAsync(cliente, Usuario);
        await AnalisarAsync(cliente, Usuario);
        Assert.True(UltimaChamadaTemContextoDoUsuario());

        var exclusao = await ClienteSessoes.ExcluirContextoAsync(cliente, Usuario, cancelamento: Cancelamento);
        var consulta = await ClienteSessoes.ObterContextoAsync(cliente, Usuario, cancelamento: Cancelamento);
        await AnalisarAsync(cliente, Usuario);

        Assert.Equal(HttpStatusCode.NoContent, exclusao.Status);
        Assert.Equal(HttpStatusCode.NotFound, consulta.Status);
        Assert.False(UltimaChamadaTemContextoDoUsuario());
        var recomecado = ClienteSessoes.Contexto(await ClienteSessoes.ObterContextoAsync(cliente, Usuario, cancelamento: Cancelamento));
        Assert.All(recomecado.CnpjsAuditados, c => Assert.Equal(1, c.QuantidadeDocumentos));
    }

    [Fact]
    public async Task Prazo_vencido_ignora_o_contexto_e_nova_execucao_cria_um_vazio_TS058()
    {
        const string Usuario = "usuario-ctx-058";
        var cliente = _ambiente.CriarFabrica(configuracoes: new Dictionary<string, string?> { ["ContextoUsuario:RetencaoInatividade"] = "1.00:00:00" }).CreateClient();
        var inicio = _ambiente.Relogio.Agora;
        await AnalisarAsync(cliente, Usuario);
        await AnalisarAsync(cliente, Usuario);
        Assert.True(UltimaChamadaTemContextoDoUsuario());

        try
        {
            _ambiente.Relogio.Avancar(TimeSpan.FromDays(1) + TimeSpan.FromMinutes(1));
            var vencido = await ClienteSessoes.ObterContextoAsync(cliente, Usuario, cancelamento: Cancelamento);
            await AnalisarAsync(cliente, Usuario);

            Assert.Equal(HttpStatusCode.NotFound, vencido.Status);
            Assert.False(UltimaChamadaTemContextoDoUsuario());
            var novo = ClienteSessoes.Contexto(await ClienteSessoes.ObterContextoAsync(cliente, Usuario, cancelamento: Cancelamento));
            Assert.Equal(1, Assert.Single(novo.CnpjsAuditados, c => c.Papel == "Emitente").QuantidadeDocumentos);
            Assert.Equal(_ambiente.Relogio.Agora.AddDays(1), novo.ExpiraEm);
        }
        finally
        {
            _ambiente.Relogio.Definir(inicio);
        }
    }

    [Fact]
    public async Task Usuario_nao_identificado_nao_le_nem_grava_contexto_TS059()
    {
        var colecao = BancoMongoTeste.Colecao(_ambiente.Mongo, "contextos_usuario");
        var antes = await colecao.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: Cancelamento);
        var cliente = _ambiente.CriarCliente();
        _ambiente.ChatClient.Responder(RespostasIa.Valida());

        var envelope = Concluida(await ClienteExecucoes.CriarAsync(cliente, [ArquivoEnvio.Fixture(NFeValida)], usuarioId: null, chamadorId: null, cancelamento: Cancelamento));
        var consulta = await ClienteSessoes.ObterContextoAsync(cliente, null, null, Cancelamento);
        var preferencias = await ClienteSessoes.DeclararPreferenciasAsync(cliente, """{"nivelDetalhe":"Resumido","tributosInteresse":["ICMS"]}""", null, null, Cancelamento);

        Assert.False(envelope.UsuarioIdentificado);
        Assert.Contains(LimitacoesParecer.UsuarioNaoIdentificado, envelope.Parecer!.Limitacoes);
        Assert.False(UltimaChamadaTemContextoDoUsuario());
        Assert.Equal(HttpStatusCode.NotFound, consulta.Status);
        Assert.Equal(HttpStatusCode.BadRequest, preferencias.Status);
        Assert.Equal(CodigoErro.MensagemForaDoContrato, preferencias.ErroResposta.Erro.Codigo);
        Assert.Equal(antes, await colecao.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: Cancelamento));
        Assert.Equal(0, await colecao.CountDocumentsAsync(new BsonDocument("usuarioId", IdentificacaoSolicitante.UsuarioAnonimo), cancellationToken: Cancelamento));
    }

    [Theory]
    [InlineData("""{"nivelDetalhe":"Detalhado","tributosInteresse":["ICMS","CPF"]}""")]
    [InlineData("""{"nivelDetalhe":"Detalhado","tributosInteresse":["ICMS","ICMS"]}""")]
    [InlineData("""{"nivelDetalhe":"1","tributosInteresse":[]}""")]
    [InlineData("""{"nivelDetalhe":"Detalhado","tributosInteresse":[],"observacao":"texto livre"}""")]
    [InlineData("""{"nivelDetalhe":"Detalhado"}""")]
    [InlineData("""[]""")]
    public async Task Preferencia_fora_da_lista_e_recusada_com_ERR007(string corpo)
    {
        var resposta = await ClienteSessoes.DeclararPreferenciasAsync(_ambiente.CriarCliente(), corpo, "usuario-ctx-pref", cancelamento: Cancelamento);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.Status);
        Assert.Equal(CodigoErro.MensagemForaDoContrato, resposta.ErroResposta.Erro.Codigo);
    }

    [Fact]
    public async Task Preferencias_declaradas_e_correcoes_pelo_canal_direto_FR059()
    {
        const string Usuario = "usuario-ctx-059";
        var cliente = _ambiente.CriarCliente();
        _ambiente.ChatClient.Responder(RespostasIa.Valida());
        Concluida(await ClienteExecucoes.CriarAsync(cliente, [ArquivoEnvio.Fixture(FixturesXml.Casos.TotalizadorDivergente)], usuarioId: Usuario, cancelamento: Cancelamento));

        var declaracao = await ClienteSessoes.DeclararPreferenciasAsync(cliente, """{"nivelDetalhe":"Detalhado","tributosInteresse":["ICMS","ICMS-ST"]}""", Usuario, cancelamento: Cancelamento);
        var remocaoCnpj = await ClienteSessoes.RemoverCnpjAsync(cliente, CnpjDestinatario, "Destinatario", Usuario, cancelamento: Cancelamento);
        var remocaoAchado = await ClienteSessoes.RemoverAchadoRecorrenteAsync(cliente, "W04-10", Usuario, cancelamento: Cancelamento);
        var inexistente = await ClienteSessoes.RemoverAchadoRecorrenteAsync(cliente, "W99-99", Usuario, cancelamento: Cancelamento);

        Assert.Equal(HttpStatusCode.OK, declaracao.Status);
        var declarado = ClienteSessoes.Contexto(declaracao);
        Assert.Equal("Detalhado", declarado.Preferencias!.NivelDetalhe);
        Assert.Equal(["ICMS", "ICMS-ST"], declarado.Preferencias.TributosInteresse);
        Assert.Equal("DeclaracaoUsuario", declarado.Preferencias.Origem.Tipo);
        Assert.Contains(declarado.AchadosRecorrentes, a => a.Chave == "W04-10" && a.Categoria == "InconsistenciaNegocio");
        Assert.All(new[] { remocaoCnpj, remocaoAchado, inexistente }, r => Assert.Equal(HttpStatusCode.NoContent, r.Status));

        var corrigido = ClienteSessoes.Contexto(await ClienteSessoes.ObterContextoAsync(cliente, Usuario, cancelamento: Cancelamento));
        Assert.DoesNotContain(corrigido.CnpjsAuditados, c => c.Cnpj == CnpjDestinatario);
        Assert.Contains(corrigido.CnpjsAuditados, c => c.Cnpj == CnpjEmitente);
        Assert.Empty(corrigido.AchadosRecorrentes);
    }

    [Fact]
    public async Task JsonSchema_recusa_campo_fora_do_data_model()
    {
        var colecao = BancoMongoTeste.Colecao(_ambiente.Mongo, "contextos_usuario");
        var identificacao = IdentificacaoSolicitante.Criar("usuario-ctx-schema", "sofia");

        await Assert.ThrowsAsync<MongoWriteException>(() =>
            colecao.InsertOneAsync(Valido(identificacao).Add("cpf", "12345678909"), cancellationToken: Cancelamento));
        await Assert.ThrowsAsync<MongoWriteException>(() =>
            colecao.InsertOneAsync(Valido(identificacao).Set("usuarioId", IdentificacaoSolicitante.UsuarioAnonimo), cancellationToken: Cancelamento));
        var comValorDeDocumento = Valido(identificacao);
        comValorDeDocumento["cnpjsAuditados"] = new BsonArray
        {
            new BsonDocument
            {
                ["cnpj"] = CnpjEmitente,
                ["papel"] = "Emitente",
                ["quantidadeDocumentos"] = 1,
                ["ultimaAuditoriaEm"] = DateTime.UtcNow,
                ["origem"] = new BsonDocument { ["tipo"] = "Execucao", ["registradoEm"] = DateTime.UtcNow },
                ["vNF"] = "100.00",
            },
        };
        await Assert.ThrowsAsync<MongoWriteException>(() => colecao.InsertOneAsync(comValorDeDocumento, cancellationToken: Cancelamento));

        await colecao.InsertOneAsync(Valido(identificacao), cancellationToken: Cancelamento);
        await colecao.DeleteOneAsync(new BsonDocument("_id", identificacao.ChaveIsolamento), Cancelamento);
    }

    private static BsonDocument Valido(IdentificacaoSolicitante identificacao)
    {
        var agora = DateTime.UtcNow;
        return new BsonDocument
        {
            ["_id"] = identificacao.ChaveIsolamento,
            ["usuarioId"] = identificacao.UsuarioId,
            ["chamadorId"] = identificacao.ChamadorId,
            ["cnpjsAuditados"] = new BsonArray(),
            ["achadosRecorrentes"] = new BsonArray(),
            ["preferencias"] = BsonNull.Value,
            ["execucoesContabilizadas"] = new BsonArray(),
            ["criadoEm"] = agora,
            ["atualizadoEm"] = agora,
            ["ultimaAtividadeEm"] = agora,
            ["expiraEm"] = agora.AddDays(90),
        };
    }
}
