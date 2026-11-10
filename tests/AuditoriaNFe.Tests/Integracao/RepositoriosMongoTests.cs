using System.Diagnostics;
using AuditoriaNFe.Application.Abstracoes;
using AuditoriaNFe.Application.Configuracao;
using AuditoriaNFe.Domain.ContextoUsuario;
using AuditoriaNFe.Domain.Documentos;
using AuditoriaNFe.Domain.Identificacao;
using AuditoriaNFe.Domain.Sessoes;
using AuditoriaNFe.Domain.Validacao;
using AuditoriaNFe.Infrastructure.Persistencia.Mongo;
using AuditoriaNFe.Tests.Integracao.Infra;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AuditoriaNFe.Tests.Integracao;

public sealed class RepositoriosMongoTests(ContainerMongo mongo) : IClassFixture<ContainerMongo>
{
    private const string ChaveAcesso = "35261112345678000195550010000000011000000010";
    private static readonly DateTimeOffset Agora = new(2026, 11, 4, 12, 0, 0, 123, TimeSpan.Zero);
    private static readonly IdentificacaoSolicitante Dono = IdentificacaoSolicitante.Criar("u1", "sofia");
    private static readonly IdentificacaoSolicitante Outro = IdentificacaoSolicitante.Criar("u2", "sofia");

    private ContextoMongo Contexto(TimeSpan? timeout = null, string? connectionString = null) =>
        new(connectionString ?? mongo.ConnectionString,
            Options.Create(new PersistenciaOpcoes { TimeoutMongo = timeout ?? TimeSpan.FromSeconds(5), TimeoutPostgres = TimeSpan.FromSeconds(5) }));

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sessao_e_gravada_e_lida_so_com_a_mesma_chave()
    {
        var contexto = Contexto();
        var repositorio = new SessaoRepositorio(contexto);
        var sessao = Sessao.Criar(Guid.CreateVersion7(), Dono, TimeSpan.FromHours(24), Agora, contextoA2A: "ctx-1");
        sessao.RegistrarDocumento(Guid.CreateVersion7(), ChaveAcesso, new string('a', 64), Agora);
        sessao.RegistrarExecucao(Guid.CreateVersion7(), Agora);

        await repositorio.InserirAsync(sessao, Cancelamento);
        var lida = await repositorio.ObterAsync(sessao.Id, Dono.ChaveIsolamento, Cancelamento);

        Assert.NotNull(lida);
        Assert.Equal(sessao.UltimaAtividadeEm, lida.UltimaAtividadeEm);
        Assert.Equal(TimeSpan.FromHours(24), lida.ExpiraApos);
        Assert.Equal(EstadoSessao.Ativa, lida.Estado);
        Assert.Equal("ctx-1", lida.ContextoA2A);
        Assert.Equal(sessao.Documentos, lida.Documentos);
        Assert.Equal(sessao.Execucoes, lida.Execucoes);
        Assert.Equal(new string('a', 64), lida.HashesPorChave[ChaveAcesso]);
        Assert.Null(await repositorio.ObterAsync(sessao.Id, Outro.ChaveIsolamento, Cancelamento));
    }

    [Fact]
    public async Task Atualizacao_de_sessao_respeita_a_chave()
    {
        var repositorio = new SessaoRepositorio(Contexto());
        var sessao = Sessao.Criar(Guid.CreateVersion7(), Dono, TimeSpan.FromHours(24), Agora);
        await repositorio.InserirAsync(sessao, Cancelamento);

        sessao.Encerrar(Agora.AddMinutes(5));
        await repositorio.AtualizarAsync(sessao, Cancelamento);
        var lida = await repositorio.ObterAsync(sessao.Id, Dono.ChaveIsolamento, Cancelamento);

        Assert.Equal(EstadoSessao.Encerrada, lida!.Estado);
        Assert.Equal(Agora.AddMinutes(5), lida.EncerradaEm);
    }

