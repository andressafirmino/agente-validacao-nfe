# Modelo de dados: Agente de Auditoria de NF-e (AuditoriaNFe)

**Feature**: `001-agente-auditoria-nfe` | **Data**: 2026-10-29, revisado em 2026-11-04 e
2026-11-05 | **Plano**: [plan.md](./plan.md)

Este documento detalha as entidades da seção "Key Entities" da [spec](./spec.md): campos,
relacionamentos, validações, estados e onde cada uma é persistida. Os nomes são os do Domínio
(`AuditoriaNFe.Domain`); os tipos são conceituais, sem código de implementação.

**Revisão de 2026-11-04**: identificação do usuário e do chamador por metadados (desvio D-05),
seleção do pacote de schema pela data de referência, pacote de schema e vigência, regra de conteúdo
com versão e vigência (catálogo aprovado), contexto do usuário e registro de consumo ampliado.
Depois da análise de consistência das tarefas: chamadas de ferramentas, trechos consultados e regras
aplicadas registrados na execução, para a rastreabilidade do NFR-008.

**Revisão de 2026-11-05 (contrato SOFIA v1.0, ADR-0011)**: identificação A2A pelo nome do usuário
(§0), `usuarioNome` na sessão e na execução, IDs da SOFIA na execução, `idDocumento` no arquivo
recebido e DTOs do contrato SOFIA com o mapeamento para o parecer (§18). A sessão deixa de guardar
o `contextId` A2A, e a sessão do hosting A2A do MAF deixa de existir.

## Convenções

- **Identificadores**: gerados pelo sistema com GUID versão 7 (P09); o LLM nunca gera nem altera ids.
- **Datas e horas**: UTC, ISO 8601, preenchidas pelo sistema. Exceção: as datas do documento
  (`dhEmi`, `dhRecbto`) são guardadas como no XML, com o deslocamento de fuso, e a data de referência
  é uma data de calendário nesse deslocamento.
- **Valores monetários**: `decimal`, copiados do XML sem arredondamento; cálculos no Domínio.
- **Classificação de dados (constitution 5.1)**: cada campo é marcado como **Público**,
  **Interno**, **Fiscal** (dado fiscal do documento) ou **Pessoal** (dado pessoal, LGPD). Campos
  Pessoais nunca aparecem em logs e são mascarados no envio ao LLM (ADR DP-14).
- **Persistência**: **M** = MongoDB (`auditoria_agente`), **P** = PostgreSQL (`auditoria_agente`,
  schema `consumo`), **J** = arquivo JSON local de demonstração, **R** = arquivo versionado no
  repositório e carregado na inicialização, **E** = efêmero (só durante a execução).

## Visão geral dos relacionamentos

```text
IdentificacaoSolicitante ── chaveIsolamento ──┬── Sessao 1 ──< Execucao
                                              └── ContextoUsuario 0..1

Sessao 1 ──< Execucao 1 ──< ArquivoRecebido >── 0..1 DocumentoNFe
   │             │                                     │
   │             ├──< Achado >──< Evidencia            │
   │             ├── Parecer ──< RecomendacaoDemonstrativa 1 ── 0..1 CheckpointAprovacao
   │             ├──< RegistroCalculo                  │
   │             └──< ChamadaProvedor (P)              │
   └──< DocumentoNFe (base da sessão, dados extraídos) ┘

PacoteSchema (R) 1 ── 1 VigenciaPacote (R) ──< ArquivoRecebido.validacaoLayout (pacote aplicado)
RegraConteudo (R, por versão) ──< Achado (inconsistência de negócio)
```

## 0. IdentificacaoSolicitante (E, derivada de cada solicitação)

Identidade informada pelo chamador, sem verificação na versão 1 (desvio D-05; FR-052a, FR-052c,
FR-052d).

| Campo | Tipo | Classe | Regras |
| ----- | ---- | ------ | ------ |
| `usuarioId` | string | Pessoal | REST: cabeçalho `X-Usuario-Id`, até 128 caracteres de `[A-Za-z0-9._:@-]`, ausente → `"anonimo"`. A2A: `"nome-sha256:"` + SHA-256 hexadecimal minúsculo do `usuario.nome` normalizado (abaixo) |
| `chamadorId` | string | Interno | REST: cabeçalho `X-Chamador-Id`, mesmo formato, ausente → `"nao-informado"`. A2A: configuração `A2A:ChamadorPadrao` (inicial `"sofia"`) |
| `usuarioNome` | string? | Pessoal | Só no A2A: `usuario.nome` sem espaços nas pontas, até 200 caracteres. Persistido na sessão e na execução; nunca em logs, no envio ao LLM nem no contexto do usuário |
| `usuarioIdentificado` | bool | Interno | `false` quando `usuarioId = "anonimo"`; sempre `true` no A2A (o nome é obrigatório) |
| `chaveIsolamento` | string (SHA-256 hex) | Interno | Hash de `chamadorId` + separador + `usuarioId`; usada em sessões, contexto do usuário, limitação de taxa e logs |

**Normalização do nome (A2A)**: Unicode NFC, espaços nas pontas removidos, sequências de espaços
internos reduzidas a um e minúsculas invariantes. O hash é calculado sobre os bytes UTF-8 do nome
normalizado. O `usuarioNome` guardado é o original sem espaços nas pontas, sem as demais
normalizações. Nomes que normalizam igual compartilham o contexto do usuário (risco do D-05).

**Validações**: formato inválido → resposta 400 com ERR-007 (REST), sem execução; nome vazio ou
acima de 200 caracteres → resultado A2A com status `erro` e ERR-007. Com o fim do D-05, o
`chamadorId` passa a vir da credencial de serviço autenticada, e não da mensagem nem da
configuração.

## 1. Sessao (M)

Contexto conversacional de um usuário (FR-036 a FR-042).

