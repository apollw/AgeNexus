# Supabase no Age Nexus

## Ambiente hospedado

Nome da organização, identificador do projeto, região, endereço do banco e credenciais devem permanecer apenas no painel do provedor e nos cofres de segredos de cada ambiente. O repositório não registra esses dados.

A senha e a connection string ficam no .NET User Secrets durante o desenvolvimento e nas variáveis secretas da hospedagem em produção.

## Configuração local

Os arquivos versionáveis do Supabase estão em `supabase/`:

- `config.toml`: portas, autenticação e serviços locais;
- `migrations/`: futuras migrações SQL do banco;
- `seed.sql`: dados reproduzíveis para desenvolvimento.

As URLs locais de autenticação apontam para o front em `http://localhost:5186`.

## Pré-requisitos para executar o Supabase localmente

- Node.js 20 ou superior;
- Docker Desktop ou outro runtime compatível com a API do Docker;
- Supabase CLI executada com `npx`.

O Docker ainda precisa ser instalado nesta máquina para subir a stack local completa.

## Comandos úteis

```powershell
# Iniciar Postgres, Auth, Storage e Studio localmente
npx.cmd --yes supabase@latest start

# Abrir informações e credenciais da stack local
npx.cmd --yes supabase@latest status

# Parar a stack local sem apagar os dados
npx.cmd --yes supabase@latest stop

# Recriar o banco local aplicando migrações e seed
npx.cmd --yes supabase@latest db reset

```

O Studio local fica em <http://localhost:54323> quando a stack está em execução.

## Segredos de desenvolvimento

Para conferir apenas os nomes configurados no projeto Web:

```powershell
dotnet user-secrets list --project src/AgeNexus.Web
```

Esse comando também exibe os valores. Não compartilhe sua saída e nunca copie esses dados para commits, issues ou logs.

Caso a senha do banco seja redefinida no Dashboard, atualize os segredos locais:

```powershell
dotnet user-secrets set "Supabase:DatabasePassword" "NOVA_SENHA" --project src/AgeNexus.Web
dotnet user-secrets set "ConnectionStrings:AgeNexus" "NOVA_CONNECTION_STRING" --project src/AgeNexus.Web
```

## Decisão de arquitetura

Nesta fase, Supabase fornece PostgreSQL gerenciado e poderá fornecer armazenamento de evidências. O domínio e os casos de uso continuam em ASP.NET Core; nenhuma regra competitiva será colocada em triggers ou componentes Blazor. A decisão entre ASP.NET Core Identity e Supabase Auth será registrada antes da implementação de autenticação.

O schema da aplicação é controlado exclusivamente pelas [migrações do EF Core](EF-CORE.md). Não use `supabase db push` para alterar essas tabelas, pois isso criaria um segundo histórico de migrações concorrente.

Referências oficiais: [CLI e desenvolvimento local](https://supabase.com/docs/guides/local-development/cli/getting-started), [fluxo de migrações](https://supabase.com/docs/guides/local-development/cli-workflows) e [conexões PostgreSQL](https://supabase.com/docs/guides/database/connecting-to-postgres).
