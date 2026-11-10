# ADR-0012 — Publicação somente do Agent Card no Vercel

- **Situação**: Aprovado
- **Data**: 2026-11-05
- **Aprovação**: Aprovado por Andressa Firmino, 2026-11-05
- **Atualização (2026-11-10)**: pelo ADR-0013, o projeto Vercel deste ADR passa a rodar o agente
  completo no modo `Homologacao`, na mesma URL do card. O modo `SomenteAgentCard` continua como
  padrão da imagem e como volta rápida: sem a variável `Hospedagem__Modo`, o projeto publica só o
  card.
- **Atualização (2026-11-10, ADR-0014)**: o card deixa de ser público; o `GET` exige a credencial
  de serviço da SOFIA (`Authorization: Bearer`, variável `SOFIA_A2A_TOKEN`) e o card declara o
  esquema em `securitySchemes`. A variável é o único segredo do projeto no modo só card.
- **Relacionado a**: DP-25 (não resolvida para o agente completo), DP-27, DP-10; desvios D-05 e
  D-08 (novo); constitution 3.5; P18; FR-044, FR-044a, FR-045, FR-052b, FR-052f; ADR-0003, ADR-0011,
  ADR-0013, ADR-0014

## Contexto

A SOFIA descobre cada agente pelo Agent Card em `/.well-known/agent-card.json`. Antes de o canal A2A
existir (Fase 8), a autora quer uma URL pública que devolva o card do agente, para cadastrá-lo e
conferir a descoberta, como outros agentes da turma já fazem.

O que impede hoje:

- o D-05 limita a execução ao ambiente local, e a guarda de inicialização recusa qualquer ambiente
  diferente de `Development`;
- o plano proíbe implantação enquanto vigorar o D-05, e a seção 3.5 da constitution exige que a
  plataforma seja definida em ADR antes da primeira implantação (DP-25);
- a nota da DP-25 só admite o Vercel para um eventual frontend, porque ele não tinha runtime .NET.

Verificado na documentação do Vercel em 2026-11-05:

- o Container runtime executa imagens OCI construídas de um `Dockerfile` (ou `Dockerfile.vercel`,
  que prevalece sobre a detecção de framework) como Vercel Functions; está em beta em todos os
  planos e é cobrado por CPU ativa;
- o tráfego vai para a porta 80, ou para a porta definida na variável `PORT` do projeto;
- instâncias ociosas são encerradas após 5 minutos em produção e 30 segundos em preview; memória e
  arquivos locais não são armazenamento durável;
- Secure Compute e IPs estáticos não funcionam com contêineres próprios.

O agente completo depende de processo de longa duração para o A2A, de checkpoints em arquivo local
e de uma única instância. O Vercel não atende a esses pontos; o card, sim.

## Decisão

Publicar no Vercel, pelo Container runtime, **somente o Agent Card**, com o próprio `Api.Agente` em
um modo de hospedagem restrito:

- `Hospedagem:Modo` com os valores `Completo` (padrão) e `SomenteAgentCard`;
- no modo `SomenteAgentCard`, o host expõe só `GET /.well-known/agent-card.json` e
  `GET /health/live`; não registra MongoDB, PostgreSQL, provedor de IA, Swagger, OpenAPI, rotas
  `/api/v1` nem o endpoint A2A; não recebe XML nem dado de usuário;
- a guarda do D-05 passa a aceitar ambiente diferente de `Development` somente nesse modo; em
  qualquer outro, o agente não inicia;
- o card é o do contrato SOFIA v1.0 (formato v0.3, contrato §2), com `url` em
  `{A2A:UrlPublica}/a2a/auditoria-nfe`; no modo restrito, a extensão do contrato acrescenta o
  parâmetro `implantacao` dizendo que só o card está publicado e que `message/send` não está
  disponível naquela URL;
- imagem própria em `Dockerfile.vercel` na raiz: mesmo build multiestágio do `Dockerfile`, usuário
  não root, `ASPNETCORE_ENVIRONMENT=Production` e `Hospedagem__Modo=SomenteAgentCard` fixados na
  imagem, e porta lida da variável `PORT` (8080 por padrão);
- variáveis do projeto Vercel: `PORT=8080` e `A2A__UrlPublica` com a URL do projeto. Nenhuma
  connection string, chave da OpenAI ou outro segredo é cadastrado;
- `.vercelignore` impede o envio de `.env`, `data/`, artefatos de build e arquivos fora do build;
- publicação primeiro em preview, com verificação das rotas, e só depois em produção.

Esta decisão não resolve a DP-25: a plataforma do agente completo continua pendente e exige novo
ADR, junto com o fim do D-05.

## Consequências

- A SOFIA e qualquer pessoa na internet podem ler o card: nome, descrição, limites, `idAgente` e a
  declaração de ausência de autenticação. O card não tem segredos nem dados de usuário (FR-045).
- O card anuncia um endpoint A2A que ainda não responde naquela URL; uma chamada da SOFIA recebe
  404. O parâmetro `implantacao` do card registra essa limitação.
- O modo restrito reutiliza o mesmo projeto e a mesma fábrica do card da Fase 8, e a imagem do
  Vercel serve de base para a avaliação da DP-25.
- O Container runtime está em beta: mudanças de comportamento ou de preço podem exigir revisão.
- A antecipação da fábrica do card (T148) usa DTOs próprios no formato v0.3; a Fase 8 decide se ela
  passa a usar os tipos do SDK A2A, mantendo o mesmo JSON.

## Alternativas consideradas

- **JSON estático no Vercel**: menor superfície, mas não exercita o contêiner .NET, duplica o card
  fora do código e seria descartado depois.
- **Esperar a Fase 8, a DP-25 e o fim do D-05**: sem desvio, mas adia o cadastro e a descoberta
  pela SOFIA até o agente completo estar implantado.
- **Agente completo no Vercel**: rejeitada; o Vercel não oferece processo de longa duração,
  armazenamento local durável nem instância única, e o D-05 proíbe expor as rotas sem autenticação.

## Referências

- [Vercel: Does Vercel support Docker deployments?](https://vercel.com/kb/guide/does-vercel-support-docker-deployments)
- [Vercel changelog: Deploy a container from a plain Dockerfile (2026-10-27)](https://vercel.com/changelog/deploy-a-container-from-a-plain-dockerfile)
- Contrato SOFIA v1.0: [`contracts/sofia-a2a.md`](../../specs/001-agente-auditoria-nfe/contracts/sofia-a2a.md) §1 e §2
- Plano: [`plan.md`](../../specs/001-agente-auditoria-nfe/plan.md), Complexity Tracking (D-05 e D-08)
