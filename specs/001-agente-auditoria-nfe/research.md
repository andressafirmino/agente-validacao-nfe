# Research: Agente de Auditoria de NF-e (AuditoriaNFe)

**Feature**: `001-agente-auditoria-nfe` | **Data**: 2026-10-29, revisado em 2026-11-04 |
**Plano**: [plan.md](./plan.md)

Este documento consolida a Fase 0 do plano técnico: decisões, justificativas, alternativas avaliadas e
o uso documentado das APIs públicas atuais do Microsoft Agent Framework (MAF), do SDK A2A e do SDK
MCP. As versões foram consultadas no NuGet em 2026-10-29. Nenhum código de aplicação é definido aqui;
os trechos de código ilustram apenas a forma das APIs oficiais.

**Revisão de 2026-11-04**: alinhamento à constitution 2.0.0 (MAF obrigatório, contexto do usuário,
DP-27) e às esclarecimentos da spec da mesma data: escopo NF-e modelo 55, validação de estrutura e
de totalizadores, seleção do pacote de schema pela data de referência, contexto do usuário, versão 1
sem autenticação (desvio D-05), contrato com a SOFIA provisório, Docker com portas próprias, lições do
AgenteFinanceiro e padrão de custos do projeto-ia-infis.

**Revisão de 2026-11-05**: contrato SOFIA v1.0 (DP-27 resolvida na constitution 2.2.0, ADR-0011
proposto). O canal A2A passa ao perfil v0.3.x com a camada oficial `A2A.V0_3Compat`, servido pelo
`A2AServer` do SDK com manipulador próprio; os pacotes de hosting e de cliente A2A do MAF deixam de
ser usados (R-11, R-12, R-32, R-33, §3.9 a §3.11).

## 1. Pacotes oficiais e versões fixadas

As versões abaixo DEVEM ser fixadas por Central Package Management (`Directory.Packages.props`).
Pacotes em pré-lançamento podem mudar de forma incompatível entre versões; a atualização exige PR
próprio com execução completa dos testes de contrato A2A.

| Pacote | Versão | Situação | Uso | Projeto |
| ------ | ------ | -------- | --- | ------- |
| `Microsoft.Agents.AI` | 1.26.0 | Estável | `AIAgent`, `ChatClientAgent`, `AgentSession`, `AIContextProvider`, middleware | Application |
| `Microsoft.Agents.AI.OpenAI` | 1.26.0 | Estável | Criação de agentes sobre o cliente OpenAI (`AsAIAgent`) e `IChatClient` OpenAI | Infrastructure |
| `Microsoft.Agents.AI.Workflows` | 1.26.0 | Estável | Workflow de aprovação humana, `RequestPort`, checkpoints | Application, Infrastructure |
| `Microsoft.Agents.AI.Hosting.AspNetCore` | 1.26.0-preview.261023.1 | Pré-lançamento | Hospedagem do agente; o isolamento por claims fica para o fim do desvio D-05 | Api.Agente |
| `A2A` / `A2A.AspNetCore` | 1.0.0-preview3 | Pré-lançamento | SDK oficial A2A: `A2AServer`, `IAgentHandler`, `TaskUpdater`, `InMemoryTaskStore`, `AgentCard` | Api.Agente |
| `A2A.V0_3Compat` | 1.0.0-preview3 | Pré-lançamento | Perfil A2A v0.3 sobre o SDK 1.0: `MapA2AWithV03Compat`, `MapAgentCardGetWithV03Compat` (inclui `A2A.V0_3`) | Api.Agente |
| `A2A.V0_3` | 1.0.0-preview3 | Pré-lançamento | Cliente oficial v0.3 (`A2ACardResolver`, `A2AClient.SendMessageAsync`) | Sofia.Simulator, Tests |
| `Microsoft.Extensions.AI.OpenAI` | 10.11.0 | Estável | Adaptador `IChatClient` para OpenAI (transitivo do MAF OpenAI) | Infrastructure |
| `OpenAI` | ≥ 2.14.0 | Estável | SDK oficial OpenAI (transitivo) | Infrastructure |
| `ModelContextProtocol.Core` | 2.3.0 | Estável | Cliente MCP (`McpClient`, `HttpClientTransport`) | Infrastructure |
| `MongoDB.Driver` | 3.13.0 | Estável | Sessões, execuções, resultados e contexto do usuário | Infrastructure |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.3 | Estável | Registro de consumo e custos | Infrastructure |
| `Microsoft.AspNetCore.OpenApi` | 10.0.14 | Estável | Documento OpenAPI do canal direto | Api.Agente |
| `Swashbuckle.AspNetCore.SwaggerUI` | 10.3.1 | Estável | Interface Swagger UI (somente a interface) | Api.Agente |
| `OpenTelemetry.Extensions.Hosting` | 1.20.1 | Estável | Traces, métricas e logs | Api.Agente |
| `xunit.v3` | 4.0.2 | Estável | Testes | Tests |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.14 | Estável | `WebApplicationFactory` | Tests |
| `Testcontainers.MongoDb` / `Testcontainers.PostgreSql` | 4.16.0 | Estável | Bancos reais e descartáveis (P17) | Tests |

`Microsoft.AspNetCore.Authentication.JwtBearer` saiu da lista: a versão 1 não tem autenticação
(desvio D-05, R-17). O pacote do mecanismo de credencial será escolhido no ADR da DP-10.

`Microsoft.Agents.AI.Hosting.A2A.AspNetCore` e `Microsoft.Agents.AI.A2A` (1.26.0-preview.261023.1)
saíram da lista em 2026-11-05: o hosting A2A do MAF responde só com Message e não atende o contrato
SOFIA v1.0 (R-11). Os pacotes A2A ficam em 1.0.0-preview3, a versão verificada; a 1.0.0-preview4 já
existe no NuGet e só entra em PR próprio, com os testes de contrato.

Diagnósticos experimentais com supressão pontual e documentada no `Directory.Build.props`:

- APIs do SDK A2A ou da camada `A2A.V0_3Compat` marcadas com `[Experimental]`, no `Api.Agente`,
  quando houver.
- `MAAI001` (`AgentSessionStore` experimental no MAF 1.26), no `AuditoriaNFe.Application`, no
  `AuditoriaNFe.Infrastructure` e nos testes (lição V-08 do AgenteFinanceiro, R-33).

**SDK do .NET**: o `global.json` fixa o SDK 10 (LTS) e o runner de testes
`Microsoft.Testing.Platform` (lição V-07). Em 2026-11-04 o SDK 10.0.403 foi instalado na máquina
de desenvolvimento, que também tem os SDKs 9.0.302 e 11.0 RC; sem o `global.json`, o SDK 11 RC
seria escolhido por ser o mais recente.

## 2. Decisões

### R-01 — Plataforma: .NET 10 e ASP.NET Core 10 (Minimal APIs)

- **Decisão**: .NET 10 (LTS) e ASP.NET Core 10 com Minimal APIs no `Api.Agente`; aplicativo de console
  .NET 10 no `Sofia.Simulator`. Nullable habilitado e avisos tratados como erro (seção 5.3).
- **Justificativa**: exigido pela constitution (3.1) e pela spec; Minimal APIs reduzem cerimônia sem
  misturar regra de negócio nos endpoints (3.3).
- **Alternativas**: Controllers MVC (mais código sem ganho para o número de endpoints da feature).

### R-02 — Framework de agentes: Microsoft Agent Framework

- **Decisão**: usar o Microsoft Agent Framework 1.26.0 para o agente, as ferramentas por Function
  Calling (`AIFunctionFactory` do Microsoft.Extensions.AI), os context providers, o middleware, as
  sessões e o workflow de aprovação. A hospedagem A2A do contrato SOFIA v1.0 usa o servidor do SDK
  A2A com manipulador próprio, porque o hosting A2A do MAF na versão fixada responde só com Message
  (R-11; desvio D-07 do plano, pendente de aprovação).
- **Justificativa**: exigido pela constitution 2.0.0 (seção 3.1). O MAF cobre, em um único modelo,
  Function Calling, sessões serializáveis, context providers (usados no contexto do usuário),
  middleware, workflows com checkpoint e hospedagem A2A oficial. O AgenteFinanceiro usa a mesma
  versão e serve de referência (R-33).
- **Situação**: o antigo desvio D-01 (Semantic Kernel) deixou de existir com a emenda 2.0.0.
- **Alternativas**: Semantic Kernel (exigiria ADR pela seção 3.1 e não oferece hospedagem A2A nem
  workflows com checkpoint integrados).

### R-03 — Provedor de IA: OpenAI por Chat Completions, sem armazenamento no provedor

- **Decisão**: `Microsoft.Agents.AI.OpenAI` sobre o SDK oficial OpenAI, usando o cliente de Chat
  Completions (`OpenAIClient.GetChatClient(modelo)`), convertido em `IChatClient` com middleware de
  consumo. O histórico fica no MongoDB, e não no provedor.
- **Justificativa**: o histórico sob controle do sistema atende ao P15, à retenção (DP-13) e à
  minimização (DP-14); a API de Chat Completions suporta Structured Outputs com JSON Schema e Function
  Calling, exigidos pelo P08 e pelas ferramentas.
- **Modelo (DP-05)**: parâmetro obrigatório `OpenAI:Modelo`, sem valor padrão no código. O modelo
  DEVE suportar Structured Outputs com JSON Schema estrito e Function Calling. A escolha final DEVE
  ser registrada com o resultado do conjunto de avaliação (SC-003, P13) antes do merge da
  implementação de IA.
- **Alternativas**: API Responses com conversas armazenadas no provedor (rejeitada: estado fora do
  sistema, conflito com DP-13/DP-14); Azure OpenAI (fora da decisão "OpenAI" da spec).

### R-04 — Orquestração: fluxo determinístico em código e IA somente na etapa 7

- **Decisão**: o fluxo das nove etapas (constitution 3.4) é executado por um caso de uso da camada de
  Aplicação (`ExecutarSolicitacaoAuditoria`), em código determinístico. O LLM participa apenas da
  etapa 7, por um `ChatClientAgent` interno que recebe os resultados das etapas 3 e 4 como dados. O
  manipulador A2A (`ManipuladorSofiaA2A`, R-11) é a porta de entrada do canal A2A e delega ao mesmo
  caso de uso usado pelo canal REST (P18: sem desvio de canal). O agente personalizado
  `AgenteAuditoriaNFe` (`AIAgent`) previsto para o hosting A2A do MAF deixou de ser necessário em
  2026-11-05.
- **Justificativa**: P03 (determinístico precede e prevalece), FR-016, NFR-005 e ERR-013 a ERR-016
  exigem que validações e resultados determinísticos existam mesmo sem IA. Deixar o LLM decidir a
  ordem das etapas violaria o P10.
