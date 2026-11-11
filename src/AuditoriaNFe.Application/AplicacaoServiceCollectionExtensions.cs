using AuditoriaNFe.Application.Agente;
using AuditoriaNFe.Application.CasosDeUso;
using AuditoriaNFe.Application.Ferramentas;
using AuditoriaNFe.Application.Fluxo;
using AuditoriaNFe.Application.Middleware;
using AuditoriaNFe.Application.Servicos;
using AuditoriaNFe.Application.Sessoes;
using Microsoft.Extensions.DependencyInjection;

namespace AuditoriaNFe.Application;

/// <summary>Fluxo de nove etapas, agente interno, sessões, contexto do usuário e casos de uso.</summary>
public static class AplicacaoServiceCollectionExtensions
{
    public static IServiceCollection AdicionarFluxoAuditoria(this IServiceCollection services)
    {
        services.AddSingleton<MiddlewareExecucaoAgente>();
        services.AddSingleton<MiddlewareFunctionCalling>();
        services.AddSingleton<FerramentasLeitura>();
        services.AddSingleton<FabricaAgenteInterno>();
        services.AddSingleton<ValidadorSaidaIa>();
        services.AddSingleton<AtualizadorContextoUsuario>();
        services.AddSingleton<FluxoNoveEtapas>();
        services.AddSingleton<ServicoSessoes>();
        services.AddSingleton<ExecutarSolicitacaoAuditoria>();
        services.AddSingleton<GerirContextoUsuario>();
        return services;
    }
}
