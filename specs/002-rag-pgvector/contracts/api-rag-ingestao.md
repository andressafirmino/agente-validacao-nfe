# Contrato REST: ingestão e administração da base (Api.RAG)

**Versão**: 1.0 | **Feature**: `002-rag-pgvector` | **Data**: 2026-11-11

Base path: `/api/v1/rag`. Autenticação: `Authorization: Bearer <Rag:ChaveAdministracao>`.

## POST `/ingestao`

**Corpo**: JSON conforme [ingestao-rag-v1.schema.json](./ingestao-rag-v1.schema.json).

## POST `/ingestao/pdf`

**Corpo**: `multipart/form-data`

| Campo | Obrigatório | Descrição |
| ----- | ----------- | --------- |
| `arquivo` | Sim | PDF (`application/pdf`) |
| `documentoIdExterno` | Sim | Id estável do documento |
| `titulo` | Sim | Título para listagem |
| `versaoDocumento` | Sim | Versão do conteúdo |
| `vigenciaInicio` | Sim | `yyyy-MM-dd` |
| `vigenciaFim` | Não | `yyyy-MM-dd` |
| `tipoDocumento`, `modelo`, `versaoLayout` | Não | Padrões `NF-e`, `55`, `4.00` |
| `regrasRelacionadas` | Não | IDs separados por vírgula (`W16-10,W04-10`) |
| `fonte` | Não | Proveniência; padrão = nome do arquivo |

O texto é extraído do PDF (sem OCR) e reutiliza o mesmo pipeline de chunking/embeddings do POST JSON.
Respostas e códigos HTTP iguais ao `/ingestao`.

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
