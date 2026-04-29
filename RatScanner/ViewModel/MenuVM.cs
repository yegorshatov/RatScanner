using RatScanner.Scan;
using RatScanner.TarkovDev.GraphQL;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Web;

namespace RatScanner.ViewModel;

internal class MenuVM : INotifyPropertyChanged {
	private RatScannerMain _dataSource;

	public RatScannerMain DataSource {
		get => _dataSource;
		set {
			_dataSource = value;
			OnPropertyChanged();
		}
	}

	public ItemQueue ItemScans => DataSource.ItemScans;

	public ItemScan LastItemScan => ItemScans.LastOrDefault() ?? throw new Exception("ItemQueue is empty!");

	public Item LastItem => LastItemScan.Item;

	public string DiscordLink => ApiManager.GetResource(ApiManager.ResourceType.DiscordLink);

	public string GithubLink => ApiManager.GetResource(ApiManager.ResourceType.GithubLink);

	public string PatreonLink => ApiManager.GetResource(ApiManager.ResourceType.PatreonLink);

	public string Updated => DateTime.Parse(LastItem.Updated).ToLocalTime().ToString(CultureInfo.CurrentCulture);

	public string WikiLink {
		get {
			string? link = LastItem.WikiLink;
			if (link?.Length > 3) return link;
			return $"https://escapefromtarkov.gamepedia.com/{HttpUtility.UrlEncode(LastItem.Name.Replace(" ", "_"))}";
		}
	}

	public MarketSnapshot MarketSnapshot => PriceProvider.GetSnapshot(LastItem);

	public int LastPrice => MarketSnapshot.Price;
	public bool ShowLastPrice => MarketSnapshot.FromTarkovMarket && MarketSnapshot.Price > 0 && RatConfig.Market.TarkovMarket.ShowLastPrice;
	public int Avg24hPrice { get { var s = MarketSnapshot; return s.Avg24h > 0 ? s.Avg24h : s.Price; } }
	public bool ShowAvg24h => MarketSnapshot.FromTarkovMarket && RatConfig.Market.TarkovMarket.ShowAvg24h;
	public int Avg7dPrice => MarketSnapshot.Avg7d;
	public bool ShowAvg7d => MarketSnapshot.FromTarkovMarket && MarketSnapshot.Avg7d > 0 && RatConfig.Market.TarkovMarket.ShowAvg7d;
	public string Diff24hText { get { var s = MarketSnapshot; return s.FromTarkovMarket && RatConfig.Market.TarkovMarket.ShowTrends && s.Diff24h != 0 ? $"{(s.Diff24h > 0 ? "↑" : "↓")}{Math.Abs(s.Diff24h):F1}%" : ""; } }
	public bool Diff24hPositive => MarketSnapshot.Diff24h >= 0;
	public bool ShowDiff24h => MarketSnapshot.FromTarkovMarket && RatConfig.Market.TarkovMarket.ShowTrends && MarketSnapshot.Diff24h != 0;

	public int PricePerSlot {
		get {
			var snap = MarketSnapshot;
			int price = snap.Avg24h > 0 ? snap.Avg24h : snap.Price;
			int size = LastItem.Width * LastItem.Height;
			return size > 0 ? price / size : 0;
		}
	}

	public ItemPrice? BestTraderOffer => LastItem.GetBestTraderOffer();
	public TraderOffer? BestTraderOfferVendor => LastItem.GetBestTraderOfferVendor();

    public (int count, int kappaCount) TaskRemainingResult => LastItem.GetTaskRemaining();

    public int TaskRemaining => TaskRemainingResult.count;

    public int TaskRemainingKappa => TaskRemainingResult.kappaCount;

	public bool KappaNeeded => TaskRemainingKappa > 0;
	
	public int HideoutRemaining => LastItem.GetHideoutRemaining();

	public bool ItemNeeded => TaskRemaining + HideoutRemaining > 0;

	public bool ShowKappaNeeds => RatConfig.Tracking.ShowKappaNeeds;

	public List<KeyValuePair<string, KeyValuePair<int, int>>>? ItemTeamNeeds {
		get {
			if (!RatConfig.Tracking.TarkovTracker.Enable) return null;
			List<FetchModels.TarkovTracker.UserProgress> progress = RatScannerMain.Instance.TarkovTrackerDB.Progress;
			IEnumerable<FetchModels.TarkovTracker.UserProgress> teamProgress = progress.Where(x => x.UserId != RatScannerMain.Instance.TarkovTrackerDB.Self);

			List<KeyValuePair<string, KeyValuePair<int, int>>> needs = new();
			foreach (FetchModels.TarkovTracker.UserProgress? memberProgress in teamProgress) {
				int task = LastItem.GetTaskRemaining(memberProgress).Item1;
				int hideout = LastItem.GetHideoutRemaining(memberProgress);

				if (task == 0 && hideout == 0) continue;

				KeyValuePair<int, int> need = new(task, hideout);

				string name = memberProgress.DisplayName ?? "Unknown";
				for (int i = 2; i < 99; i++) {
					if (needs.All(n => n.Key != name)) break;
					name = $"{memberProgress.DisplayName} #{i}";
				}

				needs.Add(new KeyValuePair<string, KeyValuePair<int, int>>(name, need));
			}

			return needs;
		}
	}

	public (int task, int hideout) ItemTeamNeedsSummed => (ItemTeamNeeds?.Sum(i => i.Value.Key) ?? 0, ItemTeamNeeds?.Sum(i => i.Value.Value) ?? 0);

	public bool ItemTeamNeeded => ItemTeamNeeds != null && ItemTeamNeeds.Any();

	public event PropertyChangedEventHandler PropertyChanged;

	public MenuVM(RatScannerMain ratScanner) {
		DataSource = ratScanner;
		DataSource.PropertyChanged += ModelPropertyChanged;
	}

	protected virtual void OnPropertyChanged(string propertyName = null) {
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public void ModelPropertyChanged(object? sender, PropertyChangedEventArgs e) {
		OnPropertyChanged();
	}

	// Still used in minimal menu
	public string IntToLongPrice(int? value) {
		if (value == null) return "0 ₽";

		string text = $"{value:n0}";
		string numberGroupSeparator = NumberFormatInfo.CurrentInfo.NumberGroupSeparator;
		return text.Replace(numberGroupSeparator, RatConfig.ToolTip.DigitGroupingSymbol) + " ₽";
	}
}