| Campo | Tipo | Classe | Regras |
| ----- | ---- | ------ | ------ |
| `id` | GUID | Interno | Gerado na primeira solicitação sem sessão |
| `chaveIsolamento` | string | Interno | Da `IdentificacaoSolicitante`; toda leitura confere a chave (FR-039) |
| `usuarioId` / `chamadorId` | string | Pessoal / Interno | Registrados como informados ou derivados (FR-052a, FR-052d) |
| `usuarioNome` | string? | Pessoal | Só em sessões criadas pelo A2A (FR-052d); nunca em log |
| `estado` | enum | Interno | `Ativa`, `Expirada`, `Encerrada` |
| `criadaEm` / `ultimaAtividadeEm` | data-hora | Interno | Atualizadas pelo sistema a cada solicitação aceita |
| `expiraApos` | duração | Interno | Configuração `Sessao:ExpiracaoInatividade` (inicial 24 h) |
| `encerradaEm` | data-hora? | Interno | Preenchida no encerramento (FR-041) |
| `documentos` | lista de referência a `DocumentoNFe` | Fiscal | Base analisada da sessão |
| `hashesPorChave` | mapa chave de acesso → SHA-256 | Fiscal | Detecção de duplicidade e divergência (FR-009, FR-037a) |
| `execucoes` | lista de ids | Interno | Ordem cronológica |
| `historicoAgente` | documento serializado | Interno | `AgentSession` do agente interno (histórico e estado dos providers), isolada pela chave |

Sessões criadas pelo A2A recebem uma única execução (o contrato SOFIA v1.0 não tem continuidade) e
seguem a mesma expiração e retenção das demais.

**Validações**: acesso só com a mesma `chaveIsolamento` (senão ERR-005, sem revelar existência);
estado `Expirada` quando `agora - ultimaAtividadeEm > expiraApos` (ERR-006); `Encerrada` recusa novas
solicitações (ERR-006, mensagem de sessão encerrada).

**Estados**:

```text
[criação] → Ativa ──(inatividade > expiraApos)──→ Expirada ──(retenção DP-13)──→ [removida]
              └────(encerramento pelo usuário)──→ Encerrada ──(retenção DP-13)──→ [removida]
```

Na transição para `Expirada` ou `Encerrada`, as recomendações `PendenteAprovacao` da sessão passam a
`Indisponivel` (FR-035i).

## 2. Execucao (M)

Processamento de uma solicitação (FR-054, NFR-008).

| Campo | Tipo | Classe | Regras |
| ----- | ---- | ------ | ------ |
| `id` | GUID | Interno | Gerado na recepção |
| `correlacaoId` | string? | Interno | REST: recebido do chamador e devolvido sem alteração; A2A: o `idSolicitacao` da SOFIA (FR-043c); máx. 128 caracteres |
| `idExecucaoSofia` / `idSolicitacaoSofia` | GUID? | Interno | Só no A2A: `idExecucao` e `idSolicitacao` recebidos, devolvidos sem alteração no resultado (FR-043c, FR-043e). Repetição segue o FR-043h (abaixo) |
| `chaveIdempotenciaSofia` | string? | Interno | Só no A2A: `chamadorId` e `idSolicitacao` unidos pelo separador da `IdentificacaoSolicitante` (§0). Gravada na criação da execução e removida quando ela passa a `Falhou`; índice único parcial (só documentos com o campo) garante uma execução ativa ou terminada com sucesso por chave (FR-043h) |
| `sessaoId` | GUID | Interno | Sessão criada ou continuada |
| `usuarioId` / `chamadorId` / `usuarioIdentificado` | string / string / bool | Pessoal / Interno / Interno | Da `IdentificacaoSolicitante` |
| `usuarioNome` | string? | Pessoal | Só no A2A (FR-052d); nunca em log nem no envio ao LLM |
| `canal` | enum | Interno | `Direto`, `A2A` |
| `tipo` | enum | Interno | `AnaliseLote`, `Pergunta`, `AnaliseComPergunta`, `ConsultaRecomendacoes` |
| `pergunta` | string? | Pessoal* | Guardada no histórico da sessão; nunca em log. *Pode conter dados pessoais |
| `estado` | enum | Interno | `Recebida`, `EmProcessamento`, `Concluida`, `Falhou`, `Rejeitada` |
| `etapas` | lista de `EtapaExecucao` (9) | Interno | Uma por etapa da constitution 3.4 |
| `erro` | `ErroExecucao`? | Interno | Código ERR-xxx, mensagem pt-BR, arquivo afetado |
| `iniciadaEm` / `concluidaEm` | data-hora | Interno | |
| `versoes` | objeto | Interno | Envelope, contrato do LLM, manifesto de schemas, catálogo de regras, catálogo de extração, instruções do agente, tabela de preços |
| `integracaoMcpHabilitada` | bool | Interno | FR-047d |
| `modelo` | string? | Interno | Modelo OpenAI usado, quando houve chamada |
| `consumoResumo` | objeto | Interno | Somatório das chamadas (tokens, custo, origem informada/estimada, `registroCompleto`); NULL quando desconhecido |
| `contextoUsuarioAtualizado` | bool | Interno | `true` quando a etapa 9 gravou o contexto do usuário (FR-057) |
| `chamadasFerramentas` | lista | Interno | Uma por chamada de ferramenta da IA: `nome`, `iniciadaEm`, `duracaoMs`, `status` (`Sucesso`, `Falha`, `Timeout`, `NaoAutorizada`), `erro?` e `calculoId?`; sem argumentos de texto livre nem valores de documentos (P16, NFR-008) |
| `trechosConsultados` | lista | Interno | Trechos de manual recuperados na etapa 5: `chunkId`, `documento`, `versao`; vazia com a integração MCP desabilitada (NFR-008) |

