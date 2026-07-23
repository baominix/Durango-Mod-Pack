using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Durango.Offline;
using Durango.UI;
using HarmonyLib;
using Messages;
using UnityEngine;
using Yaml;
using OfflineConnection = Durango.Offline.Connection;
using OfflinePlayer = Durango.Offline.Player;
using PacketHeader = Durango.Network.PacketHeader;

namespace BaoX.DurangoOriginal.TamedIslandRestoration
{
    internal sealed class TamedPioneerState
    {
        private static readonly Dictionary<string, TamedPioneerState> States =
            new Dictionary<string, TamedPioneerState>();

        private static readonly int[] GradePoints =
        {
            0, 0, 657, 1458, 2809, 5305, 7394, 12261,
            15811, 19771, 25954, 40848, 41701, 56857, 72561, 102857
        };

        private readonly ConfigEntry<int> _grade;
        private readonly ConfigEntry<float> _point;
        private readonly ConfigEntry<string> _dailyPoints;
        private readonly ConfigEntry<string> _dailyDate;
        private readonly ConfigEntry<double> _paymentEndsAt;

        private TamedPioneerState(string ownerId)
        {
            string section = "Pioneer Progress " + Sanitize(ownerId);
            _grade = TamedIslandRestorationPlugin.PluginConfig.Bind(section, "Grade", 1,
                "Persistent Tamed Island Pioneer Rank (1 through 15).");
            _point = TamedIslandRestorationPlugin.PluginConfig.Bind(section, "Point", 0f,
                "Points accumulated toward the next Pioneer Rank.");
            _dailyPoints = TamedIslandRestorationPlugin.PluginConfig.Bind(section,
                "DailyExchangedPoints", string.Empty,
                "Original daily Pioneer exchange-rate usage, serialized as rate=value pairs.");
            _dailyDate = TamedIslandRestorationPlugin.PluginConfig.Bind(section,
                "DailyExchangeDateUtc", DateTime.UtcNow.ToString("yyyyMMdd"),
                "UTC date for the daily Pioneer exchange-rate counters.");
            _paymentEndsAt = TamedIslandRestorationPlugin.PluginConfig.Bind(section,
                "SignalAmplifierEndsAt", 0.0,
                "Unix timestamp at which the Signal Amplifier 400% zone expires.");
            Normalize();
        }

        public int Grade
        {
            get { return Math.Max(1, Math.Min(15, _grade.Value)); }
        }

        public float Point
        {
            get { return Math.Max(0f, _point.Value); }
        }

        public static TamedPioneerState Get(string ownerId)
        {
            string key = string.IsNullOrEmpty(ownerId) ? "local-player" : ownerId;
            TamedPioneerState state;
            if (!States.TryGetValue(key, out state))
            {
                state = new TamedPioneerState(key);
                States[key] = state;
            }
            state.ResetDailyIfNeeded();
            return state;
        }

        public static int GetMaximumEstateSize(string ownerId)
        {
            return Get(ownerId).MaximumEstateSize;
        }

        public int MaximumEstateSize
        {
            get
            {
                int grade = Grade;
                if (grade >= 15) return 350;
                if (grade >= 11) return 300;
                if (grade >= 7) return 250;
                if (grade >= 6) return 200;
                if (grade >= 2) return 150;
                return 100;
            }
        }

        public int AccessLevel
        {
            get
            {
                int grade = Grade;
                if (grade >= 13) return 8;
                if (grade >= 11) return 7;
                if (grade >= 9) return 6;
                if (grade >= 7) return 5;
                if (grade >= 5) return 4;
                if (grade >= 4) return 3;
                if (grade >= 2) return 2;
                return 1;
            }
        }

        public int NextGradePoint
        {
            get { return Grade >= 15 ? 0 : GradePoints[Grade + 1]; }
        }

        public double? PaymentEndsAt
        {
            get
            {
                double value = _paymentEndsAt.Value;
                return value > UnixNow() ? new double?(value) : null;
            }
        }

        public bool IsPaid
        {
            get { return PaymentEndsAt != null; }
        }

