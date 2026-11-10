# ADR-0013 — Homologação pública do agente completo no Vercel, com MongoDB Atlas e Neon

- **Situação**: Aprovado
- **Data**: 2026-11-10
- **Aprovação**: Aprovado por Andressa Firmino, 2026-11-10
- **Revisão (2026-11-10, aprovada pela autora)**: a homologação usa o projeto Vercel do Agent Card
  (ADR-0012), e não um projeto separado, para que a SOFIA continue com a URL já cadastrada.
- **Relacionado a**: desvio D-09 (novo); D-05, D-08; DP-10, DP-25; constitution 3.5, 5.1, 5.2 e P18;
  FR-004, FR-052b, FR-052f, FR-052g; ADR-0003, ADR-0005, ADR-0006, ADR-0012

## Contexto

A autora precisa testar o agente completo (canal direto e, depois da Fase 8, o canal A2A) fora da
máquina local, num ambiente de homologação. O ADR-0003 e o D-05 proíbem a execução fora do ambiente
local sem autenticação, e o ADR-0012 rejeitou o agente completo no Vercel. A autora decidiu adiar a
autenticação também na homologação e aceitar o acesso público.

Verificado na documentação oficial em 2026-11-10 (research R-36):

- **Vercel (Container runtime, beta)**: a imagem roda como Vercel Function; duração máxima de 300 s
  no Hobby; 2 GB de memória e 1 vCPU; corpo de requisição e de resposta limitado a 4,5 MB (413
  `FUNCTION_PAYLOAD_TOO_LARGE`); instâncias ociosas encerradas após 5 minutos em produção; memória e
  disco não duráveis; sem IP de saída fixo nem Secure Compute para contêineres próprios; região
  padrão `iad1`; Hobby com 4 horas de CPU ativa por mês e uso só pessoal e não comercial.
- **MongoDB Atlas Free**: 0,5 GB, 100 operações por segundo, 500 conexões, 10 GB de tráfego por
  semana em cada sentido, sem private endpoint nem peering; o usuário de banco é criado no painel.
- **Neon Free**: 1 GB por projeto, 100 CU-hora por mês, suspensão após 5 minutos ociosos, 5 GB de
  tráfego de saída, extensões como `vector` disponíveis; sem regras de IP no plano Free.

O que o agente completo exige e como fica no Vercel:

- análise de até 120 s: cabe nos 300 s;
- lote de até 5 MB (FR-004): não cabe no corpo de 4,5 MB;
- estado em MongoDB e PostgreSQL externos: compatível;
- limites de taxa e concorrência em memória: valem por instância;
- checkpoints de aprovação em arquivo local (Fase 7, ainda não implementada): não são duráveis;
- Tasks A2A em memória (Fase 8): o `message/send` devolve a Task final na mesma requisição, então
  basta uma instância por requisição.

## Decisão

Publicar o agente completo num **ambiente de homologação público e sem autenticação**, pelo desvio
D-09:

- **Modo de hospedagem `Homologacao`**: novo valor de `Hospedagem:Modo`. Registra o agente completo
  como no modo `Completo`, com Swagger e OpenAPI, e é o único modo, além do `SomenteAgentCard`, que a
  guarda aceita fora de `Development` com a autenticação desabilitada. Nesse modo, o agente recusa
  iniciar com o provedor de IA simulado, com migrações na inicialização ou com limites de corpo
  acima do que o Vercel aceita, e o Agent Card declara, no parâmetro `implantacao`, que é uma
  homologação pública, sem autenticação e só para dados sintéticos.
- **Vercel Hobby, o mesmo projeto do Agent Card**: `agente-auditoria-nfe` (ADR-0012), em
  `https://agente-auditoria-nfe.vercel.app`, passa do modo `SomenteAgentCard` para o `Homologacao`.
  Uma só URL serve o card, a API e, depois da Fase 8, o endpoint A2A, e a SOFIA continua com o card
  já cadastrado. Mesma imagem `Dockerfile.vercel`, com o modo vindo da variável `Hospedagem__Modo`
  do projeto; sem a variável, a imagem volta ao `SomenteAgentCard`. O projeto continua ligado ao Git
  e publica a cada merge na `main` (`vercel.json`); por isso, o código do modo `Homologacao` chega
  à `main` antes de as variáveis serem cadastradas, senão o deploy seguinte da `main` não inicia.
- **Limites na homologação**: `Limites:TamanhoTotalBytes` = 4.000.000 e `Limites:CorpoHttpBytes` =
  4.500.000, abaixo do limite do Vercel, para que a recusa seja o ERR-002 do agente, e não o 413 da
  plataforma. Os demais limites seguem a spec.
