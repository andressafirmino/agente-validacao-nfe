using AuditoriaNFe.Domain.ContextoUsuario;
using AuditoriaNFe.Domain.Identificacao;
using AuditoriaNFe.Domain.Validacao;

namespace AuditoriaNFe.Tests.Dominio;

/// <summary>
/// Contexto do usuário (data-model §13; FR-055a, FR-055b, FR-057, FR-060): o que conta, a origem de
/// cada item, idempotência por execução, limites e renovação de <c>expiraEm</c>.
/// </summary>
public sealed class ContextoUsuarioTests
{
    private const string CnpjEmitente = "11222333000181";
    private const string CnpjDestinatario = "44555666000199";
    private const string CnpjAlfanumerico = "12ABC34501DE35";
    private static readonly DateTimeOffset Agora = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly LimitesContextoUsuario Limites = new(TimeSpan.FromDays(90), 200, 100);
    private static readonly IdentificacaoSolicitante Usuario = IdentificacaoSolicitante.Criar("usuario-ctx", "sofia");
    private static readonly Guid Sessao = Guid.CreateVersion7();

    private static ContextoUsuario Novo(LimitesContextoUsuario? limites = null) =>
        ContextoUsuario.Criar(Usuario.ChaveIsolamento, Usuario.UsuarioId, Usuario.ChamadorId, Agora, limites ?? Limites);

    private static Achado Inconsistencia(string regraId) =>
        new(Guid.CreateVersion7(), CategoriaAchado.InconsistenciaNegocio, Severidade.Alta, OrigemAchado.Deterministica,
            $"Divergência na regra {regraId}.", valorInformado: "19.00", valorEsperado: "18.00", regraId: regraId, versaoRegra: 1);

    private static Achado ErroSchema() =>
        Achado.ErroDeSchema(Guid.CreateVersion7(), "Violação do schema: natOp ausente.", "PL_010e_v1.02", "a.xml", 10, 5);

    private static ContribuicaoExecucao Contribuicao(Guid execucao, IReadOnlyList<DocumentoAuditado> documentos, IReadOnlyList<Achado>? achados = null) =>
        new(execucao, Sessao, documentos, achados ?? []);

