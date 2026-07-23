using System;
using System.Collections.Generic;
using Durango.Network;
using InteractionData;
using Messages;
using Shared.Item;
using UnityEngine;

namespace BaoX.DurangoOriginal.OfflineCombat
{
    internal static class BrachioLootRuntime
    {
        private const int BrachioEntityType = 2004;
        private const int EliteBrachioEntityType = 2124;
        private const string CollectibleId = "brachio";

        private sealed class LootDefinition
        {
            internal string GeneratorId;
            internal string PrototypeId;
            internal string Name;
            internal string Icon;
            internal int Minimum;
            internal int Maximum;
            internal float Chance;
            internal float Duration;
        }

        private sealed class LootEntry
        {
            internal LootDefinition Definition;
            internal int Amount;
        }

        private sealed class LootState
        {
            internal string EntityId;
            internal string EntityName;
            internal int Level;
            internal AnimalBehavior Animal;
            internal readonly List<LootEntry> Entries = new List<LootEntry>();
        }

        private sealed class PendingCollect
        {
            internal Durango.Offline.Player Player;
            internal string EntityId;
            internal string GeneratorId;
            internal uint ReplyOf;
            internal float DueAt;
        }

        private static readonly LootDefinition[] Definitions =
        {
            Define("fat", "fat", "Fat", "icon_nat_fat", 2, 4, 0.80f, 5.0f),
            Define("meat", "meat", "Meat", "icon_nat_meat", 12, 16, 1.00f, 4.1f),
            Define("bone_head_big", "bone_head_big", "Skull", "icon_nat_bone_head_big", 1, 1, 0.45f, 7.5f),
            Define("leather_raw", "leather_raw", "Leather", "icon_nat_leather", 8, 12, 1.00f, 4.1f),
            Define("bone_leg_thick", "bone_leg_thick", "Large Leg Bone", "bone_leg_big", 2, 4, 0.85f, 7.5f),
            Define("bone_rib", "bone_rib", "Rib", "icon_nat_bone_rib", 1, 1, 0.60f, 6.9f),
            Define("tendon", "tendon", "Tendon", "icon_nat_tendon", 1, 3, 0.75f, 4.6f),
            Define("organ", "organ", "Intestine", "icon_nat_organ", 2, 4, 0.75f, 4.6f),
            Define("meat_serloin", "meat_serloin", "Sirloin", "icon_nat_meat_serloin", 1, 3, 0.65f, 6.9f)
        };

        private static readonly Dictionary<string, LootState> States =
            new Dictionary<string, LootState>(StringComparer.Ordinal);
        private static readonly List<PendingCollect> Pending =
            new List<PendingCollect>();

        private static LootDefinition Define(
            string generatorId,
            string prototypeId,
            string name,
            string icon,
            int minimum,
            int maximum,
            float chance,
            float duration)
        {
            return new LootDefinition
            {
                GeneratorId = generatorId,
                PrototypeId = prototypeId,
                Name = name,
                Icon = icon,
                Minimum = minimum,
                Maximum = maximum,
                Chance = chance,
                Duration = duration
            };
        }

        internal static void Register(
            Durango.Offline.Player player,
            Durango.Offline.Connection connection)
        {
            connection.Recv<Collect>(delegate(Collect message, PacketHeader header)
            {
                BeginCollect(player, message, header.Seq);
            });
            connection.Recv<GetCollectible>(delegate(GetCollectible message, PacketHeader header)
            {
                SendCollectible(player, message.EntityId, header.Seq);
            });
            connection.Recv<GiveUpDistribution>(delegate(
                GiveUpDistribution message,
                PacketHeader header)
            {
                GiveUp(player, message.EntityId);
            });
            connection.Recv<Canceled>(delegate(Canceled message, PacketHeader header)
            {
                Cancel(player);
            });
        }

