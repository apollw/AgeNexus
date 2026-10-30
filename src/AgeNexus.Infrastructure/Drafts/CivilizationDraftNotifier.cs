using AgeNexus.Application.Drafts;
using Microsoft.Extensions.Logging;

namespace AgeNexus.Infrastructure.Drafts;

public sealed class CivilizationDraftNotifier(ILogger<CivilizationDraftNotifier> logger) : ICivilizationDraftNotifier
{
    public event Func<string, Task>? Changed;

    public async Task NotifyAsync(string shareCode)
    {
        var handlers = Changed;
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<Func<string, Task>>())
        {
            try
            {
                await handler(shareCode);
            }
            catch (Exception exception)
            {
                // Um circuito com problema (aba fechada, banco indisponível) não pode derrubar a ação de quem jogou.
                logger.LogWarning(exception, "Falha ao notificar a sala do draft {ShareCode}", shareCode);
            }
        }
    }
}
