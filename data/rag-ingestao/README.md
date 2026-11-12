# Pacotes de ingestão (fonte → base RAG)

Estes arquivos **não são** a base de conhecimento. Eles são **cargas prontas** para popular o
PostgreSQL (PgVector) via API. Após ingestão, a busca usa somente o que está no banco.

**Pré-requisitos**: `Rag__ModoRecuperacao=Vetorial`, `OpenAI__ApiKey` na Api.RAG, Bearer admin.

## Arquivos

| JSON | Etapa 5 / perguntas |
| --- | --- |
| `moc-anexo-i-totalizadores-icms.v1.json` | Totalizadores ICMSTot (regras W) |
| `nt-2023-004-w16-vnf.v1.json` | Valor total da NF (`vNF`, W16-10) |
| `nt-2025-002-ibs-cbs-is.v1.json` | IBS/CBS/IS a partir de 2025-11-10 |

PDFs oficiais: use `POST /api/v1/rag/ingestao/pdf` (ver contrato em
`specs/002-rag-pgvector/contracts/api-rag-ingestao.md`). Lista de PDFs em
`docs/notas-tecnicas/inventario.csv`.

## Ingerir todos os JSON (PowerShell)

```powershell
$uri = "http://127.0.0.1:5320/api/v1/rag/ingestao"
$headers = @{ Authorization = "Bearer $env:RAG_CHAVE_ADMINISTRACAO" }
Get-ChildItem "$PSScriptRoot\*.json" | ForEach-Object {
    $body = Get-Content $_.FullName -Raw -Encoding UTF8
    Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -Body $body -ContentType "application/json; charset=utf-8"
    Write-Host "Ingerido: $($_.Name)"
}
```

## Agente

Com `Mcp__Habilitado=true`, a etapa 5 consulta a base na auditoria e em perguntas no escopo NF-e
modelo 55 (sem XML usa a data UTC atual como referência).
