using RatScanner.TarkovDev.GraphQL;

namespace RatScanner;

public record MarketSnapshot(
	int Price,
	int Avg24h,
	int Avg7d,
	double Diff24h,
	double Diff7d,
	bool FromTarkovMarket
);

public static class PriceProvider {
	public static MarketSnapshot GetSnapshot(Item item) {
		if (RatConfig.Market.Provider == RatConfig.Market.ProviderKind.TarkovMarket
			&& TarkovMarketAPI.IsConfigured
			&& !TarkovMarketAPI.BadKey) {
			var m = TarkovMarketAPI.GetItem(item.Id);
			if (m != null) {
				return new MarketSnapshot(
					Price: m.Price,
					Avg24h: m.Avg24hPrice > 0 ? m.Avg24hPrice : m.Price,
					Avg7d: m.Avg7daysPrice,
					Diff24h: m.Diff24h,
					Diff7d: m.Diff7days,
					FromTarkovMarket: true
				);
			}
		}

		return new MarketSnapshot(
			Price: item.Avg24HPrice ?? 0,
			Avg24h: item.Avg24HPrice ?? 0,
			Avg7d: 0,
			Diff24h: 0,
			Diff7d: 0,
			FromTarkovMarket: false
		);
	}
}
