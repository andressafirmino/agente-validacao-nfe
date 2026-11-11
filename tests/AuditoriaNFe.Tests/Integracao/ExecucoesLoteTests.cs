using System.Diagnostics;
using System.Net;
using AuditoriaNFe.Application.Contratos;
using AuditoriaNFe.Application.Fluxo;
using AuditoriaNFe.Infrastructure.Persistencia.Mongo;
using AuditoriaNFe.Tests.Duplos;
using AuditoriaNFe.Tests.Fixtures.Xml;
using AuditoriaNFe.Tests.Integracao.Infra;
using MongoDB.Bson;
using MongoDB.Driver;
using static AuditoriaNFe.Tests.Integracao.Infra.AssercoesEnvelope;

namespace AuditoriaNFe.Tests.Integracao;

/// <summary>
/// Análise de lote por <c>POST /api/v1/execucoes</c> no pipeline real (US1). Os erros por arquivo
/// (ERR-008 a ERR-012) aparecem no parecer com HTTP 200, com o código na descrição do achado ou no
/// motivo da validação não realizada do arquivo.
/// </summary>
[Collection(ColecaoIntegracao.Nome)]
public sealed class ExecucoesLoteTests
{
    private readonly AmbienteIntegracao _ambiente;

    public ExecucoesLoteTests(AmbienteIntegracao ambiente)
    {
        _ambiente = ambiente;
        _ambiente.ChatClient.Limpar();
    }

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private static string Nome(string caminho) => Path.GetFileName(caminho);

    private static ArquivoEnvio Sintetica(int numero) =>
        new($"nfe-{numero}.xml", GeradorNFeSintetica.GerarBytes(new OpcoesNFeSintetica { Numero = numero }));

    private async Task<EnvelopeV1> AnalisarAsync(params ArquivoEnvio[] arquivos)
    {
        _ambiente.ChatClient.Responder(RespostasIa.Valida());
        return Concluida(await ClienteExecucoes.CriarAsync(_ambiente.CriarCliente(), arquivos, cancelamento: Cancelamento));
    }

    [Fact]
    public async Task Lote_sem_documento_analisavel_nao_chama_ia()
    {
        _ambiente.ChatClient.Limpar();

        var envelope = Concluida(await ClienteExecucoes.CriarAsync(
            _ambiente.CriarCliente(),
            [ArquivoEnvio.Fixture(FixturesXml.Casos.EventoCancelamento), ArquivoEnvio.Fixture(FixturesXml.Casos.Malformado)],
            cancelamento: Cancelamento));

        Assert.Empty(_ambiente.ChatClient.Chamadas);
        Assert.Equal("NaoAplicavel", Etapa(envelope, 7).Estado);
        Assert.Equal(FluxoNoveEtapas.JustificativaIaSemDocumentos, Etapa(envelope, 7).Justificativa);
        Assert.Equal("NaoAplicavel", Etapa(envelope, 8).Estado);
        Assert.Null(envelope.Erro);
    }

    [Fact]
    public async Task Lote_valido_sem_inconsistencias_TS001()
    {
        var envelope = await AnalisarAsync(ArquivoEnvio.Fixture(NFeValida), Sintetica(901));

        Assert.Null(envelope.Erro);
        Assert.Equal("SemInconsistencias", envelope.Parecer!.ResultadoGeral);
        Assert.DoesNotContain(envelope.Parecer.Achados, a => a.Categoria is "ErroEstrutural" or "InconsistenciaNegocio" or "FalhaTecnica");
        Assert.Equal(new ResumoQuantitativoV1(2, 2, 0, 0), envelope.Parecer.ResumoQuantitativo);
        Assert.All(envelope.Parecer.Documentos, d =>
        {
            Assert.Equal("Realizada", d.ValidacaoLayout);
            Assert.Equal("Realizada", d.ValidacaoConteudo);
            Assert.Equal(FixturesXml.Casos.Pacote, d.PacoteAplicado);
        });
        Assert.All(envelope.Etapas, e => Assert.Contains(e.Estado, new[] { "Executada", "NaoAplicavel" }));
    }