- **Alternativas**: (a) agente único que chama ferramentas de validação por decisão do LLM —
  rejeitado (P03, P10); (b) fluxo inteiro como Workflow do MAF — rejeitado nesta versão: as etapas
  são sequenciais, e os checkpoints do workflow conteriam dados extraídos do XML, contrariando a
  restrição dos checkpoints.

### R-05 — Saída estruturada versionada e validada (P08)

- **Decisão**: o agente interno é executado com `RunAsync<ContratoIaV1>(...)`, que gera o JSON Schema a
  partir do tipo e solicita Structured Outputs ao provedor. O schema publicado em
  [contracts/contrato-ia-v1.schema.json](./contracts/contrato-ia-v1.schema.json) é a referência
  normativa; um teste de contrato compara o schema gerado pelo tipo com o arquivo publicado. O
  sistema valida o resultado em duas etapas: estrutural (desserialização e campos obrigatórios) e
  semântica (achados citados existem, evidências apontam para campos extraídos, todo valor
  financeiro é rastreável a um campo ou a um cálculo registrado — FR-027/FR-028). Falha gera nova
  tentativa com mensagem de correção, até `Ia:MaxTentativas` (inicial: 3 tentativas no total). Cada
  tentativa é registrada no consumo (P14).
- **Justificativa**: `RunAsync<T>` é o caminho oficial do MAF para saída estruturada; a validação
  semântica não é coberta pelo schema. O projeto-ia-infis já usa JSON Schema em modo `strict`
  (`PromptAtendimento.cs`), o que confirma a viabilidade com a OpenAI.
- **Alternativas**: `AgentRunOptions.ResponseFormat` com `ChatResponseFormat.ForJsonSchema(...)`
  carregando o arquivo publicado (mantido como alternativa caso o schema gerado pelo tipo divirja do
  publicado).

### R-06 — Ferramentas por Function Calling e autorização pelo sistema (P10)

- **Decisão**: ferramentas criadas com `AIFunctionFactory.Create(...)` e `[Description]`, todas de
  leitura sobre os dados da sessão: listar documentos, obter campos extraídos de um documento, calcular
  totais determinísticos (com documentos incluídos e excluídos — FR-031) e listar achados. O cálculo
  é feito no Domínio, e cada resultado recebe um `calculoId` registrado na execução. Um middleware de
  Function Calling aplica a lista de ferramentas permitidas por execução, valida argumentos, impõe
  timeout e registra a chamada (seção 4.6). A consulta a manuais (etapa 5) é executada pelo sistema, e
  não oferecida ao LLM como ferramenta nesta versão. Nenhuma ferramenta lê ou grava o contexto do
  usuário (FR-057).
- **Justificativa**: o LLM não calcula valores; ele só cita valores devolvidos por ferramentas
  determinísticas, o que torna a verificação de FR-027/FR-028 objetiva.
- **Aprovação de ferramentas**: `ApprovalRequiredAIFunction` não é usado na v1, porque não há
  ferramenta com efeito colateral. A aprovação humana da spec é sobre recomendações (R-10).
- **Alternativas**: deixar o LLM somar valores e verificar depois (rejeitado: mais regenerações e
  risco de FR-030).

### R-07 — Context providers

- **Decisão**: três `AIContextProvider` no agente interno:
  1. `ContextoExecucaoProvider`: injeta, como dados delimitados, os resultados determinísticos da
     execução, o estado das etapas, o pacote de schema e a data de referência de cada documento e,
     quando habilitados, os trechos de manual recuperados.
  2. `ContextoSessaoProvider`: injeta o resumo da base analisada da sessão para perguntas de
     continuidade (FR-037, FR-042).
  3. `ContextoUsuarioProvider`: injeta o contexto do usuário (R-31) como dado não confiável e
     delimitado, limitado por `ContextoUsuario:MaxItensNoPrompt` (P15, FR-058). Não é usado para
     o usuário "anonimo" (FR-052c). Não implementa gravação em `StoreAIContextAsync`: a gravação é
     feita pelo caso de uso, a partir dos resultados determinísticos (FR-057).
  O estado por sessão usa `ProviderSessionState<T>`, nunca campos da instância do provider.
- **Justificativa**: separa a montagem do contexto do caso de uso e mantém os resultados da sessão
  disponíveis mesmo quando o histórico textual é reduzido. O AgenteFinanceiro usa o mesmo mecanismo
  (`ContextoFinanceiroProvider.cs`).
- **Alternativas**: concatenar tudo na mensagem do usuário (mistura instrução e dado, contra o P07);
  gravar o contexto do usuário no `StoreAIContextAsync` a partir da resposta do LLM (rejeitado:
  FR-057 proíbe que a IA grave o contexto).

### R-08 — Sessões e memória conversacional: MongoDB (DP-01, resolvida na constitution 2.0.0)

- **Decisão**: MongoDB para (a) sessões do MAF serializadas (`SerializeSessionAsync` e
  `DeserializeSessionAsync`) por meio de uma implementação de `AgentSessionStore`; (b) documentos de
  domínio: `sessoes`, `execucoes` (com parecer e recomendações), dados extraídos por documento e
  `contextos_usuario` (R-31). O XML original não é persistido (FR-037a). A expiração por
  inatividade (24 h, configurável) é verificada a cada solicitação; a remoção física segue o ADR de
  retenção (DP-13).
- **Isolamento sem autenticação (D-05)**: a chave de isolamento é o SHA-256 de `chamadorId` e
  `usuarioId`, informados nos cabeçalhos (REST) ou derivados do contrato SOFIA (A2A: chamador
  padrão e hash do nome, data-model §0), ou dos valores padrão "nao-informado" e "anonimo"
  (FR-052a, FR-052c, FR-052d). O caso de uso confere a chave em todo acesso a sessão e responde
  ERR-005 sem revelar a existência da sessão (FR-039).
- **Sessão A2A**: o contrato SOFIA v1.0 não tem continuidade; cada solicitação abre uma sessão de
  domínio nova, e o `contextId` A2A não é associado a sessão nenhuma. Não há sessão do hosting A2A
  nem `AgentIsolationKeyProvider`.
- **Redução de histórico (FR-042)**: o histórico textual é reduzido por um redutor do
  Microsoft.Extensions.AI configurado no provedor de histórico da sessão; o ponto exato de
  configuração DEVE ser confirmado na versão 1.26.0 durante as tarefas. Os resultados de análise ficam
  fora do histórico e não são afetados.
- **Justificativa**: a constitution 2.0.0 (3.1) define MongoDB para memória e contexto do usuário; a
  documentação do hosting indica que o `InMemoryAgentSessionStore` e o `InMemoryTaskStore` servem
  apenas para desenvolvimento. O identificador `contextId` do A2A não é token de autorização.
- **Alternativas**: Redis (não permitido pela constitution 2.0.0 para memória); usar o
  `AgentIsolationKeyProvider` com identidade lida do corpo (rejeitado: o provedor só recebe o
  `HttpContext`, e ler o corpo antes do hosting acoplaria a borda ao formato A2A).

### R-09 — Middleware

- **Decisão**: três níveis de middleware:
  1. **Execução do agente** (`AsBuilder().Use(runFunc:, runStreamingFunc:)`): correlação, timeout da
     execução (ERR-016), mapeamento de exceções do provedor para ERR-014 e logs sem conteúdo.
  2. **Function Calling** (`Use(Func<AIAgent, FunctionInvocationContext, ...>)`): lista de
     ferramentas permitidas, validação de argumentos, timeout, registro de execução e
     `context.Terminate` em violação.
  3. **`IChatClient`** (um `DelegatingChatClient` próprio mais `UseOpenTelemetry()`): registra cada
     chamada ao provedor, inclusive cada volta do loop de ferramentas e cada retentativa, com
     `ChatResponse.Usage` (`InputTokenCount`, `OutputTokenCount` e tokens de entrada em cache,
     quando informados). Valor ausente é gravado como NULL, nunca como zero (P14, R-16).
- **Justificativa**: cada responsabilidade transversal fica em um ponto testável, fora dos casos de uso.
- **Alternativas**: registrar consumo no caso de uso (perderia as chamadas internas do loop de
  ferramentas).

### R-10 — Aprovação humana: Workflow do MAF com `RequestPort` e checkpoints JSON locais

- **Decisão**: cada recomendação sensível inicia um workflow `FluxoAprovacaoRecomendacao` com três
  executores: registrar pendência, `RequestPort<SolicitacaoAprovacao, DecisaoAprovacao>` e aplicar a
  decisão. O workflow roda com `CheckpointManager` sobre `FileSystemJsonCheckpointStore`, em um
  diretório configurável. A execução termina com o pedido pendente salvo em checkpoint; a decisão
  do aprovador (REST) retoma o workflow com `InProcessExecution.ResumeStreamingAsync(...)`, recebe
  de novo o `RequestInfoEvent` pendente e envia a resposta. Além do checkpoint do workflow, o sistema
  grava o registro de auditoria `CheckpointAprovacao` (schema v1 próprio, campos do FR-035f) em JSON,
  com escrita atômica e controle de versão.
- **Aprovador na v1 (D-05)**: o aprovador é o usuário informado na solicitação de decisão,
  registrado com o chamador e sem verificação (FR-035d). O controle de papel (FR-035h, ERR-019) volta
  com a autenticação.
- **Lições do AgenteFinanceiro**: executores que chamam `YieldOutputAsync` DEVEM declarar
  `[YieldsOutput(typeof(T))]`; o estado compartilhado entre executores usa
  `QueueStateUpdateAsync`/`ReadStateAsync` com escopo nomeado, que sobrevive ao checkpoint.
- **Justificativa**: atende ao pedido de Workflow com aprovação humana e ao FR-035j. O formato
  interno de checkpoint do MAF não é contrato estável; por isso, os campos de auditoria exigidos pelo
  FR-035f ficam no registro próprio. As mensagens do workflow carregam apenas identificadores e
  dados da decisão, nunca conteúdo de XML ou dados pessoais.
- **Limitações**: válido para uma única instância e para demonstração (ver plano, Limitações). Não
  funciona em ambiente sem disco durável (por exemplo, Vercel Functions).
- **Alternativas**: máquina de estados simples gravando apenas o registro JSON (mais simples, mas não
  atende ao pedido de Workflow); `ApprovalRequiredAIFunction` (aprova execução de ferramenta, não
  liberação de conteúdo).

### R-11 — Servidor A2A e Agent Card (contrato SOFIA v1.0, ADR-0011)

