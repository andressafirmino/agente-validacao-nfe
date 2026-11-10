using AuditoriaNFe.Domain.Parecer;
using AuditoriaNFe.Domain.Validacao;

namespace AuditoriaNFe.Tests.Dominio;

/// <summary>Regras de cálculo do resultado geral (data-model §10; FR-019, FR-024, FR-047b).</summary>
public sealed class ResultadoGeralTests
{
    private const string Arquivo = "nfe-001.xml";

    [Fact]
    public void Lote_valido_sem_achados_e_sem_inconsistencias_TS001()
    {
        var avaliacao = CalculadoraResultadoGeral.Calcular(Entrada());

        Assert.Equal(ResultadoGeral.SemInconsistencias, avaliacao.Resultado);
        Assert.Empty(avaliacao.Limitacoes);
        Assert.False(avaliacao.RevisaoHumana.Necessaria);
        Assert.Empty(avaliacao.RevisaoHumana.Motivos);
    }

    [Theory]
    [InlineData(CategoriaAchado.ErroEstrutural)]
    [InlineData(CategoriaAchado.InconsistenciaNegocio)]
    public void Achado_deterministico_resulta_em_com_inconsistencias(CategoriaAchado categoria)
    {
        var avaliacao = CalculadoraResultadoGeral.Calcular(Entrada() with { Achados = [AchadoDeterministico(categoria)] });

        Assert.Equal(ResultadoGeral.ComInconsistencias, avaliacao.Resultado);
    }

    [Fact]
    public void Versao_nao_suportada_nunca_resulta_em_sem_inconsistencias_TS009()
    {
        var entrada = Entrada() with
        {
            ValidacoesNaoRealizadas = [new ValidacaoNaoRealizada(Arquivo, "Layout", "versão de layout não suportada")],
        };

        var avaliacao = CalculadoraResultadoGeral.Calcular(entrada);

        Assert.Equal(ResultadoGeral.Inconclusiva, avaliacao.Resultado);
        Assert.True(avaliacao.RevisaoHumana.Necessaria);
        Assert.Contains(RevisaoHumana.MotivoResultadoInconclusivo, avaliacao.RevisaoHumana.Motivos);
    }

    [Fact]
    public void Schema_indisponivel_para_a_data_nunca_resulta_em_sem_inconsistencias_ERR023()
    {
        var entrada = Entrada() with
        {
            ValidacoesNaoRealizadas =
            [
                new ValidacaoNaoRealizada(Arquivo, "Layout", "schema não disponível para a data"),
                new ValidacaoNaoRealizada(Arquivo, "Conteudo", "schema não disponível para a data"),
            ],
        };

        Assert.Equal(ResultadoGeral.Inconclusiva, CalculadoraResultadoGeral.Calcular(entrada).Resultado);
    }

    [Fact]
    public void Validacao_nao_realizada_com_achado_deterministico_resulta_em_com_inconsistencias()
    {
        var entrada = Entrada() with
        {
            ValidacoesNaoRealizadas = [new ValidacaoNaoRealizada("nfe-002.xml", "Layout", "schema não disponível para a data")],
            Achados = [AchadoDeterministico(CategoriaAchado.InconsistenciaNegocio)],
        };

        Assert.Equal(ResultadoGeral.ComInconsistencias, CalculadoraResultadoGeral.Calcular(entrada).Resultado);
    }

    [Theory]
    [InlineData(1, EstadoEtapa.Falhou)]
    [InlineData(4, EstadoEtapa.NaoExecutada)]
    [InlineData(7, EstadoEtapa.Falhou)]
    [InlineData(9, EstadoEtapa.NaoExecutada)]
    public void Etapa_que_falhou_ou_nao_foi_executada_resulta_em_inconclusiva(int etapa, EstadoEtapa estado)
    {
        var avaliacao = CalculadoraResultadoGeral.Calcular(Entrada() with { Etapas = Etapas((etapa, estado)) });

        Assert.Equal(ResultadoGeral.Inconclusiva, avaliacao.Resultado);
        Assert.True(avaliacao.RevisaoHumana.Necessaria);
    }

    [Fact]
    public void Dependencia_indisponivel_sem_achados_deterministicos_resulta_em_inconclusiva()
    {
        var avaliacao = CalculadoraResultadoGeral.Calcular(Entrada() with { DependenciaIndisponivel = true });

        Assert.Equal(ResultadoGeral.Inconclusiva, avaliacao.Resultado);
        Assert.Contains(RevisaoHumana.MotivoResultadoInconclusivo, avaliacao.RevisaoHumana.Motivos);
    }

    [Fact]
    public void Dependencia_indisponivel_com_achados_deterministicos_resulta_em_com_inconsistencias()
    {
        var entrada = Entrada() with
        {
            DependenciaIndisponivel = true,
            Achados = [AchadoDeterministico(CategoriaAchado.ErroEstrutural)],
        };

        Assert.Equal(ResultadoGeral.ComInconsistencias, CalculadoraResultadoGeral.Calcular(entrada).Resultado);
    }

    [Fact]
    public void Achado_de_falha_tecnica_impede_sem_inconsistencias()
    {
        var falha = new Achado(Guid.CreateVersion7(), CategoriaAchado.FalhaTecnica, Severidade.Alta, OrigemAchado.Sistema,
            "Base de conhecimento indisponível.");

        var avaliacao = CalculadoraResultadoGeral.Calcular(Entrada() with { Achados = [falha] });

        Assert.Equal(ResultadoGeral.Inconclusiva, avaliacao.Resultado);
    }

