# Modelo de dados: Api.RAG (base de conhecimento)

**Feature**: `002-rag-pgvector` | **Data**: 2026-11-11 | **Plano**: [plan.md](./plan.md)

Persistência: PostgreSQL, schema **`rag`** (mesmo banco `auditoria_agente` no compose; Neon dedicado
em homologação). Classificação: trechos podem conter **Fiscal**; nunca **Pessoal** na ingestão (validação
de conteúdo mínima — rejeitar se detectar padrões CPF/CNPJ completos em fixture de teste).

## Convenções

- **Identificadores**: `Guid` v7 para entidades novas; `chunk_id` string estável exposta no MCP (ex.:
  `nt-sintetica-w16-10-001`).
- **Datas**: vigência em `date` (UTC calendário); ingestão em `timestamptz` UTC.
- **Embedding**: coluna `vector(1536)` não serializada em APIs REST.

## Visão geral

```text
DocumentoOficial 1 ──< ChunkIndexado >── 1 Embedding (coluna na mesma linha)
        │
        └──< JobIngestao (histórico de tentativas)

ChamadaEmbedding (P) ── opcional FK ── JobIngestao
```

## 1. DocumentoOficial (`rag.documentos`)

| Campo | Tipo | Regras |
| ----- | ---- | ------ |
| `id` | uuid | PK |
| `documento_id_externo` | string(128) | Identificador lógico estável (ex. `NT-2024-001`) |
| `titulo` | string(512) | Nome legível |
| `tipo_documento` | string(16) | Inicial `NF-e` (filtro MCP) |
| `modelo` | string(8) | `55` |
| `versao_layout` | string(16) | `4.00` |
| `versao_documento` | string(32) | Versão editorial do manual/NT |
| `vigencia_inicio` | date | Obrigatório |
| `vigencia_fim` | date? | Null = aberto |
| `hash_conteudo` | string(64) | SHA-256 hex do texto fonte ingerido |
| `fonte` | string(256) | URI ou rótulo (sem segredos) |
| `ingestido_em` | timestamptz | |
| `responsavel` | string(128) | Identificador do admin (header ou claim) |

**Unique**: `(documento_id_externo, versao_documento, hash_conteudo)`.

## 2. ChunkIndexado (`rag.chunks`)

| Campo | Tipo | Regras |
| ----- | ---- | ------ |
| `id` | uuid | PK |
| `chunk_id` | string(128) | Único global; retornado no MCP |
| `documento_id` | uuid | FK |
| `sequencia` | int | Ordem no documento |
| `trecho` | text | Conteúdo (Fiscal) |
| `secao` | string(128)? | |
| `pagina` | int? | |
| `regras_relacionadas` | text[] | Ids catálogo regras |
| `palavras_chave` | text[] | Opcional |
| `embedding` | vector(1536) | Obrigatório quando estado = Ativo |

**Índice**: HNSW ou IVFFlat em `embedding` (decisão na migração após volume de teste).

## 3. JobIngestao (`rag.jobs_ingestao`)

| Campo | Tipo | Regras |
| ----- | ---- | ------ |
| `id` | uuid | PK |
| `estado` | enum | `Pendente`, `Processando`, `Concluido`, `Falhou` |
| `documento_id_externo` | string | |
| `versao_documento` | string | |
| `hash_conteudo` | string | |
| `mensagem_erro` | text? | Sem stack em resposta HTTP |
| `iniciado_em` | timestamptz | |
| `concluido_em` | timestamptz? | |

## 4. ChamadaEmbedding (`rag.chamadas_embedding`)

| Campo | Tipo | Regras |
| ----- | ---- | ------ |
| `id` | uuid | PK |
| `job_ingestao_id` | uuid? | FK |
| `modelo` | string(64) | |
| `tokens_entrada` | int | |
| `custo_estimado_usd` | decimal(12,6)? | Opcional v1 |
| `registrado_em` | timestamptz | |

## Mapeamento para contrato MCP 0.1

| MCP JSON | Origem |
| -------- | ------ |
| `chunkId` | `chunks.chunk_id` |
| `documento` | `documentos.titulo` |
| `versao` | `documentos.versao_documento` |
| `vigencia.inicio/fim` | ISO date do documento |
| `localizacao` | `secao`, `pagina` |
| `trecho` | `trecho` |
| `pontuacao` | score da busca |