**`EtapaExecucao`**: `numero` (1–9), `nome`, `estado` (`Executada`, `Falhou`, `NaoAplicavel`,
`NaoExecutada`), `justificativa` (obrigatória para `NaoAplicavel` e `NaoExecutada`), `inicioEm`,
`fimEm`. Nenhuma etapa é omitida (constitution 3.4).

**Estados da execução**:

```text
Recebida ──(limites e identificação ok)──→ EmProcessamento ──→ Concluida
   │                                           └──(timeout ERR-016, cancelamento ERR-027 ou falha não recuperável)──→ Falhou
   └──(ERR-001 a ERR-003, ERR-005 a ERR-007)──→ Rejeitada
```

`Concluida` inclui execuções com resultado "Inconclusiva" (dependência indisponível com resultados
determinísticos entregues). `Falhou` registra as etapas concluídas e as não executadas.

**Repetição no A2A (FR-043h)**: antes de criar a execução, o adaptador procura a execução com a
mesma `chaveIdempotenciaSofia`:

| Execução encontrada | Ação |
| ------------------- | ---- |
| Nenhuma (ou só execuções `Falhou`) | Cria a execução gravando a chave; se a gravação violar o índice único (corrida), segue a linha da execução concorrente |
| `Recebida` ou `EmProcessamento` | Resultado `erro` com ERR-026, sem criar execução |
| `Concluida` ou `Rejeitada` | Resultado montado a partir da execução existente (§18), sem nova análise, chamada ao provedor de IA nem atualização do contexto do usuário |
| Qualquer estado, com `idExecucaoSofia` ou `usuarioId` diferentes | Resultado `erro` com ERR-007, sem criar execução |

## 3. ArquivoRecebido (M, sem conteúdo)

| Campo | Tipo | Classe | Regras |
| ----- | ---- | ------ | ------ |
| `nome` | string | Interno | Nome informado; saneado, sem caminho |
| `idDocumento` | string? | Interno | Só no A2A: `documentos[].idDocumento` da SOFIA, até 128 caracteres, único na execução; usado em `achados[].idDocumentos` e nas observações (FR-043b, FR-043f) |
| `tamanhoBytes` | long | Interno | Somado para o limite total (FR-004); no A2A, bytes UTF-8 do `texto` |
| `hashSha256` | string | Fiscal | Calculado sobre os bytes recebidos |
| `bemFormado` | bool | Interno | Pelo conteúdo, não pela extensão (FR-006) |
| `identificacao` | `IdentificacaoDocumento`? | Fiscal | Ver abaixo (FR-007, FR-007a) |
| `validacaoLayout` | `ValidacaoLayout` | Interno | Ver abaixo (FR-010 a FR-011) |
| `validacaoConteudo` | `EstadoValidacao` | Interno | `NaoRealizada — layout inválido` quando o layout falhou (FR-015); `NaoRealizada` com motivo em ERR-023 |
| `regrasAplicadas` | lista | Interno | Uma por regra do catálogo avaliada no documento: `regraId`, `versaoRegra`, `resultado` (`Conforme`, `Divergente`, `NaoAplicavel` com condição ou grupo ausente) (NFR-008, FR-013a) |
| `documentoId` | GUID? | Interno | Documento gerado ou já existente (duplicado) |
| `duplicadoDe` | GUID? | Interno | Preenchido quando for duplicata (FR-009) |

**`IdentificacaoDocumento`**:

| Campo | Regras |
| ----- | ------ |
| `elementoRaiz` | `NFe` ou `nfeProc`; outro valor → não suportado |
| `namespace` | DEVE ser `http://www.portalfiscal.inf.br/nfe` |
| `versaoLayout` | Atributo `versao`; só `4.00` é suportada (ERR-010) |
| `modeloIde` / `modeloChave` | `ide/mod` e posições 21–22 da chave; os dois DEVEM ser `55` (FR-007a); NFC-e (`65`) → ERR-009 |
| `chaveAcesso` | 44 caracteres, do atributo `Id` de `infNFe` sem o prefixo `NFe` |
| `possuiProtocolo` | `nfeProc` com `protNFe/infProt` |
| `dhEmi` | `ide/dhEmi`, como no XML |
| `dhRecbto` | `protNFe/infProt/dhRecbto`, quando houver protocolo |
| `suportado` / `motivoNaoSuportado` | Resultado da identificação; não suportado não passa pelo schema (FR-008) |

**`ValidacaoLayout`**:

| Campo | Regras |
| ----- | ------ |
| `estado` | `Realizada` (com ou sem achados) ou `NaoRealizada` com motivo |
| `dataReferencia` | Data de calendário de `dhRecbto` (com protocolo) ou de `dhEmi` (FR-010a) |
| `origemDataReferencia` | `dhRecbto` ou `dhEmi` (FR-010d) |
| `pacoteVigente` | Pacote cuja vigência cobre a data (FR-010b) |
| `pacoteAplicado` | Pacote cujo resultado foi adotado; igual ao vigente, salvo convivência (FR-010f) |
| `pacotesTentados` | Lista de pacotes usados, na ordem |
| `emConvivencia` | `true` quando a data estava na convivência do pacote anterior |
| `schemaUsado` | `nfe_v4.00.xsd` ou `procNFe_v4.00.xsd`, conforme o elemento raiz |
| `motivoNaoRealizada` | Por exemplo, "schema não disponível para a data" (ERR-023) |

O conteúdo do arquivo existe apenas em memória durante a execução (E) e é descartado ao fim
(FR-037a, constitution 5.1).

## 4. DocumentoNFe (M)

Documento identificado e seus dados extraídos, usados nas validações e nas respostas.

