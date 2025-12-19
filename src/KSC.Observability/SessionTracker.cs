using KSC.Observability.Extensions;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace KSC.Observability;

public class SessionTracker
{
    private readonly IMemoryCache _memoryCache;  // اگر از in-memory session استفاده می‌کنی
    private ConcurrentDictionary<string, long> _sessionSizes = new();  // برای cache اندازه هر session

    public SessionTracker(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    // این متد رو periodically صدا بزن (مثل هر 10 ثانیه) برای update metrics
    public void UpdateMetrics()
    {
        // دسترسی به session collection با reflection (برای InMemory)
        var sessionCollectionField = typeof(MemoryCache).GetField("_entries", BindingFlags.NonPublic | BindingFlags.Instance);
        if (sessionCollectionField == null) return;

        var entries = sessionCollectionField.GetValue(_memoryCache) as ConcurrentDictionary<object, object>;
        if (entries == null) return;

        int activeCount = 0;
        long totalMemory = 0;

        foreach (var entry in entries)
        {
            // کلید session معمولاً string مثل "session_id"
            if (entry.Key is string key && key.StartsWith("session_"))  // فرض prefix - بسته به configت تنظیم کن
            {
                if (entry.Value is ICacheEntry cacheEntry)
                {
                    // Check expiration
                    if (!cacheEntry.AbsoluteExpiration.HasValue || cacheEntry.AbsoluteExpiration > DateTimeOffset.Now)
                    {
                        activeCount++;

                        // محاسبه اندازه: serialize session data به JSON و طول bytes بگیر
                        if (_sessionSizes.TryGetValue(key, out long size))
                        {
                            totalMemory += size;
                        }
                        else
                        {
                            // اگر cache نشده, محاسبه کن (سنگین هست, پس cache کن)
                            var sessionData = cacheEntry.Value;  // این object session هست
                            var json = JsonSerializer.Serialize(sessionData);
                            size = json.Length * sizeof(char);  // تقریبی bytes (UTF-16)
                            _sessionSizes[key] = size;
                            totalMemory += size;
                        }
                    }
                }
            }
        }

        // update gauges
      //  OpenTelemetryExtensions.UpdateSessionMetrics(activeCount, totalMemory);
    }
}