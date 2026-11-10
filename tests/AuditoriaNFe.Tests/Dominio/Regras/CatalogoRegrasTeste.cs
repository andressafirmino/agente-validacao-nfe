using AuditoriaNFe.Domain.Regras;
using AuditoriaNFe.Domain.Validacao;

namespace AuditoriaNFe.Tests.Dominio.Regras;

/// <summary>
/// Espelho, para os testes, das regras de <c>specs/001-agente-auditoria-nfe/catalogo-regras-totalizadores.md</c>
/// (aprovado em 2026-11-04). A implementação de cada versão tem o id <c>{regra}.v{versao}</c>.
/// </summary>
internal static class CatalogoRegrasTeste
{
    private static readonly DateOnly Inicio2017 = new(2017, 12, 4);
    private static readonly DateOnly InicioMono = new(2023, 10, 30);
    private static readonly DateOnly InicioRtc = new(2025, 11, 10);

    private static readonly FonteRegra AnexoI120 = new("MOC 7.0 Anexo I", "7.03", "120");
    private static readonly FonteRegra AnexoI121 = new("MOC 7.0 Anexo I", "7.03", "121");
    private static readonly FonteRegra AnexoI123 = new("MOC 7.0 Anexo I", "7.03", "123");
    private static readonly FonteRegra AnexoI125 = new("MOC 7.0 Anexo I", "7.03", "125-126");
    private static readonly FonteRegra Nt2016002 = new("NT 2016.002", "1.61", "62");
    private static readonly FonteRegra Nt2023001 = new("NT 2023.001", "1.60", "39-40");
    private static readonly FonteRegra Nt2025002 = new("NT 2025.002", "1.53", "70-71");
    private static readonly FonteRegra Nt2026008 = new("NT 2026.008", "1.01", "8");

