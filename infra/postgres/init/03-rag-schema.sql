-- Schema vetorial da Api.RAG (feature 002). Executado na primeira subida do volume PostgreSQL.
-- O usuário da aplicação é o mesmo do Api.Agente (ADR-0006); o search_path padrão dele continua consumo.

\set ON_ERROR_STOP on
\getenv usuario_app POSTGRES_APP_USER
\getenv senha_app POSTGRES_APP_PASSWORD
\if :{?usuario_app}
\else
  \set usuario_app ''
\endif
\if :{?senha_app}
\else
  \set senha_app ''
\endif

SELECT coalesce(:'usuario_app', '') = '' OR coalesce(:'senha_app', '') = '' AS faltando_credenciais \gset
\if :faltando_credenciais
  DO $$ BEGIN RAISE EXCEPTION 'Defina POSTGRES_APP_USER e POSTGRES_APP_PASSWORD no arquivo .env'; END $$;
\endif

CREATE EXTENSION IF NOT EXISTS vector;

CREATE SCHEMA IF NOT EXISTS rag AUTHORIZATION :"usuario_app";

GRANT USAGE, CREATE ON SCHEMA rag TO :"usuario_app";

COMMENT ON SCHEMA rag IS 'Base de conhecimento vetorial da Api.RAG (feature 002-rag-pgvector).';
