# Implementation Plan: Api.RAG — base de conhecimento PgVector

**Branch**: `002-rag-pgvector` | **Date**: 2026-11-11 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification `specs/002-rag-pgvector/spec.md` e constitution 2.2.0 (§4.1, 3.2).

**Artefatos desta etapa**: [research.md](./research.md), [data-model.md](./data-model.md),
[contracts/api-rag-ingestao.md](./contracts/api-rag-ingestao.md),
[contracts/ingestao-rag-v1.schema.json](./contracts/ingestao-rag-v1.schema.json),
[quickstart.md](./quickstart.md). Contrato MCP consumidor (inalterado):
`../001-agente-auditoria-nfe/contracts/consulta-manuais-mcp.md`.

## Summary

Evoluir a Api.RAG de catálogo JSON em memória para **PostgreSQL + pgvector**, com **ingestão REST
administrada**, embeddings OpenAI e recuperação pela ferramenta MCP `buscar_trechos_manual` (contrato
0.1). O Api.Agente não muda contratos; só aponta `Mcp:Endpoint` para a RAG com base populada.

## Technical Context

**Language/Version**: C# 14 / .NET 10; ASP.NET Core 10 (Minimal APIs + MCP já existente).

**Primary Dependencies**: `ModelContextProtocol.AspNetCore` 2.3.0; `Npgsql.EntityFrameworkCore.PostgreSQL`
10.0.3; `Pgvector` + `Pgvector.EntityFrameworkCore` (central package); `Microsoft.Extensions.AI.OpenAI`
(já no monorepo) para embeddings.

**Storage**: PostgreSQL schema `rag` na mesma instância do compose (`pgvector/pgvector:0.8.7-pg17`); Neon
dedicado em homologação para a Api.RAG.

**Testing**: xUnit v3; Testcontainers PostgreSQL; testes em `tests/AuditoriaNFe.Tests/Rag/`; reutilizar
`ServidorApiRagMcp` para MCP.

**Target Platform**: Linux container (`src/Api.RAG/Dockerfile`); desenvolvimento `127.0.0.1:5320`.

**Performance Goals**: ingestão de documento sintético até ~50 chunks em < 60 s local; busca MCP p95 <
`Mcp:Timeout` do agente (10 s).

**Constraints**: sem texto oficial SEFAZ em fixtures; sem ingestão via MCP; sem acesso do agente ao banco
rag (P11).

**Scale/Scope**: v1 — dezenas de documentos, milhares de chunks; sem UI admin.

## Constitution Check

| Princípio | Avaliação |
| --------- | --------- |
| P06 Rastreabilidade | ChunkId estável + metadados de documento na execução do agente |
| P07 Conteúdo não confiável | Trechos continuam não confiáveis no LLM |
| P11 Dados por serviço | Só Api.RAG acessa schema `rag` |
| P16 Mensuração | `chamadas_embedding` no schema `rag` |
| 3.2 MCP | Somente leitura na ferramenta de busca |
| 4.1 Documentos oficiais | Ingestão com versão, vigência, hash, responsável |
| 5.1 Administração | POST/GET protegidos por Bearer |

**GATE**: Aprovado para implementação com desvio documentado: auth admin v1 por chave compartilhada
(evolução DP-10).

## Project Structure

### Documentation (this feature)

```text
specs/002-rag-pgvector/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── tasks.md
└── contracts/
```

### Source Code

```text
src/Api.RAG/
├── ApiRagHosting.cs
├── Configuracao/
├── Infraestrutura/          # DbContext, migracoes, repositorios
├── Servicos/                # Ingestao, Busca, Segmentador, CatalogoTrechos (fallback)
├── Endpoints/               # Ingestao, Documentos, Saude
└── Mcp/                     # FerramentasBaseConhecimento (delega ServicoBusca)

infra/postgres/init/
├── 03-rag-schema.sql

tests/AuditoriaNFe.Tests/
├── Rag/
└── Fixtures/Rag/
```

**Structure Decision**: manter Api.RAG como projeto web único; EF Core dentro do projeto (sem nova
camada Domínio compartilhada na v1) para reduzir escopo; reavaliar extração se crescer.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Auth Bearer estática v1 | Desbloquear ingestão local/homolog antes de DP-10 completo | Sem auth viola constitution 4.1 |