| Campo | Tipo | Classe | Regras |
| ----- | ---- | ------ | ------ |
| `id` | GUID | Interno | |
| `chaveAcesso` | string (44) | Fiscal | Única por sessão; mesma chave com hash diferente gera ERR-012 |
| `modelo` / `versaoLayout` | string | Fiscal | `55` e `4.00` na v1 |
| `possuiProtocolo` | bool | Fiscal | `nfeProc` com protocolo anexado |
| `dhEmi` / `dhRecbto` | data-hora com deslocamento | Fiscal | Como no XML |
| `dataReferencia` / `origemDataReferencia` | data / enum | Fiscal | Iguais às da validação de layout |
| `pacoteSchema` | string | Interno | Pacote aplicado |
| `hashConteudo` | string | Fiscal | SHA-256 dos bytes do arquivo de origem |
| `dadosExtraidos` | objeto | Fiscal/Pessoal | Somente os campos do catálogo de extração versionado; cada valor guarda o caminho do campo no XML |
| `arquivoOrigem` / `execucaoOrigem` | string / GUID | Interno | Rastreabilidade (FR-027) |

**Catálogo de extração** (R, `Catalogos/catalogo-extracao.v1.json`): lista fechada de campos
(caminho no XML, tipo, classe de dado), derivada dos schemas versionados e do Anexo I. Cobre, no
mínimo, os campos usados pelas regras do catálogo de conteúdo (totais de `ICMSTot`, `ISSQNtot`,
`ISTot`, `IBSCBSTot`, campos dos itens somados por elas, `cobr/fat`, `cobr/dup` e `pag`), a
identificação (`ide`), o CNPJ do emitente e do destinatário (para o contexto do usuário) e os campos
necessários às perguntas da avaliação. Campo não catalogado não é extraído; pergunta que dependa dele
segue o FR-037b. A carga falha se uma regra usar campo fora do catálogo.

## 5. PacoteSchema e VigenciaPacote (R, `Schemas/NFe/manifesto.json`)

Conjunto oficial de XSDs do leiaute 4.00 e o período em que esteve em produção (FR-010b, FR-010e).
O manifesto é a fonte única dos dois; ele deriva de
[vigencia-pacotes-schema.md](./vigencia-pacotes-schema.md).

**Manifesto (cabeçalho)**: `versao`, `geradoEm`, `namespace`, `versaoLeiaute` (`4.00`), `modelo`
(`55`), `tabelaVigencia` (caminho do documento aprovado), `aprovacao` (`por`, `em`, `observacao`) e
`fonteProcNFe` (origem do `procNFe` usado nos pacotes PL_010).

**Pacote** (um por item de `pacotes[]`):

| Campo | Tipo | Regras |
| ----- | ---- | ------ |
| `ordem` | int | Ordem cronológica, única |
| `id` | string | Nome da pasta do pacote, por exemplo `PL_010e_v1.02` |
| `notasTecnicas` | string | NTs que o pacote implementa |
| `vigenciaInicio` | data | Início da produção (NT oficial, com página na tabela) |
| `vigenciaFim` | data? | Dia anterior ao início do pacote seguinte; `null` no último (aberto até a data atual) |
| `inicioInferido` | bool | `true` quando a data de início foi inferida (pendências da tabela) |
| `fimConvivencia` | data? | Fim da convivência do pacote após o início do seguinte; `null` em todos os pacotes atuais |
| `schemaNFe` / `schemaNFeProc` | string | `nfe_v4.00.xsd` e `procNFe_v4.00.xsd` |
| `origem` | objeto | Zip oficial: `arquivo`, `titulo`, `url`, `obtidoEm`, `sha256` |
| `arquivos[]` | lista | `nome`, `origem` (zip de onde veio) e `sha256` de cada XSD |

**Validações de carga**: ordens e vigências sem sobreposição nem lacuna; só o último com fim aberto;
cada XSD presente com o SHA-256 do manifesto; nenhum arquivo fora do manifesto; cada pacote compila
(`TNFe` e `TNfeProc` definidos). Qualquer falha impede a inicialização.

**Seleção (FR-010b, FR-010c)**: pacote com `vigenciaInicio ≤ dataReferencia ≤ vigenciaFim` (fim
aberto = data atual do relógio do sistema). Nenhum pacote → ERR-023.

**Convivência (FR-010f)**: se o pacote vigente apontar erro estrutural e `dataReferencia ≤
fimConvivencia` do pacote anterior, valida-se também com o anterior; sem erros no anterior, ele passa
a ser o `pacoteAplicado`; senão, ficam os erros do vigente.

## 6. RegraConteudo (R, `Catalogos/catalogo-regras.v1.json`)

Uma entrada por versão de regra do catálogo aprovado em
[catalogo-regras-totalizadores.md](./catalogo-regras-totalizadores.md).

| Campo | Tipo | Regras |
| ----- | ---- | ------ |
| `id` | string | Id do Anexo I ou da NT, por exemplo `W16-10`; estável entre versões do catálogo |
| `versaoRegra` | int | 1 em diante; a W16-10 tem 4 versões e a W07-10 tem 2 |
| `grupo` | enum | `ICMSTot`, `ISSQNtot`, `ISTot`, `IBSCBSTot`, `Cobranca`, `Pagamento` |
| `descricao` | string | pt-BR, com a fórmula |
| `fonte` | objeto | `documento`, `versao`, `paginas` — obrigatório (FR-013, P02) |
| `vigencia` | objeto | `inicio` e `fim` (`null` = aberta); versões de uma mesma regra sem sobreposição |
| `severidade` | enum | `Alta`, `Media`, `Baixa`, `Informativa` (FR-023) |
| `tolerancia` | decimal? | R$ 0,50 na W16-10; 0,01 nas quantidades monofásicas; ausente = comparação exata |
| `aplicacaoSefaz` | objeto | `tipo` (`Obrigatoria` ou `Facultativa`) e `codigoRejeicao` |
| `condicoes` | lista de string | Condições e exceções, como no catálogo aprovado |
| `implementacao` | string | Id da implementação determinística no Domínio |

