using AuditoriaNFe.Application.Abstracoes;
using AuditoriaNFe.Application.Configuracao;
using AuditoriaNFe.Domain.Regras;
using AuditoriaNFe.Infrastructure.Catalogos;
using AuditoriaNFe.Infrastructure.Comum;
using AuditoriaNFe.Infrastructure.Ia;
using AuditoriaNFe.Infrastructure.Mcp;
using AuditoriaNFe.Infrastructure.Schemas;
using AuditoriaNFe.Infrastructure.Xml;
using AuditoriaNFe.Infrastructure.Persistencia.Mongo;
using AuditoriaNFe.Infrastructure.Persistencia.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuditoriaNFe.Infrastructure;

/// <summary>Composição da Infraestrutura usada pelo host.</summary>
public static class InfraestruturaServiceCollectionExtensions
{
    public static IServiceCollection AdicionarComum(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IRelogio, RelogioSistema>();
        services.TryAddSingleton<IGeradorIds, GeradorIdsV7>();
        return services;
    }

    public static IServiceCollection AdicionarPersistenciaMongo(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(provedor => new ContextoMongo(
            configuration.GetConnectionString(ContextoMongo.NomeConnectionString)
                ?? throw new InvalidOperationException($"ConnectionStrings:{ContextoMongo.NomeConnectionString} é obrigatória."),
            provedor.GetRequiredService<IOptions<PersistenciaOpcoes>>()));
        services.AddSingleton<ISessaoRepositorio, SessaoRepositorio>();
        services.AddSingleton<IExecucaoRepositorio, ExecucaoRepositorio>();
        services.AddSingleton<IDocumentoRepositorio, DocumentoRepositorio>();
        services.AddSingleton<IContextoUsuarioRepositorio, ContextoUsuarioRepositorio>();
        return services;
    }

    public static IServiceCollection AdicionarPersistenciaConsumo(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConsumoDbContext.NomeConnectionString)
            ?? throw new InvalidOperationException($"ConnectionStrings:{ConsumoDbContext.NomeConnectionString} é obrigatória.");
        services.AddDbContextFactory<ConsumoDbContext>((provedor, builder) =>
            ConsumoDbContext.Configurar(builder, connectionString, provedor.GetRequiredService<IOptions<PersistenciaOpcoes>>().Value));
        return services;
    }

    /// <summary>
    /// Cliente de IA com registro de consumo; <paramref name="ambiente"/> restringe o provedor simulado.
    /// Requer o <c>IMeterFactory</c> registrado pelo host.
    /// </summary>
    public static IServiceCollection AdicionarIa(this IServiceCollection services, string ambiente)
    {
        services.AddSingleton(provedor => TabelaPrecos.Carregar(
            Path.Combine(AppContext.BaseDirectory, provedor.GetRequiredService<IOptions<CatalogosOpcoes>>().Value.Precos)));
        services.AddSingleton<CalculadoraCusto>();
        services.AddSingleton<GravadorConsumo>();
        services.AddSingleton(provedor => new FabricaChatClient(
            provedor.GetRequiredService<IOptions<IaOpcoes>>(),
            provedor.GetRequiredService<IOptions<OpenAIOpcoes>>(),
            provedor.GetRequiredService<GravadorConsumo>(),
            provedor.GetRequiredService<ILoggerFactory>(),
            ambiente));
        services.TryAddSingleton(provedor => provedor.GetRequiredService<FabricaChatClient>().Criar());
        return services;
    }

    /// <summary>
    /// Leitura, identificação, schemas, catálogos e consulta a manuais. Manifesto e catálogos são
    /// carregados uma vez; carga inválida lança <see cref="CargaInvalidaException"/>.
    /// </summary>
    public static IServiceCollection AdicionarProcessamentoNFe(this IServiceCollection services)
    {
        services.AddSingleton(provedor =>
        {
            var xml = provedor.GetRequiredService<IOptions<XmlOpcoes>>().Value;
            var limites = provedor.GetRequiredService<IOptions<LimitesOpcoes>>().Value;
            return new LeitorXmlSeguro(xml.ProfundidadeMaxima, xml.MaxCaracteresEfetivo(limites));
        });
        services.AddSingleton(provedor => new ProvedorPacotesSchema(
            Path.Combine(AppContext.BaseDirectory, provedor.GetRequiredService<IOptions<SchemasOpcoes>>().Value.Diretorio)));
        services.AddSingleton(provedor => new ValidadorXsd(provedor.GetRequiredService<ProvedorPacotesSchema>()));
        services.AddSingleton(provedor =>
        {
            var catalogos = provedor.GetRequiredService<IOptions<CatalogosOpcoes>>().Value;
            return CarregadorCatalogos.Carregar(
                Path.Combine(AppContext.BaseDirectory, catalogos.Regras),
                Path.Combine(AppContext.BaseDirectory, catalogos.Extracao),
                ImplementacoesRegras.Todas());
        });
        services.AddSingleton(provedor => new ExtratorDados(provedor.GetRequiredService<CatalogosCarregados>().CamposExtracao));
        services.AddSingleton<ICatalogoRegras>(provedor => new CatalogoRegras(provedor.GetRequiredService<CatalogosCarregados>()));
        services.AddSingleton<IProcessadorXml, ProcessadorXml>();
        services.TryAddSingleton<IConsultaManuais>(provedor =>
            provedor.GetRequiredService<IOptions<McpOpcoes>>().Value.Habilitado
                ? new ConsultaManuaisMcp(provedor.GetRequiredService<IOptions<McpOpcoes>>(), provedor.GetRequiredService<ILoggerFactory>())
                : new ConsultaManuaisDesabilitada());
        services.TryAddSingleton<IFerramentasExternas>(provedor =>
        {
            var mcp = provedor.GetRequiredService<IOptions<McpOpcoes>>().Value;
            return mcp is { Habilitado: true, FerramentasExternasHabilitado: true }
                ? new FerramentasExternasMcp(provedor.GetRequiredService<IOptions<McpOpcoes>>(), provedor.GetRequiredService<ILoggerFactory>())
                : new FerramentasExternasDesabilitada();
        });
        return services;
    }
}
