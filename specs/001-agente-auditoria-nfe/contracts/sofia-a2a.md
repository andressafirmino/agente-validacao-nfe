# Contrato A2A: SOFIA ↔ AgenteAuditoriaNFe

**Situação**: **contrato SOFIA v1.0** (padrão da turma INFIS para agentes especializados), resolve a
DP-27 (constitution 2.2.0) | **Versão do contrato**: `1.0` | **Perfil de protocolo**: A2A v0.3.x,
JSON-RPC 2.0 (`message/send`) | **SDK**: `A2A` 1.0.0-preview3 com a camada oficial
`A2A.V0_3Compat` 1.0.0-preview3 (research R-11) | **Decisão**: ADR-0011 | **Feature**:
`001-agente-auditoria-nfe` | **Data**: 2026-11-05

Este documento substitui a proposta v1 provisória de 2026-11-04. A SOFIA envia uma
`SolicitacaoAnaliseSofia` serializada em JSON dentro de uma Message A2A; o agente devolve uma Task
A2A concluída com um Artifact que contém um `ResultadoAnaliseSofia`. Não há API REST de análise
disfarçada de A2A: o canal direto ([api-agente.openapi.yaml](./api-agente.openapi.yaml)) continua
existindo para os demais clientes e usa o mesmo caso de uso.

A versão do contrato de negócio (`versaoContrato = "1.0"`) é independente da versão do protocolo
A2A (perfil v0.3.x). Os JSON Schemas do contrato estão em
[sofia-solicitacao-analise-v1.schema.json](./sofia-solicitacao-analise-v1.schema.json) e
[sofia-resultado-analise-v1.schema.json](./sofia-resultado-analise-v1.schema.json).

Este contrato complementa a [spec](../spec.md) (FR-043 a FR-047, FR-035g, FR-048 a FR-052d) e o
[modelo de dados](../data-model.md) (§0, §2, §3 e §18).

> **Credencial de serviço da SOFIA (FR-052h, ADR-0014, 2026-11-10).** O Agent Card e o endpoint A2A
> exigem `Authorization: Bearer {chave}`, com a chave compartilhada da turma configurada em
> `SOFIA_A2A_TOKEN`. A autenticação é de transporte (fora dos DTOs) e não identifica o chamador nem
> o usuário: isso continua no desvio D-05 e na DP-10. Publicação fora da máquina local: só o card
> no modo `SomenteAgentCard` (FR-052f, D-08, ADR-0012) ou a homologação (FR-052g, D-09, ADR-0013).

## 1. Endereços

| Recurso | Método e caminho | Autenticação |
| ------- | ---------------- | ------------ |
| Agent Card | `GET /.well-known/agent-card.json` | Bearer da credencial de serviço da SOFIA (FR-052h); sem segredos nem dados de usuário (FR-045) |
| Endpoint A2A (JSON-RPC 2.0) | `POST /a2a/auditoria-nfe`, método `message/send` | Bearer da credencial de serviço da SOFIA (FR-052h); vale para todo caminho sob `/a2a` |

- Sem o cabeçalho, com cabeçalho repetido ou malformado, ou com outra chave: HTTP 401 com
  `WWW-Authenticate: Bearer`, sem corpo JSON-RPC e antes de qualquer processamento. O esquema
  `Bearer` não diferencia maiúsculas; a chave sim. A chave na URL ou no corpo não é aceita.
- `GET /health/live` não exige a credencial.

- A URL base é configurável (`A2A:UrlPublica`; no ambiente local, `http://localhost:5310`). O campo
  `url` do card é `{A2A:UrlPublica}/a2a/auditoria-nfe`.
- O card é servido no formato v0.3 estrito quando o cliente não envia o cabeçalho `A2A-Version` ou
  envia `A2A-Version: 0.3`; com `A2A-Version: 1.0`, o mesmo card sai no formato A2A 1.0
  (`MapAgentCardGetWithV03Compat` com `blendedCard = false`).
