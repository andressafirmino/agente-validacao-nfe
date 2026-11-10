# Architecture Decision Records (ADRs)

Decisões de arquitetura do AgenteAuditoriaNFe, conforme a DP-20 da constitution (seção 9.1).

- Um arquivo por decisão, numerado em sequência: `NNNN-titulo.md`.
- Situação: **Proposto**, **Aprovado** ou **Substituído** (com link para o ADR que substitui).
- Aprovação registrada no próprio ADR como "Aprovado por \<nome\>, \<data\>". Aprovadora atual:
  Andressa Firmino, enquanto for a única mantenedora.
- Um ADR aprovado não é reescrito: mudanças de decisão geram um novo ADR que substitui o anterior.

| ADR | Título | Situação |
| --- | ------ | -------- |
| [0001](./0001-estrutura-solucao-camadas.md) | Estrutura da solução e camadas | Aprovado |
| [0002](./0002-mongodb-memoria-contexto-usuario.md) | MongoDB para memória, sessões e contexto do usuário | Aprovado |
| [0003](./0003-versao-1-sem-autenticacao.md) | Versão 1 sem autenticação e modelo futuro de credencial | Aprovado |
| [0004](./0004-minimizacao-dados-llm.md) | Minimização de dados enviados ao LLM | Aprovado |
| [0005](./0005-retencao.md) | Retenção de sessões, contexto do usuário, consumo e checkpoints | Aprovado |
| [0006](./0006-topologia-postgresql.md) | Topologia do PostgreSQL | Aprovado |
| [0007](./0007-protocolo-a2a.md) | Protocolo A2A, Agent Card e contrato provisório | Aprovado |
| [0008](./0008-ci-protecao-main.md) | CI, branches `dev` e `main` e proteção | Aprovado |
| [0009](./0009-observabilidade-opentelemetry.md) | Observabilidade com OpenTelemetry | Aprovado |
| [0010](./0010-schemas-xsd-versionados.md) | Schemas XSD oficiais versionados e seleção pela data de referência | Aprovado |
| [0011](./0011-contrato-sofia-v1-a2a-v03.md) | Contrato SOFIA v1.0 sobre A2A v0.3 (substitui o 0007 quando aprovado) | Proposto |
| [0012](./0012-publicacao-agent-card-vercel.md) | Publicação somente do Agent Card no Vercel (D-08) | Aprovado |
| [0013](./0013-homologacao-publica-vercel-atlas-neon.md) | Homologação pública do agente completo no Vercel, com MongoDB Atlas e Neon (D-09) | Aprovado |
| [0014](./0014-credencial-servico-sofia-a2a.md) | Credencial de serviço da SOFIA no Agent Card e no canal A2A | Aprovado |

## Modelo

```markdown
# ADR-NNNN — Título

- **Situação**: Proposto | Aprovado | Substituído por ADR-XXXX
- **Data**: AAAA-MM-DD
- **Aprovação**: Aprovado por <nome>, <data>
- **Relacionado a**: DPs, princípios, desvios e features

## Contexto
## Decisão
## Consequências
## Alternativas consideradas
## Referências
```
