// Coleções, validadores e índices do Api.Agente. Executado pelo entrypoint do MongoDB local na primeira
// inicialização (com o usuário root) e, na homologação, uma vez no Atlas com mongosh e o usuário da
// aplicação (quickstart §8.9). Não lê variáveis de ambiente e pode ser reexecutado: coleções e índices
// existentes são mantidos.
// Convenções esperadas pela Infraestrutura (T028):
// - GUIDs gravados como binData subtipo 4 (GuidRepresentation.Standard do driver);
// - datas em UTC como BSON date;
// - chaveIsolamento = SHA-256 em hexadecimal minúsculo (data-model §0).

const banco = db.getSiblingDB("auditoria_agente");
const existentes = new Set(banco.getCollectionNames());

// Uma coleção criada antes deste script (por exemplo, pela primeira gravação da aplicação) fica sem o
// validador; alterá-lo exige collMod, fora da permissão readWrite do usuário da aplicação.
function criarColecao(nome, opcoes) {
  if (!existentes.has(nome)) {
    banco.createCollection(nome, opcoes);
    return;
  }

  const atual = banco.getCollectionInfos({ name: nome })[0];
  if (opcoes && opcoes.validator && !(atual.options && atual.options.validator)) {
    throw new Error(`A coleção ${nome} já existe sem o validador $jsonSchema; remova-a ou aplique o validador com collMod.`);
  }

  print(`Coleção ${nome} já existe; mantida.`);
}

const chaveIsolamento = { bsonType: "string", pattern: "^[0-9a-f]{64}$" };
const identificador = { bsonType: "string", pattern: "^[A-Za-z0-9._:@-]{1,128}$" };
const guid = { bsonType: "binData" };

// Origem de cada item do contexto do usuário (FR-055b).
const origem = {
  bsonType: "object",
  additionalProperties: false,
  required: ["tipo", "registradoEm"],
  properties: {
    tipo: { enum: ["Execucao", "DeclaracaoUsuario"] },
    execucaoId: { bsonType: ["binData", "null"] },
    sessaoId: { bsonType: ["binData", "null"] },
    registradoEm: { bsonType: "date" }
  }
};

// Contexto do usuário (data-model §13, FR-055a): só os campos abaixo. CPF, nomes, endereços,
// valores de documentos e texto livre não têm onde ser gravados. Não existe para "anonimo" (FR-052c).
criarColecao("contextos_usuario", {
  validator: {
    $jsonSchema: {
      bsonType: "object",
      additionalProperties: false,
      required: [
        "_id", "usuarioId", "chamadorId", "cnpjsAuditados", "achadosRecorrentes",
        "execucoesContabilizadas", "criadoEm", "atualizadoEm", "ultimaAtividadeEm", "expiraEm"
      ],
      properties: {
        _id: chaveIsolamento,
        usuarioId: { ...identificador, not: { enum: ["anonimo"] } },
        chamadorId: identificador,
        cnpjsAuditados: {
          bsonType: "array",
          items: {
            bsonType: "object",
            additionalProperties: false,
            required: ["cnpj", "papel", "quantidadeDocumentos", "ultimaAuditoriaEm", "origem"],
            properties: {
              cnpj: { bsonType: "string", pattern: "^[0-9A-Z]{12}[0-9]{2}$" },
              papel: { enum: ["Emitente", "Destinatario"] },
              quantidadeDocumentos: { bsonType: ["int", "long"], minimum: 1 },
              ultimaAuditoriaEm: { bsonType: "date" },
              origem: origem
            }
          }
        },
        achadosRecorrentes: {
          bsonType: "array",
          items: {
            bsonType: "object",
            additionalProperties: false,
            required: ["chave", "categoria", "frequencia", "ultimaOcorrenciaEm", "origem"],
            properties: {
              chave: { bsonType: "string", pattern: "^[A-Za-z0-9._-]{1,64}$" },
              categoria: { enum: ["ErroEstrutural", "InconsistenciaNegocio"] },
              frequencia: { bsonType: ["int", "long"], minimum: 1 },
              ultimaOcorrenciaEm: { bsonType: "date" },
              origem: origem
            }
          }
        },
        preferencias: {
          bsonType: ["object", "null"],
          additionalProperties: false,
          required: ["nivelDetalhe", "tributosInteresse", "origem"],
          properties: {
            nivelDetalhe: { enum: ["Resumido", "Detalhado"] },
            tributosInteresse: {
              bsonType: "array",
              uniqueItems: true,
              items: { enum: ["ICMS", "ICMS-ST", "FCP", "IPI", "PIS", "COFINS", "ISSQN", "IBS", "CBS", "IS"] }
            },
            origem: origem
          }
        },
        execucoesContabilizadas: { bsonType: "array", items: guid },
        criadoEm: { bsonType: "date" },
        atualizadoEm: { bsonType: "date" },
        ultimaAtividadeEm: { bsonType: "date" },
        expiraEm: { bsonType: "date" }
      }
    }
  },
  validationLevel: "strict",
  validationAction: "error"
});

// Remoção automática no instante de expiraEm (FR-060: 90 dias sem atividade, configurável).
banco.contextos_usuario.createIndex(
  { expiraEm: 1 },
  { name: "ttl_contextos_usuario_expira_em", expireAfterSeconds: 0 });

criarColecao("sessoes");
criarColecao("execucoes");
criarColecao("documentos");
criarColecao("sessoes_agente");

// Todo acesso confere a chave de isolamento (FR-039). createIndex com o mesmo nome e a mesma
// definição não altera um índice existente.
banco.sessoes.createIndex(
  { chaveIsolamento: 1, ultimaAtividadeEm: -1 },
  { name: "ix_sessoes_chave_atividade" });

banco.execucoes.createIndex(
  { chaveIsolamento: 1, sessaoId: 1, iniciadaEm: -1 },
  { name: "ix_execucoes_chave_sessao_inicio" });

banco.documentos.createIndex(
  { chaveIsolamento: 1, sessaoId: 1 },
  { name: "ix_documentos_chave_sessao" });

banco.sessoes_agente.createIndex(
  { chaveIsolamento: 1 },
  { name: "ix_sessoes_agente_chave" });
