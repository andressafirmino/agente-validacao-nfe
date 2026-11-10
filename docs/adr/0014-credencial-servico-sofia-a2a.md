# ADR-0014 — Credencial de serviço da SOFIA no Agent Card e no canal A2A

- **Situação**: Aprovado
- **Data**: 2026-11-10
- **Aprovação**: Aprovado por Andressa Firmino, 2026-11-10
- **Relacionado a**: D-05 (revisão parcial), D-08, D-09; DP-10, DP-17; constitution 5.1, 5.2 e P18;
  FR-044, FR-045, FR-052, FR-052f, FR-052g, FR-052h (novo); TS-029, TS-071, TS-074, TS-076 (novo);
  ADR-0003, ADR-0011, ADR-0012, ADR-0013

## Contexto

A SOFIA (`https://sofia-infis.onrender.com/`) passou a autenticar a descoberta e a execução dos
agentes da turma. No código inspecionado em 2026-11-10 (`AgentDiscoveryService`,
`AgentCredentialProvider`), ela:

- envia `Authorization: Bearer {chave}` no `GET` do card (`DiscoveryCredentialRef`) e nas chamadas
  JSON-RPC (`CredentialRef`), com o valor lido de uma variável de ambiente;
- recusa o card (`UNSUPPORTED_AGENT_SECURITY`) se ele não declarar `securitySchemes` com um esquema
  `http`/`bearer` e `security` exigindo esse esquema.

Com o card atual, sem `securitySchemes` (D-05), a SOFIA não descobre o agente. A chave é uma só
para a turma, definida pelo professor, e chega ao agente pela variável `SOFIA_A2A_TOKEN`.

## Decisão

Adotar a credencial de serviço da SOFIA no canal A2A, encerrando o D-05 só nesse ponto:

- **Rotas protegidas**: `GET /.well-known/agent-card.json` e todo caminho sob `/a2a`, nos modos que
  publicam o card (`SomenteAgentCard` e `Homologacao`). Um middleware confere o cabeçalho
  `Authorization` antes do roteamento; por isso, o endpoint JSON-RPC da Fase 8 já nasce protegido,
  incluindo `message/send`, `tasks/get` e `tasks/cancel`.
- **Validação**: Bearer opaco, sem exigir JWT. O esquema `Bearer` não diferencia maiúsculas; a
  chave é comparada exatamente, pelo resumo SHA-256 e `CryptographicOperations.FixedTimeEquals`.
  Cabeçalho ausente, repetido, malformado ou com outra chave → HTTP 401 com
  `WWW-Authenticate: Bearer`, sem corpo e antes de qualquer processamento ou chamada ao LLM. A
  chave na URL ou no corpo não é aceita.
- **Configuração**: `SOFIA_A2A_TOKEN`, só no `.env` local (ignorado pelo Git) e nas variáveis do
  projeto Vercel, marcada como sensível. Sem valor, ou com espaços nas extremidades, o agente não
  inicia; nunca há acesso livre por falta de configuração. A troca da chave é só de configuração.
- **Agent Card**: declara `"securitySchemes": {"sofiaService": {"type": "http", "scheme": "bearer"}}`
  e `"security": [{"sofiaService": []}]`, sem o valor; o parâmetro `autenticacao` da extensão passa
  a `"bearer — credencial de serviço da SOFIA"`. A versão do contrato de negócio continua `1.0`.
- **Fora do escopo**: a API REST `/api/v1`, o Swagger e o OpenAPI continuam sem autenticação na
  homologação (D-09), por decisão da autora; a identificação do usuário, o papel de aprovador e a
  autenticação do canal direto continuam no D-05. `/health/live` continua sem credencial, para a
  verificação da plataforma.
- **Logs**: o cabeçalho `Authorization` não é registrado; a validação não registra nada.

## Consequências

- A SOFIA descobre o card com a chave; sem ela, ninguém lê o card nem chama o A2A.
- A chave é compartilhada pela turma: autentica a SOFIA, mas não distingue um chamador de outro.
  Quem tiver a chave pode chamar o agente; por isso continuam valendo os dados só sintéticos e o
  limite de gasto da OpenAI (D-09).
- O projeto Vercel precisa da variável `SOFIA_A2A_TOKEN` **antes** de este código chegar à `main`;
  senão o deploy seguinte não inicia e o card sai do ar.
- Navegadores e ferramentas que liam o card público (ADR-0012) passam a receber 401.
- Não há limite de tentativas específico para a chave; os limites de taxa do agente valem só nas
  rotas de análise.
- O D-05 continua aberto para o canal direto, o usuário e o aprovador; a DP-10 continua aberta para
  o modelo de credencial por chamador.

## Alternativas consideradas

- **Card público e só o JSON-RPC protegido**: compatível com o A2A, mas a política da turma exige a
  chave também na descoberta.
- **Autenticação do ASP.NET Core com política de autorização por endpoint**: exigiria marcar cada
  endpoint e não cobriria caminhos `/a2a` ainda não mapeados; o middleware por caminho cobre os dois.
- **Exigir a chave também na API REST e no Swagger**: recusado pela autora; a homologação do canal
  direto continua aberta (D-09).

## Referências

- `SofiaInfis.Api/Services/A2A/AgentDiscoveryService.cs` e
  `SofiaInfis.Api/Services/A2A/Security/AgentCredentialProvider.cs`, em
  <https://github.com/sergiocotiazure20265-ops/SofiaInfis>, inspecionados em 2026-11-10
- [RFC 6750 — Bearer Token Usage](https://www.rfc-editor.org/rfc/rfc6750)
- `specs/001-agente-auditoria-nfe/contracts/sofia-a2a.md` — §1, §2 e §8
- `specs/001-agente-auditoria-nfe/plan.md` — Complexity Tracking, D-05
