# Quickstart: Agente de Auditoria de NF-e (AuditoriaNFe)

**Feature**: `001-agente-auditoria-nfe` | **Data**: 2026-10-29, revisado em 2026-11-04 |
**Plano**: [plan.md](./plan.md)

Guia para subir o ambiente local e validar de ponta a ponta os cenários principais da feature. Ele
descreve comandos e resultados esperados; a implementação é definida em `tasks.md`. Contratos e
modelo de dados: [contracts/api-agente.openapi.yaml](./contracts/api-agente.openapi.yaml),
[contracts/sofia-a2a.md](./contracts/sofia-a2a.md) e [data-model.md](./data-model.md).

> Este guia vale depois da implementação das tarefas. Os nomes de projetos, variáveis e rotas são os
> definidos no [plano](./plan.md).

> **Somente ambiente local (desvio D-05).** A versão 1 não tem autenticação. Execute apenas na sua
> máquina, com as portas em `127.0.0.1`, e apenas com XMLs sintéticos ou anonimizados (FR-052b).

## 1. Pré-requisitos

| Item | Versão | Observação |
| ---- | ------ | ---------- |
| .NET SDK | 10.0 (LTS) | `dotnet --list-sdks` DEVE listar um SDK 10.0.x; o `global.json` fixa essa versão (10.0.403, instalado em 2026-11-04 na máquina de desenvolvimento) |
| Docker Desktop (contêineres Linux) | atual | Necessário para o compose e para os testes de integração (Testcontainers) |
| Git | atual | |
| PowerShell | 7.x | Os exemplos usam `Invoke-RestMethod -Form`, que não existe no Windows PowerShell 5.1 |
| Chave da API OpenAI | — | Opcional: sem ela, use o provedor simulado (seção 3) |

Nenhum segredo é versionado. Chaves e senhas ficam no `.env` local (ignorado pelo Git) ou em
`dotnet user-secrets`. Não reutilize chaves de outros projetos.

## 2. Obter o código

```powershell
git clone https://github.com/<organizacao>/AgenteAuditoriaNFe.git
cd AgenteAuditoriaNFe
git switch 001-agente-auditoria-nfe
```

## 3. Configurar

Copie o modelo e preencha os valores locais:

```powershell
Copy-Item .env.example .env
```

| Variável | Exemplo | Uso |
| -------- | ------- | --- |
| `MONGO_INITDB_ROOT_USERNAME` / `MONGO_INITDB_ROOT_PASSWORD` | (local) | Obrigatórias no compose (`${VAR:?}`) |
| `MONGO_APP_USERNAME` / `MONGO_APP_PASSWORD` | (local) | Usuário do Api.Agente no banco `auditoria_agente`, criado pelo script de inicialização |
| `POSTGRES_USER` / `POSTGRES_PASSWORD` | (local) | Administrador do PostgreSQL do compose |
| `POSTGRES_APP_USER` / `POSTGRES_APP_PASSWORD` | (local) | Usuário do Api.Agente, com acesso só ao schema `consumo` |
| `Ia__Provedor` | `Simulado` ou `OpenAI` | `Simulado` só é aceito em `Development`; o parecer indica "modelo: simulado" |
| `OpenAI__ApiKey` | (sua chave) | Obrigatória só com `Ia__Provedor=OpenAI` |
| `OpenAI__Modelo` | (modelo aprovado pela avaliação, DP-05) | Sem valor padrão |
| `Mcp__Habilitado` | `false` | Integração MCP desabilitada nesta feature |
| `A2A__UrlPublica` | `http://localhost:5310` | URL base publicada no Agent Card |
| `A2A__IdAgente` | `agente-auditoria-nfe` | `idAgente` do card e do resultado; igual ao registry da SOFIA |
| `A2A__ChamadorPadrao` | `sofia` | `chamadorId` das solicitações A2A (D-05) |
| `Cors__AllowedOrigins__0` | vazio | Origem de um frontend web autorizado nas rotas `/api/v1` (FR-052e); não é necessária para a SOFIA |
| `Limites__TamanhoTotalBytes` / `Limites__QuantidadeArquivos` | `5242880` / `20` | FR-004, FR-005 |
| `ContextoUsuario__RetencaoInatividade` | `90.00:00:00` | FR-060 |

O compose define `ASPNETCORE_ENVIRONMENT=Development` e `Seguranca__AutenticacaoHabilitada=false`.
O Api.Agente recusa iniciar sem autenticação fora de `Development` (D-05).

## 4. Subir o ambiente

**Tudo em contêineres** (um comando — constitution 3.5):

```powershell
docker compose up -d --build
docker compose ps
```

| Serviço | Endereço no host | Observação |
| ------- | ---------------- | ---------- |
| `api-agente` | `http://localhost:5310` | Swagger em `/swagger` |
| `mongodb` | `localhost:27020` | Coleções e índices criados por `infra/mongodb/init` |
| `postgres` | `localhost:5436` | Schema `consumo`; tabela criada pelas migrações na inicialização da API |

Todas as portas são publicadas só em `127.0.0.1`. Os checkpoints de aprovação ficam no volume
`./data/aprovacoes`.

**Alternativa para desenvolvimento** (bancos em contêiner, API pelo SDK):

```powershell
docker compose up -d mongodb postgres
dotnet run --project src/Api.Agente
```

## 5. Verificar a saúde e a descoberta

```powershell
Invoke-RestMethod http://localhost:5310/health/live
Invoke-RestMethod http://localhost:5310/health/ready
Invoke-RestMethod http://localhost:5310/.well-known/agent-card.json
```

Esperado:

