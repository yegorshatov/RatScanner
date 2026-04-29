using Newtonsoft.Json;
using System;

namespace RatScanner.FetchModels.TarkovMarket;

public class MarketItem {
	[JsonProperty("uid")]
	public string Uid { get; set; } = "";

	[JsonProperty("bsgId")]
	public string BsgId { get; set; } = "";

	[JsonProperty("name")]
	public string Name { get; set; } = "";

	[JsonProperty("shortName")]
	public string ShortName { get; set; } = "";

	[JsonProperty("price")]
	public int Price { get; set; }

	[JsonProperty("basePrice")]
	public int BasePrice { get; set; }

	[JsonProperty("avg24hPrice")]
	public int Avg24hPrice { get; set; }

	[JsonProperty("avg7daysPrice")]
	public int Avg7daysPrice { get; set; }

	[JsonProperty("diff24h")]
	public double Diff24h { get; set; }

	[JsonProperty("diff7days")]
	public double Diff7days { get; set; }

	[JsonProperty("traderName")]
	public string TraderName { get; set; } = "";

	[JsonProperty("traderPrice")]
	public int TraderPrice { get; set; }

	[JsonProperty("traderPriceCur")]
	public string TraderPriceCur { get; set; } = "";

	[JsonProperty("slots")]
	public int Slots { get; set; }

	[JsonProperty("link")]
	public string Link { get; set; } = "";

	[JsonProperty("updated")]
	public DateTime? Updated { get; set; }
}