- O endpoint aceita requisições v0.3 e v1.0 (`MapA2AWithV03Compat`); só o perfil v0.3
  (`message/send`) é contratual. Streaming (`message/stream`), `tasks/resubscribe` e notificações
  push não são oferecidos (`capabilities.streaming = false`, `pushNotifications = false`).

## 2. Agent Card

Gerado a partir da configuração na inicialização. Exemplo no formato v0.3 (os nomes exatos são os
produzidos pelo `V03TypeConverter` do SDK e são conferidos pelo teste de contrato):

```json
{
  "name": "AgenteAuditoriaNFe",
  "description": "Analisa XMLs de NF-e (modelo 55, layout 4.00) enviados pela SOFIA: valida a estrutura contra o schema oficial vigente na data de cada documento e confere os totalizadores do catálogo aprovado, sem inventar valores. Acesso pela credencial de serviço da SOFIA; somente dados sintéticos.",
  "url": "http://localhost:5310/a2a/auditoria-nfe",
  "version": "1.0.0",
  "protocolVersion": "0.3.0",
  "preferredTransport": "JSONRPC",
  "capabilities": {
    "streaming": false,
    "pushNotifications": false,
    "extensions": [
      {
        "uri": "urn:infis:sofia:contrato",
        "description": "Contrato SOFIA v1.0: SolicitacaoAnaliseSofia em TextPart; ResultadoAnaliseSofia em Artifact de Task concluída.",
        "required": false,
        "params": {
          "versaoContrato": "1.0",
          "idAgente": "agente-auditoria-nfe",
          "nomeArtifact": "analise-sofia-v1",
          "autenticacao": "bearer — credencial de serviço da SOFIA",
          "limites": { "tamanhoTotalBytes": 5242880, "quantidadeDocumentos": 20 },
          "documentosSuportados": [{ "tipo": "NF-e", "modelo": "55", "versoesLayout": ["4.00"], "tipoMime": "application/xml" }],
          "idioma": "pt-BR"
        }
      }
    ]
  },
  "defaultInputModes": ["text"],
  "defaultOutputModes": ["text"],
  "skills": [
    {
      "id": "analisar_documentos",
      "name": "Auditoria de XMLs de NF-e",
      "description": "Recebe da SOFIA os textos de XMLs de NF-e modelo 55 com uma pergunta, valida estrutura e totalizadores e devolve achados com evidências rastreáveis.",
      "tags": ["infis", "fiscal", "sofia", "nfe", "auditoria"],
      "examples": ["Verifique divergências fiscais nos documentos."]
    }
  ],
  "securitySchemes": {
    "sofiaService": { "type": "http", "scheme": "bearer" }
  },
  "security": [{ "sofiaService": [] }]
}
```

Regras:

- O card publica nome, descrição, versão do agente (`version`), versão do contrato e `idAgente`
  (extensão), a habilidade, os formatos, os limites e o esquema de autenticação (FR-044, FR-052h).
- `idAgente` vem de `A2A:IdAgente` (inicial `agente-auditoria-nfe`) e DEVE ser igual ao cadastrado
  no registry da SOFIA e ao devolvido em `ResultadoAnaliseSofia.agente.idAgente`.
- O id da habilidade é `analisar_documentos`, o mesmo do padrão da turma.
- O card declara o esquema `sofiaService` (`http`, `bearer`) em `securitySchemes` e o exige em
  `security`, sem o valor da chave (FR-052h, ADR-0014). A SOFIA recusa cards sem esse esquema
  (`UNSUPPORTED_AGENT_SECURITY`). A autenticação é de transporte e não muda a versão `1.0` do
  contrato de negócio.