- `live` e `ready` saudáveis; `ready` falha se o MongoDB, o PostgreSQL ou o diretório de checkpoints
  estiverem indisponíveis, ou se algum XSD não conferir com o SHA-256 do manifesto.
- O Agent Card no formato A2A v0.3 (`contracts/sofia-a2a.md` §2): `name = AgenteAuditoriaNFe`,
  `url = http://localhost:5310/a2a/auditoria-nfe`, `protocolVersion = 0.3.0`, `preferredTransport =
  JSONRPC`, a habilidade `analisar_documentos` e a extensão `urn:infis:sofia:contrato` com
  `versaoContrato = 1.0`, `idAgente = agente-auditoria-nfe`, `nomeArtifact = analise-sofia-v1`, os
  limites (5 MB, 20 documentos) e `autenticacao = nenhuma — somente ambiente local de testes`. Nenhum
  segredo nem `securitySchemes` no card (TS-029). Com o cabeçalho `A2A-Version: 1.0`, o mesmo card sai
  no formato A2A 1.0.

## 6. Validar o núcleo pelo canal direto

Os exemplos usam XMLs sintéticos de `tests/AuditoriaNFe.Tests/Fixtures/Xml/`, um por período de
vigência dos pacotes de schema.

```powershell
$cabecalhos = @{ 'X-Usuario-Id' = 'usuario-demo-01'; 'X-Chamador-Id' = 'teste-local' }
$form = @{
  pergunta = 'Quais notas têm totalizador divergente?'
  arquivos = Get-Item tests/AuditoriaNFe.Tests/Fixtures/Xml/PL_010e_v1.02/*.xml
}
$envelope = Invoke-RestMethod -Method Post http://localhost:5310/api/v1/execucoes `
  -Headers $cabecalhos -Form $form
$envelope.parecer.documentos | Format-Table arquivo, dataReferencia, origemDataReferencia, pacoteAplicado
$envelope.parecer.achados | Format-Table categoria, severidade, regraId, arquivo
```

Esperado:

- Estado `Concluida`, as nove etapas registradas, etapa 5 "não aplicável — integração não
  habilitada" e a limitação "consulta a manuais oficiais não habilitada" (TS-046).
- Para cada documento: data de referência, origem (`dhRecbto` quando há protocolo, senão `dhEmi`) e
  pacote aplicado (FR-010d).
- Divergências de totalizador como `InconsistenciaNegocio`, com valor informado, valor esperado e
  `regraId` do catálogo (TS-004).
- Uma NFC-e (modelo 65) do diretório de fixtures sai como "não suportado" (ERR-009), sem validação de
  schema (TS-052).

Repita sem os cabeçalhos: o envelope traz `usuarioIdentificado = false`, a limitação de usuário não
identificado e nenhum contexto do usuário é gravado (TS-059).

## 7. Validar o contexto do usuário

```powershell
Invoke-RestMethod -Method Put http://localhost:5310/api/v1/contexto-usuario/preferencias `
  -Headers $cabecalhos -ContentType 'application/json' `
  -Body '{ "nivelDetalhe": "Detalhado", "tributosInteresse": ["ICMS", "IPI"] }'
Invoke-RestMethod http://localhost:5310/api/v1/contexto-usuario -Headers $cabecalhos
Invoke-RestMethod -Method Delete http://localhost:5310/api/v1/contexto-usuario -Headers $cabecalhos
```

Esperado: depois da análise da seção 6, o contexto lista os CNPJs auditados com contagem, os achados
recorrentes e a preferência declarada, cada item com a origem; nada de valores, nomes, CPF ou texto
livre (TS-057). Com outro `X-Usuario-Id` ou `X-Chamador-Id`, a consulta não encontra o contexto
(TS-054). Após a exclusão, a consulta responde 404 (TS-056).

## 8. Validar com o Sofia.Simulator

O simulador fala o contrato SOFIA v1.0 sobre A2A v0.3 (`contracts/sofia-a2a.md`): envia uma
`SolicitacaoAnaliseSofia` com nome fictício, uma pergunta e os XMLs sintéticos de
`src/Sofia.Simulator/Exemplos` lidos como texto, e lê o `ResultadoAnaliseSofia` do Artifact
`analise-sofia-v1`. O endereço do agente é a única configuração (SC-005).

### 8.1 Fluxo básico — TS-035 (obrigatório)

```powershell
dotnet run --project src/Sofia.Simulator -- --endereco http://localhost:5310 --cenario basico
```

Esperado no console, em ordem:

1. Agent Card obtido, com a habilidade `analisar_documentos`, o `idAgente`, os limites e a ausência
   de autenticação.
2. Solicitação enviada por `message/send`, com `idExecucao` e `idSolicitacao` novos.
3. Task no estado `completed`; `status` (`concluido` ou `parcial`), a `analise` em pt-BR, os
   `achados` com severidade, documentos e evidências, e as `observacoes` (validação determinística,
   pacote de schema, limitações e revisão humana).
4. Conferência dos IDs: `idExecucao` e `idSolicitacao` do resultado iguais aos enviados.

### 8.2 Agent Card inacessível — TS-035a (obrigatório)

```powershell
dotnet run --project src/Sofia.Simulator -- --endereco http://localhost:9999 --cenario basico
```

Esperado: mensagem de falha de descoberta e nenhuma solicitação enviada (código de saída diferente
de zero).

### 8.3 Documento não suportado — TS-065 (opcional)

```powershell
dotnet run --project src/Sofia.Simulator -- --endereco http://localhost:5310 --cenario documento-nao-suportado
```

Esperado: uma NF-e válida e um texto que não é NF-e na mesma solicitação; `status: parcial`, achados
da NF-e e uma observação com ERR-009 para o outro documento.

