using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StockPilot.Application.Common;
using StockPilot.Domain.Inventory;
using StockPilot.Infrastructure.Data;

namespace StockPilot.Infrastructure.Inventory;

public sealed class OperationExecutor(ApplicationDbContext db)
{
    public async Task<T> ExecuteAsync<T>(Guid requestId, string actorId, string action, object payload,
        Func<Task<T>> work, CancellationToken ct)
    {
        if (requestId == Guid.Empty) throw new BusinessException(400, "A request ID is required.");
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { action, payload })));
        var previous = await db.Set<StockOperation>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == requestId, ct);
        if (previous is not null) return Replay<T>(previous, actorId, fingerprint);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await work();
            db.Add(new StockOperation { Id = requestId, ActorId = actorId, Fingerprint = fingerprint, ResultJson = JsonSerializer.Serialize(result) });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (DbUpdateException error) when (IsConflict(error))
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            previous = await db.Set<StockOperation>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == requestId, ct);
            if (previous is not null) return Replay<T>(previous, actorId, fingerprint);
            throw new BusinessException(409, "Stock or order changed. Reload before trying again.");
        }
    }

    private static bool IsConflict(DbUpdateException error) => error is DbUpdateConcurrencyException
        || error.InnerException is SqlException { Number: 2601 or 2627 or 1205 };

    private static T Replay<T>(StockOperation operation, string actorId, string fingerprint)
    {
        if (operation.ActorId != actorId || operation.Fingerprint != fingerprint)
            throw new BusinessException(409, "This request ID has already been used for another operation.");
        return JsonSerializer.Deserialize<T>(operation.ResultJson)!;
    }
}