- **MongoDB Atlas Free** na AWS `us-east-1`, perto do `iad1`: usuário de banco só com `readWrite`
  no banco `auditoria_agente`, senha forte e TLS; coleções, validadores `$jsonSchema` e índices
  criados pelo script `mongosh` `infra/mongodb/init/02-colecoes.js`, comum ao contêiner local e sem
  criação de usuário. **Acesso de rede `0.0.0.0/0`**, aprovado pela autora porque o Vercel não tem IP de saída
  fixo, com estes controles compensatórios: usuário de permissão mínima, senha forte gerada e só no
  projeto Vercel, TLS obrigatório, dados só sintéticos e cluster exclusivo da homologação.
- **Neon Free** na AWS `us-east-1`: banco e usuário exclusivos do Api.Agente, só com o schema
  `consumo` (ADR-0006), conexão com `SSL Mode=Require`; migrações aplicadas fora da inicialização,
  por script SQL idempotente gerado pelo EF Core. A extensão `vector` não é usada pelo Api.Agente e
  fica para a Api.RAG (feature 002).
- **Provedor de IA**: OpenAI com chave exclusiva da homologação, num projeto da OpenAI com limite de
  gasto mensal.
- **Diretório de aprovações** em `/tmp/aprovacoes`, gravável e não durável.
- **Segredos** só nas variáveis de Production do projeto Vercel; nada no Git, na imagem ou nos
  logs.
- **Publicação**: imagem conferida localmente; código na `main` pela PR `dev` → `main`; variáveis
  cadastradas; redeploy da produção; verificações registradas no research R-36.

Esta decisão não resolve a DP-25 para produção nem encerra o D-05.

## Consequências

- Qualquer pessoa com a URL pode enviar XMLs, ler sessões, execuções e contexto de outro usuário
  se souber os identificadores, e consumir a chave da OpenAI até o limite de gasto.
- Os limites de taxa e de análises simultâneas valem por instância do Vercel, então não limitam o
  total; o limite de gasto da OpenAI e as cotas do Hobby são o teto de custo. Ao estourar as cotas
  do Hobby, o Vercel pausa o projeto por até 30 dias.
- Lotes acima de 4.000.000 bytes são recusados na homologação (ERR-002), embora a spec permita
  5 MB no ambiente local.
- O banco do Atlas aceita conexões de qualquer IP; a proteção depende da credencial e do TLS.
- Os dados ficam nos Estados Unidos; por isso, só dados sintéticos.
- Instância encerrada após 5 minutos ociosa: a primeira requisição seguinte paga a inicialização
  (carga dos schemas e catálogos).
- Logs de execução retidos por 1 hora no Hobby.
- Quando a Fase 7 existir, as aprovações na homologação se perdem ao encerrar a instância; o
  armazenamento durável continua pré-requisito de produção.
- A SOFIA só poderá chamar a homologação por A2A depois da Fase 8; até lá, o card na URL já
  cadastrada aponta para um endpoint A2A que responde 404.
- O Agent Card público deixa de ser só um card: cada merge na `main` publica o agente completo e
  sem autenticação. Para voltar ao modo restrito, basta remover `Hospedagem__Modo` e publicar de
  novo.

## Alternativas consideradas

- **Preview protegido pelo login do Vercel**: menor risco, recusado pela autora porque a
  homologação precisa ser acessível sem a conta Vercel.
- **Render Free**: aceita 5 MB, mas suspende após 15 minutos sem uso e tem 512 MB de memória;
  recusado pela autora.
- **Esperar a autenticação (DP-10)**: sem desvio, mas adia os testes de homologação.
- **Projeto Vercel separado** (`agente-auditoria-nfe-homolog`, sem Git, publicado pelo CLI):
  escolhido na primeira versão deste ADR e trocado na revisão de 2026-11-10, porque exigiria
  cadastrar outra URL na SOFIA.

## Referências

- [Vercel: Does Vercel support Docker deployments?](https://vercel.com/kb/guide/does-vercel-support-docker-deployments)
- [Vercel Functions Limits](https://vercel.com/docs/functions/limitations)
- [Vercel Hobby Plan](https://vercel.com/docs/plans/hobby)
- [MongoDB Atlas Free Cluster Limits](https://www.mongodb.com/docs/atlas/reference/free-shared-limitations/)
- [Neon Pricing](https://neon.com/pricing)
- `specs/001-agente-auditoria-nfe/research.md` — R-36
- `specs/001-agente-auditoria-nfe/plan.md` — Complexity Tracking, D-09