- **Decisão**: servir o perfil A2A v0.3.x exigido pelo contrato SOFIA v1.0 com o SDK oficial `A2A`
  1.0.0-preview3 e a camada oficial `A2A.V0_3Compat` 1.0.0-preview3:
  - `A2AServer(IAgentHandler, ITaskStore, ...)` com o manipulador próprio
    `ManipuladorSofiaA2A : IAgentHandler` e `InMemoryTaskStore`;
  - `MapA2AWithV03Compat(endpoints, requestHandler, "/a2a/auditoria-nfe")`, que aceita requisições
    v0.3 e v1.0 no mesmo caminho; só `message/send` v0.3 é contratual;
  - `MapAgentCardGetWithV03Compat(endpoints, requestHandler, fabricaCard, ...)` em
    `/.well-known/agent-card.json`, com `blendedCard = false`: o formato do card segue o cabeçalho
    `A2A-Version` (sem cabeçalho ou `0.3` → v0.3 estrito; `1.0` → A2A 1.0).
- **Manipulador**: em `ExecuteAsync(RequestContext, AgentEventQueue, CancellationToken)`, lê a única
  parte de texto da Message, desserializa e valida a `SolicitacaoAnaliseSofia` (FR-043a), monta a
  identificação pelo nome (FR-052d), chama o mesmo caso de uso do canal direto e publica pelo
  `TaskUpdater(eventQueue, taskId, contextId)`: `SubmitAsync`, `StartWorkAsync`,
  `AddArtifactAsync` (Artifact `analise-sofia-v1` com o `ResultadoAnaliseSofia`) e `CompleteAsync`;
  sem IDs válidos, `FailAsync` com a mensagem "ERR-007: …" (FR-043d).
- **Bloqueio**: a camada de compatibilidade mapeia `ReturnImmediately = !Blocking`. A SOFIA DEVE
  omitir `configuration` ou enviar `blocking: true`; o contrato registra a regra, e o teste de
  contrato cobre os dois casos.
- **Por que não o hosting A2A do MAF**: `AddA2AServer` com `AgentRunMode.DisallowBackground` (MAF
  1.26.0-preview) responde com Message, e o contrato exige Task com Artifact. O servidor do SDK
  continua sendo o SDK oficial (FR-043), sem implementar JSON-RPC à mão.
- **Autenticação no card (D-05)**: o card não declara `securitySchemes` e informa, na extensão
  `urn:infis:sofia:contrato`, `autenticacao: "nenhuma — somente ambiente local de testes"`
  (FR-052b).
- **Estados (D-04 revisado)**: a análise roda dentro da requisição; a Task volta no estado final.
  Os estados intermediários emitidos pelo `TaskUpdater` não são observáveis pela SOFIA na v1.
- **Validação inicial (spike)**: antes do manipulador, um teste confirma, na versão fixada, os
  nomes do card v0.3 produzidos pelo conversor, a Task com Artifact devolvida por `message/send`
  v0.3 com e sem `configuration`, e o erro do `FailAsync` na resposta. O spike também verifica se o
  `AddA2AServer` do MAF aceita um `IAgentHandler` próprio registrado com a chave do agente e se o
  `IA2ARequestHandler` resultante pode ser entregue ao `MapA2AWithV03Compat`. Se sim, esse caminho
  é adotado e o desvio D-07 (constitution 3.1, hospedagem A2A pelo MAF) é encerrado; se não, segue
  o `A2AServer` direto com o D-07 aprovado.
- **Alternativas**: hosting A2A do MAF (não atende o contrato); implementar o protocolo à mão
  (rejeitado pelo FR-043); fixar o SDK A2A 0.3.x (abandona a linha 1.0 e a convivência com A2A 1.0).

### R-12 — Cliente A2A do Sofia.Simulator

- **Decisão**: cliente oficial v0.3 do pacote `A2A.V0_3` 1.0.0-preview3: `A2ACardResolver` para
  obter o card (sem cabeçalho `A2A-Version`, recebendo o formato v0.3) e `A2AClient` apontado para o
  `url` do card, com `SendMessageAsync` e uma Message com uma parte de texto contendo a
  `SolicitacaoAnaliseSofia` serializada (IDs gerados, nome fictício, pergunta e XMLs sintéticos
  lidos como texto). A resposta é lida como Task: estado, Artifact `analise-sofia-v1` e o
  `ResultadoAnaliseSofia` desserializado e conferido contra os IDs enviados. Sem credencial na v1
  (D-05).
- **Justificativa**: usa somente o contrato público (FR-048) e o SDK oficial, no mesmo perfil que a
  SOFIA usa.
- **Alternativas**: `HttpClient` cru contra o endpoint (rejeitado pelo FR-043/FR-048); cliente
  A2A do MAF (`Microsoft.Agents.AI.A2A`), que fala A2A 1.0 e não exercita o perfil v0.3.

### R-13 — Ponto de integração MCP (preparado e desabilitado)

- **Decisão**: interface `IConsultaManuais` na Aplicação; implementação MCP na Infraestrutura com
  `McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions { Endpoint, Name,
  TransportMode = HttpTransportMode.StreamableHttp, AdditionalHeaders }))` e `ListToolsAsync()`;
  implementação nula quando `Mcp:Habilitado = false`, que registra a etapa 5 como "não aplicável —
  integração não habilitada" (FR-047b). O contrato do lado consumidor está em
  [contracts/consulta-manuais-mcp.md](./contracts/consulta-manuais-mcp.md); o transporte definitivo
  e a versão do protocolo ficam para o ADR da DP-18 na feature 002.
- **Alternativas**: chamar a Api.RAG por REST (viola a seção 3.2 e o P11).

### R-14 — Validação de XML e XSD com seleção do pacote pela data (resolve DP-03 nesta feature)

- **Leitura segura**: `XmlReader` com `DtdProcessing.Prohibit`, `XmlResolver = null`,
  `MaxCharactersInDocument` e limite de profundidade; detecção de XML bem formado pelo conteúdo.
- **Identificação (FR-007, FR-007a, FR-008)**: elemento raiz `NFe` ou `nfeProc` no namespace
  `http://www.portalfiscal.inf.br/nfe`, atributo `versao="4.00"`, `ide/mod = 55` e modelo 55 na chave
  de acesso (posições 21–22 da chave de 44 caracteres). Qualquer divergência → não suportado
  (ERR-009 ou ERR-010), antes do schema. A NFC-e (modelo 65) é recusada aqui, embora o schema seja o
  mesmo. São extraídos `dhEmi` e, com protocolo, `protNFe/infProt/dhRecbto`.
- **Schemas versionados no repositório**: 19 pacotes oficiais em
  `src/AuditoriaNFe.Infrastructure/Schemas/NFe/<pacote>/`, com o fechamento de includes de
  `nfe_v4.00.xsd` e o `procNFe_v4.00.xsd`. Os pacotes da série PL_010 não trazem `procNFe`; é usado o
  do PL_009q, idêntico ao de todos os pacotes PL_009 exceto pelo fim de linha. O `manifesto.json` da
  pasta registra, por pacote: ordem, notas técnicas, vigência (início, fim, início inferido), zip
  oficial de origem (título, URL do Portal, data de obtenção, SHA-256) e SHA-256 de cada XSD. O
  `.gitattributes` marca os XSDs como `-text`, para que o Git não altere os bytes conferidos. Em
  2026-11-04, os 19 pacotes compilaram sem erros no `XmlSchemaSet`, com os tipos `TNFe` e
  `TNfeProc`.
- **Integridade na inicialização**: o sistema confere o SHA-256 de cada XSD contra o manifesto e não
  inicia se houver divergência, arquivo ausente ou arquivo fora do manifesto.
- **Resolução de includes**: `XmlSchemaSet` com um `XmlResolver` próprio que só resolve caminhos
  dentro da pasta do pacote (sem URL remota, sem `..` para fora da pasta); qualquer outro pedido falha.
  O documento recebido nunca resolve recursos externos.
- **Seleção do pacote (FR-010 a FR-010d)**: a data de referência é a data de calendário de
  `dhRecbto`, quando houver protocolo, ou de `dhEmi`, no deslocamento de fuso informado no próprio
  XML (as datas de vigência das NTs são datas de Brasília, e o XML traz hora local com deslocamento).
  O pacote é o que satisfaz `vigenciaInicio ≤ data ≤ vigenciaFim`; o último pacote tem fim aberto e
  vale até a data atual do relógio do sistema. Data anterior ao primeiro pacote ou posterior à data
  atual → ERR-023. A fonte da tabela é o próprio `manifesto.json`, derivado de
  [vigencia-pacotes-schema.md](./vigencia-pacotes-schema.md), aprovada em 2026-11-04 e sujeita a
  ajustes, que entram como nova versão do manifesto.
- **Convivência (FR-010e, FR-010f)**: o manifesto tem o campo `fimConvivencia`, vazio em todos os
  pacotes atuais, porque nenhuma NT do leiaute 4.00 prevê convivência. A regra de nova tentativa
  com o pacote anterior é implementada de forma genérica e testada com uma tabela de vigência
  sintética de teste (TS-060 a TS-062).
- **Desempenho e concorrência**: o `XmlSchemaSet` de cada pacote é compilado uma única vez (sob
  demanda) e mantido em cache somente leitura. A validação concorrente sobre o mesmo conjunto
  compilado DEVE ser confirmada por teste de carga (NFR-004); se falhar, usa-se um conjunto por
  validação a partir de um pool por pacote.
- **Validação**: `NFe` isolada pelo `nfe_v4.00.xsd` e `nfeProc` pelo `procNFe_v4.00.xsd` do pacote
  selecionado. Cada violação vira achado `ErroEstrutural` com linha, posição e caminho (FR-011).
- **Alternativas**: baixar os schemas em tempo de execução (rejeitado: dependência externa e sem
  reprodutibilidade, NFR-005); bibliotecas de terceiros, como NFePHP ou ACBr, como fonte de schemas
  (rejeitado: não são fonte oficial, P02); um único pacote para todas as datas (rejeitado: documentos
  antigos seriam reprovados por campos criados depois).

### R-15 — Catálogo de regras de conteúdo (resolve DP-04 nesta feature)

- **Decisão**: o catálogo aprovado em
  [catalogo-regras-totalizadores.md](./catalogo-regras-totalizadores.md) é convertido em
  `src/AuditoriaNFe.Infrastructure/Catalogos/catalogo-regras.v1.json`. Cada entrada tem: `id` (por
  exemplo, `W04-10`), `versaoRegra`, descrição, fonte (`documento`, `versao`, `paginas`), vigência
  (`inicio`, `fim`), severidade, tolerância (decimal, opcional), aplicação na SEFAZ (`Obrig.` ou
  `Facult.`, com código de rejeição) e `implementacao` (id da implementação determinística no
  Domínio). A W16-10 tem quatro entradas, uma por versão. A W07-10 tem duas.
- **Carga**: o sistema não inicia se houver regra sem fonte, sem vigência, com implementação
  inexistente ou com versões de uma mesma regra com vigências sobrepostas.
