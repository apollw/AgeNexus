using AgeNexus.Application.Drafts;

namespace AgeNexus.Infrastructure.Drafts;

public sealed class CivilizationDraftNotifier : ICivilizationDraftNotifier
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
            await handler(shareCode);
        }
    }
}