    [Fact]
    public async Task Formato_gravado_segue_as_convencoes_do_init()
    {
        var contexto = Contexto();
        var sessao = Sessao.Criar(Guid.CreateVersion7(), Dono, TimeSpan.FromHours(24), Agora);
        await new SessaoRepositorio(contexto).InserirAsync(sessao, Cancelamento);

        var bruto = await contexto.Banco.GetCollection<BsonDocument>("sessoes")
            .Find(new BsonDocument("_id", new BsonBinaryData(sessao.Id, GuidRepresentation.Standard)))
            .SingleAsync(Cancelamento);

        Assert.Equal(BsonBinarySubType.UuidStandard, bruto["_id"].AsBsonBinaryData.SubType);
        Assert.Equal(BsonType.DateTime, bruto["ultimaAtividadeEm"].BsonType);
        Assert.Equal("Ativa", bruto["estado"].AsString);
        Assert.Matches("^[0-9a-f]{64}$", bruto["chaveIsolamento"].AsString);
        Assert.True(bruto.Contains("hashesPorChave"));
    }

    [Fact]
    public async Task Execucao_preserva_etapas_ferramentas_trechos_e_consumo()
    {
        var repositorio = new ExecucaoRepositorio(Contexto());
        var sessaoId = Guid.CreateVersion7();
        var execucao = Execucao.Receber(
            Guid.CreateVersion7(), Dono, CanalExecucao.A2A, TipoExecucao.AnaliseComPergunta, Agora,
            correlacaoId: "corr-1", pergunta: "Qual o total?", versoes: new Dictionary<string, string> { ["envelope"] = "1.0" });
        execucao.AssociarSessao(sessaoId);
        execucao.IniciarProcessamento();
        execucao.RegistrarEtapa(1, EstadoEtapa.Executada, Agora, Agora.AddMilliseconds(3));
        execucao.RegistrarEtapa(5, EstadoEtapa.NaoAplicavel, justificativa: "Integração MCP desabilitada.");
        execucao.RegistrarChamadaFerramenta(new ChamadaFerramenta("calcular_total", Agora, 4, StatusChamadaFerramenta.Sucesso, calculoId: Guid.CreateVersion7()));
        execucao.RegistrarTrechoConsultado(new TrechoConsultado("c1", "MOC", "7.0"));
        execucao.DefinirModelo("gpt-4.1-mini");
        execucao.DefinirConsumo(new ConsumoResumo(100, null, 20, 0.00012345m, "USD", OrigemValoresConsumo.InformadoProvedor, true));
        execucao.Falhar(new ErroExecucao("ERR-016", "Tempo esgotado."), Agora.AddSeconds(2));

        await repositorio.InserirAsync(execucao, Cancelamento);
        var lida = await repositorio.ObterAsync(execucao.Id, Dono.ChaveIsolamento, Cancelamento);

        Assert.NotNull(lida);
        Assert.Equal(EstadoExecucao.Falhou, lida.Estado);
        Assert.Equal(sessaoId, lida.SessaoId);
        Assert.Equal(9, lida.Etapas.Count);
        Assert.Equal(EstadoEtapa.NaoAplicavel, lida.Etapa(5).Estado);
        Assert.Equal("Integração MCP desabilitada.", lida.Etapa(5).Justificativa);
        Assert.True(lida.Etapas.All(e => e.Registrada));
        Assert.Equal("calcular_total", Assert.Single(lida.ChamadasFerramentas).Nome);
        Assert.Equal("c1", Assert.Single(lida.TrechosConsultados).ChunkId);
        Assert.Equal("1.0", lida.Versoes["envelope"]);
        Assert.Equal(0.00012345m, lida.ConsumoResumo!.CustoTotal);
        Assert.Null(lida.ConsumoResumo.TokensEntradaCache);
        Assert.Equal("ERR-016", lida.Erro!.Codigo);
        Assert.Null(await repositorio.ObterAsync(execucao.Id, Outro.ChaveIsolamento, Cancelamento));
    }

    [Fact]
    public async Task Execucoes_da_sessao_vem_da_mais_recente_para_a_mais_antiga()
    {
        var repositorio = new ExecucaoRepositorio(Contexto());
        var sessaoId = Guid.CreateVersion7();
        var antiga = Execucao.Receber(Guid.CreateVersion7(), Dono, CanalExecucao.Direto, TipoExecucao.Pergunta, Agora);
        var recente = Execucao.Receber(Guid.CreateVersion7(), Dono, CanalExecucao.Direto, TipoExecucao.Pergunta, Agora.AddMinutes(1));
        antiga.AssociarSessao(sessaoId);
        recente.AssociarSessao(sessaoId);

        await repositorio.InserirAsync(antiga, Cancelamento);
        await repositorio.InserirAsync(recente, Cancelamento);

        Assert.Equal([recente.Id, antiga.Id], (await repositorio.ListarPorSessaoAsync(sessaoId, Dono.ChaveIsolamento, Cancelamento)).Select(e => e.Id));
        Assert.Empty(await repositorio.ListarPorSessaoAsync(sessaoId, Outro.ChaveIsolamento, Cancelamento));
    }