- **Aplicação (FR-012 a FR-014, FR-013a)**: para cada documento aprovado no layout, aplica as
  versões vigentes na mesma data de referência usada na seleção do schema. Comparação em `decimal`
  exato, exceto as tolerâncias do catálogo: R$ 0,50 na W16-10 (Anexo I, p. 140) e 0,01 nas
  quantidades monofásicas. Campo opcional ausente vale zero. As regras de IBS, CBS e IS só se aplicam
  com os grupos preenchidos.
- **Catálogo de extração**: lista fechada de campos necessários às regras e às perguntas, mantida em
  `Catalogos/catalogo-extracao.v1.json`; todo campo usado por uma regra DEVE estar no catálogo de
  extração (verificado na carga).
- **Testes (P17)**: cada regra tem ao menos um XML sintético válido e um inválido, com a data de
  referência dentro da vigência; regras com versões têm um caso por versão.
- **Alternativas**: regras codificadas sem arquivo de catálogo (rejeitado: FR-013 exige fonte e
  vigência auditáveis por execução).

### R-16 — Consumo e custos: PostgreSQL com EF Core (padrão do projeto-ia-infis ampliado)

- **Base**: o projeto-ia-infis registra uma linha por interação em `consumo_tokens`, com custos em
  `decimal(18,8)`, preços por milhão de tokens na configuração e cálculo
  `tokens × preço ÷ 1.000.000` arredondado em 8 casas (`MidpointRounding.AwayFromZero`)
  (`Api.Atendimento/Llm/CustoDeTokens.cs`). Este plano reaproveita o cálculo e a escala.
- **Ampliações exigidas pela constitution 4.5 e pela skill `controle-custos-llm`**:
  1. uma linha por chamada ao provedor, inclusive cada retentativa (`tentativa`) e cada volta do loop
     de ferramentas, e não uma por interação;
  2. `tipo_chamada` (`Chat` ou `Embedding`) e `tokens_embedding`; nesta feature não há chamadas de
     embeddings (a Api.RAG chega na feature 002 e a DP-15 define como o consumo dela é correlacionado);
  3. `tokens_entrada_cache`, quando o provedor informar;
  4. status, duração, provedor, serviço e operação;
  5. tokens e custos desconhecidos como NULL, nunca zero; custo NULL quando não houver preço do modelo
     na tabela;
  6. `origem_valores` (`InformadoProvedor` ou `EstimadoSistema`), moeda e versão da tabela de preços;
  7. distinção entre custo calculado pelo sistema e faturamento efetivo: só o custo calculado é
     registrado; o faturamento da OpenAI não é importado.
- **Tabela de preços (DP-22)**: arquivo versionado `Catalogos/precos-openai.v{n}.json`, com preço por
  milhão de tokens de entrada, de entrada em cache e de saída por modelo, moeda USD, URL da página
  oficial de preços e data de consulta. Os valores DEVEM ser preenchidos a partir da página oficial no
  momento da tarefa; este plano não presume preços.
- **Falha de gravação**: estratégia "melhor esforço documentado": a falha ao gravar o consumo é
  registrada em log e em métrica e não altera o resultado da execução; o resumo de consumo da
  execução indica "registro incompleto".
- **Relatório**: visão `consumo.vw_consumo_diario` (por dia, modelo e tipo de chamada) e resumo por
  execução no envelope (NFR-009).
- **Topologia (DP-16)**: instância PostgreSQL local do compose, banco `auditoria_agente`, schema
  `consumo` e usuário exclusivo do Api.Agente, com permissão só nesse schema; a Api.RAG terá banco
  próprio na feature 002. Migrações EF Core aplicadas na inicialização somente quando
  `Persistencia:AplicarMigracoes = true` (ambiente local).
- **Alternativas**: Dapper com scripts SQL (menos verboso, mas sem migrações versionadas); SQL Server
  como no projeto-ia-infis (rejeitado: a constitution 3.1 exige PostgreSQL).

### R-17 — Autenticação: fora da versão 1 (desvio D-05)

- **Decisão**: a versão 1 não autentica o chamador nem o usuário. No canal direto, o chamador
  informa `chamadorId` e `usuarioId` nos cabeçalhos `X-Chamador-Id` e `X-Usuario-Id`; sem eles,
  usam-se "nao-informado" e "anonimo" (FR-052a, FR-052c). Os valores são validados quanto ao
  formato (até 128 caracteres de `[A-Za-z0-9._:@-]`), registrados e usados no isolamento, sem
  verificação de identidade. No A2A, o chamador é `A2A:ChamadorPadrao` e o usuário vem do
  `usuario.nome` do contrato SOFIA (FR-052d, data-model §0).
- **Mitigação técnica do D-05 (FR-052b)**:
  1. o `Api.Agente` recusa iniciar fora do ambiente `Development` enquanto
     `Seguranca:AutenticacaoHabilitada = false`, que é o único valor aceito na v1;
  2. o compose publica todas as portas só em `127.0.0.1`;
  3. o Agent Card declara que não há autenticação;
  4. os dados de teste e os exemplos do simulador são sintéticos ou anonimizados.
- **Modelo futuro (DP-10, ADR)**: o da constitution 5.2 — credencial de serviço do chamador,
  rotacionável e fornecida por configuração segura, com o usuário informado pelo chamador
  autenticado; controle do papel de aprovador (FR-035h, ERR-019). O fim do D-05 exige ADR aprovado,
  testes TS-031 e TS-042 ativos e o isolamento combinando o usuário com o chamador autenticado.
- **Alternativas**: JWT com `dotnet user-jwts` já na v1 (rejeitado pelo solicitante em 2026-11-04: a
  v1 serve para testar a validação, e a autenticação entra depois).

### R-18 — Minimização de dados enviados ao LLM (DP-14, ADR-0004 aprovado)

- **Decisão** (ADR-0004): enviar ao provedor apenas os dados extraídos necessários à análise e os
  resultados determinísticos; mascarar CPF, nomes de pessoas físicas, endereços, telefones e e-mails;
  nunca enviar o XML integral; sem armazenamento no provedor (R-03). O contexto do usuário enviado
  contém só CNPJs, contagens, achados recorrentes e preferências (FR-055a). A lista de campos enviados
  é configuração versionada e revisada.
- **Situação**: ADR-0004 aprovado por Andressa Firmino em 2026-11-04; a primeira chamada ao LLM com
  dados de documento DEVE seguir esta política.

### R-19 — Retenção (DP-13, ADR-0005 aprovado)

- **Definido pela spec**: XML descartado ao fim da execução (FR-037a); sessão expirada após 24 h de
  inatividade (configurável); contexto do usuário excluído após 90 dias sem atividade do usuário
  (FR-060, configurável), com prazo renovado a cada execução ou declaração.
- **Implementação**: o contexto do usuário tem o campo `expiraEm` (última atividade + prazo) com
  índice TTL do MongoDB (`expireAfterSeconds: 0`); a renovação atualiza `expiraEm`. A remoção pelo
  TTL ocorre em até cerca de um minuto após o vencimento (ciclo do monitor de TTL); a leitura também
  ignora contextos vencidos, para que o prazo seja exato do ponto de vista do usuário.
- **Decisão** (ADR-0005): sessões expiradas ou encerradas ficam indisponíveis na hora e são
  removidas fisicamente após prazo configurável; registros de consumo mantidos sem conteúdo;
  checkpoints JSON de demonstração removidos junto com a sessão. Os prazos de remoção física de
  sessões e de retenção do consumo não foram definidos: enquanto não houver valor aprovado, não há
  remoção física automática além do TTL do contexto do usuário. Isso não bloqueia a v1 local (D-05),
  mas DEVE ser resolvido antes de qualquer uso com dados reais.

### R-20 — Documentação da API: OpenAPI e Swagger UI

- **Decisão**: `Microsoft.AspNetCore.OpenApi` gera o documento; `Swashbuckle.AspNetCore.SwaggerUI`
  exibe a interface. O contrato de referência está em
  [contracts/api-agente.openapi.yaml](./contracts/api-agente.openapi.yaml); um teste de contrato
  compara o documento gerado com ele. A interface só é mapeada em `Development` (constitution 5.2),
  que é o único ambiente permitido enquanto vigorar o D-05.
- **Alternativas**: Scalar (interface equivalente; rejeitada porque a spec pede "interface Swagger").

### R-21 — Health checks

- **Decisão**: `/health/live` (processo) e `/health/ready` (MongoDB, PostgreSQL, diretório de
  checkpoints gravável e integridade dos schemas e catálogos carregados), com verificações próprias na
  Infraestrutura. A OpenAI não é verificada ativamente para não gerar custo; a indisponibilidade
  aparece como ERR-014. As respostas não expõem dados nem detalhes internos.
- **Alternativas**: pacotes da comunidade `AspNetCore.HealthChecks.*` (dependência extra para
  verificações simples).

### R-22 — Observabilidade (DP-23)

- **Decisão**: OpenTelemetry para traces, métricas e logs, exportação OTLP configurável e console em
  desenvolvimento; `UseOpenTelemetry()` no agente e no `IChatClient`, sem dados sensíveis. Toda linha
  de log carrega `execucaoId` e `correlacaoId`; conteúdo de XML, perguntas, respostas, contexto do
  usuário e identificadores de usuário não são logados (o log usa a chave de isolamento em hash).

### R-23 — Testes

- **Decisão**: um projeto `AuditoriaNFe.Tests` (xUnit v3, Microsoft.Testing.Platform) organizado por
  pastas: unidade (Domínio, seleção de pacote, regras do catálogo e cálculos), aplicação (fluxo com
  `IChatClient` simulado), ferramentas, aprovação (workflow e checkpoints em diretório temporário),
  contrato (A2A, OpenAPI, schema do contrato do LLM), integração (`WebApplicationFactory` com MongoDB
  e PostgreSQL reais via Testcontainers, P17), arquitetura (dependências entre camadas) e avaliação
  (conjunto de 50+ perguntas do SC-003, executado sob demanda com OpenAI real).
- **XMLs de teste**: sintéticos, um por período de vigência (TS-050, SC-011), gerados a partir de um
  modelo mínimo válido e ajustados ao pacote; nenhum dado real de contribuinte (P17).
- **Duplos de teste**: `IChatClient` determinístico que devolve respostas roteirizadas, inclusive
  inválidas, para TS-016, TS-027, TS-028 e TS-055; relógio controlável para expiração e vigência.
- **Comandos (lição V-07)**: `dotnet test --project tests/AuditoriaNFe.Tests`; a avaliação é marcada
  com o trait `Categoria=Avaliacao` e excluída por padrão com `--filter-not-trait`.

### R-24 — Contêineres e ambiente local

- **Dockerfile** (`src/Api.Agente/Dockerfile`): multiestágio, com SDK .NET 10 no build e
  `mcr.microsoft.com/dotnet/aspnet:10.0.14` no runtime (mesma versão das bibliotecas ASP.NET Core
  fixadas), tag de patch fixa e nunca flutuante. Executa como usuário não root (`$APP_UID`), porta
  interna 8080 por `ASPNETCORE_HTTP_PORTS`, sem segredos e sem `appsettings` com credenciais. Os
  schemas, o manifesto e os catálogos são copiados para a imagem como conteúdo do projeto de
  Infraestrutura.
