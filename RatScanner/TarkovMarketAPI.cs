using Newtonsoft.Json;
using RatScanner.FetchModels.TarkovMarket;
using RatScanner.FetchModels.TarkovTracker;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RatScanner;

public static class TarkovMarketAPI {
	private static readonly ConcurrentDictionary<string, (long expire, MarketItem[] items)> Cache = new();
	private static readonly ConcurrentDictionary<string, bool> PendingRequests = new();

	private static bool _badKey = false;
	public static bool BadKey => _badKey;

	public static bool IsConfigured => RatConfig.Market.TarkovMarket.Enable;

	private static string ApiBase => RatConfig.Market.TarkovMarket.Endpoint;

	private static string AllItemsPath =>
		RatConfig.GameMode == TarkovDev.GraphQL.GameMode.Pve ? "/pve/items/all" : "/items/all";

	private static string CacheKey =>
		$"tarkov_market_{RatConfig.GameMode}";

	private static Dictionary<string, MarketItem> _itemsByBsgId = new();
	public static IReadOnlyDictionary<string, MarketItem> ItemsByUid => _itemsByBsgId;

	public static MarketItem[] GetAllItems() {
		if (_badKey) return Array.Empty<MarketItem>();

		string key = CacheKey;

		if (!Cache.TryGetValue(key, out var cached)) {
			FetchAllItemsCore(key);
			return Cache.TryGetValue(key, out cached) ? cached.items : Array.Empty<MarketItem>();
		}

		long now = DateTimeOffset.Now.ToUnixTimeSeconds();
		if (now > cached.expire && PendingRequests.TryAdd(key, true)) {
			Task.Run(() => FetchAllItemsCore(key));
		}

		return cached.items;
	}

	public static MarketItem? GetItem(string bsgId) {
		GetAllItems();
		return _itemsByBsgId.TryGetValue(bsgId, out MarketItem? item) ? item : null;
	}

	// Salewa BSG UID — lightweight endpoint to validate the key without downloading all items
	private const string TestItemUid = "544fb25a4bdc2dfb738b4567";

	public static bool TestConnection(string apiKey) {
		if (string.IsNullOrWhiteSpace(apiKey)) return false;
		try {
			string url = $"{RatConfig.Market.TarkovMarket.Endpoint}/item?uid={TestItemUid}";
			string result = APIClient.GetWithApiKey(url, apiKey);
			Logger.LogInfo($"TarkovMarket: TestConnection OK, response length={result.Length}");
			return true;
		} catch (UnauthorizedTokenException) {
			Logger.LogWarning("TarkovMarket: TestConnection failed — API key rejected (401)");
			return false;
		} catch (RateLimitExceededException) {
			Logger.LogWarning("TarkovMarket: TestConnection failed — rate limited (429)");
			return false;
		} catch (Exception e) {
			Logger.LogWarning("TarkovMarket: TestConnection failed — " + e.Message, e);
			return false;
		}
	}

	public static void ResetBadKey() {
		_badKey = false;
	}

	private static void FetchAllItemsCore(string cacheKey) {
		try {
			string url = $"{ApiBase}{AllItemsPath}";
			string json = APIClient.GetWithApiKey(url, RatConfig.Market.TarkovMarket.ApiKey);
			MarketItem[] items = JsonConvert.DeserializeObject<MarketItem[]>(json) ?? Array.Empty<MarketItem>();

			long ttl = DateTimeOffset.Now.ToUnixTimeSeconds() + RatConfig.Market.TarkovMarket.CacheTtlSeconds;
			Cache[cacheKey] = (ttl, items);

			// Index by bsgId (= BSG item ID, matches tarkov.dev item.Id).
			// uid is tarkov-market's internal ID and does NOT match tarkov.dev.
			_itemsByBsgId = items
				.Where(i => !string.IsNullOrEmpty(i.BsgId))
				.GroupBy(i => i.BsgId)
				.ToDictionary(g => g.Key, g => g.First());

			if (items.Length > 0)
				Logger.LogInfo($"TarkovMarket: fetched {items.Length} items, sample bsgId={items[0].BsgId}, uid={items[0].Uid}");
			else
				Logger.LogInfo("TarkovMarket: fetched 0 items");
			RatConfig.WriteToCache(cacheKey, json);
		} catch (UnauthorizedTokenException) {
			_badKey = true;
			Logger.LogWarning("TarkovMarket: API key rejected, falling back to tarkov.dev");
		} catch (RateLimitExceededException) {
			Logger.LogWarning("TarkovMarket: rate limited");
			ExtendCacheTtl(cacheKey, RatConfig.SuperShortTTL);
		} catch (JsonReaderException e) {
			Logger.LogWarning("TarkovMarket: invalid JSON response", e);
			TryLoadFromOfflineCache(cacheKey);
		} catch (Exception e) {
			Logger.LogWarning("TarkovMarket: fetch failed", e);
			TryLoadFromOfflineCache(cacheKey);
		} finally {
			PendingRequests.TryRemove(cacheKey, out _);
		}
	}

	private static void TryLoadFromOfflineCache(string cacheKey) {
		if (!RatConfig.ReadFromCache(cacheKey, out string cached) || string.IsNullOrEmpty(cached)) return;
		try {
			MarketItem[] items = JsonConvert.DeserializeObject<MarketItem[]>(cached) ?? Array.Empty<MarketItem>();
			long ttl = DateTimeOffset.Now.ToUnixTimeSeconds() + RatConfig.SuperShortTTL;
			Cache[cacheKey] = (ttl, items);
			_itemsByBsgId = items
				.Where(i => !string.IsNullOrEmpty(i.BsgId))
				.GroupBy(i => i.BsgId)
				.ToDictionary(g => g.Key, g => g.First());
			Logger.LogInfo($"TarkovMarket: loaded {items.Length} items from offline cache");
		} catch (Exception e) {
			Logger.LogWarning("TarkovMarket: offline cache load failed", e);
		}
	}

	private static void ExtendCacheTtl(string cacheKey, int extraSeconds) {
		if (Cache.TryGetValue(cacheKey, out var existing)) {
			long now = DateTimeOffset.Now.ToUnixTimeSeconds();
			Cache[cacheKey] = (now + extraSeconds, existing.items);
		}
	}
}
