-- Homologação pública (desvio D-09, ADR-0013, quickstart §8.9): permissões do Api.Agente no banco
-- auditoria_agente do Neon. Executar no SQL Editor do Neon (ou em psql) com o usuário dono do banco,
-- depois de criar o usuário da aplicação por SQL; usuários criados pelo painel do Neon recebem
-- neon_superuser e são recusados. Sem meta-comandos do psql; reexecutável, num único bloco.
-- As tabelas vêm do script das migrações do EF Core (dotnet ef migrations script --idempotent),
-- executado com o usuário da aplicação, que fica dono delas.

DO $$
DECLARE
  -- Troque se o usuário da aplicação tiver outro nome.
  usuario_app CONSTANT text := 'auditoria_app';
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = usuario_app) THEN
    RAISE EXCEPTION 'Crie antes o usuário % por SQL (quickstart §8.9).', usuario_app;
  END IF;

  IF EXISTS (
      SELECT 1 FROM pg_roles
      WHERE rolname = usuario_app AND (rolsuper OR rolcreaterole OR rolcreatedb OR rolreplication OR rolbypassrls))
    OR EXISTS (
      SELECT 1
      FROM pg_auth_members m
      JOIN pg_roles grupo ON grupo.oid = m.roleid
      JOIN pg_roles membro ON membro.oid = m.member
      WHERE membro.rolname = usuario_app AND grupo.rolname = 'neon_superuser') THEN
    RAISE EXCEPTION 'O usuário % tem privilégios administrativos; crie-o por SQL, e não pelo painel do Neon.', usuario_app;
  END IF;

  -- Só o usuário do Api.Agente conecta a este banco; nada é criado no schema public.
  EXECUTE format('REVOKE ALL ON DATABASE %I FROM PUBLIC', current_database());
  EXECUTE format('GRANT CONNECT, TEMPORARY ON DATABASE %I TO %I', current_database(), usuario_app);
  REVOKE ALL ON SCHEMA public FROM PUBLIC;

  -- O schema fica com o dono do banco; o usuário da aplicação cria e usa as tabelas dentro dele.
  CREATE SCHEMA IF NOT EXISTS consumo;
  EXECUTE format('GRANT USAGE, CREATE ON SCHEMA consumo TO %I', usuario_app);
  EXECUTE format('ALTER ROLE %I IN DATABASE %I SET search_path = consumo', usuario_app, current_database());

  COMMENT ON SCHEMA consumo IS 'Consumo e custos das chamadas ao provedor de IA do Api.Agente (data-model §14).';
END
$$;