### 8.4 Contrato inválido — TS-066 (opcional)

```powershell
dotnet run --project src/Sofia.Simulator -- --endereco http://localhost:5310 --cenario contrato-invalido
```

Esperado: solicitação com `versaoContrato: "2.0"`; Task `completed` com `status: erro`,
`erro.codigo = ERR-007`, os IDs devolvidos e nenhuma execução criada.

### 8.5 Chamada manual (sem o simulador)

```powershell
$solicitacao = @{
  versaoContrato = '1.0'
  idExecucao     = [guid]::NewGuid().ToString()
  idSolicitacao  = [guid]::NewGuid().ToString()
  usuario        = @{ nome = 'Usuária Fictícia' }
  pergunta       = 'Verifique divergências fiscais nos documentos.'
  documentos     = @(@{
    idDocumento = 'doc-001'
    nome        = 'nfe-sintetica.xml'
    texto       = Get-Content -Raw src/Sofia.Simulator/Exemplos/<arquivo-valido>.xml
  })
} | ConvertTo-Json -Depth 5 -Compress

$envelope = @{
  jsonrpc = '2.0'; id = 'rpc-001'; method = 'message/send'
  params  = @{ message = @{
    kind = 'message'; role = 'user'; messageId = [guid]::NewGuid().ToString()
    parts = @(@{ kind = 'text'; text = $solicitacao })
  } }
} | ConvertTo-Json -Depth 8 -Compress

$resposta = Invoke-RestMethod -Method Post http://localhost:5310/a2a/auditoria-nfe `
  -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($envelope))
$resposta.result.status.state
($resposta.result.artifacts | Where-Object name -eq 'analise-sofia-v1').parts[0].text | ConvertFrom-Json
```

Esperado: `completed` e um `ResultadoAnaliseSofia` que valida em
`contracts/sofia-resultado-analise-v1.schema.json`. O nome do usuário aparece na sessão e na
execução gravadas no MongoDB, mas não nos logs da API (TS-067).

Repita o último `Invoke-RestMethod` com o mesmo `$envelope`: o resultado é o mesmo, com o mesmo
identificador interno da execução, e nenhuma nova chamada de IA aparece em `consumo.vw_consumo_diario`
(TS-068). Os exemplos completos de card, envio, Task e resultados estão em
`specs/001-agente-auditoria-nfe/contracts/exemplos/`.

### 8.6 SOFIA em contêiner

Quando a SOFIA rodar em outro `docker compose` na mesma máquina, ela acessa o agente pelo nome do
serviço, não por `localhost`. Conecte o contêiner da SOFIA à rede do agente e publique no card o
endereço que ela alcança:

```powershell
docker network connect auditoria-nfe-net <conteiner-da-sofia>
$env:A2A__UrlPublica = 'http://api-agente:8080'
docker compose up -d api-agente
Invoke-RestMethod http://localhost:5310/.well-known/agent-card.json | Select-Object url
```

Esperado: `url = http://api-agente:8080/a2a/auditoria-nfe`, e a SOFIA descobre o agente em
`http://api-agente:8080/.well-known/agent-card.json`. Sem TLS e sem credencial, só em rede local
isolada (D-05).

Teste de dentro do contêiner da SOFIA (ou de qualquer contêiner na rede), sem `Origin` e sem CORS:

```powershell
docker run --rm --network auditoria-nfe-net curlimages/curl -s http://api-agente:8080/.well-known/agent-card.json
```

### 8.7 CORS para frontend web (TS-070, opcional)

CORS só importa para um navegador chamando as rotas `/api/v1` diretamente; a SOFIA pelo backend não
precisa dele. Por padrão nenhuma origem é permitida. Para autorizar um frontend local, informe a
origem no `.env` (esquema + host + porta, sem barra final) e recrie o contêiner:

```powershell
# no .env: Cors__AllowedOrigins__0=http://localhost:4200
docker compose up -d api-agente

$preflight = @{
  'Origin'                         = 'http://localhost:4200'
  'Access-Control-Request-Method'  = 'POST'
  'Access-Control-Request-Headers' = 'content-type,x-usuario-id,x-chamador-id'
}
Invoke-WebRequest -Method Options http://localhost:5310/api/v1/execucoes -Headers $preflight |
  Select-Object StatusCode, @{ n = 'Allow-Origin'; e = { $_.Headers['Access-Control-Allow-Origin'] } }

$preflight.Origin = 'https://site-nao-autorizado.example'
Invoke-WebRequest -Method Options http://localhost:5310/api/v1/execucoes -Headers $preflight -SkipHttpErrorCheck |
  Select-Object StatusCode, @{ n = 'Allow-Origin'; e = { $_.Headers['Access-Control-Allow-Origin'] } }
```

Esperado (a partir da T089, quando `POST /api/v1/execucoes` existir): a origem configurada recebe
`Access-Control-Allow-Origin: http://localhost:4200`, sem `Access-Control-Allow-Credentials`; a
outra origem não recebe o cabeçalho. Agent Card, `/a2a/auditoria-nfe` e `/health/*` nunca recebem
cabeçalhos CORS. `curl` e o PowerShell não aplicam CORS: o bloqueio só acontece no navegador.

### 8.8 Agent Card publicado no Vercel (D-08, TS-071)

