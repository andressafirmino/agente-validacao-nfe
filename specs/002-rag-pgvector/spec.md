# Feature Specification: Api.RAG — base de conhecimento com PgVector

**Feature Branch**: `002-rag-pgvector`

**Created**: 2026-11-11

**Status**: Draft

**Input**: Base de conhecimento Api.RAG com ingestão PostgreSQL pgvector, embeddings e recuperação MCP alinhada ao contrato `consulta-manuais-mcp.md` (feature 001).

**Constitution**: v2.2.0 — princípios P01–P18; seções 3.2 (MCP somente leitura), 4.1 (documentos oficiais), 5.1 (segurança administrativa). DP-03 (manuais a ingerir), DP-07 (limiar de relevância), DP-14 (sem dados pessoais na consulta), DP-15 (consumo de embeddings), DP-18 (transporte MCP).

**Dependências**: Feature `001-agente-auditoria-nfe` (cliente `ConsultaManuaisMcp`, etapa 5/6; PR com Api.RAG semente e MCP v1). Contrato consumidor: `specs/001-agente-auditoria-nfe/contracts/consulta-manuais-mcp.md`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ingerir documento oficial versionado (Priority: P1)

Como **administrador da base**, quero enviar um manual ou nota técnica oficial (PDF ou texto estruturado) com metadados de versão e vigência, para que o agente recupere trechos corretos na data de referência da NF-e.

**Why this priority**: Sem ingestão persistida não há base real; o catálogo JSON atual é apenas semente de desenvolvimento.

**Independent Test**: POST de ingestão com documento sintético de teste → chunks e embeddings gravados → `buscar_trechos_manual` retorna trecho com `chunkId` estável e vigência válida.

**Acceptance Scenarios**:

1. **Given** base vazia, **When** admin envia ingestão idempotente do mesmo documento/versão duas vezes, **Then** não há chunks duplicados e a segunda operação é no-op ou substituição explícita da mesma versão.
2. **Given** documento vigente em 2024-06-01, **When** consulta MCP com `dataReferencia` anterior à vigência, **Then** o trecho não é retornado.
3. **Given** ingestão sem credencial administrativa, **When** POST é chamado, **Then** 401/403 sem alteração de dados.

---

### User Story 2 - Recuperação vetorial via MCP (Priority: P1)

Como **Api.Agente**, quero que `buscar_trechos_manual` consulte PgVector filtrando por tipo/modelo/layout e data, retornando JSON contrato 0.1, para cumprir a etapa 5 sem alterar o consumidor.

**Why this priority**: É o contrato já implementado no agente; a troca deve ser interna à Api.RAG.

**Independent Test**: Testcontainers Postgres com `vector` → ingestão de fixture → chamada MCP igual aos testes `McpApiRagIntegracaoTests` com trecho real.

**Acceptance Scenarios**:

1. **Given** chunks indexados, **When** `regrasRelacionadas` contém id do catálogo, **Then** trechos relacionados pontuam acima de irrelevantes (mesmo contrato de saída).
2. **Given** provedor de embeddings indisponível na ingestão, **When** job falha, **Then** estado de ingestão registra falha e consulta não expõe chunks incompletos.
3. **Given** consulta sem resultados, **When** MCP responde lista vazia válida, **Then** agente marca etapa 5 como Executada (FR-022).

---

### User Story 3 - Administração e rastreabilidade (Priority: P2)

Como **administrador**, quero listar documentos ingeridos, versão, hash, responsável e datas, para auditoria e reprocessamento controlado.

**Why this priority**: Constitution 4.1 exige rastreabilidade de ingestão; suporta DP-15 e operação em homologação.

**Independent Test**: API REST autenticada lista documentos após ingestão; campos obrigatórios da constitution presentes.

**Acceptance Scenarios**:

1. **Given** documento ingerido, **When** admin consulta por `documentoId`, **Then** vê versão, vigência, hash, data e responsável.
2. **Given** nova versão do mesmo manual, **When** ingestão concluída, **Then** buscas na vigência nova usam chunks da versão correta sem misturar versão antiga na mesma data.