**Catálogo (cabeçalho)**: `versaoCatalogo`, `aprovacao` (`por`, `em`), `fontes[]` (documento,
versão e SHA-256 do inventário). A versão do catálogo é registrada em cada execução.

**Validação de carga**: regra sem fonte, sem vigência, com implementação inexistente, com campo fora
do catálogo de extração ou com versões sobrepostas impede a inicialização.

**Aplicação (FR-013a)**: a versão da regra aplicada a um documento é a vigente na `dataReferencia`
dele; documento fora de toda vigência da regra não gera achado para ela.

## 7. Achado (M, dentro da execução)

| Campo | Tipo | Classe | Regras |
| ----- | ---- | ------ | ------ |
| `id` | GUID | Interno | Referenciável por recomendações |
| `categoria` | enum | Interno | `ErroEstrutural`, `InconsistenciaNegocio`, `IndicioIa`, `AusenciaEvidencias`, `FalhaTecnica` (P04, FR-020) |
| `severidade` | enum | Interno | Erros estruturais sempre `Alta`; de conteúdo, a do catálogo (FR-023) |
| `origem` | enum | Interno | `Deterministica`, `IA`, `Sistema` |
| `arquivo` / `chaveAcesso` | string? | Fiscal | |
| `campo` | string? | Fiscal | Caminho do campo no XML |
| `linha` / `posicao` | int? | Interno | Para erros de schema (FR-011) |
| `item` | int? | Fiscal | Número do item, quando aplicável |
| `valorInformado` / `valorEsperado` | string? | Fiscal | Obrigatórios em inconsistência de negócio (FR-014) |
| `descricao` | string | Interno | pt-BR |
| `regraId` / `versaoRegra` | string? / int? | Interno | Obrigatórios em `InconsistenciaNegocio` |
| `pacoteSchema` | string? | Interno | Obrigatório em `ErroEstrutural` de schema |
| `evidencias` | lista de `Evidencia` | — | Obrigatória em `IndicioIa` (FR-021) |

**Validações**: achado `IndicioIa` nunca altera ou remove achado `Deterministica` (FR-021); o
validador semântico rejeita indício sem evidência verificável.

## 8. Evidencia

| Campo | Tipo | Regras |
| ----- | ---- | ------ |
| `tipo` | enum | `CampoXml`, `RegraCatalogo`, `TrechoManual`, `Calculo` |
| `chaveAcesso`, `caminho`, `valor` | string? | Para `CampoXml`: DEVEM existir nos dados extraídos da sessão |
| `regraId`, `versaoRegra`, `versaoCatalogo` | string? | Para `RegraCatalogo` |
| `chunkId`, `documento`, `versao`, `trecho` | string? | Para `TrechoManual`: DEVEM pertencer ao conjunto recuperado na execução (P06) |
| `calculoId` | GUID? | Para `Calculo`: DEVE existir no registro de cálculos da execução |

## 9. RegistroCalculo (M, dentro da execução)

Resultado de uma ferramenta de cálculo determinístico, usado para validar valores citados pelo LLM.

| Campo | Tipo | Regras |
| ----- | ---- | ------ |
| `id` | GUID | Gerado pelo sistema e devolvido à IA na resposta da ferramenta |
| `operacao` | enum | Ex.: soma de um campo catalogado |
| `campo` | string | Campo do catálogo de extração |
| `documentosIncluidos` | lista de chaves | FR-031 |
| `documentosExcluidos` | lista de {chave, motivo} | Erro estrutural, duplicidade etc. |
| `resultado` | decimal | |
| `registradoEm` | data-hora | |

## 10. Parecer (M, dentro da execução; contrato v1)

| Campo | Tipo | Regras |
| ----- | ---- | ------ |
| `resumoQuantitativo` | objeto | `recebidos`, `analisados`, `comAchados`, `naoAnalisados` (FR-017) |
| `resultadoGeral` | enum | `SemInconsistencias`, `ComInconsistencias`, `Inconclusiva` (FR-018) |
| `documentos` | lista | Por arquivo: `arquivo`, `chaveAcesso`, `dataReferencia`, `origemDataReferencia`, `pacoteAplicado`, `pacotesTentados`, `emConvivencia`, estado das validações (FR-010d, FR-010f) |
| `achados` | lista de `Achado` | Agrupáveis por arquivo, categoria e severidade |
| `limitacoes` | lista de string | Inclui "consulta a manuais oficiais não habilitada" (FR-047b) e "usuário não identificado" quando `usuarioIdentificado = false` (FR-052c) |
| `validacoesNaoRealizadas` | lista | Arquivo, validação e motivo |
| `revisaoHumana` | objeto | `necessaria` e `motivos` (FR-024) |
| `recomendacoes` | lista de `RecomendacaoDemonstrativa` (visão por estado) | FR-033 a FR-035c |
| `resumoTextual` | string | pt-BR; produzido pela IA e validado, ou por modelo de texto do sistema quando a IA estiver indisponível, com o conteúdo e o vocabulário do FR-017b ("inconsistências" e "alertas", nunca "achados") |
| `integracaoMcp` | objeto | `habilitada` (FR-047d) |
| `versoes` | objeto | Manifesto de schemas e catálogo de regras usados |

**Regra de cálculo do resultado geral (FR-019)** — calculada pelo sistema, nunca pela IA:

1. Se houver etapa obrigatória `Falhou` ou `NaoExecutada`, validação "não realizada" em qualquer
   arquivo (inclusive ERR-023), ou dependência indisponível: `ComInconsistencias` se houver achados
   determinísticos de erro estrutural ou inconsistência de negócio; caso contrário, `Inconclusiva`.
2. Senão, se houver achado `ErroEstrutural` ou `InconsistenciaNegocio`: `ComInconsistencias`.
3. Senão: `SemInconsistencias`. A etapa 5 em `NaoAplicavel` (MCP desabilitado) não impede este
   resultado, mas gera limitação e revisão humana (FR-047b).