Depois da conclusão da Fase 11 (D-08 e ADR-0012 aprovados em 2026-11-05). O Vercel recebe apenas o
modo `SomenteAgentCard`: o card e `/health/live`, sem banco, IA nem rotas de análise. O único
segredo do projeto Vercel é `SOFIA_A2A_TOKEN`, a credencial de serviço da SOFIA (FR-052h,
ADR-0014): o card e os caminhos `/a2a` exigem `Authorization: Bearer {chave}`, e, sem a variável, o
agente não inicia. A chave é a compartilhada da turma, informada pelo professor; ela não entra no
Git, no chat nem em comandos com o valor visível. A partir da Fase 12, o mesmo projeto passa ao
modo `Homologacao` (seção 8.9); o modo só card fica como volta rápida.

1. Conferir a imagem localmente (porta só em `127.0.0.1`):

   ```powershell
   docker build -f Dockerfile.vercel -t auditoria-nfe-card:local .
   $env:SOFIA_A2A_TOKEN = Read-Host -MaskInput 'Credencial de serviço da SOFIA'
   docker run --rm -d --name auditoria-nfe-card -p 127.0.0.1:5311:8080 `
     -e PORT=8080 -e A2A__UrlPublica=http://localhost:5311 -e SOFIA_A2A_TOKEN auditoria-nfe-card:local
   $sofia = @{ Authorization = "Bearer $env:SOFIA_A2A_TOKEN" }
   Invoke-WebRequest http://localhost:5311/.well-known/agent-card.json -SkipHttpErrorCheck | Select-Object StatusCode
   Invoke-RestMethod http://localhost:5311/.well-known/agent-card.json -Headers $sofia | ConvertTo-Json -Depth 10
   Invoke-WebRequest http://localhost:5311/health/ready -SkipHttpErrorCheck | Select-Object StatusCode
   docker stop auditoria-nfe-card
   ```

   Esperado: sem o cabeçalho, 401; com ele, o card do contrato §2 com `url` =
   `http://localhost:5311/a2a/auditoria-nfe`, `implantacao` na extensão e `securitySchemes`
   Bearer, sem o valor da chave; `/health/ready` responde 404. Sem `-e SOFIA_A2A_TOKEN`, o
   contêiner encerra com a mensagem que cita a variável.

2. Publicar em preview e conferir antes da produção (CLI autenticada na conta da autora):

   ```powershell
   npm.cmd install -g vercel
   vercel login
   vercel link
   vercel env add PORT production          # valor: 8080 (repetir para preview)
   vercel env add A2A__UrlPublica production   # valor: https://<projeto>.vercel.app
   vercel env add SOFIA_A2A_TOKEN production --sensitive   # valor digitado no prompt (repetir para preview)
   vercel env ls
   vercel deploy
   Invoke-RestMethod https://<url-do-preview>/.well-known/agent-card.json | ConvertTo-Json -Depth 10
   ```

   Esperado: o mesmo card com a URL pública (com o cabeçalho `$sofia` do passo 1; sem ele, 401);
   `/health/live` responde 200; `/swagger` e `/api/v1/...` respondem 404; `POST /a2a/auditoria-nfe`
   responde 401 sem o cabeçalho e 404 com ele. Em preview, a proteção de deployments do Vercel exige
   login; confira com `vercel curl https://<url-do-preview>/.well-known/agent-card.json`.

   Antes do primeiro `vercel deploy`: o primeiro deploy de um projeto vai para produção, e o
   `vercel.json` precisa ter `"framework": "container"`, senão o preset "Other" do projeto publica
   o repositório como site estático. O autor do último commit precisa ser a conta dona do projeto
   (`git config user.email` deste repositório com o e-mail da conta), senão o deploy fica "Blocked".
   No PowerShell, repita `--environment` em vez de separar ambientes por vírgula.

3. Produção pela `main`: o `vercel.json` só permite deploy automático da `main`. Depois que a PR
   `dev` → `main` com o `vercel.json` for mesclada, conectar o repositório ao projeto (`vercel git
   connect` ou Project Settings > Git) e conferir que a Production Branch é `main`. A partir daí, cada
   merge na `main` publica a produção; para publicar o merge que já aconteceu, use Redeploy no painel
   ou `vercel deploy --prod` a partir da `main` atualizada. Repita as verificações do passo 2 na URL
   de produção, sem o login do Vercel (`Invoke-WebRequest` ou `curl`, com o cabeçalho `$sofia`),
   porque a produção é pública. **Cadastre `SOFIA_A2A_TOKEN` em Production antes de mesclar na
   `main` o código da credencial de serviço (Fase 13)**: sem a variável, o deploy seguinte não
   inicia e o card sai do ar. O
   build do contêiner leva alguns minutos; enquanto o deploy estiver "Initializing", a URL responde
   404. Os domínios `<projeto>-<conta>.vercel.app` e `<projeto>-git-main-<conta>.vercel.app` ficam
   atrás da proteção de deployments; a SOFIA usa só `https://<projeto>.vercel.app`.

### 8.9 Homologação pública (D-09, TS-075)

Depois da conclusão das tarefas T178 a T182 (D-09 e ADR-0013 aprovados em 2026-11-10). O agente
completo roda no modo `Homologacao` no mesmo projeto Vercel do Agent Card (seção 8.8),
`agente-auditoria-nfe`, e na mesma URL, `https://agente-auditoria-nfe.vercel.app`, com MongoDB
Atlas Free, Neon Free e OpenAI. A SOFIA continua com o card já cadastrado; o `message/send` só
funciona depois da Fase 8. **A API REST e o Swagger não têm autenticação**: qualquer pessoa com a
URL pode enviar XMLs e ler dados de outro usuário se souber os identificadores. Só o card e o A2A
exigem a credencial de serviço da SOFIA (seção 8.8). Use só XMLs sintéticos.

