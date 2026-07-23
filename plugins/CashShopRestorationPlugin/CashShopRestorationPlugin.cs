using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Durango.Logic;
using Durango.Logic.Clusters;
using Durango.Offline;
using Durango.UI;
using Durango.UI.Control;
using Durango.UI.Popup;
using HarmonyLib;
using Messages;
using Newtonsoft.Json;
using Shared.Building;
using Shared.Economy;
using Shared.Etc;
using Shared.Purchaser;
using UnityEngine;
using Yaml;
using Yaml.Util;
using OfflineConnection = Durango.Offline.Connection;
using OfflinePlayer = Durango.Offline.Player;
using PacketHeader = Durango.Network.PacketHeader;

namespace BaoX.DurangoOriginal.CashShopRestoration
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.baox.durango.original.tamedislandrestoration",
        BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class CashShopRestorationPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.baox.durango.original.cashshoprestoration";
        public const string PluginName = "Cash Shop Restoration Plugin";
        public const string PluginVersion = "0.2.4";

        internal static ManualLogSource Log;
        internal static ConfigFile PluginConfig;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<long> InitialCoin;
        internal static ConfigEntry<long> InitialGem;
        internal static ConfigEntry<long> InitialTStone;
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            PluginConfig = Config;
            Enabled = Config.Bind("General", "Enabled", true,
                "Restore the retail Cash Shop UI and a local offline purchase backend.");
            InitialCoin = Config.Bind("Offline Wallet", "InitialCoin", 1000L,
                "Coin balance assigned when an offline profile first opens the restored shop.");
            InitialGem = Config.Bind("Offline Wallet", "InitialGem", 1000L,
                "Warp Gem balance assigned when an offline profile first opens the restored shop.");
            InitialTStone = Config.Bind("Offline Wallet", "InitialTStone", 100000L,
                "T-Stone balance assigned when an offline profile first opens the restored shop.");

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            Logger.LogInfo(PluginName + " loaded.");
        }

        private void OnDestroy()
        {
            if (_harmony == null) return;
            _harmony.UnpatchSelf();
            _harmony = null;
        }
    }

    internal sealed class OfflineWalletState
    {
        private static readonly Dictionary<string, OfflineWalletState> States =
            new Dictionary<string, OfflineWalletState>();

        private readonly ConfigEntry<long> _coin;
        private readonly ConfigEntry<long> _gem;
        private readonly ConfigEntry<long> _tstone;
        private readonly ConfigEntry<long> _mileage;
        private readonly ConfigEntry<long> _rpiece;
        private readonly ConfigEntry<long> _warpMatter;

        private OfflineWalletState(string ownerId)
        {
            string section = "Wallet " + Sanitize(ownerId);
            _coin = CashShopRestorationPlugin.PluginConfig.Bind(section, "Coin",
                CashShopRestorationPlugin.InitialCoin.Value, "Offline Durango Coin balance.");
            _gem = CashShopRestorationPlugin.PluginConfig.Bind(section, "Gem",
                CashShopRestorationPlugin.InitialGem.Value, "Offline Warp Gem balance.");
            _tstone = CashShopRestorationPlugin.PluginConfig.Bind(section, "TStone",
                CashShopRestorationPlugin.InitialTStone.Value, "Offline T-Stone balance.");
            _mileage = CashShopRestorationPlugin.PluginConfig.Bind(section, "Mileage", 0L,
                "Offline Cash Shop mileage balance.");
            _rpiece = CashShopRestorationPlugin.PluginConfig.Bind(section, "RPiece", 0L,
                "Offline random-number-piece balance.");
            _warpMatter = CashShopRestorationPlugin.PluginConfig.Bind(section, "WarpMatter", 0L,
                "Offline Warp Matter balance.");
        }

        public static OfflineWalletState Get(string ownerId)
        {
            string key = string.IsNullOrEmpty(ownerId) ? "local-player" : ownerId;
            OfflineWalletState state;
            if (!States.TryGetValue(key, out state))
            {
                state = new OfflineWalletState(key);
                States[key] = state;
                CashShopRestorationPlugin.PluginConfig.Save();
            }
            return state;
        }

        public bool CanSpend(Currency currency, long amount)
        {
            return amount <= 0L || GetBalance(currency) >= amount;
        }

        public bool Spend(Currency currency, long amount)
        {
            if (!CanSpend(currency, amount)) return false;
            Add(currency, -amount);
            return true;
        }

        public void Add(Currency currency, long amount)
        {
            ConfigEntry<long> entry = GetEntry(currency);
            if (entry == null) return;
            entry.Value = Math.Max(0L, entry.Value + amount);
            CashShopRestorationPlugin.PluginConfig.Save();
        }

        public long GetBalance(Currency currency)
        {
            ConfigEntry<long> entry = GetEntry(currency);
            return entry == null ? 0L : Math.Max(0L, entry.Value);
        }

        public Wallet MakeWallet()
        {
            Dictionary<Currency, long> paid = new Dictionary<Currency, long>();
            paid[Currency.TStone] = GetBalance(Currency.TStone);
            paid[Currency.Gem] = GetBalance(Currency.Gem);
            paid[Currency.PcCoin] = GetBalance(Currency.Coin);
            paid[Currency.MobileCoin] = GetBalance(Currency.Coin);
            paid[Currency.CashshopMileage] = GetBalance(Currency.CashshopMileage);
            paid[Currency.RPiece] = GetBalance(Currency.RPiece);
            paid[Currency.WarpMatter] = GetBalance(Currency.WarpMatter);
            Wallet wallet = default(Wallet);
            wallet.PaidBalances = paid;
            wallet.UnpaidBalances = new Dictionary<Currency, long>();
            wallet.Vouchers = new VoucherInfo[0];
            return wallet;
        }

        private ConfigEntry<long> GetEntry(Currency currency)
        {
            if (currency == Currency.Coin || currency == Currency.PcCoin ||
                currency == Currency.MobileCoin) return _coin;
            if (currency == Currency.Gem) return _gem;
            if (currency == Currency.TStone) return _tstone;
            if (currency == Currency.CashshopMileage) return _mileage;
            if (currency == Currency.RPiece) return _rpiece;
            if (currency == Currency.WarpMatter) return _warpMatter;
            return null;
        }

        internal static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "local-player";
            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_')
                    chars[i] = '_';
            }
            return new string(chars);
        }
    }

    internal sealed class PendingPurchaseData
    {
        public string Id;
        public string CommodityId;
        public double PurchasedAt;
        public string PrototypeId;
        public int Level;
        public string ColorR;
        public string ColorG;
        public string ColorB;
        public string BlueprintId;
        public int SizeX;
        public int SizeY;
        public float Durability;
        public Dictionary<string, string> Parts;
        public Dictionary<string, string> Textures;
        public Dictionary<Currency, long> Money;
        public bool SignalAmplifier;

        [JsonIgnore]
        public bool HasItem
        {
            get { return !string.IsNullOrEmpty(PrototypeId); }
        }
    }

    internal sealed class PurchaseStateData
    {
        public List<PendingPurchaseData> Pending;
        public Dictionary<string, string> FirstPurchases;

        public PurchaseStateData()
        {
            Pending = new List<PendingPurchaseData>();
            FirstPurchases = new Dictionary<string, string>();
        }

        public void Repair()
        {
            if (Pending == null) Pending = new List<PendingPurchaseData>();
            if (FirstPurchases == null)
                FirstPurchases = new Dictionary<string, string>();
        }
    }

    internal sealed class OfflinePurchaseState
    {
        private static readonly Dictionary<string, OfflinePurchaseState> States =
            new Dictionary<string, OfflinePurchaseState>();
        private readonly ConfigEntry<string> _json;

        private OfflinePurchaseState(string ownerId)
        {
            string section = "Cash Shop State " + OfflineWalletState.Sanitize(ownerId);
            _json = CashShopRestorationPlugin.PluginConfig.Bind(section, "StateJson", "{}",
                "Persistent offline Cash Shop storage and first-purchase state.");
            try
            {
                Data = JsonConvert.DeserializeObject<PurchaseStateData>(_json.Value);
            }
            catch (Exception ex)
            {
                CashShopRestorationPlugin.Log.LogError(
                    "Could not load offline Cash Shop state: " + ex.Message);
                Data = null;
            }
            if (Data == null) Data = new PurchaseStateData();
            Data.Repair();
        }

        public PurchaseStateData Data;

        public static OfflinePurchaseState Get(string ownerId)
        {
            string key = string.IsNullOrEmpty(ownerId) ? "local-player" : ownerId;
            OfflinePurchaseState state;
            if (!States.TryGetValue(key, out state))
            {
                state = new OfflinePurchaseState(key);
                States[key] = state;
            }
            return state;
        }

        public void Save()
        {
            Data.Repair();
            _json.Value = JsonConvert.SerializeObject(Data, Formatting.None);
            CashShopRestorationPlugin.PluginConfig.Save();
        }
    }

    internal static class OfflineCashShopBackend
    {
        public static void Register(OfflinePlayer player, OfflineConnection connection,
            World world, PlayerContext context, string ownerId)
        {
            OfflineWalletState wallet = OfflineWalletState.Get(ownerId);
            OfflinePurchaseState purchaseState = OfflinePurchaseState.Get(ownerId);
            RepairLegacyCapsules(player, context, ownerId);
            SendWallet(player, ownerId, wallet);

            connection.Recv<GetCommodities>(delegate(GetCommodities request, PacketHeader header)
            {
                Messages.Commodities result = MakeCommodities();
                player.Send<Messages.Commodities>(result, header.Seq);
                CashShopRestorationPlugin.Log.LogInfo(
                    "Cash Shop commodity list sent: count=" + result.CommodityInfos.Length);
            });
            connection.Recv<GetPurchases>(delegate(GetPurchases request, PacketHeader header)
            {
                Purchases result = default(Purchases);
                result._Purchases = MakePurchaseMessages(purchaseState.Data.Pending);
                player.Send<Purchases>(result, header.Seq);
                CashShopRestorationPlugin.Log.LogInfo(
                    "Offline Cash Shop storage sent: count=" + result._Purchases.Length);
            });
            connection.Recv<GetAcceptableSubPurchases>(delegate(
                GetAcceptableSubPurchases request, PacketHeader header)
            {
                AcceptableSubPurchases result = default(AcceptableSubPurchases);
                result.Ids = new AcceptableSubPurchase[0];
                player.Send<AcceptableSubPurchases>(result, header.Seq);
            });
            connection.Recv<GetUserFirstPurchaseHistory>(delegate(
                GetUserFirstPurchaseHistory request, PacketHeader header)
            {
                UserFirstPurchaseHistory result = default(UserFirstPurchaseHistory);
                List<UserFirstPurchase> history = new List<UserFirstPurchase>();
                foreach (KeyValuePair<string, string> pair in
                    purchaseState.Data.FirstPurchases)
                {
                    UserFirstPurchase entry = default(UserFirstPurchase);
                    entry.CommodityId = pair.Key;
                    entry.PurchaseId = pair.Value;
                    history.Add(entry);
                }
                result._UserFirstPurchaseHistory = history.ToArray();
                player.Send<UserFirstPurchaseHistory>(result, header.Seq);
            });
            connection.Recv<GetSpecialDeals>(delegate(GetSpecialDeals request, PacketHeader header)
            {
                SpecialDeals result = default(SpecialDeals);
                result.Deals = new SpecialDeal[0];
                player.Send<SpecialDeals>(result, header.Seq);
            });
            connection.Recv<PurchaseCommodity>(delegate(PurchaseCommodity request, PacketHeader header)
            {
                Purchase(player, ownerId, wallet, purchaseState,
                    request.CommodityId, header);
            });
            connection.Recv<PurchaseCommodityWithVoucher>(delegate(
                PurchaseCommodityWithVoucher request, PacketHeader header)
            {
                // The restored wallet currently has no vouchers; use the normal
                // currency path so direct item links still behave consistently.
                Purchase(player, ownerId, wallet, purchaseState,
                    request.CommodityId, header);
            });
            connection.Recv<AcceptPurchase>(delegate(AcceptPurchase request, PacketHeader header)
            {
                Accept(player, context, ownerId, wallet, purchaseState,
                    request, header);
            });
            connection.Recv<PlaceCapsulatedArtifact>(delegate(
                PlaceCapsulatedArtifact request, PacketHeader header)
            {
                PlaceCapsulated(player, world, context, ownerId, request, header);
            });

            CashShopRestorationPlugin.Log.LogInfo(
                "Registered offline Cash Shop backend: owner=" + ownerId +
                ", coin=" + wallet.GetBalance(Currency.Coin) +
                ", gem=" + wallet.GetBalance(Currency.Gem));
        }

        private static Messages.Commodities MakeCommodities()
        {
            List<CommodityInfo> infos = new List<CommodityInfo>();
            Yaml.Commodities yaml = Singleton<Yaml.Commodities>.Instance;
            if (yaml != null && yaml.PostedCommodities != null)
            {
                foreach (KeyValuePair<string, Yaml.Commodity> pair in yaml.PostedCommodities)
                {
                    if (string.IsNullOrEmpty(pair.Key) || pair.Value == null) continue;
                    CommodityInfo info = default(CommodityInfo);
                    info.Id = pair.Key;
                    info.MaxPurchasableCount = new int?(999);
                    info.PeriodicPurchasableAt = null;
                    info.PeriodicPurchasableCount = null;
                    info.SpecialDealExpiresAt = null;
                    infos.Add(info);
                }
            }
            Messages.Commodities result = default(Messages.Commodities);
            result.CommodityInfos = infos.ToArray();
            return result;
        }

        private static void Purchase(OfflinePlayer player, string ownerId,
            OfflineWalletState wallet, OfflinePurchaseState purchaseState,
            string commodityId, PacketHeader header)
        {
            Yaml.Commodity data = FindCommodity(commodityId);
            if (data == null || !IsSupported(data, commodityId) ||
                !wallet.CanSpend(data.PriceCurrency, data.PriceAmount))
            {
                player.Send<Abort>(default(Abort), header.Seq);
                CashShopRestorationPlugin.Log.LogWarning(
                    "Rejected offline shop purchase: " + commodityId);
                return;
            }

            bool firstPurchase =
                !purchaseState.Data.FirstPurchases.ContainsKey(commodityId);
            List<PendingPurchaseData> queued;
            if (!PreparePurchases(data, commodityId, firstPurchase, out queued) ||
                queued.Count == 0)
            {
                player.Send<Abort>(default(Abort), header.Seq);
                CashShopRestorationPlugin.Log.LogWarning(
                    "Unsupported or empty offline shop reward: " + commodityId);
                return;
            }

            bool amplifier = IsSignalAmplifier(commodityId);
            if (amplifier && !PioneerBridge.Available)
            {
                player.Send<Abort>(default(Abort), header.Seq);
                CashShopRestorationPlugin.Log.LogWarning(
                    "Signal Amplifier requires Tamed Island Restoration 0.7.0 or newer.");
                return;
            }

            if (!wallet.Spend(data.PriceCurrency, data.PriceAmount))
            {
                player.Send<Abort>(default(Abort), header.Seq);
                return;
            }

            try
            {
                purchaseState.Data.Pending.AddRange(queued);
                if (firstPurchase)
                    purchaseState.Data.FirstPurchases[commodityId] = queued[0].Id;
                purchaseState.Save();
                SendWallet(player, ownerId, wallet);
                Purchased result = default(Purchased);
                result.Purchases = MakePurchaseMessages(queued);
                player.Send<Purchased>(result, header.Seq);

                CashShopRestorationPlugin.Log.LogInfo(
                    "Offline shop purchase queued: commodity=" + commodityId +
                    ", price=" + data.PriceAmount + " " + data.PriceCurrency +
                    ", storage-entries=" + queued.Count + ", amplifier=" + amplifier);
            }
            catch (Exception ex)
            {
                for (int i = 0; i < queued.Count; i++)
                    purchaseState.Data.Pending.Remove(queued[i]);
                if (firstPurchase)
                    purchaseState.Data.FirstPurchases.Remove(commodityId);
                wallet.Add(data.PriceCurrency, data.PriceAmount);
                SendWallet(player, ownerId, wallet);
                player.Send<Abort>(default(Abort), header.Seq);
                CashShopRestorationPlugin.Log.LogError(
                    "Offline shop purchase failed: " + ex);
            }
        }

        private static bool IsSupported(Yaml.Commodity data, string commodityId)
        {
            if (data.PriceCurrency == Currency.Invalid && data.PriceAmount > 0L) return false;
            if (IsSignalAmplifier(commodityId)) return true;
            ShopContents contents = data.Contents;
            if (contents.StatusEffects != null && contents.StatusEffects.Length > 0) return false;
            if (contents.Motions != null && contents.Motions.Length > 0) return false;
            if (contents.Vouchers != null && contents.Vouchers.Length > 0) return false;
            if (contents.RefillVouchers != null && contents.RefillVouchers.Length > 0) return false;
            if (contents.WeightedMotions != null && contents.WeightedMotions.Length > 0) return false;
            if (contents.WeightedItems != null && contents.WeightedItems.Length > 0) return false;
            return true;
        }

        private static bool PreparePurchases(Yaml.Commodity commodity,
            string commodityId, bool firstPurchase,
            out List<PendingPurchaseData> pending)
        {
            pending = new List<PendingPurchaseData>();
            ShopContents contents = commodity.Contents;
            if (contents.Items != null)
            {
                for (int i = 0; i < contents.Items.Length; i++)
                {
                    ItemContent content = contents.Items[i];
                    int count = Math.Max(1, content.count);
                    for (int j = 0; j < count; j++)
                    {
                        PendingPurchaseData item = NewPending(commodityId);
                        item.PrototypeId = content.prototype_id;
                        item.Level = Math.Max(1, content.level);
                        ApplyColors(item, content.colors);
                        if (item.PrototypeId != null &&
                            item.PrototypeId.StartsWith("capsulated_",
                                StringComparison.Ordinal))
                        {
                            item.BlueprintId = item.PrototypeId.Substring(
                                "capsulated_".Length);
                        }
                        Item preview;
                        if (!TryMakeItem(item, out preview)) return false;
                        pending.Add(item);
                    }
                }
            }
            if (contents.Modulars != null)
            {
                for (int i = 0; i < contents.Modulars.Length; i++)
                {
                    ModularArtifactContent content = contents.Modulars[i];
                    PendingPurchaseData modular = NewPending(commodityId);
                    modular.PrototypeId = content.prototype_id;
                    modular.Level = Math.Max(1, content.level);
                    modular.BlueprintId = content.artifact_id;
                    modular.SizeX = Math.Max(1, content.size_x);
                    modular.SizeY = Math.Max(1, content.size_y);
                    modular.Durability = content.durability;
                    modular.Parts = Copy(content.overridden_parts);
                    modular.Textures = Copy(content.overridden_textures);
                    Item preview;
                    if (!TryMakeItem(modular, out preview)) return false;
                    pending.Add(modular);
                }
            }

            Dictionary<Currency, long> money = BuildMoneyRewards(commodity, firstPurchase);
            bool amplifier = IsSignalAmplifier(commodityId);
            if (money.Count > 0 || amplifier)
            {
                PendingPurchaseData currency = NewPending(commodityId);
                currency.Money = money;
                currency.SignalAmplifier = amplifier;
                pending.Add(currency);
            }
            return true;
        }

        private static PendingPurchaseData NewPending(string commodityId)
        {
            PendingPurchaseData result = new PendingPurchaseData();
            result.Id = "offline-shop:" + Guid.NewGuid().ToString("N");
            result.CommodityId = commodityId;
            result.PurchasedAt = UnixNow();
            result.Durability = 1f;
            return result;
        }

        private static Dictionary<Currency, long> BuildMoneyRewards(
            Yaml.Commodity data, bool firstPurchase)
        {
            Dictionary<Currency, long> result = new Dictionary<Currency, long>();
            if (data.Contents.Money != null)
            {
                for (int i = 0; i < data.Contents.Money.Length; i++)
                    AddMoney(result, data.Contents.Money[i].currency,
                        data.Contents.Money[i].amount);
            }
            AddMoney(result, Currency.Gem, data.GemAmount);
            long coins = data.CoinAmount + data.CoinBonus;
            if (firstPurchase) coins += data.CoinFirstPurchaseBonus;
            AddMoney(result, Currency.Coin, coins);
            AddMoney(result, Currency.CashshopMileage, data.BonusMileage);
            return result;
        }

        private static void AddMoney(Dictionary<Currency, long> result,
            Currency currency, long amount)
        {
            if (amount <= 0L || currency == Currency.Invalid) return;
            long current;
            result.TryGetValue(currency, out current);
            result[currency] = current + amount;
        }

        private static Purchase[] MakePurchaseMessages(
            IList<PendingPurchaseData> pending)
        {
            List<Purchase> result = new List<Purchase>();
            for (int i = 0; i < pending.Count; i++)
            {
                PendingPurchaseData source = pending[i];
                Purchase purchase = default(Purchase);
                purchase.Id = source.Id;
                purchase.CommodityId = source.CommodityId;
                purchase.PurchasedAt = source.PurchasedAt;
                purchase.AcceptedAt = null;
                purchase.ExpiresAt = source.PurchasedAt + 2592000.0;
                purchase.SubAcceptedAt = null;
                if (source.HasItem)
                {
                    Item item;
                    if (!TryMakeItem(source, out item))
                    {
                        CashShopRestorationPlugin.Log.LogError(
                            "Could not rebuild Storage preview: " + source.PrototypeId);
                        continue;
                    }
                    purchase.Content = new ItemPurchaseContent { Item = item };
                }
                else
                {
                    purchase.Content = null;
                }
                result.Add(purchase);
            }
            return result.ToArray();
        }

        private static void Accept(OfflinePlayer player, PlayerContext context,
            string ownerId, OfflineWalletState wallet,
            OfflinePurchaseState purchaseState, AcceptPurchase request,
            PacketHeader header)
        {
            PendingPurchaseData pending = purchaseState.Data.Pending.Find(
                delegate(PendingPurchaseData value) { return value.Id == request.PurchaseId; });
            if (pending == null || !string.IsNullOrEmpty(request.SubId))
            {
                player.Send<Abort>(default(Abort), header.Seq);
                CashShopRestorationPlugin.Log.LogWarning(
                    "Rejected duplicate or unknown Storage receive: " + request.PurchaseId);
                return;
            }

            Item received = default(Item);
            bool hasItem = pending.HasItem;
            if (hasItem)
            {
                if (context.InventoryItems.Count >= 200 ||
                    !TryMakeItem(pending, out received))
                {
                    player.Send<Abort>(default(Abort), header.Seq);
                    return;
                }
            }

            PioneerGradeInfo amplifierInfo = default(PioneerGradeInfo);
            if (pending.SignalAmplifier &&
                !PioneerBridge.Activate(ownerId, 7, out amplifierInfo))
            {
                player.Send<Abort>(default(Abort), header.Seq);
                return;
            }

            try
            {
                if (hasItem)
                {
                    context.InventoryItems.Add(received);
                    context.Save();
                }
                if (pending.Money != null)
                {
                    foreach (KeyValuePair<Currency, long> pair in pending.Money)
                        wallet.Add(pair.Key, pair.Value);
                }

                purchaseState.Data.Pending.Remove(pending);
                purchaseState.Save();

                if (hasItem)
                    SendInventoryAdded(player, ownerId, received);
                if (pending.Money != null && pending.Money.Count > 0)
                    SendWallet(player, ownerId, wallet);
                if (pending.SignalAmplifier)
                    player.Send<PioneerGradeInfo>(amplifierInfo, 0U);
                player.Send<OK>(default(OK), header.Seq);

                CashShopRestorationPlugin.Log.LogInfo(
                    "Offline Storage received once: purchase=" + pending.Id +
                    ", commodity=" + pending.CommodityId +
                    ", item=" + (hasItem ? pending.PrototypeId : "currency/effect"));
            }
            catch (Exception ex)
            {
                if (hasItem)
                {
                    context.InventoryItems.RemoveAll(
                        delegate(Item value) { return value.Id == received.Id; });
                    context.Save();
                }
                player.Send<Abort>(default(Abort), header.Seq);
                CashShopRestorationPlugin.Log.LogError(
                    "Offline Storage receive failed: " + ex);
            }
        }

        private static bool TryMakeItem(PendingPurchaseData source, out Item result)
        {
            result = default(Item);
            Item? made = Cheats.MakeItem(source.PrototypeId, Math.Max(1, source.Level));
            if (!made.HasValue) return false;
            result = made.Value;
            if (!string.IsNullOrEmpty(source.ColorR)) result.ColorR = source.ColorR;
            if (!string.IsNullOrEmpty(source.ColorG)) result.ColorG = source.ColorG;
            if (!string.IsNullOrEmpty(source.ColorB)) result.ColorB = source.ColorB;
            if (string.IsNullOrEmpty(source.BlueprintId)) return true;

            Building.Blueprint blueprint = GameSystem<RecipeSystem>.Instance().GetBlueprint(
                source.BlueprintId);
            if (blueprint == null) return false;

            string artifactId = Guid.NewGuid().ToString();
            ArtifactDisplay display = blueprint.GetDefaultDisplay();
            if (source.Parts != null && source.Parts.Count > 0)
            {
                if (display.Parts == null)
                    display.Parts = new Dictionary<string, string>();
                foreach (KeyValuePair<string, string> pair in source.Parts)
                    display.Parts[pair.Key] = pair.Value;
            }
            if (source.Textures != null && source.Textures.Count > 0)
            {
                if (display.Textures == null)
                    display.Textures = new Dictionary<string, string>();
                foreach (KeyValuePair<string, string> pair in source.Textures)
                    display.Textures[pair.Key] = pair.Value;
            }
            display.EntityId = artifactId;

            ArtifactState state = default(ArtifactState);
            state.EntityId = artifactId;
            state.BuildingState = BuildingState.Completed;
            state.Durability = FullGauge();
            state.Level = (byte)Math.Min(255, Math.Max(1, source.Level));
            state.MaxHealth = 1f;

            ArtifactCapsule capsule = default(ArtifactCapsule);
            capsule.EntityId = artifactId;
            capsule.BlueprintId = source.BlueprintId;
            capsule.ArtifactLevel = Math.Max(1, source.Level);
            capsule.Tags = result.Tags ?? new Messages.Tag[0];
            capsule.Performance = result.Performance ?? new Performance[0];
            capsule.Display = display;
            capsule.State = state;
            capsule.LookNames = new Dictionary<string, string>();
            Point2 size = (source.SizeX > 0 && source.SizeY > 0)
                ? new Point2(source.SizeX, source.SizeY) : blueprint.Size;
            capsule.OccupySize = new Point2?(size);
            result.Ext = capsule;
            result.Icon = blueprint.ArtifactIcon;
            result.Name = blueprint.Name;
            return true;
        }

        private static void RepairLegacyCapsules(OfflinePlayer player,
            PlayerContext context, string ownerId)
        {
            List<Item> repaired = new List<Item>();
            for (int i = 0; i < context.InventoryItems.Count; i++)
            {
                Item oldItem = context.InventoryItems[i];
                if (oldItem.Ext is ArtifactCapsule ||
                    string.IsNullOrEmpty(oldItem.Prototype) ||
                    !oldItem.Prototype.StartsWith("capsulated_",
                        StringComparison.Ordinal)) continue;

                PendingPurchaseData source = new PendingPurchaseData();
                source.PrototypeId = oldItem.Prototype;
                source.Level = Math.Max(1, oldItem.Level);
                source.ColorR = oldItem.ColorR;
                source.ColorG = oldItem.ColorG;
                source.ColorB = oldItem.ColorB;
                source.BlueprintId = oldItem.Prototype.Substring(
                    "capsulated_".Length);
                source.Durability = 1f;
                Item fixedItem;
                if (!TryMakeItem(source, out fixedItem)) continue;
                fixedItem.Id = oldItem.Id;
                fixedItem.FounderId = oldItem.FounderId;
                fixedItem.FounderCategory = oldItem.FounderCategory;
                context.InventoryItems[i] = fixedItem;
                repaired.Add(fixedItem);
            }
            if (repaired.Count == 0) return;
            context.Save();
            InventoryUpdated update = default(InventoryUpdated);
            update.EntityId = ownerId;
            update.Items = repaired.ToArray();
            update.RemovedItemIds = new string[0];
            update.ItemOrder = null;
            update.ProtectedItems = null;
            player.Send<InventoryUpdated>(update, 0U);
            CashShopRestorationPlugin.Log.LogInfo(
                "Repaired legacy purchased building capsules: " + repaired.Count);
        }

        private static void PlaceCapsulated(OfflinePlayer player, World world,
            PlayerContext context, string ownerId, PlaceCapsulatedArtifact request,
            PacketHeader header)
        {
            int index = context.InventoryItems.FindIndex(
                delegate(Item value) { return value.Id == request.ItemId; });
            if (index < 0 || !(context.InventoryItems[index].Ext is ArtifactCapsule))
            {
                player.Send<Abort>(default(Abort), header.Seq);
                return;
            }

            Item item = context.InventoryItems[index];
            ArtifactCapsule capsule = (ArtifactCapsule)item.Ext;
            Building.Blueprint blueprint = GameSystem<RecipeSystem>.Instance().GetBlueprint(
                capsule.BlueprintId);
            if (blueprint == null)
            {
                player.Send<Abort>(default(Abort), header.Seq);
                return;
            }

            Point2 size = capsule.OccupySize ?? blueprint.Size;
            string[] args = new string[]
            {
                "capsule",
                blueprint.EntityType.ToString(),
                "position:" + request.Tile.x + "," + request.Tile.y,
                "size:" + size.x + "," + size.y,
                "rotation:" + request.Rotation,
                "level:" + Math.Max(1, capsule.ArtifactLevel)
            };
            AddOns? addOns;
            AppearArtifact? made = Cheats.MakeAppearArtifact(args, out addOns);
            if (!made.HasValue)
            {
                player.Send<Abort>(default(Abort), header.Seq);
                return;
            }

            try
            {
                AppearArtifact artifact = made.Value;
                artifact.IsAlive = true;
                artifact.Height = blueprint.Height;
                artifact.Floor = request.Floor;
                artifact.FounderEntityId = ownerId;
                ArtifactDisplay display = capsule.Display;
                if (display.Parts == null || display.Parts.Count == 0)
                    display = blueprint.GetDefaultDisplay();
                display.EntityId = artifact.EntityId;
                artifact.Display = display;
                ArtifactState state = capsule.State;
                state.EntityId = artifact.EntityId;
                state.BuildingState = BuildingState.Completed;
                state.Durability = FullGauge();
                state.Level = (byte)Math.Min(255, Math.Max(1, capsule.ArtifactLevel));
                if (state.MaxHealth <= 0f) state.MaxHealth = 1f;
                artifact.States = state;
                artifact.Tags = new Messages.Tags
                {
                    EntityId = artifact.EntityId,
                    _Tags = capsule.Tags ?? new Messages.Tag[0]
                };

                context.InventoryItems.RemoveAt(index);
                context.Save();
                world.ConstructArtifact(artifact, addOns);
                SendInventoryRemoved(player, ownerId, item.Id);
                Timer timer = default(Timer);
                timer.Duration = 0.1f;
                player.Send<Timer>(timer, header.Seq);
                CashShopRestorationPlugin.Log.LogInfo(
                    "Placed purchased capsule: blueprint=" + capsule.BlueprintId +
                    ", tile=" + request.Tile);
            }
            catch (Exception ex)
            {
                if (context.InventoryItems.FindIndex(
                    delegate(Item value) { return value.Id == item.Id; }) < 0)
                {
                    context.InventoryItems.Insert(Math.Min(index,
                        context.InventoryItems.Count), item);
                    context.Save();
                }
                player.Send<Abort>(default(Abort), header.Seq);
                CashShopRestorationPlugin.Log.LogError(
                    "Purchased capsule placement failed: " + ex);
            }
        }

        private static void ApplyColors(PendingPurchaseData item, string[] colors)
        {
            if (colors == null) return;
            if (colors.Length > 0) item.ColorR = colors[0];
            if (colors.Length > 1) item.ColorG = colors[1];
            if (colors.Length > 2) item.ColorB = colors[2];
        }

        private static Dictionary<string, string> Copy(
            Dictionary<string, string> source)
        {
            return source == null ? null : new Dictionary<string, string>(source);
        }

        private static Gauge FullGauge()
        {
            return new Gauge(1f, 0f, new GaugeNode[]
            {
                new GaugeNode { Time = 0.0, Value = 1f }
            });
        }

        private static void SendInventoryAdded(OfflinePlayer player,
            string ownerId, Item item)
        {
            InventoryUpdated update = default(InventoryUpdated);
            update.EntityId = ownerId;
            update.Items = new Item[] { item };
            update.RemovedItemIds = new string[0];
            update.ItemOrder = null;
            update.ProtectedItems = null;
            player.Send<InventoryUpdated>(update, 0U);
        }

        private static void SendInventoryRemoved(OfflinePlayer player,
            string ownerId, string itemId)
        {
            InventoryUpdated update = default(InventoryUpdated);
            update.EntityId = ownerId;
            update.Items = new Item[0];
            update.RemovedItemIds = new string[] { itemId };
            update.ItemOrder = null;
            update.ProtectedItems = null;
            player.Send<InventoryUpdated>(update, 0U);
        }

        private static Yaml.Commodity FindCommodity(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            Yaml.Commodities yaml = Singleton<Yaml.Commodities>.Instance;
            if (yaml == null || yaml.PostedCommodities == null) return null;
            Yaml.Commodity value;
            return yaml.PostedCommodities.TryGetValue(id, out value) ? value : null;
        }

        private static bool IsSignalAmplifier(string commodityId)
        {
            return commodityId == "signal_amplifier_package" ||
                commodityId == "signal_amplifier_package_special_deal_01";
        }

        private static void SendWallet(OfflinePlayer player, string ownerId,
            OfflineWalletState wallet)
        {
            WalletUpdated update = default(WalletUpdated);
            update.EntityId = ownerId;
            update.Wallet = wallet.MakeWallet();
            player.Send<WalletUpdated>(update, 0U);
        }

        private static double UnixNow()
        {
            return (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
        }
    }

    internal static class PioneerBridge
    {
        private static MethodInfo _activate;
        private static bool _resolved;

        public static bool Available
        {
            get
            {
                Resolve();
                return _activate != null;
            }
        }

        public static bool Activate(string ownerId, int days, out PioneerGradeInfo info)
        {
            info = default(PioneerGradeInfo);
            Resolve();
            if (_activate == null) return false;
            try
            {
                object value = _activate.Invoke(null, new object[] { ownerId, days });
                if (!(value is PioneerGradeInfo)) return false;
                info = (PioneerGradeInfo)value;
                return true;
            }
            catch (Exception ex)
            {
                CashShopRestorationPlugin.Log.LogError(
                    "Signal Amplifier bridge failed: " + ex);
                return false;
            }
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            Type type = Type.GetType(
                "BaoX.DurangoOriginal.TamedIslandRestoration.TamedPioneerApi, " +
                "TamedIslandRestorationPlugin", false);
            if (type != null)
                _activate = type.GetMethod("ActivateSignalAmplifier",
                    BindingFlags.Public | BindingFlags.Static);
        }
    }

    [HarmonyPatch(typeof(OptionSystem), "IsShopEnabled")]
    internal static class EnableCashShopPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (CashShopRestorationPlugin.Enabled.Value) __result = true;
        }
    }

    internal static class CashShopUiRuntime
    {
        private static readonly MethodInfo InitCommodityList =
            typeof(ShopSystem).GetMethod("InitCommodityList",
                BindingFlags.Instance | BindingFlags.NonPublic);

        public static void EnableMenu()
        {
            MenuSystem menu = GameSystem<MenuSystem>.Instance();
            if (menu != null) menu.EnableMenu(MenuType.Shop, true, true);
        }

        public static void BindGiftButton(PioneerPointPopup popup)
        {
            if (popup == null) return;
            PresetButton button = Traverse.Create(popup).Field("_shopButton").GetValue<PresetButton>();
            if (button == null)
            {
                CashShopRestorationPlugin.Log.LogError(
                    "Pioneer gift button was not found in PioneerPointPopup.");
                return;
            }
            button.Disabled = false;
            button.Clicked = delegate { OpenSignalAmplifier(popup); };
            CashShopRestorationPlugin.Log.LogInfo("Pioneer gift button connected to Cash Shop.");
        }

        private static void OpenSignalAmplifier(PioneerPointPopup popup)
        {
            CashShopRestorationPlugin.Log.LogInfo("Pioneer gift button clicked.");
            EnableMenu();

            ShopSystem shopSystem = GameSystem<ShopSystem>.Instance();
            if (shopSystem == null)
            {
                CashShopRestorationPlugin.Log.LogError("ShopSystem is not available.");
                return;
            }
            if (InitCommodityList != null) InitCommodityList.Invoke(shopSystem, null);

            ShopGroup shop = UIManager.FindScript<ShopGroup>();
            if (shop == null)
            {
                CashShopRestorationPlugin.Log.LogError("ShopGroup UI is not available.");
                return;
            }

            popup.Hide();
            shopSystem.GetPurchasableCommodities(delegate(List<Durango.Logic.Shop.Commodity> list)
            {
                int count = list == null ? 0 : list.Count;
                Durango.Logic.Shop.Commodity signal = shopSystem.GetCommodity(
                    "signal_amplifier_package");
                CashShopRestorationPlugin.Log.LogInfo(
                    "Opening Signal Amplifier shop: commodities=" + count +
                    ", signal-found=" + (signal != null));
                shop.Open("signal_amplifier_package", true);
            }, true);
        }
    }

    // Shop item descriptions normally request a fully-expanded prototype from
    // the retired gateway.  Animal and RandomBox/Express Cargo previews depend
    // on the performance fields in that response (especially pet_entity_type),
    // so icons can load while the 3D viewer remains empty in offline mode.
    // Rebuild the same preview-oriented data from the packaged offline YAML.
    [HarmonyPatch(typeof(PrototypePreset), "Request")]
    internal static class OfflinePrototypePresetPatch
    {
        private static readonly HashSet<string> Reported = new HashSet<string>();

        private static bool Prefix(string prototypeId, int level,
            Action<PrototypePreset> response)
        {
            if (!CashShopRestorationPlugin.Enabled.Value ||
                GameManager.ClusterMode == Mode.Online)
                return true;

            if (response == null || string.IsNullOrEmpty(prototypeId))
                return false;

            try
            {
                PrototypePreset preset = Build(prototypeId, Math.Max(1, level));
                response(preset);
                if (preset != null && Reported.Add(prototypeId))
                    CashShopRestorationPlugin.Log.LogDebug(
                        "Built offline Shop prototype preset: " + prototypeId);
            }
            catch (Exception ex)
            {
                CashShopRestorationPlugin.Log.LogError(
                    "Failed to build offline Shop prototype preset " +
                    prototypeId + ": " + ex);
                response(null);
            }
            return false;
        }

        private static PrototypePreset Build(string requestedId, int level)
        {
            string prototypeId = requestedId.Replace('.', '_');
            Prototype prototype = PrototypeYaml.GetItemPrototype(prototypeId, level) ??
                PrototypeYaml.GetItemPrototype(prototypeId);
            if (prototype == null) return null;

            List<PrototypePresetTag> tags = new List<PrototypePresetTag>();
            if (prototype.Tags != null)
            {
                foreach (KeyValuePair<string, string> pair in prototype.Tags)
                {
                    PrototypePresetTag tag = default(PrototypePresetTag);
                    tag.Id = pair.Key;
                    tag.Level = level;
                    tags.Add(tag);
                }
            }

            List<PrototypePresetPerformance> performances =
                new List<PrototypePresetPerformance>();

            string addOnModel;
            if (PerformanceYaml.TryGetAddOnModelKey(prototypeId, out addOnModel))
                performances.Add(StringPerformance("add_on", new Dictionary<string, string>
                {
                    { "add_on_model_key", addOnModel }
                }));

            PerformanceYaml.Weapon weapon = PerformanceYaml.GetWeapon(prototypeId);
            if (weapon != null)
                performances.Add(StringPerformance("weapon", new Dictionary<string, string>
                {
                    { "weapon_framework", weapon.WeaponFramework },
                    { "model", weapon.Model },
                    { "slot", weapon.Slot }
                }));

            PerformanceYaml.Armor armor = PerformanceYaml.GetArmor(prototypeId);
            if (armor != null)
                performances.Add(StringPerformance("armor", new Dictionary<string, string>
                {
                    { "female_model", armor.FemaleModel },
                    { "male_model", armor.MaleModel },
                    { "slot", armor.Slot }
                }));

            PerformanceYaml.Instrument instrument =
                PerformanceYaml.GetInstrument(prototypeId);
            if (instrument != null)
                performances.Add(StringPerformance("instrument",
                    new Dictionary<string, string>
                    {
                        { "timbre", instrument.Timbre }
                    }));

            PerformanceYaml.Rein rein = PerformanceYaml.GetRein(prototypeId);
            if (rein != null)
            {
                PrototypePresetPerformance pet = new PrototypePresetPerformance();
                pet.Id = "reins";
                pet.Nums = new Dictionary<string, float>
                {
                    { "pet_entity_type", rein.PetEntityType },
                    { "playback_rate", rein.PlaybackRate }
                };
                pet.Strs = new Dictionary<string, string>();
                performances.Add(pet);
            }

            PrototypePreset result = new PrototypePreset();
            result.PrototypeId = prototypeId;
            result.Name = prototype.Name;
            result.Description = prototype.Description;
            result.Icon = prototype.Icon;
            result.Level = level;
            result.MaxDurability = 1f;
            result.ModifiableCount = 0;
            result.Size = Math.Max(1, prototype.Size);
            result.ColorR = prototype.ColorR;
            result.ColorG = prototype.ColorG;
            result.ColorB = prototype.ColorB;
            result.Tags = tags.ToArray();
            result.Performances = performances.ToArray();
            result.ImmuneToTime = prototype.ImmuneToTime;
            result.TradeLocked = false;
            result.DumpLocked = prototype.DumpLocked;
            result.EmotionalMotions = armor == null ? null : armor.EmotionalMotions;
            if (prototypeId.StartsWith("capsulated_", StringComparison.Ordinal))
            {
                result.ExtClassName = "ArtifactCapsule";
                result.ExtClassArgs = new[]
                {
                    prototypeId.Substring("capsulated_".Length)
                };
            }
            return result;
        }

        private static PrototypePresetPerformance StringPerformance(string id,
            Dictionary<string, string> values)
        {
            PrototypePresetPerformance result = new PrototypePresetPerformance();
            result.Id = id;
            result.Nums = new Dictionary<string, float>();
            result.Strs = values;
            return result;
        }
    }

    // The PC title prefab disables embedded mobile currency widgets in Awake.
    // Retain them only for the restored Shop. Other screens must keep the retail
    // PC behavior; otherwise Skill gets a duplicate SP widget and narrow panels
    // such as Bag receive currencies anchored outside their title bounds.
    [HarmonyPatch(typeof(CurrencyWidgetTweakerForPC), "Awake")]
    internal static class OfflineCurrencyWidgetTweakerPatch
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(CurrencyWidgetTweakerForPC __instance)
        {
            if (!CashShopRestorationPlugin.Enabled.Value ||
                GameManager.ClusterMode == Mode.Online)
                return true;

            return __instance.GetComponentInParent<ShopGroup>() == null;
        }
    }

    // The retail client deliberately refuses to create the standard currency
    // widget outside Mode.Online.  The restored shop still uses the same
    // InventorySystem wallet, so build the original widget in offline mode and
    // let its normal WalletUpdated subscription keep the amount current.
    [HarmonyPatch(typeof(CurrencyWidget), "MakeComponent")]
    internal static class OfflineCurrencyWidgetPatch
    {
        private static readonly FieldInfo PresetPrefabField =
            AccessTools.Field(typeof(CurrencyWidgetBase), "_presetPrefab");
        private static readonly FieldInfo ComponentField =
            AccessTools.Field(typeof(CurrencyWidgetBase), "_component");
        private static readonly FieldInfo HideExtraButtonField =
            AccessTools.Field(typeof(CurrencyWidgetBase), "_hideExtraButton");
        private static readonly MethodInfo RefreshMethod =
            typeof(CurrencyWidgetBase).GetMethod("Refresh",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private static bool Prefix(CurrencyWidget __instance, ref bool __result)
        {
            if (!CashShopRestorationPlugin.Enabled.Value ||
                GameManager.ClusterMode == Mode.Online)
                return true;

            try
            {
                PresetCurrencyWidget preset =
                    (PresetCurrencyWidget)PresetPrefabField.GetValue(__instance);
                if (preset == null)
                {
                    __result = false;
                    return false;
                }

                PresetCurrencyWidget component =
                    (PresetCurrencyWidget)ComponentField.GetValue(__instance);
                if (component == null)
                {
                    GameObject clone = UnityEngine.Object.Instantiate<GameObject>(
                        preset.gameObject, __instance.transform);
                    component = clone.GetComponent<PresetCurrencyWidget>();
                    ComponentField.SetValue(__instance, component);
                    component.Init();
                    component.HideExtraButton(
                        (bool)HideExtraButtonField.GetValue(__instance));
                }

                RefreshMethod.Invoke(__instance, null);
                __result = true;
            }
            catch (Exception ex)
            {
                CashShopRestorationPlugin.Log.LogError(
                    "Failed to restore offline currency widget: " + ex);
                __result = false;
            }
            return false;
        }
    }

    // MenuListGroup hides the button which opens the full wallet summary while
    // offline.  CurrencyWidget is restored above, so expose the retail button
    // as well (Coin, Gem, T-Stone, Mileage, RPiece and Warp Matter).
    [HarmonyPatch(typeof(MenuListGroup), "Start")]
    internal static class OfflineWalletButtonPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(MenuListGroup __instance)
        {
            if (!CashShopRestorationPlugin.Enabled.Value ||
                GameManager.ClusterMode == Mode.Online)
                return;

            GameObject button = Traverse.Create(__instance)
                .Field("_walletPopupButton").GetValue<GameObject>();
            if (button != null) button.SetActive(true);
        }
    }

    [HarmonyPatch(typeof(PioneerPointPopup), "FillData")]
    internal static class PioneerGiftButtonPatch
    {
        private static void Postfix(PioneerPointPopup __instance)
        {
            if (CashShopRestorationPlugin.Enabled.Value)
                CashShopUiRuntime.BindGiftButton(__instance);
        }
    }

    [HarmonyPatch(typeof(MenuSystem), "IsHiddenMenu")]
    internal static class CashShopHiddenMenuPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(MenuType type, ref bool __result)
        {
            if (CashShopRestorationPlugin.Enabled.Value && type == MenuType.Shop)
                __result = false;
        }
    }

    [HarmonyPatch(typeof(MenuSystem), "GameManager_MainSceneLoaded")]
    internal static class CashShopMainSceneMenuPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix()
        {
            if (CashShopRestorationPlugin.Enabled.Value) CashShopUiRuntime.EnableMenu();
        }
    }

    [HarmonyPatch(typeof(MenuSystem), "OnWelcome")]
    internal static class CashShopWelcomeMenuPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix()
        {
            if (CashShopRestorationPlugin.Enabled.Value) CashShopUiRuntime.EnableMenu();
        }
    }

    [HarmonyPatch(typeof(OfflinePlayer), MethodType.Constructor, new Type[]
    {
        typeof(string), typeof(OfflineConnection), typeof(World), typeof(PlayerContext), typeof(bool)
    })]
    internal static class CashShopBackendPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(OfflinePlayer __instance, string entityId,
            OfflineConnection connection, World world, PlayerContext context,
            bool isLocalPlayer)
        {
            if (!CashShopRestorationPlugin.Enabled.Value || !isLocalPlayer ||
                connection == null || context == null) return;
            OfflineCashShopBackend.Register(__instance, connection, world,
                context, entityId);
        }
    }
}
