using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;

namespace WarframeInventory.Services;

public sealed class MarketPriceService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly SemaphoreSlim _rateGate = new(1, 1);
    private DateTime _lastRequestUtc = DateTime.MinValue;

    public MarketPriceService(HttpClient http, IMemoryCache cache)
    {
        _http = http;
        _cache = cache;
    }

    public async Task<MarketPrice?> GetAsync(string? slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return null;
        var key = $"market-price:{slug}";
        if (_cache.TryGetValue(key, out MarketPrice? cached))
            return cached;

        await _rateGate.WaitAsync(ct);
        try
        {
            var wait = TimeSpan.FromMilliseconds(380) - (DateTime.UtcNow - _lastRequestUtc);
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, ct);
            _lastRequestUtc = DateTime.UtcNow;
            try
            {
                var response = await _http.GetFromJsonAsync<MarketEnvelope>(
                    $"orders/item/{Uri.EscapeDataString(slug)}/top", ct);
                var price = response?.Data.Sell.OrderBy(x => x.Platinum)
                    .Select(x => (int?)x.Platinum).FirstOrDefault();
                var buy = response?.Data.Buy.OrderByDescending(x => x.Platinum)
                    .Select(x => (int?)x.Platinum).FirstOrDefault();
                var result = new MarketPrice(slug, price, buy, DateTime.UtcNow);
                _cache.Set(key, result, TimeSpan.FromMinutes(5));
                return result;
            }
            catch (HttpRequestException) { return null; }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return null; }
        }
        finally
        {
            _rateGate.Release();
        }
    }

    private sealed class MarketEnvelope
    {
        public MarketData Data { get; set; } = new();
    }
    private sealed class MarketData
    {
        public List<MarketOrder> Sell { get; set; } = [];
        public List<MarketOrder> Buy { get; set; } = [];
    }
    private sealed class MarketOrder
    {
        public int Platinum { get; set; }
    }
}

public sealed record MarketPrice(
    string Slug, int? LowestSell, int? HighestBuy, DateTime CheckedUtc);