Segredos (senhas, connection strings e a chave da OpenAI) vão direto do painel do provedor para as
variáveis do projeto Vercel ou para um gerenciador de senhas. Nunca entram no Git, no chat, no `.env`
versionado nem em comandos com o valor visível. Nos comandos abaixo, `Read-Host -MaskInput` lê o
valor sem exibi-lo.

Limites dos planos gratuitos verificados em 2026-11-10 (research R-36; confira de novo no dia, pois
mudam sem aviso):

| Serviço | Limites relevantes |
| ------- | ------------------ |
| Vercel Hobby | Uso pessoal e não comercial; 300 s por requisição; corpo até 4,5 MB; 4 horas de CPU ativa por mês (ao estourar, o projeto fica pausado até 30 dias); logs retidos por 1 hora; instância ociosa encerrada após 5 minutos |
| MongoDB Atlas Free | 1 cluster por projeto; 0,5 GB; 100 operações por segundo; pausa após 30 dias sem conexões |
| Neon Free | 1 GB; 100 CU-hora por mês; compute suspenso após 5 minutos ocioso; 5 GB de tráfego de saída |
| OpenAI | Não é gratuito: o teto de custo é o limite de gasto do projeto da OpenAI |

1. **MongoDB Atlas** (painel em `cloud.mongodb.com`):
   1. Crie um projeto `auditoria-nfe-homolog` e, nele, um cluster **Free** na AWS, região
      `us-east-1` (N. Virginia), perto do `iad1` do Vercel.
   2. **Database Access** > Add New Database User: autenticação por senha, **Autogenerate Secure
      Password** (guarde no gerenciador de senhas), **Specific Privileges** com `readWrite` no banco
      `auditoria_agente`, e nada além disso.
   3. **Network Access** > Add IP Address > `0.0.0.0/0`, com o comentário "Vercel homologação
      D-09". O Vercel não tem IP de saída fixo; os controles compensatórios estão no ADR-0013.
   4. **Connect** > Drivers: copie a string `mongodb+srv://...` e acrescente `/auditoria_agente`
      antes do `?`, como em `mongodb+srv://{usuario}:{senha}@{cluster}.mongodb.net/auditoria_agente?retryWrites=true&w=majority`.
      Sem `authSource`: o usuário do Atlas fica no banco `admin`. Caracteres especiais da senha
      precisam de codificação de URL.
   5. Crie as coleções, os validadores e os índices com o script comum ao ambiente local:

      ```powershell
      $uri = Read-Host -MaskInput 'String mongodb+srv do Atlas'
      docker run --rm -v "${PWD}/infra/mongodb/init:/scripts:ro" mongo:8.0.32 `
        mongosh $uri --quiet --file /scripts/02-colecoes.js
      docker run --rm mongo:8.0.32 mongosh $uri --quiet --eval `
        "print(db.getCollectionNames().sort().join(', ')); print(db.contextos_usuario.getIndexes().map(i => i.name).join(', '))"
      Remove-Variable uri
      ```

      Esperado: as coleções `contextos_usuario`, `documentos`, `execucoes`, `sessoes` e
      `sessoes_agente`, e o índice `ttl_contextos_usuario_expira_em`. O script pode ser executado
      de novo; coleções e índices existentes são mantidos.

2. **Neon** (painel em `console.neon.tech`):
   1. Crie um projeto `auditoria-nfe-homolog` com Postgres 17 na AWS `us-east-1` e, em
      **Databases**, o banco `auditoria_agente` com o dono padrão do projeto.
   2. Crie o usuário da aplicação **por SQL**. Usuários criados pelo painel recebem `neon_superuser`
      (privilégios de administrador), e o script do passo seguinte os recusa. Gere a senha na sua
      máquina, copie-a para a área de transferência e guarde-a no gerenciador de senhas:

      ```powershell
      [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24)).ToLowerInvariant() | Set-Clipboard
      ```

      No **SQL Editor**, com o banco `auditoria_agente`, execute
      `CREATE ROLE auditoria_app WITH LOGIN PASSWORD '<colar>';` e depois apague a consulta do
      histórico do editor.
   3. No mesmo editor, execute o conteúdo de `infra/homologacao/neon-consumo.sql`. Ele recusa
      usuário com privilégio administrativo, revoga o acesso público ao banco e ao schema `public`,
      cria o schema `consumo` e dá ao `auditoria_app` uso e criação nesse schema.
   4. Gere o script das migrações sem acesso ao banco e aplique-o com o usuário da aplicação, no
      **host direto** (sem `-pooler`), para que as tabelas fiquem com ele:

      ```powershell
      dotnet tool restore
      dotnet ef migrations script --idempotent --project src/AuditoriaNFe.Infrastructure `
        --startup-project src/AuditoriaNFe.Infrastructure -o "$env:TEMP\migracoes-consumo.sql"
      $neonDireto = 'host={endpoint}.us-east-1.aws.neon.tech dbname=auditoria_agente user=auditoria_app sslmode=require'
      $env:PGPASSWORD = Read-Host -MaskInput 'Senha do auditoria_app'
      Get-Content -Raw "$env:TEMP\migracoes-consumo.sql" |
        docker run --rm -i -e PGPASSWORD pgvector/pgvector:0.8.7-pg17 psql $neonDireto -v ON_ERROR_STOP=1 -q
      'SELECT "MigrationId" FROM consumo."__EFMigrationsHistory"; SELECT count(*) FROM consumo.chamadas_provedor;' |
        docker run --rm -i -e PGPASSWORD pgvector/pgvector:0.8.7-pg17 psql $neonDireto
      ```

      Esperado: a migração `20261105132907_Inicial` registrada e a tabela vazia. O script pode ser
      aplicado de novo sem efeito.
   5. Monte a connection string da aplicação no formato de chaves (o Npgsql não aceita o
      `postgresql://` que o painel mostra), com o **host com pooling**:
      `Host={endpoint}-pooler.us-east-1.aws.neon.tech;Database=auditoria_agente;Username=auditoria_app;Password={senha};SSL Mode=Require`.
      Em seguida, `Remove-Item Env:PGPASSWORD`.

