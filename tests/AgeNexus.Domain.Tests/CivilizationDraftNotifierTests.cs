using AgeNexus.Infrastructure.Drafts;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgeNexus.Domain.Tests;

public sealed class CivilizationDraftNotifierTests
{
    [Fact]
    public async Task Keeps_notifying_the_remaining_circuits_when_one_subscriber_fails()
    {
        var notifier = new CivilizationDraftNotifier(NullLogger<CivilizationDraftNotifier>.Instance);
        var notified = new List<string>();

        notifier.Changed += _ => throw new InvalidOperationException("O circuito foi encerrado.");
        notifier.Changed += code =>
        {
            notified.Add(code);
            return Task.CompletedTask;
        };

        await notifier.NotifyAsync("ABC123");

        Assert.Equal(["ABC123"], notified);
    }

    [Fact]
    public async Task Notifies_every_subscriber_of_the_same_room()
    {
        var notifier = new CivilizationDraftNotifier(NullLogger<CivilizationDraftNotifier>.Instance);
        var notified = new List<string>();

        notifier.Changed += code =>
        {
            notified.Add($"ban:{code}");
            return Task.CompletedTask;
        };
        notifier.Changed += code =>
        {
            notified.Add($"pick:{code}");
            return Task.CompletedTask;
        };

        await notifier.NotifyAsync("SALA01");

        Assert.Equal(["ban:SALA01", "pick:SALA01"], notified);
    }
}