    [Fact]
    public void Etapa_5_nao_aplicavel_com_MCP_desabilitado_nao_impede_o_resultado_TS046()
    {
        var entrada = Entrada() with
        {
            IntegracaoMcpHabilitada = false,
            Etapas = Etapas((5, EstadoEtapa.NaoAplicavel)),
        };

        var avaliacao = CalculadoraResultadoGeral.Calcular(entrada);

        Assert.Equal(ResultadoGeral.SemInconsistencias, avaliacao.Resultado);
        Assert.Contains(LimitacoesParecer.ConsultaManuaisNaoHabilitada, avaliacao.Limitacoes);
        Assert.True(avaliacao.RevisaoHumana.Necessaria);
        Assert.Contains(RevisaoHumana.MotivoConsultaManuaisNaoHabilitada, avaliacao.RevisaoHumana.Motivos);
    }

    [Fact]
    public void Etapa_5_nao_aplicavel_com_MCP_desabilitado_mantem_achados_deterministicos()
    {
        var entrada = Entrada() with
        {
            IntegracaoMcpHabilitada = false,
            Etapas = Etapas((5, EstadoEtapa.NaoAplicavel)),
            Achados = [AchadoDeterministico(CategoriaAchado.InconsistenciaNegocio)],
        };

        Assert.Equal(ResultadoGeral.ComInconsistencias, CalculadoraResultadoGeral.Calcular(entrada).Resultado);
    }

    [Theory]
    [InlineData(Severidade.Alta)]
    [InlineData(Severidade.Media)]
    public void Indicio_da_IA_alto_ou_medio_exige_revisao_humana_sem_mudar_o_resultado_FR024(Severidade severidade)
    {
        var avaliacao = CalculadoraResultadoGeral.Calcular(Entrada() with { Achados = [Indicio(severidade)] });

        Assert.Equal(ResultadoGeral.SemInconsistencias, avaliacao.Resultado);
        Assert.True(avaliacao.RevisaoHumana.Necessaria);
        Assert.Contains(RevisaoHumana.MotivoIndicioIa, avaliacao.RevisaoHumana.Motivos);
    }

    [Theory]
    [InlineData(Severidade.Baixa)]
    [InlineData(Severidade.Informativa)]
    public void Indicio_da_IA_baixo_ou_informativo_nao_exige_revisao_humana(Severidade severidade)
    {
        var avaliacao = CalculadoraResultadoGeral.Calcular(Entrada() with { Achados = [Indicio(severidade)] });

        Assert.Equal(ResultadoGeral.SemInconsistencias, avaliacao.Resultado);
        Assert.False(avaliacao.RevisaoHumana.Necessaria);
    }

    [Fact]
    public void Ausencia_de_evidencias_exige_revisao_humana_FR024()
    {
        var ausencia = new Achado(Guid.CreateVersion7(), CategoriaAchado.AusenciaEvidencias, Severidade.Informativa,
            OrigemAchado.Sistema, "Sem trechos de manual para a versão do documento.");

        var avaliacao = CalculadoraResultadoGeral.Calcular(Entrada() with { Achados = [ausencia] });

        Assert.Equal(ResultadoGeral.SemInconsistencias, avaliacao.Resultado);
        Assert.True(avaliacao.RevisaoHumana.Necessaria);
        Assert.Contains(RevisaoHumana.MotivoAusenciaEvidencias, avaliacao.RevisaoHumana.Motivos);
    }

    [Fact]
    public void Usuario_nao_identificado_gera_limitacao_sem_mudar_o_resultado()
    {
        var avaliacao = CalculadoraResultadoGeral.Calcular(Entrada() with { UsuarioIdentificado = false });

        Assert.Equal(ResultadoGeral.SemInconsistencias, avaliacao.Resultado);
        Assert.Contains(LimitacoesParecer.UsuarioNaoIdentificado, avaliacao.Limitacoes);
    }

    /// <summary>Execução completa: etapas executadas, salvo a 6 (sem ferramentas externas na v1).</summary>
    private static EntradaResultadoGeral Entrada() => new()
    {
        Etapas = Etapas(),
        IntegracaoMcpHabilitada = true,
        UsuarioIdentificado = true,
    };

    private static Dictionary<int, EstadoEtapa> Etapas(params (int Numero, EstadoEtapa Estado)[] alteracoes)
    {
        var etapas = Enumerable.Range(1, 9).ToDictionary(n => n, n => n == 6 ? EstadoEtapa.NaoAplicavel : EstadoEtapa.Executada);
        foreach (var (numero, estado) in alteracoes)
        {
            etapas[numero] = estado;
        }

        return etapas;
    }

    private static Achado AchadoDeterministico(CategoriaAchado categoria) => categoria == CategoriaAchado.ErroEstrutural
        ? Achado.ErroDeSchema(Guid.CreateVersion7(), "Elemento natOp ausente.", "PL_010e_v1.02", Arquivo, 12, 5)
        : new Achado(Guid.CreateVersion7(), CategoriaAchado.InconsistenciaNegocio, Severidade.Alta, OrigemAchado.Deterministica,
            "vICMS diferente da soma dos itens.", arquivo: Arquivo, campo: "total/ICMSTot/vICMS",
            valorInformado: "19.00", valorEsperado: "18.00", regraId: "W04-10", versaoRegra: 1);

    private static Achado Indicio(Severidade severidade) => new(
        Guid.CreateVersion7(), CategoriaAchado.IndicioIa, severidade, OrigemAchado.IA, "CFOP incomum para a operação.",
        arquivo: Arquivo,
        evidencias: [Evidencia.CampoXml("35261112345678000195550010000001231000001230", "det[1]/prod/CFOP", "5949")]);
}
