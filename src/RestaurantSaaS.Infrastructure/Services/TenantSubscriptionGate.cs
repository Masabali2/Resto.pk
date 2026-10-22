using System.Collections.Concurrent;

namespace RestaurantSaaS.Infrastructure.Services;

/// <summary>
/// Serializes free-trial activation for a single workspace inside one process.
///
/// WHY THIS EXISTS
/// A workspace may consume its trial exactly once. Two concurrent requests
/// (double click, two browser tabs, a retrying client) would otherwise both
/// observe "no subscription yet" and both try to create one. The database
/// enforces the invariant as well — a filtered unique index allows at most one
/// Trial/Active/PastDue subscription per workspace — but a database violation
/// surfaces as a raw constraint error and the second request has to wait for
/// the first transaction to finish before it fails.
///
/// This gate makes the failure deterministic and immediate inside a single
/// process: the second request waits for the first one and then sees the
/// already-created trial. Across processes the filtered unique index remains
/// the authority and the violation is translated into a conflict response.
///
/// The gate is deliberately a singleton: it guards a process-wide invariant,
/// not a per-request one.
/// </summary>
public sealed class TenantSubscriptionGate
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _gates = new();

    /// <summary>
    /// Acquires the gate of a workspace. Dispose the returned lease to release it.
    /// </summary>
    public async Task<IAsyncDisposable> AcquireAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var gate = _gates.GetOrAdd(tenantId, static _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(ct);

        return new Lease(gate);
    }

    private sealed class Lease(SemaphoreSlim gate) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            gate.Release();

            return ValueTask.CompletedTask;
        }
    }
}