3. **OpenAI** (painel em `platform.openai.com`): crie um projeto exclusivo da homologação, defina o
   limite de gasto mensal do projeto e confira no painel se ele bloqueia as chamadas ou só alerta.
   Depois, crie uma chave desse projeto. O modelo é o aprovado pela avaliação (DP-05).

4. **Vercel**, no projeto do Agent Card (`agente-auditoria-nfe`, seção 8.8), que é ligado ao Git
   e publica a produção a cada merge na `main`:
   1. Confirme que o código da Fase 12 já está na `main` e que a produção atual é um deploy dessa
      versão, ainda no modo só card (o card traz `implantacao` = "somente Agent Card publicado...").
      Se `Hospedagem__Modo=Homologacao` for cadastrado antes disso, o deploy seguinte da `main` não
      inicia, porque o código antigo não conhece o modo.
   2. Em **Settings > Environment Variables**, mantenha `PORT=8080`,
      `A2A__UrlPublica=https://agente-auditoria-nfe.vercel.app` e `SOFIA_A2A_TOKEN` (sensível), já
      cadastradas na seção 8.8, e acrescente, só para **Production**:

   | Variável | Valor | Sensível |
   | -------- | ----- | -------- |
   | `Hospedagem__Modo` | `Homologacao` | Não |
   | `ConnectionStrings__MongoDb` | string `mongodb+srv` do passo 1 | Sim |
   | `ConnectionStrings__Consumo` | string do Neon do passo 2 | Sim |
   | `Ia__Provedor` | `OpenAI` | Não |
   | `OpenAI__ApiKey` | chave do passo 3 | Sim |
   | `OpenAI__Modelo` | modelo aprovado (DP-05) | Não |
   | `Limites__TamanhoTotalBytes` | `4000000` | Não |
   | `Limites__CorpoHttpBytes` | `4500000` | Não |
   | `Aprovacao__Diretorio` | `/tmp/aprovacoes` | Não |
   | `Persistencia__AplicarMigracoes` | `false` | Não |

   3. As variáveis só valem em deploys novos: em **Deployments**, abra o deployment de produção
      atual e use **Redeploy**. Daí em diante, cada merge na `main` publica o agente completo.

   Sem `Hospedagem__Modo`, a imagem sobe como `SomenteAgentCard`. Com provedor simulado, migrações
   ligadas ou limites acima de 4.000.000 e 4.500.000 bytes, o agente não inicia e o log cita o D-09;
   nesse caso, a produção anterior continua no ar até um deploy novo ficar pronto.

5. **Verificações do TS-075** na URL de produção, com a API REST sem autenticação:

   ```powershell
   $base = 'https://agente-auditoria-nfe.vercel.app'
   $sofia = @{ Authorization = "Bearer $(Read-Host -MaskInput 'Credencial de serviço da SOFIA')" }
   $inicio = Get-Date; Invoke-RestMethod "$base/health/live"; "primeira resposta: $((Get-Date) - $inicio)"
   Invoke-RestMethod "$base/health/ready"
   Invoke-WebRequest "$base/.well-known/agent-card.json" -SkipHttpErrorCheck | Select-Object StatusCode
   Invoke-RestMethod "$base/.well-known/agent-card.json" -Headers $sofia | ConvertTo-Json -Depth 10
   Invoke-WebRequest "$base/swagger/index.html" | Select-Object StatusCode
   $cabecalhos = @{ 'X-Usuario-Id' = 'homolog-01'; 'X-Chamador-Id' = 'homologacao' }
   $form = @{
     pergunta = 'Quais notas têm totalizador divergente?'
     arquivos = Get-Item tests/AuditoriaNFe.Tests/Fixtures/Xml/PL_010e_v1.02/*.xml
   }
   $envelope = Invoke-RestMethod -Method Post "$base/api/v1/execucoes" -Headers $cabecalhos -Form $form
   $envelope.estado; $envelope.parecer.resultadoGeral
   $form.sessaoId = $envelope.sessaoId; $form.pergunta = 'Resuma os achados da análise anterior.'
   (Invoke-RestMethod -Method Post "$base/api/v1/execucoes" -Headers $cabecalhos -Form $form).sessaoId -eq $envelope.sessaoId
   Invoke-RestMethod "$base/api/v1/contexto-usuario" -Headers $cabecalhos
   [IO.File]::WriteAllBytes("$env:TEMP\lote-grande.xml", [byte[]]::new(4000001))
   Invoke-WebRequest -Method Post "$base/api/v1/execucoes" -Headers $cabecalhos -SkipHttpErrorCheck `
     -Form @{ arquivos = Get-Item "$env:TEMP\lote-grande.xml" } | Select-Object StatusCode, Content
   vercel logs <url-do-deployment>
   ```

   Esperado: `live` e `ready` 200; o card sem o cabeçalho com 401 e, com ele, com `url` =
   `https://agente-auditoria-nfe.vercel.app/a2a/auditoria-nfe`, `implantacao` de homologação e
   `securitySchemes` Bearer; Swagger 200; a primeira análise `Concluida` com parecer; a segunda
   na mesma sessão; o contexto com os CNPJs auditados; o lote grande com 413 e `ERR-002` no envelope
   (não o `FUNCTION_PAYLOAD_TOO_LARGE` do Vercel); uma linha nova em `consumo.chamadas_provedor`
   (consulta do passo 2.4); nos logs, nenhum segredo nem conteúdo de XML (os logs ficam só 1 hora).
   Até a Fase 8, `POST /a2a/auditoria-nfe` responde 401 sem o cabeçalho e 404 com ele. Ao
   terminar, `Remove-Variable sofia`. Registre no research R-36 a URL, o commit,
   o deployment e os resultados, sem endereços de cluster, usuários ou senhas.