- **Compose** (`docker-compose.yml` na raiz, projeto `auditoria-nfe`), no modelo do projeto-ia-infis:
  imagens com versão fixa, segredos obrigatórios com `${VAR:?Defina VAR no arquivo .env}`,
  healthchecks, scripts de inicialização montados somente leitura, volumes e rede com nome próprio.

  | Serviço | Imagem | Porta no host | Observação |
  | ------- | ------ | ------------- | ---------- |
  | `api-agente` | build local | `127.0.0.1:5310` → 8080 | `depends_on` com `service_healthy` dos bancos; volume de checkpoints |
  | `mongodb` | `mongo:8.0.32` | `127.0.0.1:27020` → 27017 | `infra/mongodb/init`: coleções com `$jsonSchema`, índices e TTL do contexto do usuário |
  | `postgres` | `pgvector/pgvector:0.8.7-pg17` | `127.0.0.1:5436` → 5432 | `infra/postgres/init`: usuário e schema `consumo`; a extensão `vector` fica disponível para a feature 002 |

- **Portas**: escolhidas para não conflitar com os outros projetos da máquina (SofiaInfis e
  projeto-ia-infis usam 27018, 5052, 5053, 5054, 5055, 5056, 1434, 5434, 5435, 5673 e 1026; o
  AgenteFinanceiro usa 5100; outras APIs usam 5001, 5147, 5253, 5285 e 8080). Todas publicadas só em
  `127.0.0.1` (FR-052b).
- **Segredos**: `.env` não versionado; `.env.example` versionado sem valores. A chave da OpenAI é
  opcional com `Ia__Provedor=Simulado`. Nenhuma chave é copiada de outros projetos.
- **Alternativas**: imagem `postgres` sem `vector` (a extensão não é usada nesta feature; mantida pela
  feature 002 e por ser a imagem já disponível localmente); executar só os bancos em contêiner (fica
  como alternativa de desenvolvimento no quickstart).

### R-25 — CI e proteção de branch (DP-26)

- **Decisão**: GitHub Actions com job `build-test` (restore, build com avisos como erro, testes com
  Docker para Testcontainers, sem chave da OpenAI) e job de build da imagem. Branches: features →
  PR → `dev` (integração) → PR → `main` (publicação). Proteção da `main` e da `dev`: PR
  obrigatório, check `build-test` obrigatório, sem push direto nem force push e **zero aprovações
  obrigatórias** (desvio D-06: o GitHub não permite que o autor aprove o próprio PR, e o projeto tem
  uma única mantenedora). Em troca, a descrição do PR traz um checklist de conformidade com a
  constitution, que a mantenedora preenche e confirma com o comentário "Aprovado" antes do merge.
  Registro de imagens (GHCR) somente quando houver implantação (DP-25). Um teste de varredura
  (`SemSegredosTests`, como no AgenteFinanceiro) falha se encontrar padrões de chave de API no
  repositório.
- **Registro**: ADR-0008, aprovado por Andressa Firmino em 2026-11-04.

### R-26 — Implantação (DP-25): Vercel avaliada e não adotada nesta feature

- **Fato verificado**: desde 2026-10-27 a Vercel oferece, em beta, um runtime de contêiner que
  implanta um `Dockerfile` (ou `Dockerfile.vercel`) como Vercel Functions, sem estado local durável,
  com escala sob demanda e cobrança por CPU ativa; a porta padrão é 80, configurável por `PORT`.
- **Decisão**: esta feature roda localmente com Docker, também por causa do D-05 (FR-052b). A Vercel
  só poderá hospedar o Api.Agente após o fim do D-05, após ADR da DP-25 e após trocar os checkpoints
  JSON locais por armazenamento durável, com MongoDB Atlas e PostgreSQL externos. O texto da DP-25 na
  constitution ("a Vercel não oferece runtime oficial para .NET") está desatualizado e deve ser
  corrigido por emenda PATCH.

### R-27 — Nome da solução

- **Decisão**: `AgenteAuditoriaNFe`, como exige a constitution (3.1). O nome "AgenteAnaliseDIMP"
  citado na descrição de arquitetura do solicitante não é adotado (DIMP é outra obrigação acessória);
  se for intencional, exige emenda da constitution.

### R-28 — Estrutura física das camadas (DP-19)

- **Decisão**: projetos separados: `Api.Agente` (Endpoints), `AuditoriaNFe.Application`,
  `AuditoriaNFe.Domain`, `AuditoriaNFe.Infrastructure`, mais `Sofia.Simulator` e
  `AuditoriaNFe.Tests`. A Api.RAG terá seus projetos na feature 002.
- **Dependências**: a Aplicação referencia as abstrações do MAF (`Microsoft.Agents.AI`,
  `Microsoft.Agents.AI.Workflows`, `Microsoft.Extensions.AI.Abstractions`), permitido pela
  constitution 2.0.0 (3.3). Clientes concretos (OpenAI, MCP, MongoDB, Npgsql) ficam na
  Infraestrutura; o servidor A2A e os DTOs do contrato SOFIA ficam no `Api.Agente`. O Domínio não referencia nenhum pacote
  externo. O antigo desvio D-02 deixou de existir.

### R-29 — Limites de requisição e concorrência (constitution 5.2)

- **Decisão**: limite do corpo HTTP configurado acima do limite lógico (para que o sistema consiga
  responder ERR-002 com o total recebido; o padrão é o dobro do limite total de arquivos, por causa da
  codificação base64 do REST e do escape JSON dos textos no A2A); validação de tamanho total e quantidade antes de qualquer
  processamento; `ConcurrencyLimiter` do ASP.NET Core para as rotas de análise (inicial: 10
  execuções simultâneas, NFR-004) e limitação de taxa por chave de isolamento com janela fixa.

### R-30 — Identificadores

- **Decisão**: identificadores de execução, sessão, recomendação e cálculo gerados pelo sistema com
  `Guid.CreateVersion7()` (ordenáveis por tempo), nunca pelo LLM (P09).

### R-31 — Contexto do usuário (FR-055 a FR-060, P15, constitution 4.7)

- **Armazenamento**: coleção MongoDB `contextos_usuario`, um documento por chave de isolamento
  (SHA-256 de `chamadorId` e `usuarioId`), com `usuarioId` e `chamadorId` em claro para consulta e
  exclusão pelo canal direto. Validação de schema (`$jsonSchema`) na criação da coleção, no modelo do
  `01-historico.js` do projeto-ia-infis, para impedir campos fora do FR-055a.
- **Conteúdo (FR-055a)**:
  1. `cnpjsAuditados`: CNPJ (14 caracteres, inclusive o formato alfanumérico do `TCnpj` do PL_010e),
     papel (emitente ou destinatário), quantidade de documentos e data da última auditoria. CPF
     nunca é gravado;
  2. `achadosRecorrentes`: chave (`regraId` ou tipo de erro estrutural), frequência e data da última
     ocorrência, só de achados determinísticos;
  3. `preferencias`: `nivelDetalhe` (`Resumido` ou `Detalhado`) e `tributosInteresse` (lista fechada
     de códigos, como ICMS, ICMS-ST, IPI, PIS, COFINS, ISSQN, IBS, CBS e IS).
  Cada item guarda a origem (`execucaoId` ou declaração, com `sessaoId` e data — FR-055b).
- **Gravação (FR-057)**: só pelo caso de uso, na etapa 9, a partir dos documentos e achados
  determinísticos da execução, e pelo endpoint de preferências do canal direto (declaração explícita
  e estruturada; nunca extraída do texto livre pela IA). O contrato SOFIA v1.0 não tem campo de
  preferências. Operações
  idempotentes por execução (a mesma execução não conta duas vezes).
- **Leitura (FR-058)**: pelo `ContextoUsuarioProvider` (R-07), como dado não confiável e delimitado,
  com limite de itens; não entra como evidência nem altera achados.
- **Usuário "anonimo" (FR-052c)**: nenhuma leitura nem gravação; o parecer indica usuário não
  identificado.
- **Retenção (FR-060)**: TTL de 90 dias sem atividade, configurável (R-19). **Exclusão e correção
  (FR-059)**: endpoints do canal direto (plano, Canal direto).
- **Alternativas**: guardar o contexto dentro da sessão do MAF (rejeitado: mistura histórico e
  contexto, contra o P15); deixar o LLM resumir o usuário ao fim de cada conversa (rejeitado: FR-057).

### R-32 — Contrato com a SOFIA: contrato SOFIA v1.0 (DP-27 resolvida)

- **Decisão**: o canal A2A fala só o contrato SOFIA v1.0 da INFIS
  ([contracts/sofia-a2a.md](./contracts/sofia-a2a.md) e os dois JSON Schemas), que cobre a análise
  de documentos com pergunta. Perguntas sem documentos, continuidade de sessão, consulta de
  execução, preferências e aprovação ficam no canal direto. O núcleo do agente continua testado pelo
  canal direto; nas tarefas, o canal A2A e o simulador vêm depois do núcleo REST.
- **Identificação (D-05)**: chamador padrão `sofia` (`A2A:ChamadorPadrao`); usuário pelo
  `usuario.nome`, com `usuarioId = "nome-sha256:<hex>"` do nome normalizado e o nome persistido na
  sessão e na execução (data-model §0). Com o ADR da DP-10, a identificação passa à credencial de
  serviço.
- **`idAgente`**: `agente-auditoria-nfe` (`A2A:IdAgente`); o `agente-fiscal` da referência
  colidiria no registry da SOFIA.
- **Repetição (FR-043h)**: a SOFIA reutiliza o `idSolicitacao` nas retentativas, e o
  `InMemoryTaskStore` não sobrevive a reinícios nem identifica a solicitação de negócio. A
  idempotência fica no MongoDB: `chaveIdempotenciaSofia` com índice único parcial
  (`partialFilterExpression` com `$exists: true`), removida quando a execução falha. Execução
  terminada com sucesso é devolvida pelo mesmo mapeador, sem custo de IA; em andamento → ERR-026.
  Alternativas: reprocessar sempre (custo duplicado e contexto do usuário contado duas vezes) ou
  recusar toda repetição (a SOFIA perderia o resultado após um tempo limite de rede).
- **Rastreamento (NFR-014)**: o ASP.NET Core extrai o `traceparent` W3C para a `Activity` da
  requisição, e o OpenTelemetry (ADR-0009) a exporta; o adaptador só acrescenta tags com os IDs da
  SOFIA. Sem metadado A2A próprio para trace na v1.
- **Justificativa**: contrato oficial da turma; o adaptador A2A isola o caso de uso de mudanças no
  contrato.
