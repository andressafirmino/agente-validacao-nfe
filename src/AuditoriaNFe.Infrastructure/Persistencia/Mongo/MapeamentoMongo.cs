using AuditoriaNFe.Domain.ContextoUsuario;
using AuditoriaNFe.Domain.Documentos;
using AuditoriaNFe.Domain.Parecer;
using AuditoriaNFe.Domain.Sessoes;
using AuditoriaNFe.Domain.Validacao;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace AuditoriaNFe.Infrastructure.Persistencia.Mongo;

/// <summary>
/// Mapeamento das entidades do Domínio para BSON, registrado uma vez por processo: GUID em
/// representação padrão (binData 4), datas como BSON date em UTC, decimais como Decimal128, enums
/// como texto e nomes em camelCase (convenções de <c>infra/mongodb/init/01-colecoes.js</c>).
/// </summary>
internal static class MapeamentoMongo
{
    private static readonly Lock Trava = new();
    private static bool _registrado;

    public static void Registrar()
    {
        lock (Trava)
        {
            if (_registrado)
            {
                return;
            }

            BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
            BsonSerializer.TryRegisterSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
            BsonSerializer.TryRegisterSerializer(new DecimalSerializer(BsonType.Decimal128));
            BsonSerializer.TryRegisterSerializer(new EstadoValidacaoSerializer());

            var convencoes = new ConventionPack
            {
                new CamelCaseElementNameConvention(),
                new IgnoreExtraElementsConvention(true),
                new EnumRepresentationConvention(BsonType.String),
            };
            ConventionRegistry.Register(
                "AuditoriaNFe",
                convencoes,
                tipo => tipo.Namespace?.StartsWith("AuditoriaNFe.", StringComparison.Ordinal) == true);

            BsonClassMap.TryRegisterClassMap<Sessao>(mapa =>
            {
                mapa.AutoMap();
                mapa.MapField("_documentos").SetElementName("documentos");
                mapa.MapField("_execucoes").SetElementName("execucoes");
                mapa.MapField("_hashesPorChave").SetElementName("hashesPorChave");
            });

            BsonClassMap.TryRegisterClassMap<Execucao>(mapa =>
            {
                mapa.AutoMap();
                mapa.MapField("_etapas").SetElementName("etapas");
                mapa.MapField("_arquivos").SetElementName("arquivos");
                mapa.MapField("_chamadasFerramentas").SetElementName("chamadasFerramentas");
                mapa.MapField("_trechosConsultados").SetElementName("trechosConsultados");
                mapa.MapField("_versoes").SetElementName("versoes");
            });

            BsonClassMap.TryRegisterClassMap<EtapaExecucao>(mapa => mapa.AutoMap());

            BsonClassMap.TryRegisterClassMap<ArquivoRecebido>(mapa =>
            {
                mapa.AutoMap();
                mapa.MapField("_regrasAplicadas").SetElementName("regrasAplicadas");
            });

            BsonClassMap.TryRegisterClassMap<ValidacaoLayout>(mapa =>
            {
                mapa.AutoMap();
                mapa.MapField("_pacotesTentados").SetElementName("pacotesTentados");
            });

            BsonClassMap.TryRegisterClassMap<ParecerExecucao>(mapa =>
            {
                mapa.AutoMap();
                mapa.MapField("_achados").SetElementName("achados");
                mapa.MapField("_limitacoes").SetElementName("limitacoes");
                mapa.MapField("_validacoesNaoRealizadas").SetElementName("validacoesNaoRealizadas");
            });

            BsonClassMap.TryRegisterClassMap<ContextoUsuario>(mapa =>
            {
                mapa.AutoMap();
                mapa.MapIdProperty(c => c.Id);
                mapa.MapField("_cnpjsAuditados").SetElementName("cnpjsAuditados");
                mapa.MapField("_achadosRecorrentes").SetElementName("achadosRecorrentes");
                mapa.MapField("_execucoesContabilizadas").SetElementName("execucoesContabilizadas");
            });

            _registrado = true;
        }
    }

    /// <summary>Forma textual do contrato: <c>Realizada</c> ou <c>NaoRealizada — motivo</c>.</summary>
    private sealed class EstadoValidacaoSerializer : SerializerBase<EstadoValidacao>
    {
        private const string PrefixoNaoRealizada = "NaoRealizada — ";

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, EstadoValidacao value) =>
            context.Writer.WriteString(value.ToString());

        public override EstadoValidacao Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var texto = context.Reader.ReadString();
            return texto.StartsWith(PrefixoNaoRealizada, StringComparison.Ordinal)
                ? EstadoValidacao.NaoRealizada(texto[PrefixoNaoRealizada.Length..])
                : EstadoValidacao.Realizada;
        }
    }
}
