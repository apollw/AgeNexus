# Publicação no Render

O Age Nexus é publicado como um Web Service Docker no plano gratuito do Render e continua usando o PostgreSQL existente no Supabase.

## Antes de criar o serviço

No painel do Supabase, abra **Connect** e copie a connection string do **Session pooler**, porta `5432`. O Render não alcança a conexão direta IPv6 do Supabase sem o add-on de IPv4.

Converta os dados para o formato aceito pelo Npgsql:

```text
Host=HOST_DO_SESSION_POOLER;Port=5432;Database=postgres;Username=postgres.PROJECT_REF;Password=SUA_SENHA;SSL Mode=Require;Trust Server Certificate=true
```

Em desenvolvimento, o schema pode ser atualizado manualmente:

```powershell
dotnet ef database update --project src/AgeNexus.Infrastructure --startup-project src/AgeNexus.Web
```

Em produção, a aplicação executa somente as migrations pendentes uma vez durante a inicialização. Isso mantém o banco compatível com a versão publicada sem criar consultas periódicas.

## Criar pelo Blueprint

1. Entre em <https://dashboard.render.com> usando o GitHub.
2. Escolha **New > Blueprint**.
3. Selecione o repositório do Age Nexus.
4. O Render encontrará o arquivo `render.yaml` e solicitará os valores secretos.

| Variável | Valor |
| --- | --- |
| `ConnectionStrings__AgeNexus` | Connection string Npgsql do Session pooler |
| `Authentication__Google__ClientId` | Client ID OAuth atual |
| `Authentication__Google__ClientSecret` | Client secret OAuth armazenado somente como segredo |
| `OperatingMode__AllowGooglePlayerLogin` | `true` para permitir novas contas Google de jogadores |
| `Supabase__Url` | URL HTTPS do projeto Supabase |
| `Supabase__ServiceRoleKey` | chave de serviço do Supabase, armazenada somente como segredo |

`Supabase__EvidenceBucket=match-evidence` já possui valor no Blueprint. Essas três configurações habilitam o envio de capturas e replays; sem elas, o restante do site e os links do YouTube continuam funcionando normalmente.

Confirme a criação. O primeiro build publica o Blazor em .NET 8 e inicia a aplicação na porta fornecida pelo Render. O Render verifica a disponibilidade do processo em `/health`, sem abrir conexões periódicas com o banco.

O diagnóstico manual `/health/database` continua disponível para confirmar a conexão com o PostgreSQL quando necessário.

## Liberar o login Google no endereço público

Depois que o Render informar o domínio, abra o cliente OAuth no Google Cloud e adicione exatamente:

```text
https://SEU_DOMINIO.onrender.com/signin-google
```

Não remova a URI local enquanto ainda quiser executar o projeto no computador.

## Contas e permissões

A autenticação é exclusivamente pelo Google; não existem formulários nem endpoints de cadastro ou login por senha. `AllowGooglePlayerLogin` controla a entrada de novas contas Google. A conta mais antiga é promovida uma única vez à função `Administrator`; somente ela registra ou exclui partidas, altera estatísticas, envia evidências e gerencia participantes. O modo `SingleAdministrator` continua controlando os fluxos competitivos que dependem dessa administração centralizada.

Cada novo jogador escolhe um nick histórico para solicitar vinculação ou cria um perfil novo. Solicitações aparecem em `/jogadores/gerenciar` e só transferem o histórico após aprovação do administrador. Perfis manuais não vinculados continuam disponíveis para partidas futuras.

Depois do primeiro deploy desta versão, o administrador deve sair e entrar novamente para que o cookie inclua a nova função. Os demais usuários podem editar somente o próprio nick, foto, localização, civilização favorita e bio.

O plano gratuito pode suspender o serviço sem tráfego. O primeiro acesso depois da suspensão pode demorar, e os usuários autenticados podem precisar entrar novamente após reinicializações ou novos deploys.

## Região e latência

Mantenha aplicação e banco na menor distância de rede oferecida pelos provedores. A localização do banco não deve ser documentada no repositório; confira-a no painel privado e compare a latência antes de alterar a região declarada no `render.yaml`. Se não houver uma região gratuita mais próxima, preserve a configuração atual para evitar migrações sem ganho comprovado.