6. **Rollback, desligamento e limpeza**:
   - Rollback: **Instant Rollback** para o deployment anterior no painel do Vercel, ou
     `vercel rollback <url-do-deployment-anterior>`. As migrações só acrescentam; para voltar o
     banco, recrie o schema `consumo` (dados sintéticos).
   - Volta ao modo só card, na mesma URL: remova `Hospedagem__Modo` e as variáveis sensíveis do
     projeto e faça o Redeploy; a SOFIA continua encontrando o card.
   - Desligamento definitivo da homologação: volte ao modo só card, revogue a chave da OpenAI e
     apague a entrada `0.0.0.0/0` e o cluster do Atlas e o projeto do Neon.
   - Limpeza dos dados sem desligar: no Data Explorer do Atlas, apague os documentos das coleções;
     no Neon, `TRUNCATE consumo.chamadas_provedor` com o usuário da aplicação.

### 8.10 Cadastro no catálogo da SOFIA (FR-052h)

A chave autentica a SOFIA, mas não cadastra o agente: a SOFIA só descobre os agentes listados em
`A2A:Agents` da configuração central. O cadastro é feito pelo responsável pela SOFIA (professor);
este projeto entrega os dados e não altera a SOFIA. Objeto a entregar:

```json
{
  "IdAgente": "agente-auditoria-nfe",
  "ExpectedName": "AgenteAuditoriaNFe",
  "Enabled": true,
  "CardUri": "https://agente-auditoria-nfe.vercel.app/.well-known/agent-card.json",
  "AllowedDestinations": [
    "https://agente-auditoria-nfe.vercel.app/.well-known/agent-card.json",
    "https://agente-auditoria-nfe.vercel.app/a2a/auditoria-nfe"
  ],
  "AllowedAddresses": ["{ip-publico-validado-pelo-responsavel-da-SOFIA}"],
  "AllowedCapabilities": ["analisar_documentos"],
  "CredentialRef": "SOFIA_A2A_TOKEN",
  "DiscoveryCredentialRef": "SOFIA_A2A_TOKEN"
}
```

- `IdAgente` é o `A2A:IdAgente` e o `agente.idAgente` dos resultados; `ExpectedName` é o `name` do
  card. As duas referências de credencial são o **nome** da variável na SOFIA, não a chave.
- O Vercel não tem IP fixo: `AllowedAddresses` depende da política de destino da SOFIA para
  domínios com IP dinâmico, a validar com o responsável central (`Resolve-DnsName
  agente-auditoria-nfe.vercel.app` mostra os endereços do momento). Não desativar as proteções.
- O `message/send` só responde depois da Fase 8; até lá, a descoberta funciona e a execução
  recebe 404.

## 9. Validar a aprovação de recomendações sensíveis (TS-039 a TS-041)

1. Execute pelo canal direto (seção 6) um lote que gere recomendação sensível. Esperado: o parecer
   chega na hora, com a recomendação "pendente de aprovação" e sem conteúdo. Pelo A2A, o
   `ResultadoAnaliseSofia` só traz a observação "pendente de aprovação", nunca o conteúdo.