- **Alternativas**: manter o contrato provisório ao lado do SOFIA v1.0 (dois contratos no mesmo
  canal, sem cliente para o provisório).

### R-33 — Lições do AgenteFinanceiro (mesma versão do MAF e do SDK A2A)

Achados registrados em `AgenteFinanceiro/specs/001-agente-fluxo-caixa/research.md` durante a
implementação, aplicáveis aqui:

| ID | Achado | Aplicação neste projeto |
| -- | ------ | ----------------------- |
| V-06 | No A2A 1.0.0-preview3, `TaskProjection.Apply` não copia os metadados de um evento de status para `AgentTask.Metadata`, e um evento `Task` substitui o estado inteiro da tarefa | Sem efeito no contrato SOFIA v1.0: o resultado vai no Artifact, não em metadados. O manipulador não publica evento `Task` depois do Artifact, para não apagá-lo; o teste de contrato confere o Artifact na Task final |
| V-07 | `dotnet test` em modo VSTest falha com xUnit v3 no SDK .NET 10 | `global.json` com `"test": { "runner": "Microsoft.Testing.Platform" }`; comandos com `--project` e filtros `--filter-trait`/`--filter-not-trait` |
| V-08 | `AgentSessionStore` é experimental no MAF 1.26 (`MAAI001`) | Supressão pontual de `MAAI001` nos projetos que usam o store (seção 1) |
| V-09 | `A2A.AgentSkill` tem o mesmo nome de `Microsoft.Agents.AI.AgentSkill` | No `Api.Agente`, referenciar os tipos do SDK A2A com `global::A2A.` (e os do v0.3 com `global::A2A.V0_3.`) |
| V-10 | O `A2AServer` recusa mensagens para tarefas encerradas com `UnsupportedOperation` antes de chamar o handler | Cada solicitação SOFIA cria uma Task nova; mensagem com `taskId` de Task encerrada recebe o erro do SDK, coberto por teste de contrato |

Outros padrões reaproveitados: `IChatClient` dublê com roteiro fixo, `WebApplicationFactory`,
testes de arquitetura, health checks, `SemSegredosTests` e a ordem de tarefas do `tasks.md`.

### R-34 — CORS e conectividade com a SOFIA

- **Decisão**: a integração SOFIA → agente é de servidor para servidor (A2A e REST) e não usa CORS.
  A política nomeada `SofiaBrowser` (middleware de CORS do ASP.NET Core, sem pacote extra) fica
  preparada só para as rotas REST `/api/v1`, aplicada com `RequireCors` no grupo de rotas, com
  origens de `Cors:AllowedOrigins` (vazia por padrão), métodos e cabeçalhos enumerados e sem
  credenciais de navegador. Agent Card, A2A, saúde e documentação ficam sem CORS.
- **Justificativa**: o navegador é o único que aplica CORS; habilitá-lo no A2A ou no card não ajuda
  a SOFIA e amplia a superfície. Política por endpoint evita liberar rotas não previstas. Validar as
  origens na inicialização evita erros silenciosos (barra final ou caminho nunca casam com o
  cabeçalho `Origin`).
- **Conectividade**: no contêiner, Kestrel em todas as interfaces na porta 8080; no host, porta só
  em `127.0.0.1` (D-05); a SOFIA em contêiner usa o nome do serviço na rede compartilhada.
- **Alternativas**: não preparar CORS (exigiria mudança de código quando surgir um frontend);
  política global com `AllowAnyOrigin` (rejeitada: libera qualquer site e todas as rotas).

### R-35 — Publicação somente do Agent Card no Vercel (D-08, ADR-0012)