- Mudança de limite por configuração altera o card sem nova versão do contrato (FR-005).
- O card não contém segredos, chaves, endereços internos nem dados de usuários (FR-045).
- No modo `SomenteAgentCard` (FR-052f, D-08), a extensão acrescenta o parâmetro
  `"implantacao": "somente Agent Card publicado; message/send indisponível nesta URL"`. Os demais
  campos não mudam. O parâmetro é opcional para a SOFIA e não altera a versão do contrato.

## 3. Solicitação (SOFIA → agente)

### 3.1 Envelope A2A

```json
{
  "jsonrpc": "2.0",
  "id": "rpc-001",
  "method": "message/send",
  "params": {
    "message": {
      "kind": "message",
      "role": "user",
      "messageId": "5f92c2ba-6870-4862-87ba-1ae8669a77f4",
      "parts": [
        {
          "kind": "text",
          "text": "{\"versaoContrato\":\"1.0\",\"idExecucao\":\"8b2f0ad6-c2e4-4a68-8bdd-20f785bc798e\",\"idSolicitacao\":\"e1b3a4cb-d729-4bd4-b796-b9c94be07b46\",\"usuario\":{\"nome\":\"Maria Oliveira\"},\"pergunta\":\"Verifique divergências fiscais nos documentos.\",\"documentos\":[{\"idDocumento\":\"doc-001\",\"nome\":\"NFe-123.xml\",\"tipo\":\"application/xml\",\"texto\":\"<?xml version=\\\"1.0\\\" encoding=\\\"UTF-8\\\"?><nfeProc versao=\\\"4.00\\\" xmlns=\\\"http://www.portalfiscal.inf.br/nfe\\\">…</nfeProc>\"}],\"contexto\":{\"idioma\":\"pt-BR\",\"instrucoes\":\"Analisar conforme a especialidade do agente.\"}}"
        }
      ]
    }
  }
}
```

| Campo A2A | Regra |
| --------- | ----- |
| `method` | `message/send` |
| `params.message.role` | `user` |
| `params.message.messageId` | Obrigatório (A2A); não é usado como identificador de negócio |
| `params.message.contextId` | Opcional; não abre nem continua sessão (o contrato v1 não tem sessão). Quando informado, é devolvido na Task |
| `params.message.parts` | Exatamente uma parte `kind: "text"` com o JSON da `SolicitacaoAnaliseSofia` em UTF-8 |
| `params.configuration` | A SOFIA DEVE omitir o campo ou enviar `"blocking": true`. Com `blocking: false`, a camada de compatibilidade pode devolver só o retrato inicial da Task, e o resultado não é garantido na resposta (fora do contrato) |

### 3.2 `SolicitacaoAnaliseSofia`

```json
{
  "versaoContrato": "1.0",
  "idExecucao": "8b2f0ad6-c2e4-4a68-8bdd-20f785bc798e",
  "idSolicitacao": "e1b3a4cb-d729-4bd4-b796-b9c94be07b46",
  "usuario": { "nome": "Maria Oliveira" },
  "pergunta": "Verifique divergências fiscais nos documentos.",
  "documentos": [
    { "idDocumento": "doc-001", "nome": "NFe-123.xml", "tipo": "application/xml", "texto": "<?xml version=\"1.0\" encoding=\"UTF-8\"?><nfeProc versao=\"4.00\" xmlns=\"http://www.portalfiscal.inf.br/nfe\">…</nfeProc>" },
    { "idDocumento": "doc-002", "nome": "pedido.txt", "tipo": "text/plain", "texto": "Pedido de compra número 123..." }
  ],
  "contexto": { "idioma": "pt-BR", "instrucoes": "Analisar conforme a especialidade do agente." }
}
```