        internal static void Create(
            Durango.Offline.Player player,
            AnimalBehavior animal)
        {
            if (player == null || animal == null ||
                (animal.EntityTypeId != BrachioEntityType &&
                 animal.EntityTypeId != EliteBrachioEntityType) ||
                string.IsNullOrEmpty(animal.EntityId) ||
                States.ContainsKey(animal.EntityId))
            {
                return;
            }

            LootState state = new LootState();
            state.EntityId = animal.EntityId;
            state.EntityName = string.IsNullOrEmpty(animal.GetName())
                ? "Brachiosaurus"
                : animal.GetName();
            state.Level = Mathf.Clamp(animal.Level, 1, 60);
            state.Animal = animal;

            for (int i = 0; i < Definitions.Length; i++)
            {
                LootDefinition definition = Definitions[i];
                if (definition.Chance < 1f && UnityEngine.Random.value > definition.Chance)
                {
                    continue;
                }

                state.Entries.Add(new LootEntry
                {
                    Definition = definition,
                    Amount = UnityEngine.Random.Range(
                        definition.Minimum,
                        definition.Maximum + 1)
                });
            }

            States[state.EntityId] = state;
            animal.IsLootable = true;
            SendPermission(player, state.EntityId, true);
            OfflineCombatBackendPlugin.Log.LogInfo(
                "Brachio loot created entity=" + state.EntityId +
                " level=" + state.Level +
                " types=" + state.Entries.Count +
                " total=" + Remaining(state));
        }

        internal static bool ApplyToTouched(ref Touched message)
        {
            LootState state;
            if (string.IsNullOrEmpty(message.EntityId) ||
                !States.TryGetValue(message.EntityId, out state))
            {
                return false;
            }

            message.EntityName = state.EntityName;
            message.Level = state.Level;
            message.PrototypeId = string.Empty;
            message.Interactions = Remaining(state) > 0
                ? new int[] { (int)Interaction.Collect }
                : new int[0];
            message.DisabledInteractions = new int[0];
            message.AccessDeniedInteractions = new int[0];
            message.Collectible = BuildCollectible(state);
            return true;
        }

