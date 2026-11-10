using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.Unicode;
using Api.Agente.Hospedagem;
using AuditoriaNFe.Application.Configuracao;
using Microsoft.Extensions.Options;

namespace Api.Agente.A2A;

/// <summary>
/// Card do contrato SOFIA v1.0 (contracts/sofia-a2a.md §2; FR-044, FR-044a, FR-045), montado uma vez
/// a partir da configuração. Sem <c>securitySchemes</c> enquanto vigorar o D-05.
/// </summary>
public sealed class FabricaAgentCard(
    IOptions<A2AOpcoes> a2a,
    IOptions<LimitesOpcoes> limites,
    IOptions<HospedagemOpcoes> hospedagem)
{
    public const string Rota = "/.well-known/agent-card.json";
    public const string RotaA2A = "/a2a/auditoria-nfe";
    public const string ImplantacaoSomenteAgentCard = "somente Agent Card publicado; message/send indisponível nesta URL";
    public const string ImplantacaoHomologacao = "homologação pública, sem autenticação; somente dados sintéticos";

    public static JsonSerializerOptions OpcoesJson { get; } = CriarOpcoesJson();

    public AgentCardV03 Card { get; } = Montar(a2a.Value, limites.Value, hospedagem.Value.Modo);

    private static AgentCardV03 Montar(A2AOpcoes a2a, LimitesOpcoes limites, ModoHospedagem modo) => new(
        Name: "AgenteAuditoriaNFe",
        Description: "Analisa XMLs de NF-e (modelo 55, layout 4.00) enviados pela SOFIA: valida a estrutura contra o schema oficial vigente na data de cada documento e confere os totalizadores do catálogo aprovado, sem inventar valores. Versão sem autenticação, somente para ambiente local de testes.",
        Url: $"{a2a.UrlPublica.TrimEnd('/')}{RotaA2A}",
        Version: VersaoAgente(),
        ProtocolVersion: "0.3.0",
        PreferredTransport: "JSONRPC",
        Capabilities: new CapacidadesAgentCard(
            Streaming: false,
            PushNotifications: false,
            Extensions:
            [
                new ExtensaoAgentCard(
                    Uri: "urn:infis:sofia:contrato",
                    Description: "Contrato SOFIA v1.0: SolicitacaoAnaliseSofia em TextPart; ResultadoAnaliseSofia em Artifact de Task concluída.",
                    Required: false,
                    Params: new ParametrosContratoSofia(
                        VersaoContrato: "1.0",
                        IdAgente: a2a.IdAgente,
                        NomeArtifact: "analise-sofia-v1",
                        Autenticacao: "nenhuma — somente ambiente local de testes",
                        Limites: new LimitesContratoSofia(limites.TamanhoTotalBytes, limites.QuantidadeArquivos),
                        DocumentosSuportados: [new DocumentoSuportadoSofia("NF-e", "55", ["4.00"], "application/xml")],
                        Idioma: "pt-BR",
                        Implantacao: modo switch
                        {
                            ModoHospedagem.SomenteAgentCard => ImplantacaoSomenteAgentCard,
                            ModoHospedagem.Homologacao => ImplantacaoHomologacao,
                            _ => null,
                        })),
            ]),
        DefaultInputModes: ["text"],
        DefaultOutputModes: ["text"],
        Skills:
        [
            new HabilidadeAgentCard(
                Id: "analisar_documentos",
                Name: "Auditoria de XMLs de NF-e",
                Description: "Recebe da SOFIA os textos de XMLs de NF-e modelo 55 com uma pergunta, valida estrutura e totalizadores e devolve achados com evidências rastreáveis.",
                Tags: ["infis", "fiscal", "sofia", "nfe", "auditoria"],
                Examples: ["Verifique divergências fiscais nos documentos."]),
        ]);

    // A versão informativa traz o commit depois de '+' (SourceLink); o card publica só a versão.
    private static string VersaoAgente()
    {
        var versao = typeof(FabricaAgentCard).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0";
        var fimVersao = versao.IndexOf('+', StringComparison.Ordinal);
        return fimVersao < 0 ? versao : versao[..fimVersao];
    }

    private static JsonSerializerOptions CriarOpcoesJson()
    {
        var opcoes = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        opcoes.MakeReadOnly();
        return opcoes;
    }
}
