using AuditoriaNFe.Application.Configuracao;

namespace Api.Agente.Hospedagem;

/// <summary>
/// Configuração exigida no modo <c>Homologacao</c> (FR-052g, desvio D-09, ADR-0013): provedor de IA
/// real, migrações fora da inicialização e limites abaixo do corpo aceito pelo Vercel (4,5 MB), para
/// que a recusa de um lote grande seja o ERR-002 do agente. Verificada antes de montar o host.
/// </summary>
public static class ValidadorModoHomologacao
{
    public const long TamanhoTotalMaximoBytes = 4_000_000;
    public const long CorpoHttpMaximoBytes = 4_500_000;

    private const string ChaveProvedor = $"{IaOpcoes.Secao}:{nameof(IaOpcoes.Provedor)}";
    private const string ChaveMigracoes = $"{PersistenciaOpcoes.Secao}:{nameof(PersistenciaOpcoes.AplicarMigracoes)}";
    private const string ChaveTamanhoTotal = $"{LimitesOpcoes.Secao}:{nameof(LimitesOpcoes.TamanhoTotalBytes)}";
    private const string ChaveCorpoHttp = $"{LimitesOpcoes.Secao}:{nameof(LimitesOpcoes.CorpoHttpBytes)}";

    public static void Verificar(IConfiguration configuration)
    {
        var erros = new List<string>();
        if (!string.Equals(configuration[ChaveProvedor]?.Trim(), nameof(ProvedorIa.OpenAI), StringComparison.OrdinalIgnoreCase))
        {
            erros.Add($"{ChaveProvedor} deve ser {nameof(ProvedorIa.OpenAI)}");
        }

        if (configuration.GetValue<bool>(ChaveMigracoes))
        {
            erros.Add($"{ChaveMigracoes} deve ser false (migrações aplicadas fora da inicialização)");
        }

        var limites = configuration.GetSection(LimitesOpcoes.Secao).Get<LimitesOpcoes>() ?? new LimitesOpcoes();
        if (limites.TamanhoTotalBytes > TamanhoTotalMaximoBytes)
        {
            erros.Add($"{ChaveTamanhoTotal} deve ser no máximo 4000000");
        }

        if (limites.CorpoHttpBytes > CorpoHttpMaximoBytes)
        {
            erros.Add($"{ChaveCorpoHttp} deve ser no máximo 4500000");
        }

        if (erros.Count > 0)
        {
            throw new InvalidOperationException(
                $"Configuração inválida para {HospedagemOpcoes.ChaveModo} = {nameof(ModoHospedagem.Homologacao)} (desvio D-09): {string.Join("; ", erros)}.");
        }
    }
}
