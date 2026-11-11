# Research: Api.RAG — base de conhecimento PgVector

**Feature**: `002-rag-pgvector` | **Data**: 2026-11-11

## R-01 — Topologia PostgreSQL (ADR-0006, ADR-0013)

- **Decisão**: Mesma instância PostgreSQL do compose (`pgvector/pgvector:0.8.7-pg17`, porta host
  `5436`) com **schema `rag`** exclusivo da Api.RAG; o schema `consumo` permanece do Api.Agente.
- **Rationale**: ADR-0006 e research R-24 já preveem extensão `vector` no mesmo contêiner; evita
  segundo Postgres em desenvolvimento.
- **Homologação**: banco/projeto Neon separado só para a Api.RAG (não misturar com `consumo` do
  agente na Vercel).
- **Alternativas**: Postgres dedicado no compose só para RAG (rejeitado: custo operacional local sem
  ganho na v1).

## R-02 — Extensão e acesso vetorial

- **Decisão**: `CREATE EXTENSION IF NOT EXISTS vector` no schema `rag` (script init
  `infra/postgres/init/03-rag-schema.sql` + migrações EF da Api.RAG).
- **Biblioteca**: `Pgvector` + `Pgvector.EntityFrameworkCore` (Central Package Management, versão
  alinhada ao Npgsql 10.0.3) para mapear coluna `vector(1536)`.
- **Métrica**: similaridade por **cosseno** (`vector_cosine_ops`); consulta ordenada por distância
  cosseno com filtro SQL de vigência antes do `ORDER BY`.
- **Alternativas**: busca só lexical (rejeitado: não atende FR-007 em escala); Qdrant externo
  (rejeitado: constitution exige Postgres PgVector).

## R-03 — Modelo de embeddings (DP-07, DP-15)

- **Decisão**: OpenAI `text-embedding-3-small`, **1536 dimensões**, via `Microsoft.Extensions.AI`
  ou cliente OpenAI já usado no monorepo; dimensão fixa em `Rag:EmbeddingDimensao` e validada na
  migração.
- **Rationale**: Mesmo provedor da feature 001; custo menor que `large`; 1536 é o default do modelo
  small.
- **Registro de consumo**: tabela `rag.chamadas_embedding` espelhando o padrão de `consumo` (tokens,
  modelo, origem `Ingestao`, correlacao com `job_ingestao_id`), sem conteúdo de documento (P11/P17).

## R-04 — Chunking e idempotência

- **Decisão**: segmentação por **caracteres** na v1 (não tokenizer): `Rag:ChunkTamanhoCaracteres`
  = 2000, `Rag:ChunkSobreposicaoCaracteres` = 200; quebra preferencial em parágrafo (`\n\n`).
- **Idempotência**: chave lógica `(documento_id_externo, versao, hash_conteudo_sha256)`; reingestão
  substitui chunks da mesma versão em transação.
- **Alternativas**: chunk por página PDF (fase posterior quando houver extrator PDF).

## R-05 — Modo de recuperação MCP

- **Decisão**: `buscar_trechos_manual` consulta o banco quando `Rag:ModoRecuperacao` = `Vetorial`
  (padrão após primeira migração); fallback para `CatalogoTrechos` (JSON) quando
  `Rag:ModoRecuperacao` = `Semente` ou zero chunks indexados e `Rag:PermitirFallbackSemente` = true.
- **Pontuação**: combinar score de similaridade (0–1) com boost por `regras_relacionadas`
  (+0.2 por regra coincidente, cap 1.0), compatível com contrato 0.1.
- **Contrato**: inalterado em `../001-agente-auditoria-nfe/contracts/consulta-manuais-mcp.md`.

## R-06 — API REST de ingestão e administração

- **Decisão**: Minimal APIs na Api.RAG sob prefixo `/api/v1/rag`:
  - `POST /api/v1/rag/ingestao` — corpo multipart ou JSON com texto + metadados (v1: JSON/texto).
  - `GET /api/v1/rag/documentos` — listagem paginada (US3).
  - `GET /api/v1/rag/documentos/{id}` — detalhe (US3).
- **Autenticação v1**: header `Authorization: Bearer` com segredo `Rag:ChaveAdministracao`
  (desenvolvimento/local; evolução para DP-10).
- **Alternativas**: ingestão via MCP (rejeitado: constitution 3.2).

## R-07 — Testes

- **Decisão**: Testcontainers Postgres existente em `AmbienteIntegracao` + novos testes em
  `tests/AuditoriaNFe.Tests/Rag/` (integração ingestão + MCP) e contratos OpenAPI/JSON da ingestão.
- **Fixtures**: documentos **sintéticos** em `tests/.../Fixtures/Rag/` (sem texto oficial SEFAZ).

## R-08 — Deploy

- **Decisão**: Api.RAG permanece serviço **separado** (compose e futuro container próprio); agente
  na Vercel aponta `Mcp__Endpoint` para URL pública da RAG quando habilitado.
- **Fora da v1**: imagem RAG na Vercel (serverless); priorizar compose + Neon.