---

### Edge Cases

- Documento acima do tamanho máximo configurado → rejeição sem persistência parcial.
- MIME/conteúdo inválido → 400 com código estável; nada gravado.
- Neon/compose sem extensão `vector` → falha de inicialização clara na Api.RAG.
- Homologação Vercel: ingestão e admin **não** expostos publicamente sem autenticação (D-05 / DP-10 evolução).
- Chunk referenciado na execução do agente com `chunkId` removido → consumidor já trata via conjunto registrado na execução (P06).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A Api.RAG DEVE persistir documentos e chunks em PostgreSQL com extensão `vector`, schema dedicado (separado do schema `consumo` do agente).
- **FR-002**: DEVE existir endpoint POST de ingestão documentado, com tamanho máximo, validação de MIME/conteúdo e **autorização administrativa** (constitution 4.1).
- **FR-003**: A ingestão DEVE ser idempotente por `(documento, versão, hash do conteúdo)` conforme constitution 4.1.
- **FR-004**: DEVE registrar por documento: fonte, versão, vigência, hash, data da ingestão, responsável.
- **FR-005**: DEVE segmentar texto com parâmetros configuráveis (tamanho/overlap) documentados no plano/ADR.
- **FR-006**: DEVE gerar embeddings com modelo e dimensão fixados em configuração e ADR (DP-07, DP-15).
- **FR-007**: A ferramenta MCP `buscar_trechos_manual` DEVE manter contrato 0.1 e comportamento descrito em `consulta-manuais-mcp.md`.
- **FR-008**: A recuperação DEVE filtrar por vigência em `dataReferencia`, tipo `NF-e`, modelo `55`, layout `4.00` (extensível por configuração).
- **FR-009**: Operações de escrita (ingestão, exclusão, reprocessamento) NÃO DEVEM ser expostas via MCP (constitution 3.2).
- **FR-010**: DEVE registrar consumo de tokens/custo de embeddings no PostgreSQL da Api.RAG (DP-15), sem violar P11.
- **FR-011**: DEVE manter fallback ou migração do catálogo semente JSON até a primeira ingestão real (compatível com compose local).

### Key Entities

- **DocumentoOficial**: manual ou NT; metadados de identificação, versão, vigência, hash, auditoria de ingestão.
- **Chunk**: trecho de texto, localização, embedding, referência ao documento, `chunkId` estável.
- **JobIngestao**: estado (pendente, concluído, falhou), correlação, mensagens de erro operacionais.
- **ChamadaEmbedding**: métricas de consumo para DP-15.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Após ingestão de fixture oficial sintética, etapa 5 do agente com MCP habilitado chega a `Executada` e cita `chunkId` persistido (teste automatizado).
- **SC-002**: Reingestão do mesmo arquivo/versão não aumenta contagem de chunks ativos.
- **SC-003**: Consulta com `dataReferencia` fora da vigência retorna zero trechos em 100% dos casos do conjunto de teste de vigência.
- **SC-004**: Ingestão sem credencial administrativa é recusada em 100% das tentativas nos testes de contrato.

## Assumptions

- Provedor de embeddings inicial: OpenAI (mesma família de configuração da feature 001), com chave só em ambiente/secret.
- PostgreSQL local: mesmo contêiner compose com script de extensão `vector`; homologação: banco Neon dedicado à Api.RAG (ADR-0013).
- Primeira onda de conteúdo: NTs e trechos já referenciados no catálogo de regras (`W04-10`, `W16-10`, etc.), não inventar texto normativo nos testes.
- Api.Agente continua único consumidor da recuperação via MCP; sem acesso direto ao banco vetorial.
- Autenticação administrativa na v1 da feature 002: chave Bearer em configuração (evolução para DP-10 alinhada ao agente).

## Fora de escopo (v1 desta feature)

- Deploy da Api.RAG na Vercel no mesmo projeto do agente (planejar serviço separado ou compose/cloud dedicado).
- Ingestão em lote massiva de todos os manuais SEFAZ sem lista DP-03 fechada.
- Ferramentas MCP de escrita ou administração.