        public double ActivateSignalAmplifier(int days)
        {
            days = Math.Max(1, days);
            double now = UnixNow();
            double current = _paymentEndsAt.Value;
            if (current > now)
            {
                _paymentEndsAt.Value = current + days * 86400.0;
            }
            else
            {
                // Retail counted the purchase day and expired at 23:59 on day 7.
                DateTime localEnd = DateTime.Now.Date.AddDays(days).AddSeconds(-1.0);
                _paymentEndsAt.Value = (localEnd.ToUniversalTime() -
                    new DateTime(1970, 1, 1)).TotalSeconds;
            }
            TamedIslandRestorationPlugin.PluginConfig.Save();
            return _paymentEndsAt.Value;
        }

        public Dictionary<float, float> GetDailyPoints()
        {
            Dictionary<float, float> result = new Dictionary<float, float>();
            string value = _dailyPoints.Value;
            if (string.IsNullOrEmpty(value)) return result;
            string[] pairs = value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < pairs.Length; i++)
            {
                string[] parts = pairs[i].Split('=');
                float rate;
                float points;
                if (parts.Length == 2 &&
                    float.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out rate) &&
                    float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out points))
                {
                    result[rate] = Math.Max(0f, points);
                }
            }
            return result;
        }

        public void Update(int grade, float point, Dictionary<float, float> dailyPoints)
        {
            _grade.Value = Math.Max(1, Math.Min(15, grade));
            _point.Value = _grade.Value >= 15 ? 0f : Math.Max(0f, point);
            _dailyPoints.Value = SerializeDailyPoints(dailyPoints);
            _dailyDate.Value = DateTime.UtcNow.ToString("yyyyMMdd");
            TamedIslandRestorationPlugin.PluginConfig.Save();
        }

        public PioneerGradeInfo MakeInfo(string ownerId, float? pointAdded)
        {
            PioneerGradeInfo info = default(PioneerGradeInfo);
            info.EntityId = ownerId;
            info.Grade = Grade;
            info.Point = Point;
            info.PointNeeded = NextGradePoint;
            info.DailyExchangedPoints = GetDailyPoints();
            info.CurrentMaximumEstateSize = MaximumEstateSize;
            info.CurrentAccessLevel = AccessLevel;
            info.PointAdded = pointAdded;
            info.PaymentEndsAt = PaymentEndsAt;
            return info;
        }

        public bool IsBlueprintUnlocked(string blueprintId)
        {
            int requiredGrade = GetBlueprintGrade(blueprintId);
            return requiredGrade <= 0 || Grade >= requiredGrade;
        }

        private void Normalize()
        {
            int grade = Grade;
            float point = Point;
            int needed = grade >= 15 ? 0 : GradePoints[grade + 1];
            if (grade >= 15) point = 0f;
            else if (needed > 0 && point >= needed) point = needed - 0.001f;
            if (_grade.Value != grade || Math.Abs(_point.Value - point) > 0.0001f)
            {
                _grade.Value = grade;
                _point.Value = point;
                TamedIslandRestorationPlugin.PluginConfig.Save();
            }
        }

        private void ResetDailyIfNeeded()
        {
            string today = DateTime.UtcNow.ToString("yyyyMMdd");
            if (_dailyDate.Value == today) return;
            _dailyDate.Value = today;
            _dailyPoints.Value = string.Empty;
            TamedIslandRestorationPlugin.PluginConfig.Save();
        }

        private static int GetBlueprintGrade(string blueprintId)
        {
            if (blueprintId == "living_tech_01") return 3;
            if (blueprintId == "light_tech_01") return 4;
            if (blueprintId == "heavy_tech_01") return 5;
            if (blueprintId == "living_tech_02") return 8;
            if (blueprintId == "light_tech_02") return 9;
            if (blueprintId == "heavy_tech_02") return 10;
            if (blueprintId == "living_tech_03") return 12;
            if (blueprintId == "light_tech_03") return 13;
            if (blueprintId == "heavy_tech_03") return 14;
            return 0;
        }

        private static string SerializeDailyPoints(Dictionary<float, float> values)
        {
            if (values == null || values.Count == 0) return string.Empty;
            List<string> parts = new List<string>();
            foreach (KeyValuePair<float, float> pair in values)
            {
                parts.Add(pair.Key.ToString(System.Globalization.CultureInfo.InvariantCulture) + "=" +
                    pair.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            return string.Join(";", parts.ToArray());
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "local-player";
            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_') chars[i] = '_';
            }
            return new string(chars);
        }

        private static double UnixNow()
        {
            return (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
        }
    }

    /// <summary>
    /// Public bridge used by the separately restored offline Cash Shop. Keeping the
    /// Pioneer state here ensures the paid rate, popup timer and item conversion all
    /// read the same persisted expiration timestamp.
    /// </summary>
    public static class TamedPioneerApi
    {
        public static PioneerGradeInfo ActivateSignalAmplifier(string ownerId, int days)
        {
            TamedPioneerState state = TamedPioneerState.Get(ownerId);
            state.ActivateSignalAmplifier(days);
            return state.MakeInfo(ownerId, null);
        }
    }

    internal static class TamedPioneerBackend
    {
        public static void Register(OfflinePlayer player, OfflineConnection connection,
            PlayerContext context, string ownerId)
        {
            TamedPioneerState state = TamedPioneerState.Get(ownerId);
            TamedPioneerItemData.NormalizeInventory(context);

            connection.Recv<GetPioneerGradeInfo>(delegate(GetPioneerGradeInfo request, PacketHeader header)
            {
                player.Send<PioneerGradeInfo>(state.MakeInfo(ownerId, null), header.Seq);
            });

            connection.Recv<UseItemsForPioneerPoint>(delegate(UseItemsForPioneerPoint request, PacketHeader header)
            {
                HandleUseItems(player, context, ownerId, state, request, header);
            });

            // CraftBuild's general offline backend intentionally exposes every normal
            // blueprint. Only the nine original Pioneer lab rewards are rank-gated here.
            connection.Recv<GetArtifactBlueprints>(delegate(GetArtifactBlueprints request, PacketHeader header)
            {
                List<string> ids = new List<string>();
                RecipeSystem recipeSystem = GameSystem<RecipeSystem>.Instance();
                if (recipeSystem != null && recipeSystem.RecipeContainer != null)
                {
                    foreach (Building.Blueprint blueprint in recipeSystem.RecipeContainer.GetAllBlueprints())
                    {
                        if (state.IsBlueprintUnlocked(blueprint.Id)) ids.Add(blueprint.Id);
                    }
                }
                ArtifactBlueprints result = default(ArtifactBlueprints);
                result.Ids = ids.ToArray();
                result.LikedBlueprintIds = new string[0];
                result.NewBlueprintIds = new string[0];
                player.Send<ArtifactBlueprints>(result, header.Seq);
            });

            TamedIslandRestorationPlugin.Log.LogInfo(
                "Pioneer progression ready: owner=" + ownerId + ", grade=" + state.Grade +
                ", point=" + state.Point + ", plots=" + state.MaximumEstateSize);
        }

        private static void HandleUseItems(OfflinePlayer player, PlayerContext context,
            string ownerId, TamedPioneerState state, UseItemsForPioneerPoint request,
            PacketHeader header)
        {
            if (context == null || request.ItemIds == null || request.ItemIds.Length == 0 ||
                state.Grade >= 15)
            {
                player.Send<Abort>(default(Abort), header.Seq);
                return;
            }

            List<int> indexes = new List<int>();
            List<string> removedIds = new List<string>();
            HashSet<string> uniqueIds = new HashSet<string>();
            float itemPoints = 0f;
            for (int i = 0; i < request.ItemIds.Length; i++)
            {
                string itemId = request.ItemIds[i];
                if (string.IsNullOrEmpty(itemId) || !uniqueIds.Add(itemId))
                {
                    player.Send<Abort>(default(Abort), header.Seq);
                    return;
                }

                int index = FindItem(context.InventoryItems, itemId);
                if (index < 0 || !IsValidPioneerItem(context.InventoryItems[index], state.AccessLevel))
                {
                    player.Send<Abort>(default(Abort), header.Seq);
                    return;
                }
                indexes.Add(index);
                removedIds.Add(itemId);
                itemPoints += context.InventoryItems[index].PioneerCost;
            }

            int oldGrade = state.Grade;
            float oldPoint = state.Point;
            int grade = oldGrade;
            float point = oldPoint;
            Dictionary<float, float> dailyPoints = state.GetDailyPoints();
            PioneerPointCalculator.Run(dailyPoints, ref grade, ref point,
                state.IsPaid, itemPoints, null);

            float credited = CalculateCreditedPoints(oldGrade, oldPoint, grade, point);
            if (credited <= 0f)
            {
                player.Send<Abort>(default(Abort), header.Seq);
                return;
            }

            indexes.Sort();
            for (int i = indexes.Count - 1; i >= 0; i--) context.InventoryItems.RemoveAt(indexes[i]);
            context.Save();
            state.Update(grade, point, dailyPoints);

            InventoryUpdated inventory = default(InventoryUpdated);
            inventory.EntityId = ownerId;
            inventory.Items = new Item[0];
            inventory.RemovedItemIds = removedIds.ToArray();
            inventory.ItemOrder = null;
            inventory.ProtectedItems = null;
            player.Send<InventoryUpdated>(inventory, 0U);
            player.Send<PioneerGradeInfo>(state.MakeInfo(ownerId, credited), 0U);
            player.Send<OK>(default(OK), header.Seq);

            TamedIslandRestorationPlugin.Log.LogInfo(
                "Pioneer materials transmitted: items=" + removedIds.Count +
                ", raw=" + itemPoints + ", credited=" + credited +
                ", grade=" + oldGrade + "->" + state.Grade +
                ", point=" + oldPoint + "->" + state.Point +
                ", plots=" + state.MaximumEstateSize);
        }

        private static int FindItem(List<Item> items, string itemId)
        {
            if (items == null) return -1;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Id == itemId) return i;
            }
            return -1;
        }

        private static bool IsValidPioneerItem(Item item, int accessLevel)
        {
            if (item.PioneerCost <= 0f || !item.Tradable || item.Tags == null) return false;
            for (int i = 0; i < item.Tags.Length; i++)
            {
                if (item.Tags[i].Id == "pioneering_material" && item.Tags[i].Level <= accessLevel)
                {
                    return true;
                }
            }
            return false;
        }

        private static float CalculateCreditedPoints(int oldGrade, float oldPoint,
            int newGrade, float newPoint)
        {
            if (newGrade == oldGrade) return Math.Max(0f, newPoint - oldPoint);
            float result = Math.Max(0f, GetNextPoint(oldGrade) - oldPoint);
            for (int grade = oldGrade + 1; grade < newGrade; grade++) result += GetNextPoint(grade);
            result += Math.Max(0f, newPoint);
            return result;
        }

        private static int GetNextPoint(int grade)
        {
            int[] values =
            {
                0, 657, 1458, 2809, 5305, 7394, 12261, 15811,
                19771, 25954, 40848, 41701, 56857, 72561, 102857
            };
            return grade >= 1 && grade < 15 ? values[grade] : 0;
        }
    }

    /// <summary>
    /// The retail server supplied PioneerCost and the effective level of
    /// pioneering_material on every inventory item. Restore those Pioneer-owned
    /// fields from shipped prototype data. Tradable belongs to TradeAvailablePlugin.
    /// </summary>
    internal static class TamedPioneerItemData
    {
        public static int NormalizeInventory(PlayerContext context)
        {
            if (context == null || context.InventoryItems == null) return 0;
            int changed = 0;
            for (int i = 0; i < context.InventoryItems.Count; i++)
            {
                Item item = context.InventoryItems[i];
                if (!Normalize(ref item)) continue;
                context.InventoryItems[i] = item;
                changed++;
            }
            if (changed > 0) context.Save();
            return changed;
        }

        public static bool Normalize(ref Item item)
        {
            bool changed = false;

            if (item.Tags == null || item.Tags.Length == 0) return changed;

            int tagIndex = -1;
            for (int i = 0; i < item.Tags.Length; i++)
            {
                if (item.Tags[i].Id == "pioneering_material")
                {
                    tagIndex = i;
                    break;
                }
            }
            if (tagIndex < 0) return changed;

            int tagLevel = Math.Max(1, item.Tags[tagIndex].Level);
            Prototype prototype = PrototypeYaml.GetItemPrototype(item.Prototype);
            if (prototype != null && prototype.Tags != null)
            {
                string configuredLevel;
                int parsedLevel;
                if (prototype.Tags.TryGetValue("pioneering_material", out configuredLevel) &&
                    int.TryParse(configuredLevel, out parsedLevel) && parsedLevel > 0)
                {
                    tagLevel = parsedLevel;
                }
            }

            if (item.Tags[tagIndex].Level != tagLevel)
            {
                Messages.Tag tag = item.Tags[tagIndex];
                tag.Level = tagLevel;
                item.Tags[tagIndex] = tag;
                changed = true;
            }

            // constants.pioneer_cost: cost * (1.501 ** (tag_level - 1)).
            // Offline has no economy/server base cost, therefore one material unit
            // is the neutral base cost while higher tag levels retain the retail
            // exponential weighting.
            float pioneerCost = (float)Math.Pow(1.501, tagLevel - 1);
            if (Math.Abs(item.PioneerCost - pioneerCost) > 0.0001f)
            {
                item.PioneerCost = pioneerCost;
                changed = true;
            }
            return changed;
        }

        public static void AttachPioneerMaterial(ref Item item, int tagLevel)
        {
            tagLevel = Math.Max(1, Math.Min(8, tagLevel));
            Normalize(ref item);

            List<Messages.Tag> tags = item.Tags == null
                ? new List<Messages.Tag>()
                : new List<Messages.Tag>(item.Tags);
            int index = -1;
            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i].Id == "pioneering_material")
                {
                    index = i;
                    break;
                }
            }
            Messages.Tag pioneerTag = new Messages.Tag
            {
                Id = "pioneering_material",
                Level = tagLevel
            };
            if (index >= 0) tags[index] = pioneerTag;
            else tags.Add(pioneerTag);

            item.Tags = tags.ToArray();
            item.PioneerCost = (float)Math.Pow(1.501, tagLevel - 1);
        }

    }

    [HarmonyPatch(typeof(Cheats), "MakeItem")]
    internal static class TamedPioneerMakeItemPatch
    {
        private static void Postfix(ref Item? __result)
        {
            if (!TamedIslandRestorationPlugin.Enabled.Value || !__result.HasValue) return;
            Item item = __result.Value;
            if (TamedPioneerItemData.Normalize(ref item)) __result = item;
        }
    }

    [HarmonyPatch(typeof(OfflinePlayer), "HandleCheatMsg")]
    internal static class TamedPioneerGiveItemCheatPatch
    {
        private static bool Prefix(OfflinePlayer __instance, string cheat, uint seq)
        {
            if (!TamedIslandRestorationPlugin.Enabled.Value || string.IsNullOrEmpty(cheat)) return true;
            string[] args = cheat.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (args.Length == 0 || !string.Equals(args[0], "pioneer_it", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            int level;
            int count;
            int tagLevel;
            if (args.Length != 5 || !int.TryParse(args[2], out level) ||
                !int.TryParse(args[3], out count) || !int.TryParse(args[4], out tagLevel) ||
                level < 1 || level > 100 || count < 1 || count > 200 ||
                tagLevel < 1 || tagLevel > 8)
            {
                __instance.Send<Info>(new Info
                {
                    Text = "Invalid givepioneer arguments."
                }, seq);
                return false;
            }

            List<Item> items = new List<Item>();
            for (int i = 0; i < count; i++)
            {
                Item? created = Cheats.MakeItem(args[1], level);
                if (!created.HasValue) break;
                Item item = created.Value;
                TamedPioneerItemData.AttachPioneerMaterial(ref item, tagLevel);
                items.Add(item);
            }

            if (items.Count == 0)
            {
                __instance.Send<Info>(new Info
                {
                    Text = "Unknown item prototype: " + args[1]
                }, seq);
                return false;
            }

            __instance.AddItems(items);
            InventoryUpdated inventory = default(InventoryUpdated);
            inventory.EntityId = __instance.EntityId;
            inventory.Items = items.ToArray();
            __instance.Send<InventoryUpdated>(inventory, 0U);
            __instance.Send<Info>(new Info
            {
                Text = items.Count + " x " + args[1] + " / Pioneer Material Lv." + tagLevel
            }, seq);
            TamedIslandRestorationPlugin.Log.LogInfo(
                "Give Pioneer item command: prototype=" + args[1] + ", level=" + level +
                ", count=" + items.Count + ", tag-level=" + tagLevel);
            return false;
        }
    }
}