    public static IReadOnlyList<RegraConteudo> Todas { get; } =
    [
        R("W03-10", 1, GrupoRegra.ICMSTot, Inicio2017, null, Severidade.Media, F("531"), AnexoI120),
        R("W04-10", 1, GrupoRegra.ICMSTot, Inicio2017, null, Severidade.Alta, F("532"), AnexoI120),
        R("W04a-10", 1, GrupoRegra.ICMSTot, Inicio2017, null, Severidade.Alta, F("795"), Nt2016002),
        R("W04b-10", 1, GrupoRegra.ICMSTot, Inicio2017, null, Severidade.Alta, O("861"), Nt2016002),
        R("W04c-10", 1, GrupoRegra.ICMSTot, Inicio2017, null, Severidade.Alta, O("798"), AnexoI120),
        R("W04e-10", 1, GrupoRegra.ICMSTot, Inicio2017, null, Severidade.Alta, O("799"), AnexoI120),
        R("W04g-10", 1, GrupoRegra.ICMSTot, Inicio2017, null, Severidade.Alta, O("800"), AnexoI120),
        R("W05-10", 1, GrupoRegra.ICMSTot, Inicio2017, null, Severidade.Media, F("533"), AnexoI120),
        R("W06-10", 1, GrupoRegra.ICMSTot, Inicio2017, null, Severidade.Alta, F("534"), AnexoI120),
        R("W06a-10", 1, GrupoRegra.ICMSTot, Inicio2017, null, Severidade.Alta, O("862"), Nt2016002),
        R("W06b-10", 1, GrupoRegra.ICMSTot, Inicio2017, null, Severidade.Alta, O("859"), Nt2016002),
        R("W06b.1-10", 1, GrupoRegra.ICMSTot, InicioMono, null, Severidade.Baixa, F("700"), Nt2023001, 0.01m),
        R("W06c-10", 1, GrupoRegra.ICMSTot, InicioMono, null, Severidade.Alta, F("967"), Nt2023001),
        R("W06c.1-10", 1, GrupoRegra.ICMSTot, InicioMono, null, Severidade.Baixa, F("723"), Nt2023001, 0.01m),
        R("W06d-10", 1, GrupoRegra.ICMSTot, InicioMono, null, Severidade.Alta, F("968"), Nt2023001),
        R("W06d.1-10", 1, GrupoRegra.ICMSTot, InicioMono, null, Severidade.Baixa, F("744"), Nt2023001, 0.01m),
        R("W06e-10", 1, GrupoRegra.ICMSTot, InicioMono, null, Severidade.Alta, F("969"), Nt2023001),
        R("W07-10", 1, GrupoRegra.ICMSTot, Inicio2017, new(2026, 8, 2), Severidade.Media, F("564"), AnexoI121),
        R("W07-10", 2, GrupoRegra.ICMSTot, new(2026, 8, 3), null, Severidade.Media, O("564"), new("NT 2025.002", "1.53", "70")),
        R("W08-10", 1, GrupoRegra.ICMSTot, Inicio2017, null, Severidade.Media, F("535"), AnexoI121),
        R("W09-10", 1, GrupoRegra.ICMSTot, Inicio2017, null, Severidade.Media, F("536"), AnexoI121),
        R("W10-10", 1, GrupoRegra.ICMSTot, Inicio2017, null, Severidade.Media, F("537"), AnexoI121),
        R("W11-10", 1, GrupoRegra.ICMSTot, Inicio2017, null, Severidade.Media, F("601"), AnexoI121),
        R("W12-10", 1, GrupoRegra.ICMSTot, Inicio2017, null, Severidade.Alta, F("538"), AnexoI121),
        R("W12a-10", 1, GrupoRegra.ICMSTot, Inicio2017, null, Severidade.Alta, F("863"), Nt2016002),
        R("W13-10", 1, GrupoRegra.ICMSTot, Inicio2017, null, Severidade.Media, F("602"), AnexoI121),
        R("W14-10", 1, GrupoRegra.ICMSTot, Inicio2017, null, Severidade.Media, F("603"), AnexoI121),
        R("W15-10", 1, GrupoRegra.ICMSTot, Inicio2017, null, Severidade.Media, F("604"), AnexoI121),
        R("W16-10", 1, GrupoRegra.ICMSTot, Inicio2017, new(2021, 10, 3), Severidade.Alta, F("610"),
            new("MOC 7.0 Anexo I", "7.03", "122 e 140"), 0.50m),
        R("W16-10", 2, GrupoRegra.ICMSTot, new(2021, 10, 4), new(2023, 3, 29), Severidade.Alta, F("610"),
            new("NT 2020.005", "1.21", "20"), 0.50m),
        R("W16-10", 3, GrupoRegra.ICMSTot, new(2023, 3, 30), new(2024, 6, 30), Severidade.Alta, F("610"),
            new("NT 2023.001", "1.60", "11 e 41"), 0.50m),
        R("W16-10", 4, GrupoRegra.ICMSTot, new(2024, 7, 1), null, Severidade.Alta, F("610"),
            new("NT 2023.004", "1.20", "5-6 e 15-16"), 0.50m),
        R("W16a-10", 1, GrupoRegra.ICMSTot, Inicio2017, null, Severidade.Informativa, F("685"), AnexoI123),
        R("W01a-10", 1, GrupoRegra.ICMSTot, new(2026, 11, 16), null, Severidade.Media, O("1282"), Nt2026008),
        R("W01a-20", 1, GrupoRegra.ICMSTot, new(2026, 11, 16), null, Severidade.Media, O("1283"), Nt2026008),

        R("W18-10", 1, GrupoRegra.ISSQNtot, Inicio2017, null, Severidade.Media, F("605"), AnexoI123),
        R("W19-10", 1, GrupoRegra.ISSQNtot, Inicio2017, null, Severidade.Media, F("606"), AnexoI123),
        R("W20-10", 1, GrupoRegra.ISSQNtot, Inicio2017, null, Severidade.Alta, F("607"), AnexoI123),
        R("W21-10", 1, GrupoRegra.ISSQNtot, Inicio2017, null, Severidade.Media, F("608"), AnexoI123),
        R("W22-10", 1, GrupoRegra.ISSQNtot, Inicio2017, null, Severidade.Media, F("609"), AnexoI123),
        R("W22b-10", 1, GrupoRegra.ISSQNtot, Inicio2017, null, Severidade.Baixa, O("364"), AnexoI123),
        R("W22c-10", 1, GrupoRegra.ISSQNtot, Inicio2017, null, Severidade.Baixa, O("365"), AnexoI123),
        R("W22d-10", 1, GrupoRegra.ISSQNtot, Inicio2017, null, Severidade.Baixa, O("366"), AnexoI123),
        R("W22e-10", 1, GrupoRegra.ISSQNtot, Inicio2017, null, Severidade.Baixa, O("367"), AnexoI123),
        R("W22f-10", 1, GrupoRegra.ISSQNtot, Inicio2017, null, Severidade.Baixa, O("368"), AnexoI123),

        R("W31-10", 1, GrupoRegra.ISTot, InicioRtc, null, Severidade.Media, O("1073"), Nt2025002),
        R("W31-20", 1, GrupoRegra.ISTot, InicioRtc, null, Severidade.Media, O("1117"), Nt2025002),
        R("W34-10", 1, GrupoRegra.IBSCBSTot, InicioRtc, null, Severidade.Media, O("1118"), Nt2025002),
        R("W34-20", 1, GrupoRegra.IBSCBSTot, InicioRtc, null, Severidade.Media, O("1119"), Nt2025002),
        R("W35-10", 1, GrupoRegra.IBSCBSTot, InicioRtc, null, Severidade.Media, O("1076"), Nt2025002),
        R("W38-10", 1, GrupoRegra.IBSCBSTot, InicioRtc, null, Severidade.Alta, O("1077"), Nt2025002),
        R("W39-10", 1, GrupoRegra.IBSCBSTot, InicioRtc, null, Severidade.Alta, O("1078"), Nt2025002),
        R("W41-10", 1, GrupoRegra.IBSCBSTot, InicioRtc, null, Severidade.Alta, O("1080"), Nt2025002),
        R("W43-10", 1, GrupoRegra.IBSCBSTot, InicioRtc, null, Severidade.Alta, O("1081"), Nt2025002),
        R("W44-10", 1, GrupoRegra.IBSCBSTot, InicioRtc, null, Severidade.Alta, O("1082"), Nt2025002),
        R("W46-10", 1, GrupoRegra.IBSCBSTot, InicioRtc, null, Severidade.Alta, O("1084"), Nt2025002),
        R("W47-10", 1, GrupoRegra.IBSCBSTot, InicioRtc, null, Severidade.Alta, O("1085"), Nt2025002),
        R("W48-10", 1, GrupoRegra.IBSCBSTot, InicioRtc, null, Severidade.Alta, O("1086"), Nt2025002),
        R("W53-10", 1, GrupoRegra.IBSCBSTot, InicioRtc, null, Severidade.Alta, O("1088"), Nt2025002),
        R("W54-10", 1, GrupoRegra.IBSCBSTot, InicioRtc, null, Severidade.Alta, O("1089"), Nt2025002),
        R("W56-10", 1, GrupoRegra.IBSCBSTot, InicioRtc, null, Severidade.Alta, O("1091"), Nt2025002),
        R("W56a-10", 1, GrupoRegra.IBSCBSTot, InicioRtc, null, Severidade.Alta, O("1087"), Nt2025002),
        R("W58-10", 1, GrupoRegra.IBSCBSTot, InicioRtc, null, Severidade.Alta, O("1092"), Nt2025002),
        R("W59-10", 1, GrupoRegra.IBSCBSTot, InicioRtc, null, Severidade.Alta, O("1093"), Nt2025002),
        R("W59a-10", 1, GrupoRegra.IBSCBSTot, InicioRtc, null, Severidade.Alta, O("1120"), Nt2025002),
        R("W59b-10", 1, GrupoRegra.IBSCBSTot, InicioRtc, null, Severidade.Alta, O("1121"), Nt2025002),
        R("W59c-10", 1, GrupoRegra.IBSCBSTot, InicioRtc, null, Severidade.Alta, O("1122"), Nt2025002),
        R("W59d-10", 1, GrupoRegra.IBSCBSTot, InicioRtc, null, Severidade.Alta, O("1123"), Nt2025002),
        R("W59f-10", 1, GrupoRegra.IBSCBSTot, new(2026, 2, 2), null, Severidade.Alta, O("1176"), Nt2025002),
        R("W59g-10", 1, GrupoRegra.IBSCBSTot, new(2026, 2, 2), null, Severidade.Alta, O("1177"), Nt2025002),

        R("Y05-10", 1, GrupoRegra.Cobranca, new(2018, 6, 4), null, Severidade.Media, O("901"), AnexoI125),
        R("Y06-10", 1, GrupoRegra.Cobranca, new(2018, 6, 4), null, Severidade.Media, O("902"), AnexoI125),
        R("Y10-10", 1, GrupoRegra.Cobranca, new(2018, 7, 2), null, Severidade.Media, O("851"), AnexoI125),
        R("YA03-20", 1, GrupoRegra.Pagamento, Inicio2017, null, Severidade.Media, F("866"), AnexoI125),
        R("YA09-10", 1, GrupoRegra.Pagamento, Inicio2017, null, Severidade.Media, O("869"), AnexoI125),
    ];

    public static RegraConteudo Regra(string id, int versao = 1) =>
        Todas.Single(r => r.Id == id && r.VersaoRegra == versao);

    public static IReadOnlyList<RegraConteudo> Versoes(string id) => [.. Todas.Where(r => r.Id == id)];

    private static AplicacaoSefaz O(string rejeicao) => new(TipoAplicacaoSefaz.Obrigatoria, rejeicao);

    private static AplicacaoSefaz F(string rejeicao) => new(TipoAplicacaoSefaz.Facultativa, rejeicao);

    private static RegraConteudo R(
        string id,
        int versao,
        GrupoRegra grupo,
        DateOnly inicio,
        DateOnly? fim,
        Severidade severidade,
        AplicacaoSefaz sefaz,
        FonteRegra fonte,
        decimal? tolerancia = null) => new()
    {
        Id = id,
        VersaoRegra = versao,
        Grupo = grupo,
        Descricao = $"Regra {id} versão {versao} do catálogo de totalizadores",
        Fonte = fonte,
        VigenciaInicio = inicio,
        VigenciaFim = fim,
        Severidade = severidade,
        Tolerancia = tolerancia,
        AplicacaoSefaz = sefaz,
        Implementacao = $"{id}.v{versao}",
    };
}