- **Fatos verificados em 2026-11-05** ([guia de contêineres](https://vercel.com/kb/guide/does-vercel-support-docker-deployments),
  [changelog de 2026-10-27](https://vercel.com/changelog/deploy-a-container-from-a-plain-dockerfile)):
  - o Container runtime está em beta em todos os planos, roda sobre Vercel Functions e é cobrado
    por CPU ativa, memória provisionada e invocações;
  - `Dockerfile.vercel` prevalece sobre a detecção de framework; a imagem é construída pelo Vercel
    e guardada no Vercel Container Registry. Na prática (T172), o projeto criado pelo `vercel link`
    ficou com o preset "Other", que prevaleceu sobre o `Dockerfile.vercel`; foi preciso declarar
    `"framework": "container"` no `vercel.json`;
  - o tráfego vai para a porta 80; outra porta exige a variável `PORT` no projeto e o servidor
    escutando nela. A imagem do .NET roda sem root, então a porta 8080 com `PORT=8080` evita
    depender de permissão para portas abaixo de 1024;
  - instâncias ociosas são encerradas após 5 minutos em produção e 30 segundos em preview, com
    `SIGTERM` e 30 segundos para encerrar; memória e arquivos locais não são duráveis;
  - Secure Compute e IPs estáticos não funcionam com contêineres próprios.
- **Decisão**: o modo `SomenteAgentCard` atende a esses limites, porque não guarda estado, não
  depende de banco nem de processo contínuo e inicia sem connection strings. O agente completo
  continua fora do Vercel (R-26).
- **Porta**: o `Dockerfile.vercel` inicia com `ASPNETCORE_HTTP_PORTS=${PORT:-8080}`, para que o
  Kestrel escute na porta indicada pelo Vercel, e o projeto define `PORT=8080`.
- **Formato do card**: o do contrato SOFIA v1.0 (perfil A2A v0.3, referência
  `exemplos-a2a-v03.md` da skill do contrato), com `url`, `protocolVersion` e `preferredTransport`.
  Cards no formato A2A 1.0 de outros agentes da turma (com `supportedInterfaces`) não mudam o
  contrato; o formato 1.0 continua disponível pelo cabeçalho `A2A-Version: 1.0` a partir da Fase 8.
- **Alternativas**: JSON estático (rejeitado: não exercita o contêiner .NET e duplica o card);
  aguardar a DP-25 (rejeitado pela autora: adia o cadastro na SOFIA).
- **Publicação (T172)**: projeto `andressafirminos-projects/agente-auditoria-nfe`, ligado ao
  repositório `andressafirmino/agente-validacao-nfe` com a `main` como branch de produção; variáveis
  `PORT` e `A2A__UrlPublica` em produção e preview, sem connection strings nem chaves.
  - Preview pelo CLI em 2026-11-05 (`dpl_HdDsFqqc5ybuESkdaownC3gAQht7`, commit `2c7ea2a` da branch
    `feat/001-contrato-sofia-v1`), conferido com `vercel curl` por causa da proteção de preview.
  - Produção pelo merge da PR #5 (`dev` → `main`), commit `bf96dd8`, deployment
    `dpl_9H4T36cxZjy5oPo7S5KQ4roWBeuK` (origem `git`, build de cerca de 2 minutos), em
    `https://agente-auditoria-nfe.vercel.app`.
- **Verificação da produção** (2026-11-05 e de novo em 2026-11-08, sem autenticação):
  `GET /.well-known/agent-card.json` → 200 `application/json; charset=utf-8`, com
  `url` = `https://agente-auditoria-nfe.vercel.app/a2a/auditoria-nfe`, `protocolVersion` `0.3.0`,
  `preferredTransport` `JSONRPC`, versão `1.0.0`, `implantacao` na extensão e sem
  `Access-Control-Allow-Origin`; `/health/live` → 200; `/`, `/health/ready`, `/api/v1/execucoes`,
  `/a2a/auditoria-nfe` (GET e `POST message/send`), `/swagger`, `/openapi/v1.json`,
  `/appsettings.json`, `/Dockerfile.vercel`, `/src/Api.Agente/Program.cs` e `/.env` → 404. Após
  ociosidade, a primeira resposta levou cerca de 2 s (instância reiniciada).
- **Ocorrências da publicação**:
  - o primeiro deploy de um projeto vai para produção, mesmo sem `--prod`. Sem o
    `"framework": "container"`, ele publicou o repositório como site estático (código-fonte de
    `src/` acessível; nenhum segredo, porque o `.vercelignore` excluiu `.env*`, `.vercel/`,
    `tests/` e `specs/`). O deployment foi removido no mesmo dia, com aprovação da autora;
  - no plano Hobby, o Vercel bloqueia o deploy quando o autor do último commit não é a conta dona
    do projeto (estado "Blocked"); o repositório passou a usar o e-mail da conta no
    `git config user.email` local;
  - os domínios `agente-auditoria-nfe-andressafirminos-projects.vercel.app` e
    `agente-auditoria-nfe-git-main-...` ficam atrás da proteção de deployments e devolvem a página
    de login; o endereço público para a SOFIA é só `agente-auditoria-nfe.vercel.app`.

### R-36 — Homologação pública do agente completo (D-09, ADR-0013)

- **Fatos verificados em 2026-11-10** ([guia de contêineres](https://vercel.com/kb/guide/does-vercel-support-docker-deployments),
  [limites das Functions](https://vercel.com/docs/functions/limitations),
  [plano Hobby](https://vercel.com/docs/plans/hobby),
  [limites do Atlas Free](https://www.mongodb.com/docs/atlas/reference/free-shared-limitations/),
  [preços do Neon](https://neon.com/pricing)):
  - Vercel Hobby: duração máxima de 300 s; 2 GB e 1 vCPU; corpo de requisição e de resposta até
    4,5 MB (413 `FUNCTION_PAYLOAD_TOO_LARGE`); região padrão `iad1`; 4 horas de CPU ativa e 360
    GB-hora de memória por mês; ao estourar, o recurso fica pausado até 30 dias; logs de execução
    retidos por 1 hora; uso só pessoal e não comercial (confirmado pela autora para o projeto do
    curso);
  - Container runtime: instâncias ociosas encerradas após 5 minutos em produção e 30 segundos em
    preview; memória e disco não duráveis; sem IP estático nem Secure Compute;
  - Atlas Free: um cluster por projeto, 0,5 GB, 100 operações por segundo, 500 conexões, 10 GB por
    semana em cada sentido, sem private endpoint nem peering, pausa após 30 dias sem conexões;
    usuários de banco criados pelo painel (o shell não grava no banco `admin`);
  - Neon Free: 1 GB por projeto, 100 CU-hora por mês, suspensão do compute após 5 minutos ocioso
    (retomada em centenas de milissegundos), 5 GB de tráfego de saída, extensões como `vector`
    disponíveis, sem regras de IP; ao estourar compute ou tráfego, o compute fica suspenso até o
    próximo ciclo.
- **Compatibilidade do agente completo**:

  | Item | Situação | Tratamento |
  | ---- | -------- | ---------- |
  | Imagem .NET 10 (`Api.Agente.dll`) | OK | `Dockerfile.vercel` existente, com o modo vindo da variável do projeto |
  | Análise em até 120 s (NFR-001) | OK | Cabe nos 300 s |
  | Lote de 5 MB (FR-004) | Adaptação | 4.000.000 bytes de arquivos e 4.500.000 de corpo na homologação (D-09) |
  | Inicialização a frio | Adaptação | Migrações fora da inicialização; medir o tempo da primeira resposta |
  | Limites de taxa e concorrência em memória | Adaptação | Valem por instância; teto de custo pelo limite de gasto da OpenAI |
  | Checkpoints de aprovação em arquivo (Fase 7) | Bloqueio para produção | `/tmp/aprovacoes` na homologação, sem durabilidade |
  | Tasks A2A em memória (Fase 8) | OK na homologação | A Task final volta na mesma requisição do `message/send` |
  | MongoDB (coleções, validadores e índices) | Adaptação | Script `mongosh` sem criação de usuário, executado uma vez no Atlas |
  | PostgreSQL de consumo | OK | Neon com schema `consumo` e migrações por script SQL idempotente |
  | Acesso do Vercel ao Atlas | Adaptação com risco | `0.0.0.0/0` com controles compensatórios (aprovado) |
  | Swagger fora de `Development` | Adaptação | Habilitado só no modo `Homologacao` (D-09) |
  | Integração com a SOFIA (A2A) | Bloqueio temporário | Depende da Fase 8 |

- **Decisão**: homologação no Vercel Hobby, pelo modo `Homologacao` (ADR-0013), com Atlas e Neon na
  AWS `us-east-1`, perto do `iad1`. Revisão de 2026-11-10, aprovada pela autora: no mesmo projeto
  do Agent Card (`agente-auditoria-nfe`, ADR-0012), para que a SOFIA continue com a URL já
  cadastrada; o código do modo chega à `main` antes de as variáveis serem cadastradas, porque o
  projeto publica a cada merge na `main`.
- **Modo pela variável do projeto**: o `Dockerfile.vercel` mantém `Hospedagem__Modo=SomenteAgentCard`
  como padrão; o projeto define `Hospedagem__Modo=Homologacao`. Falta confirmar na publicação
  (T184) que a variável do projeto prevalece sobre o `ENV` da imagem; se não prevalecer, retirar o
  `ENV` do modo do `Dockerfile.vercel`, e então, sem a variável, a guarda do D-05 impede a
  inicialização em vez de publicar só o card. Com a variável removida e o `ENV` mantido, a imagem
  volta ao modo restrito.
- **Connection strings**: o Npgsql não aceita o formato URI (`postgresql://`) que o Neon mostra;
  usar o formato de chaves (`Host={host};Database={banco};Username={usuario};Password={senha};SSL Mode=Require`), com
  o host com pooling (`-pooler`) para a aplicação e o host direto para as migrações. No Atlas, a
  string `mongodb+srv://` traz o banco `auditoria_agente` no caminho; o usuário fica no `admin` do
  Atlas, então sem `authSource=auditoria_agente`.
- **Usuário do Neon** ([Manage roles](https://neon.com/docs/manage/roles), 2026-11-10): usuários
  criados pelo painel, pelo CLI sem `--access` ou pela API recebem `neon_superuser` (CREATEDB,
  CREATEROLE, BYPASSRLS); os criados por SQL só têm os privilégios concedidos. Por isso o
  `auditoria_app` é criado por SQL, com senha de pelo menos 60 bits de entropia, e o
  `neon-consumo.sql` recusa usuário com privilégio administrativo.
- **Validação local (T180 e T181, 2026-11-10)**:
  - `neon-consumo.sql` e o script idempotente das migrações executados duas vezes num PostgreSQL 17
    com dono do banco sem superusuário, como no Neon: tabelas com dono `auditoria_app`,
    `search_path = consumo`, sem criação no `public`; usuário membro de `neon_superuser` recusado;
  - `02-colecoes.js` executado num MongoDB 8.0 vazio com usuário só `readWrite` (como no Atlas) e
    reexecutado sobre o banco criado pelo entrypoint: coleções, validador e TTL presentes, sem erro;
  - imagem do `Dockerfile.vercel` com `Hospedagem__Modo=Homologacao` na rede do compose: card com a
    implantação de homologação e limite de 4.000.000 bytes, `/health/ready`, `/swagger` e
    `/openapi/v1.json` 200, servidor `https` com `X-Forwarded-Proto`, `/tmp/aprovacoes` criado pelo
    usuário não root, logs sem senhas nem chave; sem a variável, modo `SomenteAgentCard`; com o
    provedor simulado e os limites padrão, a inicialização é recusada citando o D-09.
- **Alternativas**: preview protegido pelo login do Vercel; Render Free; esperar a DP-10 (ADR-0013).

## 3. Uso documentado das APIs do Microsoft Agent Framework

Fonte principal: documentação oficial do Microsoft Agent Framework em Microsoft Learn (páginas
"Context Providers", "Agent Middleware", "Using function tools", "Using function tools with human in
the loop approvals", "Workflows - Checkpoints", "Host agents with A2A", "A2A Hosting (.NET)",
"Self-host Agent Framework applications", "A2A agent service", "Custom Agents", "Using MCP Tools") e
o repositório `microsoft/agent-framework`. Os trechos abaixo mostram a forma pública das APIs; não são
código da aplicação.

### 3.1 `IChatClient`

Abstração do Microsoft.Extensions.AI para modelos de chat. É a base do `ChatClientAgent` e o ponto de
middleware de baixo nível (consumo, telemetria).

```csharp
IChatClient chatClient = new OpenAIClient(apiKey)
    .GetChatClient(modelo)
    .AsIChatClient()
    .AsBuilder()
    .Use(getResponseFunc: RegistrarConsumo, getStreamingResponseFunc: null)
    .UseOpenTelemetry()
    .Build();
// ChatResponse.Usage: UsageDetails { InputTokenCount, OutputTokenCount, TotalTokenCount, ... }
```

### 3.2 `AIAgent`

Classe base abstrata de todos os agentes. Métodos públicos: `RunAsync(...)`, `RunStreamingAsync(...)`,
`RunAsync<T>(...)` (saída estruturada), `CreateSessionAsync()`, `SerializeSessionAsync(...)`,
`DeserializeSessionAsync(...)`. Um agente personalizado sobrescreve os métodos protegidos
`RunCoreAsync(IEnumerable<ChatMessage>, AgentSession?, AgentRunOptions?, CancellationToken)`,
`RunCoreStreamingAsync(...)`, `CreateSessionCoreAsync(CancellationToken)` e os de serialização de
sessão. `DelegatingAIAgent` implementa o padrão decorador sobre um agente interno.

### 3.3 `ChatClientAgent`

Implementação de `AIAgent` sobre um `IChatClient`, com loop de Function Calling
(`FunctionInvokingChatClient`) embutido.

```csharp
AIAgent agente = new ChatClientAgent(chatClient, new ChatClientAgentOptions
{
    Id = "auditoria-nfe-analise",          // estável: exigido para workflows com checkpoint
    Name = "auditoria-nfe-analise",
    ChatOptions = new() { Instructions = instrucoes, Tools = ferramentas },
    AIContextProviders = [contextoExecucao, contextoSessao, contextoUsuario],
});
// Atalho com o SDK OpenAI: openAiClient.GetChatClient(modelo).AsAIAgent(options)
AgentResponse<ContratoIaV1> r = await agente.RunAsync<ContratoIaV1>(mensagens, sessao);
ContratoIaV1 resultado = r.Result;
```

### 3.4 `AgentSession`

Estado de conversa de um agente (histórico, estado dos providers em `AgentSessionStateBag`).

```csharp
AgentSession sessao = await agente.CreateSessionAsync();
await agente.RunAsync(mensagem, sessao);
JsonElement serializada = await agente.SerializeSessionAsync(sessao);
AgentSession restaurada = await agente.DeserializeSessionAsync(serializada);
```

Persistência hospedada: `AgentSessionStore` (abstrata, experimental — `MAAI001`) com
`SaveSessionAsync(AIAgent agent, string sessionStoreId, AgentSession session, CancellationToken)` e
operações assíncronas de leitura e exclusão. O `sessionStoreId` é opaco.

### 3.5 `AIContextProvider`

Executa antes e depois de cada invocação do agente para adicionar contexto (instruções, mensagens,
ferramentas) e extrair informação da resposta.

```csharp
sealed class ContextoExemplo : AIContextProvider
{
    private readonly ProviderSessionState<Estado> _estado;
    public ContextoExemplo() : base(null, null)
        => _estado = new ProviderSessionState<Estado>(_ => new Estado(), nameof(ContextoExemplo));
    public override string StateKey => _estado.StateKey;

    protected override ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context, CancellationToken cancellationToken = default)
    {
        Estado e = _estado.GetOrInitializeState(context.Session);
        return new(new AIContext { Instructions = "...", Messages = [...], Tools = [...] });
    }

    protected override ValueTask StoreAIContextAsync(
        InvokedContext context, CancellationToken cancellationToken = default)
    {
        Estado e = _estado.GetOrInitializeState(context.Session);
        _estado.SaveState(context.Session, e);
        return ValueTask.CompletedTask;
    }
}
```

Variante avançada: sobrescrever `InvokingCoreAsync` e `InvokedCoreAsync`. Regra: estado por sessão
somente via `ProviderSessionState<T>`, nunca em campos do provider (o provider é compartilhado entre
sessões).

### 3.6 Middleware

```csharp
AIAgent agenteComMiddleware = agente
    .AsBuilder()
    .Use(runFunc: ExecucaoMiddleware, runStreamingFunc: ExecucaoStreamingMiddleware)
    .Use(FunctionCallingMiddleware)
    .Build();

// Execução: Task<AgentResponse> (IEnumerable<ChatMessage> messages, AgentSession? session,
//           AgentRunOptions? options, AIAgent innerAgent, CancellationToken ct)
// Function Calling: ValueTask<object?> (AIAgent agent, FunctionInvocationContext context,
//           Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
//           CancellationToken ct) — context.Terminate = true encerra o loop.
// Use(sharedFunc: ...) só inspeciona a entrada, sem interceptar a saída.
```

O middleware de Function Calling só funciona com agentes que usam `FunctionInvokingChatClient`, como o
`ChatClientAgent`.

### 3.7 Ferramentas e aprovação de ferramentas

```csharp
AIFunction f = AIFunctionFactory.Create(MetodoComDescription);   // [Description] nos parâmetros
AIFunction comAprovacao = new ApprovalRequiredAIFunction(f);       // não usado na v1
// A resposta traz ToolApprovalRequestContent (.ToolCall: FunctionCallContent);
// responde-se com requestContent.CreateResponse(true|false) numa ChatMessage do usuário,
// na mesma sessão.
```

### 3.8 Workflow com aprovação humana e checkpoints

```csharp
var porta = RequestPort.Create<SolicitacaoAprovacao, DecisaoAprovacao>("aprovacao");
Workflow wf = new WorkflowBuilder(registrar)
    .AddEdge(registrar, porta)
    .AddEdge(porta, aplicarDecisao)
    .WithOutputFrom(aplicarDecisao)
    .Build();

var store = new FileSystemJsonCheckpointStore(new DirectoryInfo(diretorio));
CheckpointManager cm = CheckpointManager.CreateJson(store);
StreamingRun run = await InProcessExecution.RunStreamingAsync(wf, entrada, cm);
await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    // RequestInfoEvent: pedido pendente (req.Request.TryGetDataAs<T>(out ...))
    // SuperStepCompletedEvent: evt.CompletionInfo?.Checkpoint (CheckpointInfo)
    // WorkflowOutputEvent / WorkflowErrorEvent
}
// Retomada: InProcessExecution.ResumeStreamingAsync(wf, checkpointInfo, cm);
// o RequestInfoEvent pendente é reemitido; responde-se com
// run.SendResponseAsync(req.Request.CreateResponse(decisao)).
```

Executores derivam de `Executor<TEntrada>("id")` e implementam `HandleAsync(mensagem,
IWorkflowContext contexto, CancellationToken)`, usando `contexto.SendMessageAsync(...)` e
`contexto.YieldOutputAsync(...)` (com `[YieldsOutput(typeof(T))]`). O workflow usado na retomada DEVE
ter a mesma estrutura e os mesmos ids de executores; agentes dentro de workflows precisam de
`ChatClientAgentOptions.Id` estável.

### 3.9 A2A — servidor

Fonte: repositório `a2aproject/a2a-dotnet`, tag `v1.0.0-preview3` (`src/A2A`, `src/A2A.AspNetCore`
e `src/A2A.V0_3Compat`). Forma das APIs, sem código de aplicação:

```csharp
builder.Services.AddSingleton<ITaskStore, InMemoryTaskStore>();
builder.Services.AddSingleton<IAgentHandler, ManipuladorSofiaA2A>();
builder.Services.AddSingleton<IA2ARequestHandler>(sp => new A2AServer(
    sp.GetRequiredService<IAgentHandler>(), sp.GetRequiredService<ITaskStore>() /* , ... */));

var handler = app.Services.GetRequiredService<IA2ARequestHandler>();
app.MapA2AWithV03Compat(handler, "/a2a/auditoria-nfe");              // JSON-RPC v0.3 e v1.0
app.MapAgentCardGetWithV03Compat(handler, () => Task.FromResult(fabrica.Criar()),
    blendedCard: false);                                             // GET /.well-known/agent-card.json

// No manipulador (IAgentHandler.ExecuteAsync(RequestContext, AgentEventQueue, CancellationToken)):
var updater = new TaskUpdater(eventQueue, contexto.TaskId, contexto.ContextId);
await updater.SubmitAsync(ct);
await updater.StartWorkAsync(ct);
await updater.AddArtifactAsync(/* Artifact "analise-sofia-v1" com TextPart do resultado */, ct);
await updater.CompleteAsync(ct);   // ou FailAsync(mensagem "ERR-007: …") sem IDs válidos
```

A camada `V0_3Compat` traduz requisições e respostas v0.3 para os tipos do SDK 1.0 e mapeia
`ReturnImmediately = !Blocking`. Os nomes exatos dos parâmetros DEVEM seguir a versão fixada; o
spike de validação (R-11) e os testes de contrato conferem o comportamento.

### 3.10 Agent Card

```csharp
new AgentCard
{
    Name = "AgenteAuditoriaNFe", Description = "...", Version = "...",
    DefaultInputModes = ["text"], DefaultOutputModes = ["text"],
    Capabilities = new() { Streaming = false, PushNotifications = false,
        Extensions = [ /* urn:infis:sofia:contrato, required = false, params do contrato §2 */ ] },
    Skills = [ new global::A2A.AgentSkill { Id = "analisar_documentos", /* nome, descrição, tags, exemplos */ } ],
    SupportedInterfaces =
    [
        new AgentInterface { Url = "{A2A:UrlPublica}/a2a/auditoria-nfe", ProtocolBinding = ProtocolBindingNames.JsonRpc, /* ... */ },
    ],
};
// Entregue por MapAgentCardGetWithV03Compat, que converte para v0.3 (url, preferredTransport,
// protocolVersion) conforme o cabeçalho A2A-Version.
```

O card é montado nos tipos do SDK 1.0 e convertido pelo `V03TypeConverter`. Os nomes exatos das
propriedades DEVEM seguir o SDK fixado; o teste de contrato TS-029 valida o card publicado no
formato v0.3 contra o contrato §2.

### 3.11 A2A — cliente

Pacote `A2A.V0_3` 1.0.0-preview3 (cliente oficial do perfil v0.3):

```csharp
var resolver = new global::A2A.V0_3.A2ACardResolver(new Uri(urlAgente));
var card = await resolver.GetAgentCardAsync();
var cliente = new global::A2A.V0_3.A2AClient(new Uri(card.Url));
var resposta = await cliente.SendMessageAsync(new MessageSendParams
{
    Message = /* role user, messageId, uma TextPart com a SolicitacaoAnaliseSofia em JSON */,
});
// resposta é a Task final: status.state, artifacts["analise-sofia-v1"].parts[0].text.
```

### 3.12 MCP — cliente

```csharp
await using McpClient cliente = await McpClient.CreateAsync(new HttpClientTransport(
    new HttpClientTransportOptions
    {
        Endpoint = new Uri(urlApiRag), Name = "api-rag",
        TransportMode = HttpTransportMode.StreamableHttp, AdditionalHeaders = cabecalhos,
    }));
IList<McpClientTool> ferramentas = await cliente.ListToolsAsync();
// McpClientTool deriva de AIFunction; pode ser passado como AITool ou chamado diretamente.
```

## 4. Riscos e pontos de atenção

| Risco | Impacto | Mitigação |
| ----- | ------- | --------- |
| Sem autenticação na v1 (D-05) | Qualquer processo com acesso à porta pode enviar solicitações, se passar por outro usuário e aprovar recomendações | Só `Development`, portas em `127.0.0.1`, dados sintéticos, card declarando ausência de autenticação; D-05 encerrado antes de qualquer implantação ou da SOFIA real |
| Pacotes A2A e `A2A.V0_3Compat` em pré-lançamento | Mudanças incompatíveis entre versões | Versões fixadas; spike de validação; testes de contrato do card, da Message e da Task; atualização em PR próprio |
| Task devolvida só no estado final | Estados intermediários não observáveis (FR-046) | Desvio D-04 revisado; tempo máximo de 120 s (ERR-016); a SOFIA usa tempo limite ≥ 150 s |
| SOFIA envia `blocking: false` | Resposta com o retrato inicial da Task, sem resultado | Regra no contrato §3.1; teste de contrato com e sem `configuration` |
| Identificação pelo nome (D-05) | Homônimos compartilham o contexto do usuário; nome é dado pessoal | Nome fora de logs, do LLM e do contexto; hash no isolamento; TS-067; fim do D-05 antes da SOFIA real |
| Tasks só em memória | Perda das Tasks ao reiniciar; uma instância | `tasks/get` não é contratual; o resultado vai na resposta do `message/send` |
| Checkpoints JSON locais | Perda em contêiner sem volume; sem suporte a várias instâncias | Volume obrigatório no compose; limitação registrada; uma instância |
| Homologação pública sem autenticação (D-09) | Uso da chave da OpenAI por terceiros; leitura de dados de outro usuário por quem souber os identificadores | Limite de gasto no projeto da OpenAI; só dados sintéticos; limites de corpo abaixo do Vercel; card declarando a homologação; segredos só no projeto Vercel (R-36) |
| Atlas aberto a qualquer IP | Tentativas de acesso ao banco pela internet | Usuário só com `readWrite` no banco, senha forte, TLS, cluster exclusivo e dados sintéticos (R-36) |
| Tabela de vigência sujeita a ajustes | Pacote errado para alguma data | Manifesto versionado; TS-050 e SC-011 com um XML por período; pendências P1 a P5 da tabela |
| Concorrência sobre o `XmlSchemaSet` compilado | Erros intermitentes de validação sob carga | Teste de carga (NFR-004); pool por pacote se necessário |
| Ambiguidades do Anexo I (C2 a C5 do catálogo) | Achado divergente da SEFAZ em casos de borda | Decisões registradas no catálogo; testes com os casos de borda |
| SDK .NET 10 ausente na máquina | Build local falha | Instalar o SDK 10 antes das tarefas; build também pelo Dockerfile |

## 5. Itens "NEEDS CLARIFICATION" resolvidos

| Item | Resolução |
| ---- | --------- |
| Framework de agentes | MAF 1.26.0, exigido pela constitution 2.0.0 (R-02) |
| Modelo OpenAI (DP-05) | Parâmetro obrigatório sem padrão, escolhido por avaliação (R-03) |
| Memória e contexto do usuário (DP-01) | MongoDB, resolvida na constitution 2.0.0; ADR registra a decisão (R-08, R-31) |
| Schemas oficiais e seleção (DP-03) | 19 pacotes versionados com manifesto; seleção pela data de referência (R-14) |
| Catálogo de regras (DP-04) | Catálogo de totalizadores aprovado em 2026-11-04; arquivo JSON versionado (R-15) |
| Protocolo A2A (DP-17) | Perfil A2A v0.3.x (JSON-RPC, `message/send`) pelo SDK 1.0.0-preview3 com `A2A.V0_3Compat` (R-11); autenticação continua na DP-10 |
| Contrato com a SOFIA (DP-27) | Contrato SOFIA v1.0, resolvido na constitution 2.2.0; ADR-0011 proposto (R-32) |
| Estrutura física (DP-19) | Projetos separados (R-28) |
| Topologia PostgreSQL (DP-16) | Banco e usuário exclusivos do Api.Agente (R-16) |
| Tabela de preços (DP-22) | Arquivo versionado preenchido a partir da fonte oficial (R-16) |
| Observabilidade (DP-23) | OpenTelemetry (R-22) |
| CI e proteção de branch (DP-26) | GitHub Actions, branches `dev` e `main` protegidas (R-25) |
| Implantação (DP-25) | Não implantar em produção nesta feature; só o Agent Card (R-35) e a homologação pública pelo D-09 (R-36) |
| Documentação da API | Swagger UI, só em `Development` (R-20) |
| Docker e portas | Compose com portas 5310, 27020 e 5436 em `127.0.0.1` (R-24) |
| Autenticação (DP-10) | Fora da v1 (desvio D-05); modelo futuro no ADR (R-17) |
| Minimização (DP-14) e retenção (DP-13) | ADR-0004 e ADR-0005 aprovados (R-18, R-19); prazos de sessão e de contexto do usuário definidos pela spec; prazos de remoção física e de retenção do consumo ainda abertos |
