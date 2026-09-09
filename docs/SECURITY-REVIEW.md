# Revisão de segurança do AgeNexus — 2026-09-09

Base examinada: `382cb217417cd5f6d3bf56a3686db0e9f601dcc1` (master, PR #27).
Revisão de código e configuração versionada, sem acesso ao banco de produção, às variáveis do Render, às políticas reais do Supabase ou ao console Google. Não é uma certificação nem um teste de invasão completo.

## Achados e correções

| Área | Achado | Tratamento |
| --- | --- | --- |
| Privilégios | Na ausência de administrador, a conta mais antiga era promovida automaticamente. Risco alto em instalações novas/restauradas sem administrador; não prova tomada de conta na instalação atual. | Bootstrap exige e-mail explicitamente configurado e confirmado. Administradores existentes permanecem. Remover a configuração após uso. |
| Sessões Blazor | Provider lia HttpContext sem revalidar periodicamente o estado de uma conexão aberta. | Provider próprio do servidor com revalidação a cada 5 minutos de existência, bloqueio, security stamp e funções. |
| Ações em página aberta | Finalização de estatísticas e configuração de catálogo dependiam da autorização de rota. | Conferência adicional da função no banco no momento dessas ações. Demais escritas administrativas já conferem autorização em seus serviços. |
| Cookies | Cookie de autenticação usava SameAsRequest também em produção. | Secure obrigatório em produção para autenticação e antifalsificação; desenvolvimento HTTP continua possível. |
| Navegador | Ausência de cabeçalhos explícitos adicionais contra enquadramento e interpretação de conteúdo. | nosniff, SAMEORIGIN, política de referrer e CSP restrita a frame-ancestors/object-src/base-uri. Não é uma CSP completa de scripts. |
| Contêiner | Imagem executava como root. | Usa APP_UID da imagem oficial .NET. |
| Segredos locais | Arquivos .env e certificados não tinham exclusão explícita. | Exclusões em Git e no contexto Docker. Isso não remove segredos já publicados. |
| Dependências | CI não exigia auditoria explícita das dependências transitivas. | Auditoria NuGet em JSON com falha em vulnerabilidades ou diagnósticos de auditoria incompleta. |

## Controles examinados

- Login exclusivamente Google; não há endpoint de senha local. O código verifica email_verified antes de cadastrar/vincular por e-mail e limita a vinculação automática de conta existente a e-mails Gmail ou domínio hospedado informado pelo Google.
- O identificador do provedor é usado para logins subsequentes. A senha Google não chega ao aplicativo; não há persistência explícita de tokens Google nem escopos adicionais para Gmail/Drive.
- O banco guarda e-mail, ID externo, provedor e metadados locais. Nome/foto Google não são copiados automaticamente para o perfil público.
- Formulários HTTP de login, logout, atualização de perfil e exclusão de partida validam antiforgery. Redirecionamentos usam caminhos locais.
- Vinculação histórica depende do administrador; a solicitação não transfere imediatamente o histórico. Alteração de perfil e avatar usa ID do usuário autenticado, não um ID de proprietário recebido do formulário.
- Páginas de criação de partidas, estatísticas e gestão de jogadores exigem Administrator. Serviços verificam administrador para registro/exclusão, estatísticas editáveis e evidências. Métodos de serviço internos de finalização também são usados pelo CLI e não devem ser expostos como endpoints sem autorização.
- Consultas usam EF/LINQ; não foram encontradas construções de SQL bruto ou execução de processos com entrada de usuário. Razor mantém codificação de texto; não foi encontrado uso de MarkupString/innerHTML para dados dos usuários.
- Uploads de avatar: até 2 MiB; capturas: até 4 MiB e 5 itens; replays: até 50 MiB, extensões restritas, acesso de escrita administrativo. Imagens validam assinatura e extensão; isso não equivale a decodificação completa ou antivírus. Replays são downloads binários, sem execução no servidor.
- Storage usa URL HTTPS configurada no servidor e chave service-role no backend. Objetos usam chaves geradas, não caminhos arbitrários do usuário. Evidências são públicas por projeto: não devem conter material privado.
- Perfis públicos usam projeções de dados do jogador; e-mail e ProviderKey não aparecem nessas projeções. Bio, localização e avatar escolhidos pelo jogador são públicos. Histórico interno inclui ID da conta criadora, mas não foi encontrado uso desse campo na renderização pública.
- Produção usa tratamento genérico de erros, HSTS e redirecionamento HTTPS. Endpoint público /health/database permite consultas ao banco; não expõe credenciais, mas merece controle de frequência se houver abuso.

## Verificações

- Busca de padrões de alta confiança em arquivos atuais e 611 blobs do histórico local: nenhum GOCSPX, token GitHub, chave privada PEM ou JWT detectado. Busca heurística, não prova ausência de senhas ou outros segredos; não cobre referências que não estejam no clone local.
- Testes novos cobrem bootstrap, preservação de administrador existente, aprovação de vínculo, isolamento de edição de perfil, principal sem autenticação e invalidação de sessão por alteração de stamp/função/bloqueio/exclusão.
- Build, suíte e auditoria NuGet: resultados serão registrados após execução do CI. O ambiente local não possui dotnet disponível.
- Acesso HTTP de produção não pôde ser concluído neste ambiente (restrição/timeout de rede). Não foi possível verificar headers reais, cookie autenticado, HTTPS efetivo, rotas com conta comum/admin ou se o deploy corresponde à versão revisada.

## Riscos e verificações de infraestrutura ainda abertos

1. Render: conferir variáveis, segredo OAuth, modo Production, versão publicada e redes/proxies confiáveis. ASPNETCORE_FORWARDEDHEADERS_ENABLED não deve ser interpretado como validação de qualquer proxy. AllowedHosts está amplo na configuração versionada; restringir aos hosts efetivamente usados exige confirmar domínio e health checks reais.
2. Supabase/PostgreSQL: conferir políticas RLS e privilégios de anon/authenticated, exposição de schemas, segurança da service-role, acesso externo, TLS da conexão, backups e recuperação. Não se deduzem da configuração do aplicativo.
3. Google: conferir URIs autorizadas, escopos no consentimento, contas com acesso ao projeto e proteção da conta administradora. Remover permissões no Google não equivale a revogar instantaneamente um cookie já emitido pelo AgeNexus.
4. Sessões: revalidação tem janela de até 5 minutos, não revogação instantânea. Logout encerra o navegador atual; não existe fluxo de encerrar todas as sessões. Chaves Data Protection de produção são efêmeras na configuração versionada, causando logout após reinício; persistência futura deve proteger as chaves.
5. Abuso/privacidade: não há rate limiting explícito para criação de contas, solicitações de vínculo, leitura de imagens e health/database. Solicitação pendente reserva um perfil até cancelamento/rejeição. Imagens não têm metadados removidos; uploads não têm antivírus. Retenção/exclusão de contas precisa de política e fluxo próprios.
6. Não foram feitos testes destrutivos, carga, exploração de produção, acesso à conta Google de usuários ou leitura do banco real. Não há evidência, nesta revisão, de invasão ou comprometimento da conta Google.

## Referências técnicas

- https://learn.microsoft.com/aspnet/core/blazor/security/?view=aspnetcore-8.0
- https://learn.microsoft.com/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-8.0
- https://support.google.com/accounts/answer/12921417
