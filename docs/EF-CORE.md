# EF Core e PostgreSQL

O EF Core é a fonte de verdade do schema relacional do Age Nexus. As migrações ficam em `src/AgeNexus.Infrastructure/Persistence/Migrations` e devem ser versionadas.

## Componentes

- `AgeNexusDbContext`: unidade de trabalho do banco;
- configurações Fluent API: tabelas, colunas, relacionamentos, índices e checks;
- Npgsql: provider PostgreSQL do EF Core;
- `dotnet-ef`: ferramenta local fixada no manifesto `.config/dotnet-tools.json`.

## Entidades persistidas na fundação

- `PlayerProfile` → `player_profiles`;
- `AiDifficulty` → `ai_difficulties`;
- `Match` → `matches`;
- `MatchTeam` → `match_teams`;
- `MatchParticipant` → `match_participants`.

As coleções privadas dos agregados usam acesso por campo. Isso permite ao EF materializar equipes e participantes sem tornar as coleções mutáveis para o restante da aplicação.

## Configurar a conexão

Copie a connection string no botão **Connect** do Dashboard do Supabase e salve-a fora do repositório:

```powershell
dotnet user-secrets set "ConnectionStrings:AgeNexus" "CONNECTION_STRING" --project src/AgeNexus.Web
```

O projeto falha imediatamente com uma mensagem clara quando a configuração não existe.

## Restaurar a ferramenta

```powershell
dotnet tool restore
```

## Criar uma migração

Execute na raiz do repositório:

```powershell
dotnet ef migrations add NomeDaMigracao `
  --project src/AgeNexus.Infrastructure `
  --startup-project src/AgeNexus.Web `
  --output-dir Persistence/Migrations
```

Revise o arquivo gerado antes de aplicá-lo. Migrações aplicadas a ambientes compartilhados não devem ser removidas; correções devem entrar em uma nova migração.

## Verificar alterações não migradas

```powershell
dotnet ef migrations has-pending-model-changes `
  --project src/AgeNexus.Infrastructure `
  --startup-project src/AgeNexus.Web
```

## Aplicar migrações

```powershell
dotnet ef database update `
  --project src/AgeNexus.Infrastructure `
  --startup-project src/AgeNexus.Web
```

Não use `EnsureCreated`, pois ele ignora o histórico incremental de migrações.

## Segurança no Supabase

As tabelas ficam no schema `public`, que é exposto pela Data API. A migração `SecureSupabasePublicTables` habilita RLS e remove todos os privilégios dos papéis `anon` e `authenticated`. O acesso atual ocorre somente pelo backend ASP.NET usando a conexão PostgreSQL.

Quando o método de autenticação for definido, novas migrações poderão conceder operações específicas e criar políticas RLS explícitas. Não libere acesso genérico às tabelas competitivas.

## Verificação da conexão

Com a aplicação em execução, acesse:

```text
http://localhost:5186/health/database
```

Uma conexão funcional responde com HTTP 200 e `{"status":"healthy","database":"postgresql"}`.

O endpoint `/health` verifica somente se o processo web está respondendo e deve ser usado pela hospedagem. Assim, as verificações periódicas não abrem conexões desnecessárias com o banco.