    [Fact]
    public async Task Documento_guarda_so_os_dados_extraidos()
    {
        var contexto = Contexto();
        var repositorio = new DocumentoRepositorio(contexto);
        var sessaoId = Guid.CreateVersion7();
        var documento = new DocumentoNFe(
            Guid.CreateVersion7(), sessaoId, Dono.ChaveIsolamento, ChaveAcesso, "55", "4.00", true,
            "2026-11-03T10:00:00-03:00", "2026-11-03T10:05:00-03:00", new DateOnly(2026, 11, 3), OrigemDataReferencia.DhRecbto,
            "PL_010e_v1.02", new string('b', 64), [new CampoExtraido("NFe/infNFe/total/ICMSTot/vNF", "100.00")],
            "nota.xml", Guid.CreateVersion7());

        await repositorio.InserirAsync(documento, Cancelamento);
        var lido = Assert.Single(await repositorio.ListarPorSessaoAsync(sessaoId, Dono.ChaveIsolamento, Cancelamento));
        var bruto = await contexto.Banco.GetCollection<BsonDocument>("documentos")
            .Find(new BsonDocument("_id", new BsonBinaryData(documento.Id, GuidRepresentation.Standard)))
            .SingleAsync(Cancelamento);

        Assert.Equal(new DateOnly(2026, 11, 3), lido.DataReferencia);
        Assert.Equal("2026-11-03T10:00:00-03:00", lido.DhEmi);
        Assert.Equal("100.00", Assert.Single(lido.DadosExtraidos).Valor);
        Assert.DoesNotContain(bruto.Names, nome => nome.Contains("xml", StringComparison.OrdinalIgnoreCase) && nome != "arquivoOrigem");
        Assert.Empty(await repositorio.ListarPorSessaoAsync(sessaoId, Outro.ChaveIsolamento, Cancelamento));
    }

    [Fact]
    public async Task Contexto_do_usuario_passa_no_jsonSchema_e_so_e_lido_vigente()
    {
        var contexto = Contexto();
        var repositorio = new ContextoUsuarioRepositorio(contexto);
        var limites = new LimitesContextoUsuario(TimeSpan.FromDays(90), 200, 100);
        var usuario = IdentificacaoSolicitante.Criar($"ctx-{Guid.NewGuid():N}"[..20], "sofia");
        var contextoUsuario = ContextoUsuario.Criar(usuario.ChaveIsolamento, usuario.UsuarioId, usuario.ChamadorId, Agora, limites);
        var execucaoId = Guid.CreateVersion7();
        contextoUsuario.ContabilizarExecucao(
            new ContribuicaoExecucao(execucaoId, Guid.CreateVersion7(), [new DocumentoAuditado(true, "11222333000181", "12ABC34501DE35")],
                [new Achado(Guid.CreateVersion7(), CategoriaAchado.InconsistenciaNegocio, Severidade.Alta, OrigemAchado.Deterministica,
                    "Divergência.", valorInformado: "19.00", valorEsperado: "18.00", regraId: "W04-10", versaoRegra: 1)]),
            Agora,
            limites);
        contextoUsuario.DeclararPreferencias(NivelDetalhe.Detalhado, ["ICMS", "ICMS-ST"], Agora, limites);

        Assert.True(await repositorio.SalvarAsync(contextoUsuario, null, Cancelamento));
        var lido = await repositorio.ObterAsync(usuario.ChaveIsolamento, Agora, Cancelamento);

        Assert.NotNull(lido);
        Assert.Equal(usuario.UsuarioId, lido.UsuarioId);
        Assert.Equal(contextoUsuario.CnpjsAuditados, lido.CnpjsAuditados);
        Assert.Equal(contextoUsuario.AchadosRecorrentes, lido.AchadosRecorrentes);
        Assert.Equal(["ICMS", "ICMS-ST"], lido.Preferencias!.TributosInteresse);
        Assert.Equal(new OrigemContexto(TipoOrigemContexto.DeclaracaoUsuario, null, null, Agora), lido.Preferencias.Origem);
        Assert.Equal([execucaoId], lido.ExecucoesContabilizadas);
        Assert.Equal(Agora.AddDays(90), lido.ExpiraEm);
        Assert.Null(await repositorio.ObterAsync(usuario.ChaveIsolamento, lido.ExpiraEm, Cancelamento));
        Assert.Null(await repositorio.ObterAsync(Outro.ChaveIsolamento, Agora, Cancelamento));

        var bruto = await contexto.Banco.GetCollection<BsonDocument>("contextos_usuario")
            .Find(new BsonDocument("_id", usuario.ChaveIsolamento))
            .SingleAsync(Cancelamento);
        Assert.Equal(BsonType.Int32, bruto["cnpjsAuditados"][0]["quantidadeDocumentos"].BsonType);
        Assert.Equal("Execucao", bruto["cnpjsAuditados"][0]["origem"]["tipo"].AsString);
        var indices = await (await contexto.Banco.GetCollection<BsonDocument>("contextos_usuario").Indexes.ListAsync(Cancelamento)).ToListAsync(Cancelamento);
        Assert.Contains(indices, i => i["name"] == "ttl_contextos_usuario_expira_em" && i["expireAfterSeconds"].ToInt32() == 0);
    }