2. Liste as pendências e decida, informando o aprovador nos cabeçalhos (sem verificação na v1):

   ```powershell
   $aprovador = @{ 'X-Usuario-Id' = 'aprovador-01'; 'X-Chamador-Id' = 'sofia-simulador' }
   Invoke-RestMethod http://localhost:5310/api/v1/recomendacoes/pendentes -Headers $aprovador
   Invoke-RestMethod -Method Post http://localhost:5310/api/v1/recomendacoes/<id>/aprovacao -Headers $aprovador
   Invoke-RestMethod -Method Post http://localhost:5310/api/v1/recomendacoes/<id>/rejeicao -Headers $aprovador `
     -ContentType 'application/json' -Body '{ "justificativa": "Motivo da rejeição" }'
   ```

3. Consulte a execução pelo canal direto (o contrato SOFIA v1.0 não tem consulta de recomendações):

   ```powershell
   Invoke-RestMethod http://localhost:5310/api/v1/execucoes/<execucaoId> -Headers $cabecalhos
   ```

Esperado:

- Aprovada: o texto da recomendação é entregue. Rejeitada: o estado é entregue, nunca o texto.
- O arquivo `./data/aprovacoes/registros/<id>.json` contém os campos do FR-035f, com
  `aprovadorVerificado = false`, e nenhum conteúdo de XML nem texto da recomendação.
- Segunda decisão sobre a mesma recomendação: ERR-020. Decisão sem `X-Usuario-Id`: ERR-007.

## 10. Executar os testes

```powershell
dotnet test --project tests/AuditoriaNFe.Tests --filter-not-trait "Categoria=Avaliacao"
```

- Unidade (seleção de pacote, regras do catálogo, cálculos), aplicação, ferramentas, aprovação,
  contrato (A2A, OpenAPI, contrato do LLM), arquitetura e integração (MongoDB e PostgreSQL reais via
  Testcontainers; requer Docker).
- Os testes usam `IChatClient` simulado; não há chamadas pagas nem chave da OpenAI.

Conjunto de avaliação (SC-003, SC-004, SC-008), sob demanda e com OpenAI real:

```powershell
dotnet test --project tests/AuditoriaNFe.Tests --filter-trait "Categoria=Avaliacao"
```

Esperado: 0 respostas com valor financeiro não rastreável; resultado registrado conforme o P13 e
o P17.

## 11. Problemas comuns

| Sintoma | Causa provável | Ação |
| ------- | -------------- | ---- |
| `global.json` não encontra o SDK | SDK .NET 10 não instalado | Instalar o SDK 10.0.x |
| `docker compose up` falha com "Defina ... no arquivo .env" | Variável obrigatória ausente | Preencher o `.env` a partir do `.env.example` |
| Porta em uso | Outro projeto usando 5310, 27020 ou 5436 | `docker compose ps` nos outros projetos; ajustar a porta no compose e no `.env` |
| API não inicia: "autenticação desabilitada fora de Development" | `ASPNETCORE_ENVIRONMENT` diferente de `Development` | Usar o compose ou o perfil de desenvolvimento (D-05) |
| API não inicia: hash de schema divergente | XSD alterado ou convertido no fim de linha | Restaurar o arquivo do Git (`.gitattributes` marca os XSDs como `-text`) |
| `ready` não saudável | MongoDB ou PostgreSQL sem subir | `docker compose ps` e `docker compose logs` |
| Resultado "Inconclusiva" com ERR-014 | Chave ou modelo OpenAI inválido, ou provedor indisponível | Conferir `OpenAI__ApiKey` e `OpenAI__Modelo` |
| Recomendações somem após reiniciar | Volume de checkpoints não montado | Conferir `./data/aprovacoes` no compose |
| API não inicia: "Cors:AllowedOrigins contém uma origem inválida" | Origem com caminho, barra final, curinga ou sem esquema | Usar só esquema + host + porta, por exemplo `http://localhost:4200` |
| Navegador bloqueia a chamada por CORS | Origem do frontend fora de `Cors__AllowedOrigins__0` ou rota fora de `/api/v1` | Conferir a origem exata exibida no console do navegador; o Agent Card e o A2A não têm CORS |
| SOFIA em contêiner não alcança o agente | Uso de `localhost` entre contêineres ou redes diferentes | Rede `auditoria-nfe-net` e `http://api-agente:8080` (seção 8.6) |
| Vercel responde 502 ou o deploy não fica pronto | `PORT` ausente no projeto Vercel; o tráfego vai para a porta 80 | `vercel env add PORT` com `8080` e novo deploy (seção 8.8) |
| Card no Vercel com `url` de `localhost` | `A2A__UrlPublica` não cadastrada no ambiente do deploy | Cadastrar a URL do projeto em preview e produção e publicar de novo |
| A SOFIA recebe 404 ao chamar o agente no Vercel | O endpoint A2A só existe depois da Fase 8; no modo só card (D-08), nem a API existe | Usar o agente local para `message/send` até a Fase 8 ser publicada na homologação (seção 8.9) |
| Card ou `/a2a` respondem 401 | Requisição sem `Authorization: Bearer`, com outro esquema ou com chave diferente de `SOFIA_A2A_TOKEN` (a chave diferencia maiúsculas) | Enviar o cabeçalho com a chave da turma; na SOFIA, conferir `DiscoveryCredentialRef` e `CredentialRef` = `SOFIA_A2A_TOKEN` e o valor da variável (seção 8.10) |
| Agente não inicia: "SOFIA_A2A_TOKEN deve ser configurada" ou "não pode ter espaços" | Variável ausente, vazia ou com espaço ou quebra de linha nas pontas, nos modos `SomenteAgentCard` ou `Homologacao` | Cadastrar a variável sensível no projeto Vercel (ou no `docker run`) sem espaços e fazer o Redeploy (seção 8.8) |
| SOFIA marca o agente com `UNSUPPORTED_AGENT_SECURITY` | Card sem `securitySchemes` Bearer, publicado por uma versão anterior à Fase 13 | Publicar a versão atual na `main` (com a variável já cadastrada) |
| Vercel serve os arquivos do repositório e o card dá 404 | Preset "Other" do projeto prevaleceu sobre o `Dockerfile.vercel` | `"framework": "container"` no `vercel.json`, remover o deployment estático e publicar de novo |
| Deploy no Vercel fica "Blocked" | Autor do último commit não é a conta dona do projeto (plano Hobby) | `git config user.email` com o e-mail da conta e um commit novo |
| Card no Vercel abre a página de login | Domínio de preview ou alias da conta, protegidos pelo Vercel | Usar `https://<projeto>.vercel.app` ou `vercel curl` |
| Vercel responde 413 (`FUNCTION_PAYLOAD_TOO_LARGE`) em vez do ERR-002 | Corpo acima de 4,5 MB, barrado pela plataforma antes do agente | Lotes de até 4.000.000 bytes na homologação; conferir `Limites__TamanhoTotalBytes=4000000` e `Limites__CorpoHttpBytes=4500000` (seção 8.9) |
| Homologação não inicia: "Configuração inválida para Hospedagem:Modo = Homologacao (desvio D-09)" | Provedor simulado, migrações ligadas ou limites acima dos da homologação | Corrigir as variáveis listadas na mensagem (seção 8.9) e publicar de novo |
| Homologação com `ready` 503 | Atlas sem `0.0.0.0/0`, string do Neon no formato `postgresql://` ou senha com caracteres sem codificação na URL do Atlas | Conferir o Network Access e as duas connection strings (seção 8.9) |

## 12. Encerrar

```powershell
docker compose down        # mantém volumes
docker compose down -v     # remove volumes (dados de demonstração)
```