| Campo | Obrigatório | Regras do contrato SOFIA | Regras adicionais do agente |
| ----- | ----------- | ------------------------ | --------------------------- |
| `versaoContrato` | Sim | Igual a `"1.0"` | Outro valor → ERR-007 |
| `idExecucao` | Sim | UUID gerado pela SOFIA para a orquestração | Devolvido sem alteração |
| `idSolicitacao` | Sim | UUID distinto por chamada a cada agente; retentativas reutilizam | Devolvido sem alteração; vira o `correlacaoId` da execução |
| `usuario.nome` | Sim | Não vazio; só contexto de negócio | Até 200 caracteres; identifica o usuário na v1 (§4.2); dado pessoal |
| `pergunta` | Sim | Não vazia | Até 4.000 caracteres (mesmo limite do canal direto) |
| `documentos` | Sim | Array não vazio | Até `Limites:QuantidadeArquivos` itens (ERR-003) |
| `documentos[].idDocumento` | Sim | Não vazio | Até 128 caracteres; único na solicitação (repetido → ERR-007) |
| `documentos[].nome` | Sim | Não vazio | Saneado, sem caminho, como no canal direto |
| `documentos[].tipo` | Não | MIME | Informativo; o tipo real é decidido pelo conteúdo (FR-006) |
| `documentos[].texto` | Sim | Não vazio | Conteúdo do XML como texto; soma dos bytes UTF-8 de todos os textos até `Limites:TamanhoTotalBytes` (ERR-002) |
| `contexto` | Não | `idioma`, `instrucoes` | `idioma` diferente de `pt-BR` não muda o idioma da resposta (NFR-010); `instrucoes` é dado não confiável e não é aplicado como instrução (FR-053) |

Campos desconhecidos são ignorados (evolução compatível da v1). Os limites são validados antes de
qualquer análise (NFR-002), sem truncar textos.

## 4. Mapeamento para o caso de uso

O adaptador A2A só traduz; a análise é a mesma do canal direto (P18).

### 4.1 Documentos

- Cada item de `documentos` vira um arquivo recebido: `nome` → nome do arquivo, bytes UTF-8 de
  `texto` → conteúdo, `idDocumento` → `ArquivoRecebido.idDocumento` (data-model §3).
- O texto é lido como caracteres: a declaração `encoding` do XML não muda a decodificação. As demais
  regras de leitura segura valem sem exceção (DTD proibido, sem entidades externas, profundidade
  máxima — constitution 5.2).
- Documento que não é XML bem formado ou não é NF-e modelo 55 4.00 (por exemplo, `pedido.txt`)
  segue o tratamento por arquivo do canal direto (ERR-008 a ERR-010, ERR-023) e torna o resultado
  `parcial` (§5.3).
- Os textos são documentos não confiáveis: nenhum conteúdo é interpretado como instrução ao agente
  (FR-053, constitution P07).

### 4.2 Identificação (D-05)

- `chamadorId` = `A2A:ChamadorPadrao` (inicial `"sofia"`). A credencial de serviço da SOFIA
  (FR-052h) é compartilhada pela turma e não identifica o chamador; com o fim do D-05 e a DP-10,
  passa a ser o da credencial de serviço do chamador.
- `usuarioNome` = `usuario.nome` sem espaços nas pontas. É persistido na sessão e na execução
  (data-model §1 e §2), com a retenção do ADR-0005, e NÃO DEVE aparecer em logs, no envio ao
  provedor de IA (ADR-0004) nem no contexto do usuário (FR-055a).
- `usuarioId` = `"nome-sha256:"` + SHA-256 hexadecimal minúsculo do nome normalizado (Unicode NFC,
  espaços internos reduzidos a um, minúsculas invariantes). A chave de isolamento é calculada como no
  canal direto, a partir de `chamadorId` e `usuarioId`.
- O mesmo nome normalizado leva ao mesmo contexto do usuário. Homônimos compartilham contexto até a
  autenticação (risco do D-05).

### 4.3 Execução

- Tipo `AnaliseComPergunta`, canal `A2A`, uma sessão nova por solicitação (sem continuidade).
- `idExecucao` e `idSolicitacao` da SOFIA são gravados na execução (`idExecucaoSofia`,
  `idSolicitacaoSofia`); `correlacaoId` = `idSolicitacao`.
