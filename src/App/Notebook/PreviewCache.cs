using System.Security.Cryptography;
using App.Assistant;

namespace App.Notebook;

// Caches handwriting-reader results keyed by SHA-256 of the PNG bytes.
// Shared between the preview route (read-ahead) and the ink route (dedup).
// Thread-safe singleton; evicts oldest entry when full.
public sealed class PreviewCache
{
    private readonly record struct Entry(HandwritingResult Result, DateTimeOffset ExpiresAt);

    private readonly Dictionary<string, (Entry E, LinkedListNode<string> Node)> _map = new();
    private readonly LinkedList<string> _order = new();
    private readonly TimeProvider _time;
    private readonly object _lock = new();

    public const int Capacity = 32;
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    public PreviewCache(TimeProvider time) => _time = time;

    // Returns a hex-encoded SHA-256 of the raw PNG bytes, used as the cache key.
    public static string ComputeKey(byte[] pngBytes) =>
        Convert.ToHexString(SHA256.HashData(pngBytes)).ToLowerInvariant();

    // Returns the cached result for the key, or null on a miss or expiry.
    public HandwritingResult? TryGet(string key)
    {
        lock (_lock)
        {
            if (!_map.TryGetValue(key, out var item))
            {
                return null;
            }

            if (item.E.ExpiresAt <= _time.GetUtcNow())
            {
                _order.Remove(item.Node);
                _map.Remove(key);
                return null;
            }

            return item.E.Result;
        }
    }

    // Stores a result. If the key is already present, refreshes the value but
    // keeps the original insertion position for eviction ordering.
    public void Set(string key, HandwritingResult result)
    {
        lock (_lock)
        {
            var expiresAt = _time.GetUtcNow() + Ttl;

            if (_map.TryGetValue(key, out var existing))
            {
                _map[key] = (new Entry(result, expiresAt), existing.Node);
                return;
            }

            if (_map.Count >= Capacity)
            {
                var oldest = _order.First!.Value;
                _order.RemoveFirst();
                _map.Remove(oldest);
            }

            var node = _order.AddLast(key);
            _map[key] = (new Entry(result, expiresAt), node);
        }
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _map.Count;
            }
        }
    }
}
