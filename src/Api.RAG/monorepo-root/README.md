# Raiz do monorepo no build Vercel

O projeto `api-rag-auditoria-nfe` usa **Root Directory** `src/Api.RAG`. O contexto Docker fica só nesta pasta, então `global.json`, `Directory.Build.props` e `Directory.Packages.props` são copiados aqui para o `COPY` do `Dockerfile.vercel`.

Ao alterar esses arquivos na raiz do repositório, atualize as cópias nesta pasta antes do merge.