- O agente não chama outros agentes nem encaminha a solicitação; roteamento, agregação e síntese são
  da SOFIA (FR-043i).

### 4.4 Repetição do mesmo `idSolicitacao` (FR-043h)

A SOFIA reutiliza o `idSolicitacao` nas retentativas. A chave de idempotência é o chamador mais o
`idSolicitacao`:

| Situação da execução original | Resposta |
| ----------------------------- | -------- |
| `Concluida` ou `Rejeitada` | O mesmo `ResultadoAnaliseSofia`, montado a partir da execução gravada; sem nova análise, sem consumo de IA e sem atualizar o contexto do usuário |
| `Recebida` ou `EmProcessamento` | `status: erro`, `erro.codigo = "ERR-026"`; a SOFIA pode repetir depois do tempo máximo da execução (§7) |
| `Falhou` (por exemplo ERR-016) | Nova execução com os mesmos IDs da SOFIA; as duas ficam registradas, inclusive o consumo de IA (P14) |
| Qualquer uma, com `idExecucao` ou usuário diferentes | `status: erro`, `erro.codigo = "ERR-007"` |

Solicitações que terminaram em Task `failed` ou foram recusadas pelo validador do adaptador (§6) não
criam execução; a repetição é tratada como primeira solicitação.

## 5. Resposta (agente → SOFIA)

### 5.1 Envelope A2A: Task concluída com Artifact

```json
{
  "jsonrpc": "2.0",
  "id": "rpc-001",
  "result": {
    "kind": "task",
    "id": "0193f0c3-0000-7000-8000-0000000000c1",
    "contextId": "0193f0c3-0000-7000-8000-0000000000c2",
    "status": { "state": "completed", "timestamp": "2026-11-05T14:03:12Z" },
    "artifacts": [
      {
        "artifactId": "0193f0c3-0000-7000-8000-0000000000c3",
        "name": "analise-sofia-v1",
        "parts": [
          {
            "kind": "text",
            "text": "{\"versaoContrato\":\"1.0\",\"idExecucao\":\"8b2f0ad6-c2e4-4a68-8bdd-20f785bc798e\",\"idSolicitacao\":\"e1b3a4cb-d729-4bd4-b796-b9c94be07b46\",\"agente\":{\"idAgente\":\"agente-auditoria-nfe\",\"nome\":\"AgenteAuditoriaNFe\"},\"status\":\"parcial\",\"analise\":\"…\",\"achados\":[…],\"observacoes\":[…],\"erro\":null}"
          }
        ]
      }
    ]
  }
}
```

- A Task é devolvida já no estado final (`completed` ou `failed`); estados intermediários não são
  observáveis na v1 (desvio D-04, §7).
- O Artifact tem `name = "analise-sofia-v1"` e exatamente uma parte de texto com o JSON do
  `ResultadoAnaliseSofia` em UTF-8. `contextId` é o recebido ou, na falta, gerado pelo sistema.

### 5.2 `ResultadoAnaliseSofia`

```json
{
  "versaoContrato": "1.0",
  "idExecucao": "8b2f0ad6-c2e4-4a68-8bdd-20f785bc798e",
  "idSolicitacao": "e1b3a4cb-d729-4bd4-b796-b9c94be07b46",
  "agente": { "idAgente": "agente-auditoria-nfe", "nome": "AgenteAuditoriaNFe" },
  "status": "parcial",
  "analise": "Foram recebidos 2 documentos e analisado 1. Resultado geral: Com inconsistências. O total do ICMS da NF-e doc-001 difere da soma do ICMS dos itens.",
  "achados": [
    {
      "idAchado": "0193f0c2-7000-7000-8000-000000000101",
      "descricao": "Inconsistência de negócio: Total do ICMS diferente da soma do ICMS dos itens.",
      "idDocumentos": ["doc-001"],
      "evidencias": [
        "Campo NFe/infNFe/total/ICMSTot/vICMS: valor informado 100.00",
        "Valor calculado pela regra W04-10 (versão 1): 90.00",
        "Regra W04-10 do catálogo de regras v1"
      ],
      "severidade": "alta"
    }
  ],
  "observacoes": [
    "doc-001 (NFe-123.xml): NF-e 4.00 validada com o pacote PL_010e_v1.02 (data de referência 2026-09-15, origem dhRecbto).",
    "doc-002 (pedido.txt): não analisado — documento não suportado (ERR-009).",
    "Limitação: consulta a manuais oficiais não habilitada.",
    "Revisão humana recomendada: consulta a manuais oficiais não habilitada.",
    "Identificador interno da execução: 0193f0c2-6a00-7b00-8c00-0000000000aa."
  ],
  "erro": null
}
```