    [Fact]
    public void Execucao_e_preferencia_ficam_com_origem_e_sem_conteudo_do_XML_TS057()
    {
        var contexto = Novo();
        var execucao = Guid.CreateVersion7();

        Assert.True(contexto.ContabilizarExecucao(
            Contribuicao(execucao, [new(true, CnpjEmitente, CnpjDestinatario), new(true, CnpjEmitente, CnpjAlfanumerico)], [Inconsistencia("W04-10"), ErroSchema()]),
            Agora,
            Limites));
        var declaracao = Agora.AddMinutes(5);
        contexto.DeclararPreferencias(NivelDetalhe.Detalhado, ["ICMS", "ICMS-ST"], declaracao, Limites);

        var emitente = Assert.Single(contexto.CnpjsAuditados, c => c.Papel == PapelCnpj.Emitente);
        Assert.Equal((CnpjEmitente, 2), (emitente.Cnpj, emitente.QuantidadeDocumentos));
        Assert.Equal(new OrigemContexto(TipoOrigemContexto.Execucao, execucao, Sessao, Agora), emitente.Origem);
        Assert.Equal(
            [CnpjAlfanumerico, CnpjDestinatario],
            contexto.CnpjsAuditados.Where(c => c.Papel == PapelCnpj.Destinatario).Select(c => c.Cnpj).Order(StringComparer.Ordinal));

        var regra = Assert.Single(contexto.AchadosRecorrentes, a => a.Chave == "W04-10");
        Assert.Equal((CategoriaAchado.InconsistenciaNegocio, 1), (regra.Categoria, regra.Frequencia));
        var schema = Assert.Single(contexto.AchadosRecorrentes, a => a.Chave == ContextoUsuario.ChaveViolacaoSchema);
        Assert.Equal(CategoriaAchado.ErroEstrutural, schema.Categoria);
        Assert.All(contexto.AchadosRecorrentes, a => Assert.Equal(TipoOrigemContexto.Execucao, a.Origem.Tipo));

        Assert.Equal(NivelDetalhe.Detalhado, contexto.Preferencias!.NivelDetalhe);
        Assert.Equal(["ICMS", "ICMS-ST"], contexto.Preferencias.TributosInteresse);
        Assert.Equal(new OrigemContexto(TipoOrigemContexto.DeclaracaoUsuario, null, null, declaracao), contexto.Preferencias.Origem);

        Assert.Equal(
            ["AchadosRecorrentes", "AtualizadoEm", "ChamadorId", "ChaveIsolamento", "CnpjsAuditados", "CriadoEm", "ExecucoesContabilizadas",
             "ExpiraEm", "Id", "Preferencias", "UltimaAtividadeEm", "UsuarioId"],
            typeof(ContextoUsuario).GetProperties().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal(["Cnpj", "Origem", "Papel", "QuantidadeDocumentos", "UltimaAuditoriaEm"], NomesDoContrato<CnpjAuditado>());
        Assert.Equal(["Categoria", "Chave", "Frequencia", "Origem", "UltimaOcorrenciaEm"], NomesDoContrato<AchadoRecorrente>());
        Assert.Equal(["NivelDetalhe", "Origem", "TributosInteresse"], NomesDoContrato<PreferenciasUsuario>());
    }

    [Fact]
    public void So_documentos_aprovados_no_layout_contam()
    {
        var contexto = Novo();

        contexto.ContabilizarExecucao(
            Contribuicao(Guid.CreateVersion7(), [new(false, CnpjEmitente, CnpjDestinatario), new(true, CnpjAlfanumerico, null)]),
            Agora,
            Limites);

        var unico = Assert.Single(contexto.CnpjsAuditados);
        Assert.Equal((CnpjAlfanumerico, PapelCnpj.Emitente, 1), (unico.Cnpj, unico.Papel, unico.QuantidadeDocumentos));
    }

    [Fact]
    public void CPF_e_valor_fora_do_formato_de_CNPJ_nao_entram()
    {
        var contexto = Novo();

        contexto.ContabilizarExecucao(Contribuicao(Guid.CreateVersion7(), [new(true, "12345678909", "1122233300018A")]), Agora, Limites);

        Assert.Empty(contexto.CnpjsAuditados);
    }

    [Fact]
    public void So_achados_deterministicos_contam()
    {
        var contexto = Novo();
        var evidencia = Evidencia.RegraCatalogo("W04-10", 1, "1");
        Achado[] achados =
        [
            Inconsistencia("W04-10"),
            new(Guid.CreateVersion7(), CategoriaAchado.IndicioIa, Severidade.Alta, OrigemAchado.IA, "Indício da IA.", evidencias: [evidencia]),
            new(Guid.CreateVersion7(), CategoriaAchado.ErroEstrutural, Severidade.Alta, OrigemAchado.Sistema, "ERR-012: conteúdo divergente."),
            new(Guid.CreateVersion7(), CategoriaAchado.FalhaTecnica, Severidade.Media, OrigemAchado.Sistema, "ERR-014: provedor indisponível."),
            new(Guid.CreateVersion7(), CategoriaAchado.AusenciaEvidencias, Severidade.Informativa, OrigemAchado.IA, "Sem evidência."),
        ];

        contexto.ContabilizarExecucao(Contribuicao(Guid.CreateVersion7(), [], achados), Agora, Limites);

        var unico = Assert.Single(contexto.AchadosRecorrentes);
        Assert.Equal(("W04-10", CategoriaAchado.InconsistenciaNegocio), (unico.Chave, unico.Categoria));
    }

    [Fact]
    public void Mesma_execucao_nao_conta_duas_vezes()
    {
        var contexto = Novo();
        var execucao = Guid.CreateVersion7();
        var contribuicao = Contribuicao(execucao, [new(true, CnpjEmitente, null)], [Inconsistencia("W04-10")]);

        Assert.True(contexto.ContabilizarExecucao(contribuicao, Agora, Limites));
        Assert.False(contexto.ContabilizarExecucao(contribuicao, Agora.AddHours(1), Limites));
        Assert.True(contexto.ContabilizarExecucao(Contribuicao(Guid.CreateVersion7(), [new(true, CnpjEmitente, null)], [Inconsistencia("W04-10")]), Agora.AddHours(2), Limites));

        Assert.Equal(2, Assert.Single(contexto.CnpjsAuditados).QuantidadeDocumentos);
        Assert.Equal(2, Assert.Single(contexto.AchadosRecorrentes).Frequencia);
        Assert.Equal(Agora.AddHours(2), contexto.UltimaAtividadeEm);
        Assert.Equal(2, contexto.ExecucoesContabilizadas.Count);
    }

    [Fact]
    public void Limites_de_CNPJs_e_achados_mantem_os_mais_recentes()
    {
        var limites = new LimitesContextoUsuario(TimeSpan.FromDays(90), MaxCnpjs: 2, MaxAchados: 2);
        var contexto = Novo(limites);
        string[] cnpjs = ["11111111000111", "22222222000122", "33333333000133"];
        string[] regras = ["W04-10", "W04-11", "W04-12"];

        for (var i = 0; i < 3; i++)
        {
            contexto.ContabilizarExecucao(
                Contribuicao(Guid.CreateVersion7(), [new(true, cnpjs[i], null)], [Inconsistencia(regras[i])]),
                Agora.AddMinutes(i),
                limites);
        }

        Assert.Equal(["22222222000122", "33333333000133"], contexto.CnpjsAuditados.Select(c => c.Cnpj).Order(StringComparer.Ordinal));
        Assert.Equal(["W04-11", "W04-12"], contexto.AchadosRecorrentes.Select(a => a.Chave).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Execucoes_contabilizadas_sao_limitadas()
    {
        var contexto = Novo();
        for (var i = 0; i < ContextoUsuario.MaxExecucoesContabilizadas + 5; i++)
        {
            contexto.ContabilizarExecucao(Contribuicao(Guid.CreateVersion7(), []), Agora, Limites);
        }

        Assert.Equal(ContextoUsuario.MaxExecucoesContabilizadas, contexto.ExecucoesContabilizadas.Count);
    }

    [Fact]
    public void ExpiraEm_e_renovado_por_execucao_declaracao_e_correcao_FR060()
    {
        var contexto = Novo();
        Assert.Equal(Agora.AddDays(90), contexto.ExpiraEm);

        contexto.ContabilizarExecucao(Contribuicao(Guid.CreateVersion7(), [new(true, CnpjEmitente, null)]), Agora.AddDays(10), Limites);
        Assert.Equal(Agora.AddDays(100), contexto.ExpiraEm);

        contexto.DeclararPreferencias(NivelDetalhe.Resumido, [], Agora.AddDays(20), Limites);
        Assert.Equal(Agora.AddDays(110), contexto.ExpiraEm);

        Assert.True(contexto.RemoverCnpj(CnpjEmitente, null, Agora.AddDays(30), Limites));
        Assert.Equal(Agora.AddDays(120), contexto.ExpiraEm);

        Assert.False(contexto.RemoverAchadoRecorrente("W04-10", Agora.AddDays(40), Limites));
        Assert.Equal(Agora.AddDays(120), contexto.ExpiraEm);
        Assert.False(contexto.ExpiradoEm(Agora.AddDays(120).AddTicks(-1)));
        Assert.True(contexto.ExpiradoEm(Agora.AddDays(120)));
    }

    [Fact]
    public void Remocao_de_CNPJ_por_papel_e_de_achado_recorrente()
    {
        var contexto = Novo();
        contexto.ContabilizarExecucao(
            Contribuicao(Guid.CreateVersion7(), [new(true, CnpjEmitente, CnpjEmitente)], [Inconsistencia("W04-10"), Inconsistencia("W04-11")]),
            Agora,
            Limites);

        Assert.True(contexto.RemoverCnpj(CnpjEmitente, PapelCnpj.Destinatario, Agora, Limites));
        Assert.Equal(PapelCnpj.Emitente, Assert.Single(contexto.CnpjsAuditados).Papel);
        Assert.True(contexto.RemoverAchadoRecorrente("W04-10", Agora, Limites));
        Assert.Equal("W04-11", Assert.Single(contexto.AchadosRecorrentes).Chave);
    }

    [Theory]
    [InlineData("ICMS-ST", "ICMS-ST")]
    [InlineData("CPF", null)]
    [InlineData("icms", null)]
    public void Preferencias_aceitam_so_a_lista_fechada_sem_repeticao(string tributo, string? aceito)
    {
        var contexto = Novo();

        if (aceito is null)
        {
            Assert.Throws<ArgumentException>(() => contexto.DeclararPreferencias(NivelDetalhe.Resumido, [tributo], Agora, Limites));
            Assert.Null(contexto.Preferencias);
        }
        else
        {
            contexto.DeclararPreferencias(NivelDetalhe.Resumido, [tributo], Agora, Limites);
            Assert.Equal([aceito], contexto.Preferencias!.TributosInteresse);
        }

        Assert.Throws<ArgumentException>(() => contexto.DeclararPreferencias(NivelDetalhe.Resumido, ["ICMS", "ICMS"], Agora, Limites));
        Assert.Throws<ArgumentOutOfRangeException>(() => contexto.DeclararPreferencias((NivelDetalhe)7, [], Agora, Limites));
    }

    [Fact]
    public void Usuario_anonimo_nao_tem_contexto_FR052c()
    {
        var anonimo = IdentificacaoSolicitante.Criar(null, null);

        Assert.Throws<InvalidOperationException>(() =>
            ContextoUsuario.Criar(anonimo.ChaveIsolamento, anonimo.UsuarioId, anonimo.ChamadorId, Agora, Limites));
    }

    private static IEnumerable<string> NomesDoContrato<T>() =>
        typeof(T).GetProperties().Select(p => p.Name).Order(StringComparer.Ordinal);
}