O contexto do usuário não participa deste cálculo (FR-058).

## 11. RecomendacaoDemonstrativa (M, dentro da execução)

| Campo | Tipo | Regras |
| ----- | ---- | ------ |
| `id` | GUID | Gerado pelo sistema |
| `execucaoId` / `sessaoId` | GUID | |
| `rotulo` | constante | "Recomendação demonstrativa" (FR-033) |
| `aviso` | constante | Aviso de caráter ilustrativo (FR-033) |
| `texto` | string | Produzido pela IA e validado |
| `achadosVinculados` | lista de GUID | ≥ 1; DEVEM existir no parecer (FR-034) |
| `valoresCitados` | lista | Cada valor com `calculoId` ou {chave, caminho}; validado (FR-027) |
| `sensivel` | bool | Calculado pelo sistema (FR-035a) |
| `motivosSensibilidade` | lista | `AchadoSeveridadeAlta`, `MencionaValores`, `MencionaTributos` |
| `estado` | enum | `Liberada`, `PendenteAprovacao`, `Aprovada`, `Rejeitada`, `Indisponivel` |

**Classificação (FR-035a)**: sensível quando algum achado vinculado tiver severidade `Alta`, quando
`valoresCitados` não estiver vazio, quando o texto contiver valor monetário (detecção por padrão
numérico) ou quando mencionar tributo de uma lista configurável de termos (P13). A IA não informa a
classificação.

**Visibilidade**: `Liberada` e `Aprovada` entregam o texto; `PendenteAprovacao` entrega só id e estado
(FR-035c); `Rejeitada` e `Indisponivel` nunca entregam o texto (FR-035g).

**Estados**:

```text
[gerada] ──(não sensível)──→ Liberada
   └──(sensível)──→ PendenteAprovacao ──(aprovar)──────────────→ Aprovada
                          ├──(rejeitar com justificativa)───→ Rejeitada
                          └──(sessão expirada ou encerrada)──→ Indisponivel
```

Cada recomendação é decidida uma única vez (FR-035e); nova decisão gera ERR-020.

## 12. CheckpointAprovacao (J, schema v1)

Registro de auditoria da decisão humana (FR-035f), gravado em
`{Aprovacao:Diretorio}/registros/{recomendacaoId}.json`. Não contém conteúdo de XML, texto da
recomendação nem dados pessoais além de identificadores.

| Campo | Tipo | Regras |
| ----- | ---- | ------ |
| `versaoSchema` | string | `"1.0"` |
| `recomendacaoId` / `execucaoId` / `sessaoId` | GUID | Obrigatórios |
| `estado` | enum | `Pendente`, `Aprovada`, `Rejeitada`, `Indisponivel` |
| `aprovadorId` | string? | Usuário informado na solicitação de decisão, sem verificação (D-05, FR-035d); obrigatório em `Aprovada` e `Rejeitada` |
| `aprovadorChamadorId` | string? | Chamador que informou o aprovador |
| `aprovadorVerificado` | bool | `false` enquanto vigorar o D-05 |
| `decididoEm` | data-hora? | |
| `justificativa` | string? | Obrigatória em `Rejeitada` (ERR-021); máx. 2.000 caracteres |
| `motivoIndisponibilidade` | string? | `SessaoExpirada` ou `SessaoEncerrada` |
| `referenciaWorkflow` | objeto | Id da execução do workflow e do último `CheckpointInfo` |
| `versao` | int | Controle de concorrência otimista |
| `criadoEm` / `atualizadoEm` | data-hora | |

**Gravação**: escrita em arquivo temporário seguida de substituição atômica; trava por recomendação
no processo; falha de leitura ou gravação gera ERR-022 e mantém `Pendente`. Os checkpoints internos
do workflow do MAF ficam em `{Aprovacao:Diretorio}/workflow/` e seguem as mesmas restrições de
conteúdo.

## 13. ContextoUsuario (M, coleção `contextos_usuario`)

Informações próprias da auditoria que o agente mantém sobre um usuário entre sessões (FR-055 a
FR-060, P15, constitution 4.7). Não existe para o usuário "anonimo" (FR-052c).

| Campo | Tipo | Classe | Regras |
| ----- | ---- | ------ | ------ |
| `_id` | string | Interno | `chaveIsolamento` (FR-056) |
| `usuarioId` / `chamadorId` | string | Pessoal / Interno | Para consulta, correção e exclusão (FR-059) |
| `cnpjsAuditados[]` | lista | Fiscal | `cnpj` (14 caracteres, numérico ou alfanumérico), `papel` (`Emitente` ou `Destinatario`), `quantidadeDocumentos`, `ultimaAuditoriaEm`, `origem` |
| `achadosRecorrentes[]` | lista | Interno | `chave` (`regraId` ou tipo de erro estrutural), `categoria`, `frequencia`, `ultimaOcorrenciaEm`, `origem` |
| `preferencias` | objeto? | Interno | `nivelDetalhe` (`Resumido` ou `Detalhado`), `tributosInteresse[]` (lista fechada), `origem` |
| `execucoesContabilizadas` | lista de GUID (limitada) | Interno | Garante que a mesma execução não conte duas vezes |
| `criadoEm` / `atualizadoEm` | data-hora | Interno | |
| `ultimaAtividadeEm` | data-hora | Interno | Renovada por execução ou declaração do usuário (FR-060) |
| `expiraEm` | data-hora | Interno | `ultimaAtividadeEm` + `ContextoUsuario:RetencaoInatividade` (90 dias); índice TTL |

**`origem`** (FR-055b): `tipo` (`Execucao` ou `DeclaracaoUsuario`), `execucaoId?`, `sessaoId?`,
`registradoEm`.

**Validações**:

- O `$jsonSchema` da coleção não aceita campos fora desta tabela; CPF, nomes, endereços, valores de
  documentos e texto livre NÃO DEVEM ser gravados (FR-055a).
- Só documentos aprovados no layout contam em `cnpjsAuditados`; só achados `Deterministica` contam em
  `achadosRecorrentes` (FR-055a, FR-057).
- Gravação só pelo caso de uso (etapa 9) e pelo endpoint de preferências do canal direto; nenhuma
  ferramenta da IA tem acesso (FR-057).
- Leitura ignora documento com `expiraEm` vencido, mesmo antes da remoção pelo TTL.
- Listas limitadas por configuração (`ContextoUsuario:MaxCnpjs`, `ContextoUsuario:MaxAchados`),
  mantendo os itens mais recentes.

**Estados**:

```text
[sem contexto] ──(1ª execução identificada ou declaração)──→ Ativo
Ativo ──(execução ou declaração)──→ Ativo (expiraEm renovado)
Ativo ──(exclusão pelo canal direto)──→ [removido]
Ativo ──(agora > expiraEm)──→ [removido pelo TTL]
```

## 14. ChamadaProvedor (P, tabela `consumo.chamadas_provedor`)

Uma linha por chamada ao provedor, inclusive retentativas e cada volta do loop de ferramentas
(constitution 4.5, P14). Cálculo de custo no padrão do projeto-ia-infis (research R-16).

| Coluna | Tipo | Regras |
| ------ | ---- | ------ |
| `id` | uuid | PK |
| `execucao_id` / `correlacao_id` | uuid / text | `correlacao_id` anulável |
| `servico` | text | `Api.Agente` |
| `operacao` | text | Por exemplo, `AnaliseIa`, `RespostaPergunta` |
| `tipo_chamada` | text | `Chat` ou `Embedding` |
| `provedor` / `modelo` | text | |
| `tentativa` | int | Número da tentativa de geração da saída estruturada (≥ 1) |
| `tokens_entrada` / `tokens_entrada_cache` / `tokens_saida` / `tokens_embedding` | int NULL | NULL quando o provedor não informar; nunca zero por suposição; CHECK ≥ 0 |
| `iniciada_em` / `duracao_ms` | timestamptz / int | |
| `status` | text | `Sucesso`, `Falha`, `Timeout`, `RespostaInvalida` |
| `custo_entrada` / `custo_saida` / `custo_total` | numeric(18,8) NULL | NULL sem preço na tabela ou sem tokens; CHECK ≥ 0 |
| `moeda` | text NULL | `USD` |
| `versao_tabela_precos` | text NULL | |
| `origem_valores` | text | `InformadoProvedor` (tokens) ou `EstimadoSistema` |
| `chave_isolamento` | text | Hash da `IdentificacaoSolicitante`; nunca o `usuarioId` em claro |

Índices: `execucao_id`; `iniciada_em`; (`chave_isolamento`, `iniciada_em`). Sem conteúdo de
mensagens. Visão `consumo.vw_consumo_diario`: soma de tokens e custos por dia, modelo e tipo de
chamada.

## 15. Contrato do LLM — `ContratoIaV1` (E, validado e incorporado ao parecer)

Produzido pela IA por saída estruturada; o sistema valida e incorpora (P08). Schema normativo:
[contracts/contrato-ia-v1.schema.json](./contracts/contrato-ia-v1.schema.json). Não contém ids de
execução, sessão, datas, consumo, resultado geral nem contexto do usuário (P09, FR-057).

| Campo | Regras |
| ----- | ------ |
| `versaoContrato` | Constante `"1.0"`; versão diferente é rejeitada como saída inválida |
| `indicios[]` | `descricao`, `severidade`, `evidencias[]` (≥ 1), `achadosRelacionados[]` |
| `ausenciasEvidencia[]` | Pontos avaliados sem fundamentação (FR-022) |
| `resposta` | `texto` e `valoresCitados[]` (cada um com `calculoId` ou {chave, caminho}); `documentosExcluidos[]` quando houver cálculo |
| `foraDeEscopo` / `informacaoIndisponivel` | Sinalizadores para ERR-018 e ERR-017 |
| `recomendacoes[]` | `texto`, `achadosVinculados[]`, `valoresCitados[]` |
| `resumo` | Texto pt-BR do parecer |

**Validação semântica**: todo `calculoId` existe no registro da execução e o valor citado é igual
ao resultado; todo {chave, caminho} existe nos dados extraídos e o valor confere; todo achado citado
existe; nenhum número com formato monetário aparece no texto sem estar em `valoresCitados`. Falha
gera retentativa (até `Ia:MaxTentativas`) e, esgotadas as tentativas, ERR-015.

## 16. Envelope de resposta v1 (E, devolvido pelo canal direto)

| Campo | Origem | Regras |
| ----- | ------ | ------ |
| `versaoEnvelope` / `versaoContratoIa` | Sistema | Versionados (NFR-012) |
| `execucaoId` / `sessaoId` / `correlacaoId` | Sistema | FR-047, FR-054 |
| `estado` | Sistema | Estado da execução |
| `dataHora` | Sistema | |
| `canal` | Sistema | |
| `usuarioIdentificado` | Sistema | `false` para o usuário "anonimo" (FR-052c) |
| `etapas` | Sistema | Estado das nove etapas |
| `parecer` | Sistema + IA validada | Presente em análises |
| `resposta` | IA validada | Presente em perguntas |
| `recomendacoes` | Sistema | Visão por estado (consulta de recomendações) |
| `erro` | Sistema | `codigo` (ERR-xxx), `mensagem` pt-BR, `arquivo`, `detalhes` |
| `metadados` | Sistema | `modelo`, `duracaoMs`, `consumo` (tokens e custo, NULL quando desconhecidos), `versoes` |