| Campo | Origem | Regras |
| ----- | ------ | ------ |
| `versaoContrato` | Sistema | Sempre `"1.0"` |
| `idExecucao` / `idSolicitacao` | Solicitação | Os mesmos valores recebidos, sem alteração |
| `agente.idAgente` / `agente.nome` | Configuração | `A2A:IdAgente` e o nome do card |
| `status` | Sistema | `concluido`, `parcial` ou `erro` (§5.3) |
| `analise` | Sistema + IA validada | Resumo textual do parecer em pt-BR; em `erro`, a descrição da impossibilidade; sem achados, texto explicativo |
| `achados` | Parecer | Pode ser `[]` (§5.4) |
| `observacoes` | Sistema | Lista de textos, na ordem do §5.5 |
| `erro` | Sistema | `null`, salvo em `status = erro`: `{ "codigo": "ERR-xxx", "mensagem": "<pt-BR>" }`, sem dado sensível |

### 5.3 Status

| Status | Quando |
| ------ | ------ |
| `concluido` | Execução `Concluida`, todos os documentos com validação de layout e de conteúdo realizadas e resultado geral diferente de "Inconclusiva" |
| `parcial` | Execução `Concluida` com ao menos um documento não analisado ou com validação não realizada (ERR-008 a ERR-010, ERR-023), ou com resultado geral "Inconclusiva" (ERR-013 a ERR-015) |
| `erro` | Execução `Rejeitada` ou `Falhou` (ERR-001 a ERR-003, ERR-007, ERR-016, ERR-025) |

### 5.4 Achados

Um item por achado do parecer, na mesma ordem (determinísticos e indícios da IA já validados):

- `idAchado`: o identificador do achado (GUID gerado pelo sistema).
- `descricao`: categoria legível seguida da descrição do achado, por exemplo "Erro estrutural do
  documento: …", "Inconsistência de negócio: …", "Indício: …".
- `idDocumentos`: o `idDocumento` do documento do achado; `[]` para achados do lote. Só IDs
  recebidos na solicitação.
- `evidencias`: textos montados pelo sistema a partir das evidências do achado (campo do XML com o
  valor informado, valor calculado com a regra e a versão, regra do catálogo, trecho de manual com
  documento e versão). Nenhum trecho é inventado: sem evidência, a lista é `[]` e a ausência fica
  explícita na descrição ou nas observações.
- `severidade`: `Alta` → `alta`, `Media` → `media`, `Baixa` → `baixa`; `Informativa` → campo
  omitido. `critica` não é produzida pela v1.

### 5.5 Observações

Na ordem:

1. uma linha por documento: analisado (pacote aplicado, data de referência e origem, convivência
   quando houver) ou não analisado (motivo e código ERR);
2. limitações do parecer;
3. validações não realizadas;
4. revisão humana, com os motivos;
5. recomendações demonstrativas: as liberadas com rótulo, aviso, texto e achados vinculados; as
   sensíveis como "pendente de aprovação", só com o identificador e sem o conteúdo (FR-035c). A
   decisão posterior é consultada pelo canal direto (FR-035g);
6. o identificador interno da execução, para rastreabilidade (NFR-008).

