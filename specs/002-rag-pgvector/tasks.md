# Tasks: Api.RAG — base de conhecimento PgVector

**Input**: Design documents from `/specs/002-rag-pgvector/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Organization**: Fases alinhadas às user stories da spec; testes incluídos (spec exige SC-001 a SC-004).

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup (Shared Infrastructure)

- [X] T201 [P] Adicionar `Pgvector` e `Pgvector.EntityFrameworkCore` em `Directory.Packages.props` e referenciar em `src/Api.RAG/Api.RAG.csproj`; `dotnet build`
- [X] T202 Criar `src/Api.RAG/Configuracao/RagOpcoes.cs` (seção `Rag`: connection string, chave admin, limites chunk, modo recuperação, fallback semente) com validação `ValidateOnStart`
- [X] T203 [P] Criar `infra/postgres/init/03-rag-schema.sql`: `CREATE EXTENSION vector`, schema `rag`, grants ao usuário app (comentário ADR-0006); atualizar compose se precisar montar o script

## Phase 2: Foundational (Blocking Prerequisites)

**Checkpoint**: DbContext aplica migrações ou script; health falha se Postgres indisponível.

- [ ] T204 Criar `src/Api.RAG/Infraestrutura/RagDbContext.cs` + entidades EF conforme `data-model.md` e primeira migração em `src/Api.RAG/Infraestrutura/Migracoes/`
- [ ] T205 Registrar DbContext, `RagOpcoes` e provedor de embeddings em `ApiRagHosting.cs` / extensões DI
- [ ] T206 [P] Criar `src/Api.RAG/Endpoints/SaudeRagEndpoints.cs`: `/health/live` (existente) e `/health/ready` com `CanConnect` ao Postgres
- [ ] T207 [P] Criar `tests/AuditoriaNFe.Tests/Rag/RagPostgresIntegracaoFixture.cs`: Testcontainers + script `03-rag-schema` (reutilizar padrão `AmbienteIntegracao`)

## Phase 3: User Story 1 — Ingerir documento (Priority: P1) 🎯 MVP

**Goal**: POST ingestão idempotente com chunks e embeddings persistidos.

**Independent Test**: POST fixture → chunks na base → contagem esperada.

### Tests US1

- [ ] T208 [P] [US1] Fixture `tests/AuditoriaNFe.Tests/Fixtures/Rag/documento-sintetico.v1.json` + teste `IngestaoRagContratoTests` validando schema `ingestao-rag-v1.schema.json`
- [ ] T209 [US1] `tests/AuditoriaNFe.Tests/Rag/IngestaoRagIntegracaoTests.cs`: POST sem Bearer → 401; ingestão duas vezes → idempotência; vigência gravada

### Implementation US1

- [ ] T210 [US1] `src/Api.RAG/Servicos/SegmentadorTexto.cs` (tamanho/overlap configuráveis, research R-04)
- [ ] T211 [US1] `src/Api.RAG/Servicos/ServicoIngestao.cs`: hash SHA-256, transação substituir chunks, estados de job
- [ ] T212 [US1] `src/Api.RAG/Infraestrutura/ProvedorEmbeddingsOpenAi.cs` + persistência `chamadas_embedding`
- [ ] T213 [US1] `src/Api.RAG/Endpoints/IngestaoEndpoints.cs`: `POST /api/v1/rag/ingestao` conforme `contracts/api-rag-ingestao.md`
- [ ] T214 [US1] Middleware/filter Bearer `Rag:ChaveAdministracao` para rotas `/api/v1/rag/*`

**Checkpoint US1**: ingestão manual via quickstart §3 funciona localmente.

## Phase 4: User Story 2 — Recuperação MCP vetorial (Priority: P1)

**Goal**: `buscar_trechos_manual` lê do banco com score; contrato 0.1 inalterado.

**Independent Test**: Ingestão + MCP retorna `chunkId` da fixture; `McpApiRagIntegracaoTests` verde com modo vetorial.

### Tests US2

- [ ] T215 [P] [US2] `tests/AuditoriaNFe.Tests/Rag/BuscaVetorialMcpIntegracaoTests.cs` (Kestrel real + Postgres, padrão `ServidorApiRagMcp`)
- [ ] T216 [US2] Teste vigência: `dataReferencia` fora do intervalo → lista vazia no MCP

### Implementation US2

- [ ] T217 [US2] `src/Api.RAG/Servicos/ServicoBuscaTrechos.cs`: filtro vigência + pgvector + boost regras (research R-05)
- [ ] T218 [US2] Alterar `FerramentasBaseConhecimento` / substituir uso direto de `CatalogoTrechos` quando `Rag:ModoRecuperacao=Vetorial`
- [ ] T219 [US2] Fallback para `CatalogoTrechos` quando base vazia e `Rag:PermitirFallbackSemente=true`
- [ ] T220 [P] [US2] Atualizar `docker-compose.yml` envs `ConnectionStrings__Rag`, `Rag__ChaveAdministracao` para api-rag

**Checkpoint US2**: etapa 5 do agente `Executada` com MCP após ingestão local.

## Phase 5: User Story 3 — Administração (Priority: P2)

- [ ] T221 [P] [US3] `GET /api/v1/rag/documentos` e `GET /api/v1/rag/documentos/{id}` em `ListagemDocumentosEndpoints.cs`
- [ ] T222 [US3] Testes integração listagem paginada sem expor embeddings

## Phase 6: Polish & Cross-Cutting

- [ ] T223 [P] Atualizar `specs/002-rag-pgvector/quickstart.md` com variáveis reais e comando compose
- [ ] T224 Registrar ADR curto `docs/adr/0015-consumo-embeddings-rag.md` (DP-15) se implementação divergir do consumo do agente
- [ ] T225 Executar `dotnet test AgenteAuditoriaNFe.slnx -c Release` e corrigir regressões

## Dependencies & Execution Order

```text
Phase 1 (T201-T203) → Phase 2 (T204-T207) → US1 (T208-T214) → US2 (T215-T220) → US3 (T221-T222) → Polish (T223-T225)
```

**Suggested MVP stop**: concluir até checkpoint US2 (agente com base real local).
