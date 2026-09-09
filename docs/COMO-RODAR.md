# Como rodar o Age Nexus

## Pré-requisitos

Instale o [.NET SDK 9.0.203](https://dotnet.microsoft.com/download/dotnet/9.0) ou uma versão 9.0 compatível. O projeto usa o SDK 9 para compilar para o .NET 8 LTS.

Confirme a instalação:

```powershell
dotnet --version
```

O comando deve mostrar `9.0.203` ou outra versão aceita pelo arquivo `global.json`.

## 1. Acessar a pasta do projeto

No PowerShell:

```powershell
cd C:\caminho\para\AgeNexus
```

Em outro computador, substitua o caminho pela pasta onde o repositório foi clonado.

## 2. Restaurar as dependências

```powershell
dotnet restore AgeNexus.slnx
```

Esse comando baixa os pacotes necessários, incluindo as dependências usadas pelos testes.

## 3. Configurar o banco

O Age Nexus usa PostgreSQL no Supabase. Configure a connection string em User Secrets, sem gravar senha no repositório:

```powershell
dotnet user-secrets set "ConnectionStrings:AgeNexus" "Host=SEU_HOST;Port=5432;Database=postgres;Username=postgres;Password=SUA_SENHA;SSL Mode=Require;Trust Server Certificate=true" --project src/AgeNexus.Web
```

Para preparar cada ambiente, consulte [SUPABASE.md](SUPABASE.md).

Aplique as migrations:

```powershell
dotnet ef database update --project src/AgeNexus.Infrastructure --startup-project src/AgeNexus.Web
```

## 4. Compilar

```powershell
dotnet build AgeNexus.slnx --no-restore
```

Ao final, deve aparecer `Compilação com êxito` e zero erros.

## 5. Executar os testes

```powershell
dotnet test AgeNexus.slnx --no-build --no-restore
```

Os testes atuais validam as primeiras regras do domínio, incluindo partidas com equipes assimétricas, humanos contra IA e perfis históricos.

## 6. Iniciar a aplicação

```powershell
dotnet run --project src/AgeNexus.Web
```

O terminal mostrará o endereço local:

```text
http://localhost:5186
```

Abra no navegador exatamente o endereço exibido. A página inicial mostrará o dashboard vazio, pronto para receber dados reais.

Os fluxos de conta ficam em:

- `/conta/criar` — criação de conta exclusivamente pelo Google;
- `/conta/login` — entrada exclusivamente pelo Google;
- `/conta/vincular-perfil` — escolha de um nick histórico ou criação de um novo jogador;
- `/perfil` — personalização de nick, localização, foto, civilização favorita e bio;
- `/jogadores/{id}` — perfil público, histórico, civilizações usadas e recordes relevantes.

## Autenticação exclusiva com Google

Crie um cliente OAuth do tipo **Aplicativo da Web** no Google Cloud. Cadastre como URI de redirecionamento:

```text
http://localhost:5186/signin-google
```

Se a porta local for alterada, atualize também a URI no Google. Armazene as credenciais em User Secrets:

```powershell
dotnet user-secrets set "Authentication:Google:ClientId" "SEU_CLIENT_ID" --project src/AgeNexus.Web
dotnet user-secrets set "Authentication:Google:ClientSecret" "SEU_CLIENT_SECRET" --project src/AgeNexus.Web
```

No Render, cadastre as variáveis `Authentication__Google__ClientId`, `Authentication__Google__ClientSecret` e `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`. A URI autorizada no Google será:

```text
https://SEU_DOMINIO/signin-google
```

O sufixo `/signin-google` pertence ao middleware OAuth e não deve ser alterado. As credenciais nunca devem ser gravadas no `appsettings.json` nem enviadas ao Git.

O Age Nexus não expõe cadastro nem login por e-mail e senha. As páginas `/conta/criar` e `/conta/login` iniciam o mesmo fluxo seguro do Google; sem as duas credenciais OAuth configuradas, nenhum novo acesso poderá ser concluído.

Novas contas Google podem entrar como jogadores, mas não recebem permissão administrativa. Se o nick já existir no histórico, o jogador solicita a vinculação e a conta administradora aprova em `/jogadores/gerenciar`. Se o nick ainda não existir, ele pode criar um perfil novo. A ordem de cadastro não concede privilégios. Em uma instalação sem administrador, configure `Security__BootstrapAdministratorEmail` com o e-mail exato do responsável (em User Secrets: `Security:BootstrapAdministratorEmail`). A conta precisa ter e-mail confirmado. Após a atribuição, remova essa configuração de bootstrap; administradores existentes permanecem cadastrados.

Fotos de perfil usam o mesmo Storage já configurado para evidências. O servidor aceita JPEG, PNG ou WebP de até 2 MB e grava no banco somente a URL pública, nunca o arquivo dentro do PostgreSQL.

Para encerrar a aplicação, pressione `Ctrl+C` no terminal.

## Fluxo rápido

Depois da primeira restauração, o fluxo cotidiano pode ser executado com:

```powershell
dotnet build AgeNexus.slnx
dotnet test AgeNexus.slnx --no-build
dotnet run --project src/AgeNexus.Web --no-build
```

## Problemas comuns

### O SDK solicitado não foi encontrado

Execute:

```powershell
dotnet --list-sdks
```

Se nenhuma versão 9.0 estiver listada, instale o SDK indicado nos pré-requisitos.

### Falha ao acessar o NuGet

Verifique a conexão com a internet, proxy ou firewall e tente novamente:

```powershell
dotnet restore AgeNexus.slnx
```

### A porta já está em uso

Escolha outra porta:

```powershell
dotnet run --project src/AgeNexus.Web --urls http://localhost:5080
```

Depois, acesse `http://localhost:5080`.

## Estado atual

A aplicação possui interface Blazor, persistência com EF Core/PostgreSQL e autenticação por ASP.NET Core Identity. As tabelas de credenciais ficam no schema privado `identity`; o perfil público é armazenado em `public.player_profiles`.
