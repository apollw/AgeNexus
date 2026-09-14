using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AgeNexus.Infrastructure.Persistence;

// Never log SQL parameters, query text, connection strings or user data.
internal sealed class SlowDatabaseCommandInterceptor(ILogger<SlowDatabaseCommandInterceptor> logger)
    : DbCommandInterceptor
{
    private void Record(CommandExecutedEventData eventData)
    {
        if (eventData.Duration.TotalMilliseconds >= 500)
        {
            logger.LogWarning("Slow database command {CommandId}: {ElapsedMs}ms. Trace {TraceId}",
                eventData.CommandId, (long)eventData.Duration.TotalMilliseconds, Activity.Current?.TraceId.ToString());
        }
    }

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        Record(eventData);
        return result;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        DbDataReader result, CancellationToken cancellationToken = default)
    {
        Record(eventData);
        return ValueTask.FromResult(result);
    }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        Record(eventData);
        return result;
    }

    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        int result, CancellationToken cancellationToken = default)
    {
        Record(eventData);
        return ValueTask.FromResult(result);
    }
}