    [Fact]
    public async Task Violacao_de_schema_tem_localizacao_e_os_demais_arquivos_sao_analisados_TS002()
    {
        var envelope = await AnalisarAsync(ArquivoEnvio.Fixture(FixturesXml.Casos.ViolacaoSchema), ArquivoEnvio.Fixture(NFeValida));

        var erro = Assert.Single(envelope.Parecer!.Achados, a => a.Categoria == "ErroEstrutural");
        Assert.Equal(Nome(FixturesXml.Casos.ViolacaoSchema), erro.Arquivo);
        Assert.NotNull(erro.Linha);
        Assert.NotNull(erro.Posicao);
        Assert.Equal(FixturesXml.Casos.Pacote, erro.PacoteSchema);
        Assert.Equal("Realizada", Documento(envelope, Nome(NFeValida)).ValidacaoLayout);
        Assert.Equal("ComInconsistencias", envelope.Parecer.ResultadoGeral);
    }

    [Fact]
    public async Task XML_malformado_recebe_ERR008_apenas_no_proprio_arquivo_TS003()
    {
        var envelope = await AnalisarAsync(
            ArquivoEnvio.Fixture(FixturesXml.Casos.Malformado), ArquivoEnvio.Fixture(NFeValida), Sintetica(902));

        CodigoNoArquivo(envelope, Nome(FixturesXml.Casos.Malformado), "ERR-008");
        CodigoAusente(envelope, Nome(NFeValida), "ERR-008");
        Assert.Equal("Realizada", Documento(envelope, Nome(NFeValida)).ValidacaoLayout);
        Assert.Equal("Realizada", Documento(envelope, "nfe-902.xml").ValidacaoLayout);
        Assert.Equal(1, envelope.Parecer!.ResumoQuantitativo.NaoAnalisados);
    }

    [Fact]
    public async Task Totalizador_divergente_tem_valor_informado_calculado_e_regra_TS004()
    {
        var envelope = await AnalisarAsync(ArquivoEnvio.Fixture(FixturesXml.Casos.TotalizadorDivergente));

        var achado = AchadoDaRegra(envelope, "W04-10");
        Assert.Equal(("InconsistenciaNegocio", "Deterministica", 1), (achado.Categoria, achado.Origem, achado.VersaoRegra));
        Assert.Equal(("19.00", "18.00"), (achado.ValorInformado, achado.ValorEsperado));
        Assert.Contains("ICMSTot/vICMS", achado.Campo, StringComparison.Ordinal);
        Assert.NotEmpty(achado.Evidencias);
        Assert.Equal("ComInconsistencias", envelope.Parecer!.ResultadoGeral);
    }

    [Fact]
    public async Task Erro_de_layout_impede_a_validacao_de_conteudo_TS005()
    {
        var envelope = await AnalisarAsync(ArquivoEnvio.Fixture(FixturesXml.Casos.LayoutESomatorio));
        var arquivo = Nome(FixturesXml.Casos.LayoutESomatorio);

        Assert.All(envelope.Parecer!.Achados.Where(a => a.Arquivo == arquivo && a.Origem == "Deterministica"),
            a => Assert.Equal("ErroEstrutural", a.Categoria));
        Assert.Contains(envelope.Parecer.Achados, a => a.Arquivo == arquivo && a.Categoria == "ErroEstrutural");
        var documento = Documento(envelope, arquivo);
        Assert.StartsWith("NaoRealizada", documento.ValidacaoConteudo, StringComparison.Ordinal);
        Assert.Contains("layout inválido", documento.ValidacaoConteudo, StringComparison.Ordinal);
        Assert.Contains(envelope.Parecer.ValidacoesNaoRealizadas, v => v.Arquivo == arquivo && v.Validacao == "Conteudo");
    }

    [Fact]
    public async Task Lote_exatamente_no_limite_de_tamanho_e_aceito_TS006()
    {
        ArquivoEnvio[] lote = [ArquivoEnvio.Fixture(NFeValida), Sintetica(903)];
        var fabrica = _ambiente.CriarFabrica(configuracoes: new Dictionary<string, string?>
        {
            ["Limites:TamanhoTotalBytes"] = lote.Sum(a => a.Conteudo.Length).ToString(System.Globalization.CultureInfo.InvariantCulture),
        });
        _ambiente.ChatClient.Responder(RespostasIa.Valida());

        var envelope = Concluida(await ClienteExecucoes.CriarAsync(fabrica.CreateClient(), lote, cancelamento: Cancelamento));

        Assert.Equal(2, envelope.Parecer!.ResumoQuantitativo.Analisados);
    }

