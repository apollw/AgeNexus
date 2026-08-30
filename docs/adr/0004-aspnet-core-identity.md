# ADR 0004: usar ASP.NET Core Identity

- Status: aceito
- Data: 2026-08-30

## Decisão

Age Nexus usará ASP.NET Core Identity com cookies e stores do EF Core. As tabelas de segurança ficam no schema privado `identity` do mesmo PostgreSQL hospedado no Supabase. O Supabase Auth não será usado nesta fase.

`ApplicationUser` representa credenciais e segurança. `PlayerProfile` continua sendo a identidade pública e pode existir sem conta; quando uma conta é criada, um perfil é vinculado pelo identificador do usuário.

## Consequências

O domínio preserva a distinção entre conta e jogador histórico. Senhas são processadas exclusivamente pelo Identity e nunca armazenadas pela aplicação. Uma integração futura com provedores externos poderá ser adicionada pelos mecanismos do ASP.NET Core Identity sem alterar partidas.