## 6. Erros e falhas

| Situação | Como chega à SOFIA |
| -------- | ------------------ |
| JSON-RPC inválido, método desconhecido, `message` ausente | Erro JSON-RPC do SDK, sem Task |
| Corpo HTTP acima de `Limites:CorpoHttpBytes` | HTTP 413, sem corpo JSON-RPC (falha técnica de transporte) |
| Limite de taxa ou de análises simultâneas (ERR-024) | HTTP 429 com `Retry-After`, sem corpo JSON-RPC (falha técnica de transporte) |
| Mensagem sem exatamente uma parte de texto, texto que não é objeto JSON ou sem `idExecucao` e `idSolicitacao` UUID válidos | Task `failed` com mensagem de status "ERR-007: …", sem Artifact (não há como montar um resultado válido) |
| Demais violações do contrato (`versaoContrato` diferente de `"1.0"`, campo obrigatório ausente ou vazio, `idDocumento` repetido, limites de texto) | Task `completed`, `status: erro`, `erro.codigo = "ERR-007"` |
| ERR-002, ERR-003 | Task `completed`, `status: erro` |
| ERR-025 (armazenamento indisponível) | Task `completed`, `status: erro`; nenhum resultado parcial |
| ERR-026 (mesmo `idSolicitacao` em andamento) | Task `completed`, `status: erro` (§4.4) |
| ERR-016 (tempo máximo esgotado) | Task `completed`, `status: erro` |
| ERR-008 a ERR-012, ERR-023 (por documento) | Task `completed`, `status: parcial` ou `concluido` (§5.3); detalhes nas observações e nos achados |
| ERR-013, ERR-014, ERR-015 | Task `completed`, `status: parcial`, resultados determinísticos entregues |
| ERR-017, ERR-018 | Resposta normal na `analise` (não são erros técnicos) |
| Card ou caminho `/a2a` sem a credencial de serviço válida (FR-052h) | HTTP 401 com `WWW-Authenticate: Bearer`, sem corpo JSON-RPC (falha técnica de transporte) |
| ERR-004, ERR-019 | Adiados com o desvio D-05 |

As mensagens são as do catálogo de erros em pt-BR (NFR-010), sem caminho de arquivo, conteúdo de
documento, nome do usuário ou detalhe de infraestrutura.

## 7. Estados da tarefa e limitações (FR-046)

- O agente executa a análise dentro da requisição e devolve a Task no estado final. Os estados
  intermediários (`submitted`, `working`) podem ser emitidos internamente pelo `TaskUpdater`, mas não
  são observáveis pela SOFIA na v1 (desvio D-04 revisado).
- Mapeamento: execução `Concluida` ou `Rejeitada` e ERR-016 → Task `completed` com Artifact; falha
  sem IDs válidos → Task `failed`.
- A SOFIA DEVE usar tempo limite de requisição de pelo menos 150 segundos (NFR-001 mais margem). O
  agente encerra a execução no tempo máximo configurado (`Execucao:TempoMaximo`, inicial 120 s) com
  ERR-016.
- As Tasks ficam só em memória (`InMemoryTaskStore`), para uma instância; `tasks/get` de uma Task
  antiga não é contratual. A repetição segura é pelo `idSolicitacao` (§4.4), não pelo `tasks/get`.
- `tasks/cancel` não é oferecido: a Task só fica visível no estado final, que não é cancelável. Se a
  SOFIA abandonar a requisição HTTP, a execução é encerrada como `Falhou` com ERR-027, com as etapas
  registradas (FR-043i), e pode ser repetida com o mesmo `idSolicitacao`.

## 8. Versionamento

- Versão do contrato `1.0`, publicada no card (extensão) e em cada resultado.
- Alteração compatível (campo opcional novo): continua `1.x`, com acordo da turma para os nomes.
- Alteração incompatível: nova versão maior, testes de compatibilidade e período de convivência
  (NFR-012, P18).
