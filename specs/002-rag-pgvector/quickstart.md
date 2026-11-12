# Quickstart: validar base de conhecimento (feature 002)

**Pré-requisitos**: Docker, `.env` com Postgres, `Rag__ChaveAdministracao` e `OpenAI__ApiKey` para
embeddings (ou modo simulado se implementado nos testes).

## 1. Subir stack local

```powershell
docker compose up -d postgres api-rag api-agente
```

Confirme `api-rag` em `http://127.0.0.1:5320/health/live`.

## 2. Aplicar schema `rag`

Após implementar a feature: migrações EF da Api.RAG ou script `infra/postgres/init/03-rag-schema.sql`.

## 3. Ingerir base operacional ou fixture

Pacotes JSON prontos: `data/rag-ingestao/` (ver README da pasta). Para teste mínimo, use a fixture:

```powershell
$headers = @{ Authorization = "Bearer $env:RAG_ADMIN_KEY" }
$body = Get-Content "tests/AuditoriaNFe.Tests/Fixtures/Rag/documento-sintetico.v1.json" -Raw
Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:5320/api/v1/rag/ingestao" -Headers $headers -Body $body -ContentType "application/json"
```

## 4. Testar MCP (agente)

No `.env` do compose: `Mcp__Habilitado=true`, `Mcp__Endpoint=http://api-rag:8080/mcp`.

Envie execução com XML sintético via Swagger do agente (`5310`) e verifique etapa 5 `Executada` com
trecho do `chunkId` ingerido.

## 5. Testes automatizados

```powershell
dotnet test tests/AuditoriaNFe.Tests/AuditoriaNFe.Tests.csproj --filter "FullyQualifiedName~Rag"
```

## Homologação

1. Deploy da Api.RAG com Postgres Neon (schema `rag`, extensão `vector`).
2. Ingestão admin apenas com chave; agente na Vercel com `Mcp__Habilitado` + URL da RAG.
3. Não esperar ingestão só com merge na `main` — é operação REST autenticada na RAG.