    [Fact]
    public async Task Lote_um_byte_acima_do_limite_recebe_ERR002_sem_processamento_TS007()
    {
        ArquivoEnvio[] lote = [ArquivoEnvio.Fixture(NFeValida), Sintetica(904)];
        var total = lote.Sum(a => a.Conteudo.Length);
        var fabrica = _ambiente.CriarFabrica(configuracoes: new Dictionary<string, string?>
        {
            ["Limites:TamanhoTotalBytes"] = (total - 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
        });
        var cliente = fabrica.CreateClient();
        var cronometro = Stopwatch.StartNew();

        var resposta = await ClienteExecucoes.CriarAsync(cliente, lote, cancelamento: Cancelamento);

        cronometro.Stop();
        var envelope = Rejeitada(resposta, HttpStatusCode.RequestEntityTooLarge, CodigoErro.TamanhoExcedido);
        Assert.True(cronometro.Elapsed < TimeSpan.FromSeconds(2), $"Rejeição em {cronometro.Elapsed}.");
        Assert.Empty(_ambiente.ChatClient.Chamadas);
        Assert.Equal(total - 1, envelope.Erro!.Detalhes!["limiteBytes"].GetInt64());
        Assert.Equal(total, envelope.Erro.Detalhes["totalRecebidoBytes"].GetInt64());
        Assert.All(envelope.Etapas.Where(e => e.Numero > 1), e => Assert.Equal("NaoExecutada", e.Estado));
    }

    [Fact]
    public async Task Quantidade_de_arquivos_acima_do_limite_recebe_ERR003_TS008()
    {
        var fabrica = _ambiente.CriarFabrica(configuracoes: new Dictionary<string, string?> { ["Limites:QuantidadeArquivos"] = "2" });

        var resposta = await ClienteExecucoes.CriarAsync(
            fabrica.CreateClient(), [Sintetica(905), Sintetica(906), Sintetica(907)], cancelamento: Cancelamento);

        Rejeitada(resposta, HttpStatusCode.BadRequest, CodigoErro.QuantidadeArquivosExcedida);
        Assert.Empty(_ambiente.ChatClient.Chamadas);
    }

    [Fact]
    public async Task Solicitacao_sem_pergunta_e_sem_arquivos_recebe_ERR001()
    {
        var resposta = await ClienteExecucoes.CriarAsync(_ambiente.CriarCliente(), cancelamento: Cancelamento);

        Rejeitada(resposta, HttpStatusCode.BadRequest, CodigoErro.SolicitacaoSemConteudo);
        Assert.Empty(_ambiente.ChatClient.Chamadas);
    }

    [Fact]
    public async Task Versao_de_layout_nao_suportada_recebe_ERR010_e_nunca_sem_inconsistencias_TS009()
    {
        var envelope = await AnalisarAsync(ArquivoEnvio.Fixture(FixturesXml.Casos.Versao310));
        var arquivo = Nome(FixturesXml.Casos.Versao310);

        CodigoNoArquivo(envelope, arquivo, "ERR-010");
        Assert.StartsWith("NaoRealizada", Documento(envelope, arquivo).ValidacaoLayout, StringComparison.Ordinal);
        Assert.NotEqual("SemInconsistencias", envelope.Parecer!.ResultadoGeral);
    }

    [Fact]
    public async Task XML_que_nao_e_NFe_recebe_ERR009_TS010()
    {
        var envelope = await AnalisarAsync(ArquivoEnvio.Fixture(FixturesXml.Casos.EventoCancelamento), ArquivoEnvio.Fixture(NFeValida));
        var arquivo = Nome(FixturesXml.Casos.EventoCancelamento);

        CodigoNoArquivo(envelope, arquivo, "ERR-009");
        Assert.StartsWith("NaoRealizada", Documento(envelope, arquivo).ValidacaoLayout, StringComparison.Ordinal);
        Assert.Equal("Realizada", Documento(envelope, Nome(NFeValida)).ValidacaoLayout);
    }

    [Fact]
    public async Task Motivo_do_documento_nao_suportado_diz_o_que_foi_encontrado_TS072()
    {
        var envelope = await AnalisarAsync(
            ArquivoEnvio.Fixture(FixturesXml.Casos.EventoCancelamento),
            ArquivoEnvio.Fixture(FixturesXml.Casos.NFCeModelo65),
            ArquivoEnvio.Fixture(FixturesXml.Casos.Versao310));

        string Motivo(string caminho) =>
            Assert.Single(envelope.Parecer!.ValidacoesNaoRealizadas, v => v.Arquivo == Nome(caminho) && v.Validacao == "Layout").Motivo;

        Assert.Contains("ERR-009", Motivo(FixturesXml.Casos.EventoCancelamento), StringComparison.Ordinal);
        Assert.Contains("evento de NF-e (raiz `procEventoNFe`)", Motivo(FixturesXml.Casos.EventoCancelamento), StringComparison.Ordinal);
        Assert.Contains("NFC-e (modelo 65)", Motivo(FixturesXml.Casos.NFCeModelo65), StringComparison.Ordinal);
        Assert.Contains("ERR-010", Motivo(FixturesXml.Casos.Versao310), StringComparison.Ordinal);
        Assert.Contains("versão de layout 3.10", Motivo(FixturesXml.Casos.Versao310), StringComparison.Ordinal);
        Assert.All(envelope.Parecer!.ValidacoesNaoRealizadas, v => Assert.DoesNotContain("<", v.Motivo, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Documento_enviado_duas_vezes_recebe_ERR011_e_e_analisado_uma_vez_TS011()
    {
        var envelope = await AnalisarAsync(ArquivoEnvio.Fixture(FixturesXml.Casos.Duplicado1), ArquivoEnvio.Fixture(FixturesXml.Casos.Duplicado2));
        var segundo = Nome(FixturesXml.Casos.Duplicado2);

        CodigoNoArquivo(envelope, segundo, "ERR-011");
        Assert.Contains(envelope.Parecer!.Achados, a => a.Arquivo == segundo && a.Severidade == "Informativa");
        Assert.NotNull(Documento(envelope, segundo).DuplicadoDe);
        Assert.Equal((2, 1), (envelope.Parecer.ResumoQuantitativo.Recebidos, envelope.Parecer.ResumoQuantitativo.Analisados));
    }

    [Fact]
    public async Task Mesma_chave_com_conteudo_divergente_recebe_ERR012_sem_descartar_arquivos_TS012()
    {
        var envelope = await AnalisarAsync(ArquivoEnvio.Fixture(FixturesXml.Casos.MesmaChave1), ArquivoEnvio.Fixture(FixturesXml.Casos.MesmaChave2));
        var segundo = Nome(FixturesXml.Casos.MesmaChave2);

        CodigoNoArquivo(envelope, segundo, "ERR-012");
        Assert.Contains(envelope.Parecer!.Achados, a => a.Arquivo == segundo && a.Severidade == "Alta");
        Assert.Equal(2, envelope.Parecer.ResumoQuantitativo.Analisados);
        Assert.Null(Documento(envelope, segundo).DuplicadoDe);
    }

    [Fact]
    public async Task Mesmo_lote_analisado_duas_vezes_tem_achados_deterministicos_identicos_TS013()
    {
        ArquivoEnvio[] lote =
        [
            ArquivoEnvio.Fixture(FixturesXml.Casos.TotalizadorDivergente),
            ArquivoEnvio.Fixture(FixturesXml.Casos.ViolacaoSchema),
            ArquivoEnvio.Fixture(FixturesXml.Casos.Malformado),
        ];

        var primeira = await AnalisarAsync(lote);
        var segunda = await AnalisarAsync(lote);

        Assert.NotEqual(primeira.ExecucaoId, segunda.ExecucaoId);
        Assert.Equal(Deterministicos(primeira), Deterministicos(segunda));
        Assert.NotEmpty(Deterministicos(primeira));
    }

    [Fact]
    public async Task XML_original_nao_e_persistido_apos_a_analise_TS036()
    {
        var envelope = await AnalisarAsync(ArquivoEnvio.Fixture(FixturesXml.Casos.TotalizadorDivergente));

        var url = MongoUrl.Create(_ambiente.Mongo.ConnectionString);
        var banco = new MongoClient(url).GetDatabase(url.DatabaseName ?? ContextoMongo.BancoPadrao);
        var nomes = await (await banco.ListCollectionNamesAsync(cancellationToken: Cancelamento)).ToListAsync(Cancelamento);
        Assert.NotEmpty(nomes);
        var execucaoEncontrada = false;
        var idBson = new BsonBinaryData(envelope.ExecucaoId, GuidRepresentation.Standard);
        foreach (var nome in nomes)
        {
            var documentos = await banco.GetCollection<BsonDocument>(nome).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(Cancelamento);
            foreach (var documento in documentos)
            {
                var json = documento.ToJson();
                execucaoEncontrada |= documento.GetValue("_id", BsonNull.Value).Equals(idBson);
                Assert.DoesNotContain("<infNFe", json, StringComparison.Ordinal);
                Assert.DoesNotContain("<?xml", json, StringComparison.Ordinal);
                Assert.DoesNotContain("<nfeProc", json, StringComparison.Ordinal);
            }
        }

        Assert.True(execucaoEncontrada, "A execução deve estar persistida com os dados extraídos.");
    }

    [Fact]
    public async Task Execucao_consultada_por_outra_identificacao_recebe_ERR005()
    {
        var envelope = await AnalisarAsync(ArquivoEnvio.Fixture(NFeValida));
        var cliente = _ambiente.CriarCliente();

        var propria = await ClienteExecucoes.ObterAsync(cliente, envelope.ExecucaoId, cancelamento: Cancelamento);
        var deOutro = await ClienteExecucoes.ObterAsync(cliente, envelope.ExecucaoId, usuarioId: "outro-usuario", cancelamento: Cancelamento);

        Assert.Equal(envelope.ExecucaoId, Concluida(propria).ExecucaoId);
        Assert.Equal(HttpStatusCode.NotFound, deOutro.Status);
        Assert.Equal(CodigoErro.SessaoNaoEncontrada, deOutro.ErroResposta.Erro.Codigo);
        Assert.DoesNotContain(envelope.ExecucaoId.ToString(), deOutro.Corpo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Solicitacao_acima_do_limite_de_taxa_recebe_429_com_Retry_After_sem_analise_TS063()
    {
        var fabrica = _ambiente.CriarFabrica(configuracoes: new Dictionary<string, string?>
        {
            ["LimiteTaxa:PermissoesPorJanela"] = "2",
            ["LimiteTaxa:Janela"] = "00:01:00",
        });
        var cliente = fabrica.CreateClient();
        _ambiente.ChatClient.Responder(RespostasIa.Valida()).Responder(RespostasIa.Valida()).Responder(RespostasIa.Valida());
        const string usuario = "usuario-limite-taxa";

        for (var i = 0; i < 2; i++)
        {
            Concluida(await ClienteExecucoes.CriarAsync(cliente, [Sintetica(910 + i)], usuarioId: usuario, cancelamento: Cancelamento));
        }

        var chamadasAntes = _ambiente.ChatClient.Chamadas.Count;
        var excedente = await ClienteExecucoes.CriarAsync(cliente, [Sintetica(912)], usuarioId: usuario, cancelamento: Cancelamento);

        Rejeitada(excedente, HttpStatusCode.TooManyRequests, CodigoErro.LimiteAtingido);
        Assert.NotNull(excedente.Cabecalhos.RetryAfter);
        Assert.Equal(chamadasAntes, _ambiente.ChatClient.Chamadas.Count);
    }

    [Fact]
    public async Task Armazenamento_indisponivel_recebe_ERR025_dentro_do_timeout_sem_parecer_parcial_TS064()
    {
        await using var mongo = new ContainerMongo();
        await mongo.InitializeAsync();
        var fabrica = _ambiente.CriarFabrica(configuracoes: new Dictionary<string, string?>
        {
            ["ConnectionStrings:MongoDb"] = mongo.ConnectionString,
            ["Persistencia:TimeoutMongo"] = "00:00:02",
        });
        var cliente = fabrica.CreateClient();
        _ambiente.ChatClient.Responder(RespostasIa.Valida());
        await mongo.PararAsync(Cancelamento);
        var cronometro = Stopwatch.StartNew();

        var resposta = await ClienteExecucoes.CriarAsync(cliente, [ArquivoEnvio.Fixture(NFeValida)], cancelamento: Cancelamento);

        cronometro.Stop();
        Rejeitada(resposta, HttpStatusCode.ServiceUnavailable, CodigoErro.ArmazenamentoIndisponivel);
        Assert.True(cronometro.Elapsed < TimeSpan.FromSeconds(10), $"ERR-025 em {cronometro.Elapsed}.");
    }

    [Fact]
    public async Task Preflight_da_rota_de_execucoes_so_permite_a_origem_configurada_TS070()
    {
        const string origem = "https://sofia.exemplo.com.br";
        var cliente = _ambiente.CriarFabrica(configuracoes: new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = origem }).CreateClient();

        using var permitida = await cliente.SendAsync(Preflight(origem), Cancelamento);
        using var outra = await cliente.SendAsync(Preflight("https://site-nao-autorizado.example"), Cancelamento);

        Assert.Equal(origem, Cabecalho(permitida, "Access-Control-Allow-Origin"));
        Assert.Contains("POST", Cabecalho(permitida, "Access-Control-Allow-Methods"), StringComparison.Ordinal);
        Assert.Null(Cabecalho(permitida, "Access-Control-Allow-Credentials"));
        Assert.Null(Cabecalho(outra, "Access-Control-Allow-Origin"));
    }

    private static HttpRequestMessage Preflight(string origem)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Options, ClienteExecucoes.Rota);
        requisicao.Headers.Add("Origin", origem);
        requisicao.Headers.Add("Access-Control-Request-Method", "POST");
        requisicao.Headers.Add("Access-Control-Request-Headers", "content-type,x-usuario-id,x-chamador-id");
        return requisicao;
    }

    private static string? Cabecalho(HttpResponseMessage resposta, string nome) =>
        resposta.Headers.TryGetValues(nome, out var valores) ? string.Join(",", valores) : null;

    private static DocumentoParecerV1 Documento(EnvelopeV1 envelope, string arquivo) =>
        Assert.Single(envelope.Parecer!.Documentos, d => d.Arquivo == arquivo);

    private static IEnumerable<string> TextosDoArquivo(EnvelopeV1 envelope, string arquivo) =>
        envelope.Parecer!.Achados.Where(a => a.Arquivo == arquivo).Select(a => a.Descricao)
            .Concat(envelope.Parecer.ValidacoesNaoRealizadas.Where(v => v.Arquivo == arquivo).Select(v => v.Motivo));

    private static void CodigoNoArquivo(EnvelopeV1 envelope, string arquivo, string codigo)
    {
        Assert.Contains(TextosDoArquivo(envelope, arquivo), t => t.Contains(codigo, StringComparison.Ordinal));
        Assert.All(envelope.Parecer!.Documentos.Where(d => d.Arquivo != arquivo),
            d => CodigoAusente(envelope, d.Arquivo, codigo));
    }

    private static void CodigoAusente(EnvelopeV1 envelope, string arquivo, string codigo) =>
        Assert.DoesNotContain(TextosDoArquivo(envelope, arquivo), t => t.Contains(codigo, StringComparison.Ordinal));

    private static List<string> Deterministicos(EnvelopeV1 envelope) =>
    [
        .. envelope.Parecer!.Achados
            .Where(a => a.Origem is "Deterministica" or "Sistema" && a.Categoria != "FalhaTecnica")
            .Select(a => string.Join("|", a.Categoria, a.Severidade, a.Origem, a.Arquivo, a.ChaveAcesso, a.Campo, a.Linha, a.Posicao,
                a.Item, a.ValorInformado, a.ValorEsperado, a.RegraId, a.VersaoRegra, a.PacoteSchema, a.Descricao))
            .Order(StringComparer.Ordinal),
    ];
}