O schema JSON do envelope está em [contracts/api-agente.openapi.yaml](./contracts/api-agente.openapi.yaml)
(`components.schemas.EnvelopeV1`). O canal A2A não devolve o envelope: o adaptador traduz o mesmo
resultado do caso de uso para o `ResultadoAnaliseSofia` (§18).

## 17. Agent Card e Solicitação do simulador

- **Agent Card**: definido em [contracts/sofia-a2a.md](./contracts/sofia-a2a.md) §2; gerado a
  partir da configuração (`A2A:UrlPublica`, `A2A:IdAgente`, limites, versão), sem segredos (FR-044,
  FR-044a, FR-045) e declarando a ausência de autenticação na v1 (FR-052b).
- **Solicitação do simulador**: não é persistida pelo agente; o Sofia.Simulator mantém apenas em
  memória a `SolicitacaoAnaliseSofia` (pergunta, nome fictício, XMLs de exemplo sintéticos) e o
  `ResultadoAnaliseSofia` recebido, e os exibe no console.

## 18. Contrato SOFIA v1.0 (E, DTOs do adaptador A2A)

DTOs da borda (`Api.Agente/A2A/Sofia`), sem persistência própria: o que é guardado vai para a
execução, a sessão e os arquivos recebidos. Schemas normativos:
[sofia-solicitacao-analise-v1.schema.json](./contracts/sofia-solicitacao-analise-v1.schema.json) e
[sofia-resultado-analise-v1.schema.json](./contracts/sofia-resultado-analise-v1.schema.json).
Serialização JSON em camelCase, UTF-8, sem `null` em campos opcionais ausentes da solicitação;
campos desconhecidos são ignorados.

**`SolicitacaoAnaliseSofia` → caso de uso**:

| Campo SOFIA | Destino | Regra |
| ----------- | ------- | ----- |
| `versaoContrato` | Validação | Igual a `"1.0"` (ERR-007) |
| `idExecucao` / `idSolicitacao` | `Execucao.idExecucaoSofia` / `idSolicitacaoSofia`; `correlacaoId` = `idSolicitacao` | UUID; inválido → Task `failed` (FR-043d) |
| `usuario.nome` | `IdentificacaoSolicitante` (§0) | Até 200 caracteres |
| `pergunta` | `Execucao.pergunta` | Até 4.000 caracteres; tipo `AnaliseComPergunta` |
| `documentos[]` | `ArquivoRecebido` (§3) | `nome` → `nome`; bytes UTF-8 de `texto` → conteúdo; `idDocumento` → `idDocumento`; `tipo` ignorado na identificação |
| `contexto.idioma` / `contexto.instrucoes` | Não usados | `instrucoes` é dado não confiável (FR-053) |

**Parecer e execução → `ResultadoAnaliseSofia`**:

| Campo SOFIA | Origem | Regra |
| ----------- | ------ | ----- |
| `versaoContrato` | Constante | `"1.0"` |
| `idExecucao` / `idSolicitacao` | `Execucao.idExecucaoSofia` / `idSolicitacaoSofia` | Sem alteração |
| `agente.idAgente` / `agente.nome` | `A2A:IdAgente` / nome do card | |
| `status` | `Execucao.estado`, `Parecer.resultadoGeral`, `Parecer.documentos` | `concluido`: `Concluida`, todos os documentos com as duas validações realizadas e resultado diferente de `Inconclusiva`. `parcial`: `Concluida` com documento não analisado ou validação não realizada, ou `Inconclusiva`. `erro`: `Rejeitada` ou `Falhou` |
| `analise` | `Parecer.resumoTextual` | Em `erro`, a mensagem do erro; nunca vazia |
| `achados[]` | `Parecer.achados` | Um por achado, na ordem (abaixo) |
| `observacoes[]` | `Parecer.documentos`, `limitacoes`, `validacoesNaoRealizadas`, `revisaoHumana`, `recomendacoes`, `Execucao.id` | Na ordem do contrato §5.5 |
| `erro` | `Execucao.erro` | `{codigo, mensagem}` só em `erro`; senão `null` |

**`Achado` → `achados[]`**:

| Campo SOFIA | Origem | Regra |
| ----------- | ------ | ----- |
| `idAchado` | `Achado.id` | GUID em texto |
| `descricao` | `Achado.categoria` + `descricao` | Rótulo da categoria em pt-BR seguido da descrição |
| `idDocumentos` | `ArquivoRecebido.idDocumento` do arquivo do achado | `[]` para achados do lote; só IDs recebidos |
| `evidencias` | `Achado.evidencias` e campos do achado | Texto montado pelo sistema por tipo: `CampoXml` (caminho e valor), `Calculo` (valor calculado, regra e versão), `RegraCatalogo` (regra, versão e catálogo), `TrechoManual` (documento, versão e trecho); `[]` sem evidência |
| `severidade` | `Achado.severidade` | `Alta` → `alta`, `Media` → `media`, `Baixa` → `baixa`; `Informativa` → omitido |

**Falhas sem resultado**: mensagem sem exatamente uma parte de texto, texto que não é objeto JSON ou
sem `idExecucao` e `idSolicitacao` UUID → nenhuma execução é criada; a Task termina `failed` com
"ERR-007: …" (FR-043d). As demais violações do contrato são recusadas pelo validador do adaptador,
antes do caso de uso e sem criar execução (como a resposta 400 do canal direto), e geram resultado
com status `erro` e ERR-007, sem a linha do identificador interno nas observações. Os limites de
tamanho e de quantidade (ERR-002, ERR-003) seguem o caso de uso, como no canal direto.

**Repetição**: o resultado de uma repetição (FR-043h) é montado pelo mesmo mapeador a partir da
execução, do parecer e dos arquivos recebidos gravados, e é igual ao da primeira resposta, salvo o
estado atual das recomendações sensíveis (continuam sem conteúdo). ERR-026 gera resultado `erro`
sem a linha do identificador interno.
