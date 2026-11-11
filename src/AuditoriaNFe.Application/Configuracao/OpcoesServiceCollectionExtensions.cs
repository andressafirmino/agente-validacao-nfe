using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AuditoriaNFe.Application.Configuracao;

/// <summary>Registro das opções com validação na inicialização (P13; constitution 5.4).</summary>
public static class OpcoesServiceCollectionExtensions
{
    public static IServiceCollection AdicionarOpcoesAuditoria(this IServiceCollection services, IConfiguration configuration)
    {
        Registrar<LimitesOpcoes>(services, configuration, LimitesOpcoes.Secao)
            .Validate(o => o.CorpoHttpBytes >= o.TamanhoTotalBytes, "Limites:CorpoHttpBytes não pode ser menor que Limites:TamanhoTotalBytes.");
        Registrar<SessaoOpcoes>(services, configuration, SessaoOpcoes.Secao);
        Registrar<ContextoUsuarioOpcoes>(services, configuration, ContextoUsuarioOpcoes.Secao);
        Registrar<ExecucaoOpcoes>(services, configuration, ExecucaoOpcoes.Secao);
        Registrar<IaOpcoes>(services, configuration, IaOpcoes.Secao);
        Registrar<FerramentasOpcoes>(services, configuration, FerramentasOpcoes.Secao);
        Registrar<ConcorrenciaOpcoes>(services, configuration, ConcorrenciaOpcoes.Secao);
        Registrar<LimiteTaxaOpcoes>(services, configuration, LimiteTaxaOpcoes.Secao);
        Registrar<XmlOpcoes>(services, configuration, XmlOpcoes.Secao);
        Registrar<SchemasOpcoes>(services, configuration, SchemasOpcoes.Secao);
        Registrar<CatalogosOpcoes>(services, configuration, CatalogosOpcoes.Secao);
        Registrar<HistoricoOpcoes>(services, configuration, HistoricoOpcoes.Secao);
        Registrar<SensibilidadeOpcoes>(services, configuration, SensibilidadeOpcoes.Secao);
        Registrar<McpOpcoes>(services, configuration, McpOpcoes.Secao)
            .Validate(
                o => !o.Habilitado || EhUrlHttp(o.Endpoint),
                "Mcp:Endpoint deve ser uma URL http ou https absoluta quando Mcp:Habilitado = true.")
            .Validate(
                o => !o.FerramentasExternasHabilitado || o.Habilitado,
                "Mcp:FerramentasExternasHabilitado exige Mcp:Habilitado = true.");
        Registrar<AprovacaoOpcoes>(services, configuration, AprovacaoOpcoes.Secao);
        Registrar<SegurancaOpcoes>(services, configuration, SegurancaOpcoes.Secao)
            .Validate(
                o => !o.AutenticacaoHabilitada,
                "Seguranca:AutenticacaoHabilitada = true não é suportado na versão 1 (desvio D-05).");
        Registrar<PersistenciaOpcoes>(services, configuration, PersistenciaOpcoes.Secao);
        Registrar<OpenAIOpcoes>(services, configuration, OpenAIOpcoes.Secao)
            .Validate(
                o => !UsaOpenAI(configuration)
                    || (!string.IsNullOrWhiteSpace(o.ApiKey) && !string.IsNullOrWhiteSpace(o.Modelo)),
                "Com Ia:Provedor = OpenAI, OpenAI:ApiKey e OpenAI:Modelo são obrigatórios (sem modelo padrão, DP-05).");

        return services;
    }

    private static OptionsBuilder<T> Registrar<T>(IServiceCollection services, IConfiguration configuration, string secao)
        where T : class =>
        services.AddOptions<T>()
            .Bind(configuration.GetSection(secao))
            .ValidateDataAnnotations()
            .ValidateOnStart();

    // Lido da configuração, e não de IOptions<IaOpcoes>, para que um Ia:Provedor inválido falhe só uma vez.
    private static bool UsaOpenAI(IConfiguration configuration) =>
        string.Equals(configuration[$"{IaOpcoes.Secao}:{nameof(IaOpcoes.Provedor)}"], nameof(ProvedorIa.OpenAI), StringComparison.OrdinalIgnoreCase);

    private static bool EhUrlHttp(string? valor) =>
        Uri.TryCreate(valor, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
