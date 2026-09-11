-- Read-only diagnostic. Run in the Supabase SQL Editor.
-- Returns metadata/permissions only; does not read account or player records.
SELECT n.nspname AS schema_name, c.relname AS object_name,
       r.rolname AS api_role,
       c.relrowsecurity AS rls_enabled,
       c.relforcerowsecurity AS rls_forced,
       has_schema_privilege(r.oid, n.oid, 'USAGE') AS schema_usage,
       has_table_privilege(r.oid, c.oid, 'SELECT') AS can_select,
       has_table_privilege(r.oid, c.oid, 'INSERT') AS can_insert,
       has_table_privilege(r.oid, c.oid, 'UPDATE') AS can_update,
       has_table_privilege(r.oid, c.oid, 'DELETE') AS can_delete
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
CROSS JOIN pg_roles r
WHERE n.nspname IN ('public', 'identity')
  AND c.relkind IN ('r', 'p', 'v', 'm')
  AND r.rolname IN ('anon', 'authenticated')
ORDER BY n.nspname, c.relname, r.rolname;

SELECT schemaname, tablename, policyname, roles, cmd, qual, with_check
FROM pg_policies
WHERE schemaname IN ('public', 'identity')
ORDER BY schemaname, tablename, policyname;
