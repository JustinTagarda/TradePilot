using System.Collections.Concurrent;
using System.Threading;

namespace TradePilot.Api.Security;

public sealed class MemoryNonceReplayGuard : INonceReplayGuard
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _nonceExpirations = new(StringComparer.Ordinal);
    private int _cleanupCounter;

    public bool TryRegisterNonce(string sourceId, string nonce, DateTimeOffset nowUtc, TimeSpan ttl)
    {
        if (ttl <= TimeSpan.Zero)
        {
            ttl = TimeSpan.FromMinutes(5);
        }

        var expiration = nowUtc.Add(ttl);
        var key = $"{sourceId}:{nonce}";

        while (true)
        {
            if (_nonceExpirations.TryGetValue(key, out var existingExpiration))
            {
                if (existingExpiration > nowUtc)
                {
                    CleanupOccasionally(nowUtc);
                    return false;
                }

                if (_nonceExpirations.TryUpdate(key, expiration, existingExpiration))
                {
                    CleanupOccasionally(nowUtc);
                    return true;
                }

                continue;
            }

            if (_nonceExpirations.TryAdd(key, expiration))
            {
                CleanupOccasionally(nowUtc);
                return true;
            }
        }
    }

    private void CleanupOccasionally(DateTimeOffset nowUtc)
    {
        if (Interlocked.Increment(ref _cleanupCounter) % 128 != 0)
        {
            return;
        }

        foreach (var pair in _nonceExpirations)
        {
            if (pair.Value <= nowUtc)
            {
                _nonceExpirations.TryRemove(pair.Key, out _);
            }
        }
    }
}