    [Fact]
    public async Task Gravacao_do_contexto_detecta_alteracao_concorrente_e_substitui_so_o_vencido()
    {
        var repositorio = new ContextoUsuarioRepositorio(Contexto());
        var limites = new LimitesContextoUsuario(TimeSpan.FromDays(90), 200, 100);
        var usuario = IdentificacaoSolicitante.Criar($"ctx-{Guid.NewGuid():N}"[..20], "sofia");
        var original = ContextoUsuario.Criar(usuario.ChaveIsolamento, usuario.UsuarioId, usuario.ChamadorId, Agora, limites);
        Assert.True(await repositorio.SalvarAsync(original, null, Cancelamento));

        var concorrente = ContextoUsuario.Criar(usuario.ChaveIsolamento, usuario.UsuarioId, usuario.ChamadorId, Agora.AddMinutes(1), limites);
        Assert.False(await repositorio.SalvarAsync(concorrente, null, Cancelamento));

        var lido = (await repositorio.ObterAsync(usuario.ChaveIsolamento, Agora, Cancelamento))!;
        var lidoAntes = lido.AtualizadoEm;
        lido.DeclararPreferencias(NivelDetalhe.Resumido, ["IPI"], Agora.AddMinutes(2), limites);
        Assert.True(await repositorio.SalvarAsync(lido, lidoAntes, Cancelamento));
        Assert.False(await repositorio.SalvarAsync(lido, lidoAntes, Cancelamento));

        var depoisDoPrazo = Agora.AddDays(91);
        var novo = ContextoUsuario.Criar(usuario.ChaveIsolamento, usuario.UsuarioId, usuario.ChamadorId, depoisDoPrazo, limites);
        Assert.True(await repositorio.SalvarAsync(novo, null, Cancelamento));
        Assert.Null((await repositorio.ObterAsync(usuario.ChaveIsolamento, depoisDoPrazo, Cancelamento))!.Preferencias);

        await repositorio.ExcluirAsync(usuario.ChaveIsolamento, Cancelamento);
        Assert.Null(await repositorio.ObterAsync(usuario.ChaveIsolamento, depoisDoPrazo, Cancelamento));
    }

    [Fact]
    public async Task Mongo_indisponivel_gera_ERR_025_dentro_do_timeout()
    {
        var contexto = Contexto(TimeSpan.FromSeconds(1), "mongodb://127.0.0.1:1/auditoria_agente");
        var cronometro = Stopwatch.StartNew();

        await Assert.ThrowsAsync<ArmazenamentoIndisponivelException>(() =>
            new SessaoRepositorio(contexto).ObterAsync(Guid.CreateVersion7(), Dono.ChaveIsolamento, Cancelamento));

        Assert.True(cronometro.Elapsed < TimeSpan.FromSeconds(5), $"Demorou {cronometro.Elapsed}.");
    }
}