- Mudança do perfil de protocolo (por exemplo, A2A 1.0 nativo sem a camada v0.3) não muda a versão
  do contrato de negócio, mas exige acordo com a SOFIA, novo card e testes de contrato.

## 9. Campos de protocolo e campos do contrato

| Pertence ao A2A (transporte) | Pertence ao contrato SOFIA (negócio) |
| ---------------------------- | ------------------------------------ |
| `jsonrpc`, `id`, `method`, `params.configuration` | `versaoContrato` |
| `message.kind`, `role`, `messageId`, `contextId`, `parts[].kind` | `idExecucao`, `idSolicitacao` |
| `result.kind`, `id` (Task), `contextId`, `status.state`, `status.timestamp` | `usuario.nome`, `pergunta`, `contexto` |
| `artifacts[].artifactId`, `name`, `parts[].kind` | `documentos[]` (`idDocumento`, `nome`, `tipo`, `texto`) |
| Agent Card (`url`, `capabilities`, `skills`, `protocolVersion`) | `agente`, `status`, `analise`, `achados[]`, `observacoes`, `erro` |
| Autenticação (`Authorization: Bearer`, FR-052h; credencial por chamador na DP-10) | — |

## 10. Uso pelo Sofia.Simulator

O simulador usa o cliente oficial do perfil v0.3 (`A2A.V0_3`) e só o contrato público (FR-048).

| Cenário | Comportamento esperado | Referência |
| ------- | ---------------------- | ---------- |
| Descoberta | Obtém o card v0.3 com a credencial de serviço, valida campos do FR-044 e exibe habilidade, `idAgente`, limites e o esquema de autenticação | TS-029 |
| Fluxo básico | Envia `message/send` com uma `SolicitacaoAnaliseSofia` (IDs gerados, nome fictício, pergunta e XMLs sintéticos); exibe estado da Task, `status`, `analise`, achados, observações e confere os IDs devolvidos | TS-035 |
| Card inacessível | Informa falha de descoberta e não envia solicitação | TS-035a |
| Documento não suportado (opcional) | Inclui um texto que não é NF-e; exibe `status: parcial` | TS-065 |
| Contrato inválido (opcional) | Envia `versaoContrato: "2.0"`; exibe `status: erro` com ERR-007 | TS-066 |

## 11. Operação: rastreamento e rede

- **Rastreamento (NFR-014)**: a SOFIA PODE enviar o cabeçalho W3C `traceparent` no `message/send`;
  o agente continua o trace (OpenTelemetry, ADR-0009) e registra o identificador do trace com o
  `execucaoId`, o `idExecucao` e o `idSolicitacao`. Nome do usuário, pergunta e textos dos documentos
  nunca vão para logs, traces ou métricas.
- **Métricas**: duração e contagem por `status` (`concluido`, `parcial`, `erro`), por Task `failed`
  e por repetição, com o `idAgente`; tokens e custo ficam no registro de consumo (NFR-009).
- **Rede Docker**: dentro de contêineres, a SOFIA acessa o agente pelo nome do serviço na rede
  compartilhada, nunca por `localhost` de outro contêiner. Nesse caso `A2A:UrlPublica` DEVE ser o
  endereço que a SOFIA alcança (por exemplo `http://api-agente:8080`), porque a SOFIA usa o `url`
  do card como endpoint RPC. HTTP sem TLS só em rede local isolada (D-05); fora dela, HTTPS. Em
  qualquer caso, a SOFIA envia a credencial de serviço (FR-052h).
- **CORS**: não se aplica. A SOFIA chama o card e o endpoint A2A pelo backend, e essas rotas não
  emitem cabeçalhos CORS; a política `SofiaBrowser` vale só para as rotas REST `/api/v1` (FR-052e).
- **Exemplos**: mensagens completas em [exemplos/](./exemplos/), validadas contra os JSON Schemas
  e usadas como fixtures dos testes de contrato.
