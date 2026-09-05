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

O Supabase fornece PostgreSQL gerenciado e Storage para as capturas usadas como evidência. O domínio e os casos de uso continuam em ASP.NET Core; nenhuma regra competitiva é colocada em triggers ou componentes Blazor.

## Capturas de partidas no Storage

O backend cria o bucket público `match-evidence` no primeiro envio, caso ele ainda não exista. O bucket aceita somente JPEG, PNG e WebP de até 4 MB. A leitura é pública para permitir a galeria das partidas; criação e remoção são feitas exclusivamente pelo backend autenticado usando a chave de serviço.

No Render, configure:

| Variável | Valor |
| --- | --- |
| `Supabase__Url` | URL HTTPS do projeto exibida nas configurações de API |
| `Supabase__ServiceRoleKey` | chave `service_role`, armazenada como segredo |
| `Supabase__EvidenceBucket` | `match-evidence` |

Para desenvolvimento local, use User Secrets:

```powershell
dotnet user-secrets set "Supabase:Url" "https://SEU_PROJETO.supabase.co" --project src/AgeNexus.Web
dotnet user-secrets set "Supabase:ServiceRoleKey" "SUA_CHAVE_DE_SERVICO" --project src/AgeNexus.Web
dotnet user-secrets set "Supabase:EvidenceBucket" "match-evidence" --project src/AgeNexus.Web
```

A chave `service_role` ignora RLS e nunca deve ser exposta no navegador, enviada em conversa, registrada em logs ou adicionada ao Git. A aplicação envia as imagens ao Storage pelo servidor e grava apenas o caminho, hash e vínculo com a partida no PostgreSQL.

O schema da aplicação é controlado exclusivamente pelas [migrações do EF Core](EF-CORE.md). Não use `supabase db push` para alterar essas tabelas, pois isso criaria um segundo histórico de migrações concorrente.

Referências oficiais: [CLI e desenvolvimento local](https://supabase.com/docs/guides/local-development/cli/getting-started), [fluxo de migrações](https://supabase.com/docs/guides/local-development/cli-workflows) e [conexões PostgreSQL](https://supabase.com/docs/guides/database/connecting-to-postgres).
