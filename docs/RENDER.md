# Publicação no Render

O Age Nexus é publicado como um Web Service Docker no plano gratuito do Render e continua usando o PostgreSQL existente no Supabase.

## Antes de criar o serviço

No painel do Supabase, abra **Connect** e copie a connection string do **Session pooler**, porta `5432`. O Render não alcança a conexão direta IPv6 do Supabase sem o add-on de IPv4.

Converta os dados para o formato aceito pelo Npgsql:

```text
Host=HOST_DO_SESSION_POOLER;Port=5432;Database=postgres;Username=postgres.PROJECT_REF;Password=SUA_SENHA;SSL Mode=Require;Trust Server Certificate=true
```

O schema já deve estar atualizado pelas migrations aplicadas localmente antes da publicação:

```powershell
dotnet ef database update --project src/AgeNexus.Infrastructure --startup-project src/AgeNexus.Web
```

## Criar pelo Blueprint

1. Entre em <https://dashboard.render.com> usando o GitHub.
2. Escolha **New > Blueprint**.
3. Selecione o repositório do Age Nexus.
4. O Render encontrará o arquivo `render.yaml` e solicitará os três valores secretos.

| Variável | Valor |
| --- | --- |
| `ConnectionStrings__AgeNexus` | Connection string Npgsql do Session pooler |
| `Authentication__Google__ClientId` | Client ID OAuth atual |
| `Authentication__Google__ClientSecret` | Client secret OAuth armazenado somente como segredo |

Confirme a criação. O primeiro build instala .NET 8, Python e `mgz`, publica o Blazor e inicia a aplicação na porta fornecida pelo Render. O Render verifica a disponibilidade do processo em `/health`, sem abrir conexões periódicas com o banco.

O diagnóstico manual `/health/database` continua disponível para confirmar a conexão com o PostgreSQL quando necessário.

## Liberar o login Google no endereço público

Depois que o Render informar o domínio, abra o cliente OAuth no Google Cloud e adicione exatamente:

```text
https://SEU_DOMINIO.onrender.com/signin-google
```

Não remova a URI local enquanto ainda quiser executar o projeto no computador.

## Funcionamento desta primeira versão

O modo `SingleAdministrator` permanece ativo. Somente a conta administradora existente pode entrar e alterar dados. Os demais usuários acessam publicamente partidas, jogadores, rankings e estatísticas.

O plano gratuito pode suspender o serviço sem tráfego. O primeiro acesso depois da suspensão pode demorar, e os usuários autenticados podem precisar entrar novamente após reinicializações ou novos deploys.

## Região e latência

Mantenha aplicação e banco na menor distância de rede oferecida pelos provedores. A localização do banco não deve ser documentada no repositório; confira-a no painel privado e compare a latência antes de alterar a região declarada no `render.yaml`. Se não houver uma região gratuita mais próxima, preserve a configuração atual para evitar migrações sem ganho comprovado.
