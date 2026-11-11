# Contrato REST: ingestão e administração da base (Api.RAG)

**Versão**: 1.0 | **Feature**: `002-rag-pgvector` | **Data**: 2026-11-11

Base path: `/api/v1/rag`. Autenticação: `Authorization: Bearer <Rag:ChaveAdministracao>`.

## POST `/ingestao`

**Corpo**: JSON conforme [ingestao-rag-v1.schema.json](./ingestao-rag-v1.schema.json).

**Respostas**:

| Código | Situação |
| ------ | -------- |
| 202 | Job aceito; corpo com `jobId`, `documentoId` |
| 200 | Idempotente — mesma versão/hash já indexada |
| 400 | JSON inválido |
| 401/403 | Sem credencial admin |
| 413 | Conteúdo acima de `Rag:LimiteConteudoBytes` |
| 503 | Falha de embeddings |

Nenhum conteúdo de documento em logs (P17).

## GET `/documentos`

Query: `pagina`, `tamanhoPagina` (máx. 50). Lista metadados sem `conteudoTexto`.

## GET `/documentos/{id}`

Detalhe do documento + contagem de chunks (sem embeddings).

## Relação MCP

A recuperação continua **somente** pela ferramenta `buscar_trechos_manual` (feature 001). Este
contrato não substitui o MCP.