        internal static bool UsesButcherMotion(string generatorId, int animalType)
        {
            if (animalType != BrachioEntityType &&
                animalType != EliteBrachioEntityType)
            {
                return false;
            }

            for (int i = 0; i < Definitions.Length; i++)
            {
                if (string.Equals(
                    Definitions[i].GeneratorId,
                    generatorId,
                    StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        internal static void Tick()
        {
            float now = Time.unscaledTime;
            for (int i = Pending.Count - 1; i >= 0; i--)
            {
                PendingCollect pending = Pending[i];
                if (pending.DueAt > now)
                {
                    continue;
                }

                Pending.RemoveAt(i);
                CompleteCollect(pending);
            }
        }

        internal static void Reset()
        {
            Pending.Clear();
            States.Clear();
        }

        private static void BeginCollect(
            Durango.Offline.Player player,
            Collect message,
            uint replyOf)
        {
            LootState state;
            LootEntry entry;
            if (player == null || string.IsNullOrEmpty(message.EntityId) ||
                !States.TryGetValue(message.EntityId, out state) ||
                !TryGetEntry(state, message.GeneratorId, out entry) ||
                entry.Amount <= 0)
            {
                return;
            }

            Cancel(player);
            float duration = Mathf.Max(0.1f, entry.Definition.Duration);
            player.Send<Messages.Timer>(new Messages.Timer
            {
                Duration = duration
            }, replyOf);
            Pending.Add(new PendingCollect
            {
                Player = player,
                EntityId = state.EntityId,
                GeneratorId = entry.Definition.GeneratorId,
                ReplyOf = replyOf,
                DueAt = Time.unscaledTime + duration
            });
            OfflineCombatBackendPlugin.Log.LogInfo(
                "Brachio collect started entity=" + state.EntityId +
                " item=" + entry.Definition.Name +
                " remaining=" + entry.Amount +
                " duration=" + duration.ToString("F1"));
        }

        private static void CompleteCollect(PendingCollect pending)
        {
            LootState state;
            LootEntry entry;
            if (pending == null || pending.Player == null ||
                !States.TryGetValue(pending.EntityId, out state) ||
                !TryGetEntry(state, pending.GeneratorId, out entry) ||
                entry.Amount <= 0)
            {
                return;
            }

            List<Item> items = new List<Item>();
            Item? made = Durango.Offline.Cheats.MakeItem(
                entry.Definition.PrototypeId,
                state.Level);
            if (made != null)
            {
                Item item = made.Value;
                item.Name = entry.Definition.Name;
                item.Icon = entry.Definition.Icon;
                item.CollectibleId = CollectibleId;
                item.GeneratorId = entry.Definition.GeneratorId;
                items.Add(item);
                pending.Player.AddItems(items);
                pending.Player.Send<InventoryUpdated>(new InventoryUpdated
                {
                    EntityId = pending.Player.EntityId,
                    Items = items.ToArray()
                }, 0U);
                entry.Amount--;
            }

            bool ranOut = Remaining(state) <= 0;
            pending.Player.Send<Collected>(new Collected
            {
                Items = items.ToArray(),
                Result = items.Count > 0 ? Result.Success : Result.Failure,
                RanOut = ranOut
            }, pending.ReplyOf);
            pending.Player.Send<CollectibleChanged>(new CollectibleChanged
            {
                EntityId = state.EntityId
            }, 0U);

            if (ranOut)
            {
                if (state.Animal != null)
                {
                    state.Animal.IsLootable = false;
                }
                SendPermission(pending.Player, state.EntityId, false);
            }

            OfflineCombatBackendPlugin.Log.LogInfo(
                "Brachio item collected entity=" + state.EntityId +
                " item=" + entry.Definition.Name +
                " left=" + entry.Amount +
                " totalLeft=" + Remaining(state));
        }

        private static void SendCollectible(
            Durango.Offline.Player player,
            string entityId,
            uint replyOf)
        {
            LootState state;
            if (player != null && !string.IsNullOrEmpty(entityId) &&
                States.TryGetValue(entityId, out state))
            {
                player.Send<Collectible>(BuildCollectible(state), replyOf);
            }
        }

        private static void GiveUp(Durango.Offline.Player player, string entityId)
        {
            LootState state;
            if (player == null || string.IsNullOrEmpty(entityId) ||
                !States.TryGetValue(entityId, out state))
            {
                return;
            }

            for (int i = 0; i < state.Entries.Count; i++)
            {
                state.Entries[i].Amount = 0;
            }
            if (state.Animal != null)
            {
                state.Animal.IsLootable = false;
            }
            SendPermission(player, entityId, false);
            player.Send<CollectibleChanged>(new CollectibleChanged
            {
                EntityId = entityId
            }, 0U);
            OfflineCombatBackendPlugin.Log.LogInfo(
                "Brachio loot given up entity=" + entityId);
        }

        private static void Cancel(Durango.Offline.Player player)
        {
            if (player == null)
            {
                return;
            }
            Pending.RemoveAll(delegate(PendingCollect pending)
            {
                return pending.Player == player;
            });
        }

        private static Collectible BuildCollectible(LootState state)
        {
            List<Generator> generators = new List<Generator>();
            for (int i = 0; i < state.Entries.Count; i++)
            {
                LootEntry entry = state.Entries[i];
                if (entry.Amount <= 0)
                {
                    continue;
                }

                Dictionary<string, int> tools = new Dictionary<string, int>();
                tools["bare_hands"] = 1;
                generators.Add(new Generator
                {
                    Id = entry.Definition.GeneratorId,
                    Level = state.Level,
                    Name = entry.Definition.Name,
                    Icon = entry.Definition.Icon,
                    Amount = entry.Amount,
                    Effort = 1f,
                    Duration = entry.Definition.Duration,
                    ToolRequirements = tools,
                    Enabled = true
                });
            }

            Collectible collectible = default(Collectible);
            collectible.EntityId = state.EntityId;
            collectible.CollectibleId = CollectibleId;
            collectible.Size = "large";
            collectible.Generators = generators.ToArray();
            collectible.CriticalGenerator = string.Empty;
            return collectible;
        }

        private static bool TryGetEntry(
            LootState state,
            string generatorId,
            out LootEntry result)
        {
            result = null;
            if (state == null || string.IsNullOrEmpty(generatorId))
            {
                return false;
            }

            for (int i = 0; i < state.Entries.Count; i++)
            {
                LootEntry entry = state.Entries[i];
                if (string.Equals(
                    entry.Definition.GeneratorId,
                    generatorId,
                    StringComparison.Ordinal))
                {
                    result = entry;
                    return true;
                }
            }
            return false;
        }

        private static int Remaining(LootState state)
        {
            int total = 0;
            if (state == null)
            {
                return total;
            }
            for (int i = 0; i < state.Entries.Count; i++)
            {
                total += Mathf.Max(0, state.Entries[i].Amount);
            }
            return total;
        }

        private static void SendPermission(
            Durango.Offline.Player player,
            string entityId,
            bool enabled)
        {
            if (player == null)
            {
                return;
            }
            player.Send<CollectibleDisplay>(new CollectibleDisplay
            {
                EntityId = entityId,
                DistributableEntities = enabled
                    ? new string[] { player.EntityId }
                    : new string[0]
            }, 0U);
        }
    }
}
