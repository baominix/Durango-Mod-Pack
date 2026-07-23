using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using Durango.Logic.Item;
using Durango.Network;
using Durango.Offline;
using Durango.UI;
using Durango.UI.Control;
using Durango.Utils;
using HarmonyLib;
using Newtonsoft.Json;
using L10N;
using Messages;
using NestedPrefab;
using Shared.Ability;
using Shared.Animal;
using Shared.Display;
using Shared.Item;
using Shared.Pet;
using Shared.Economy;
using UnityEngine;
using Yaml;
using Yaml.Util;

namespace AnimalHandlingPlugin
{
	[BepInPlugin("com.antigravity.animalhandling", "Animal Handling Plugin", "1.2.5")]
	public class AnimalHandlingPlugin : BaseUnityPlugin
	{
		public sealed class PlayerAnimalData
		{
			public GenDict SaveData = new GenDict();
			public List<Messages.Pet> Pets = new List<Messages.Pet>();
			public List<Messages.Pet> GrazedPets = new List<Messages.Pet>();
			public Dictionary<string, List<Item>> PetInventories = new Dictionary<string, List<Item>>();
			public AppearPet ActivePet = default(AppearPet);
			public Dictionary<string, string> PendingMilestones = new Dictionary<string, string>();
			public Dictionary<string, int> PendingRanks = new Dictionary<string, int>();
		}

		private static readonly Dictionary<string, PlayerAnimalData> PlayerData =
			new Dictionary<string, PlayerAnimalData>();
		private static Durango.Offline.Player LocalPlayer;

		private void Awake()
		{
			new Harmony("com.antigravity.animalhandling").PatchAll(Assembly.GetExecutingAssembly());
			Logger.LogInfo("AnimalHandlingPlugin 1.2.5 loaded (all-pet rank reset, active skill reset, rank-based milestones, persistent pets, feeding, skills and cages).");
		}

		private static PlayerAnimalData GetData(Durango.Offline.Player player)
		{
			if (player == null)
			{
				return null;
			}

			PlayerAnimalData data;
			if (!PlayerData.TryGetValue(player.EntityId, out data))
			{
				data = new PlayerAnimalData();
				PlayerData[player.EntityId] = data;
			}
			return data;
		}

		private static PlayerContext GetContext(Durango.Offline.Player player)
		{
			return (PlayerContext)AccessTools.Field(typeof(Durango.Offline.Player), "_context").GetValue(player);
		}

		private static World GetWorld(Durango.Offline.Player player)
		{
			return (World)AccessTools.Field(typeof(Durango.Offline.Player), "_world").GetValue(player);
		}

		private static void InvokeOnContextChanged(Durango.Offline.Player player)
		{
			MethodInfo method = typeof(Durango.Offline.Player).GetMethod(
				"OnContextChanged",
				BindingFlags.NonPublic | BindingFlags.Instance);
			method.Invoke(player, null);
		}

		private static string GetSavePath(PlayerContext context)
		{
			return Path.Combine(Path.GetDirectoryName(context.Path), context.PlayerSlot + ".gen");
		}

		private static void SanitizePet(ref Messages.Pet pet)
		{
			if (pet.Stat.Life == null)
			{
				float lifeMax = pet.Statistics.DerivedAbilities == null
					? 1000f
					: pet.Statistics.DerivedAbilities.Get(Derived.LifeMax, 1000f);
				pet.Stat.Life = CreateGauge(lifeMax, lifeMax, 0f);
			}
			if (pet.Stat.Hungry == null || pet.Stat.Hungry.Max() <= 0f || pet.Stat.Hungry.Max() > 1000f)
			{
				CustomRein reinData = GetCustomReinByEntityType(pet.EntityType);
				float hungryMax = reinData != null && reinData.hungry_max > 0f ? reinData.hungry_max : 300f;
				float hungryVelocity = reinData != null && reinData.hungry_velocity < 0f ? reinData.hungry_velocity : -0.05f;
				pet.Stat.Hungry = CreateGauge(hungryMax, hungryMax, hungryVelocity);
			}
			if (pet.Statistics.RequiredExp <= 1)
			{
				pet.Statistics.RequiredExp = GetRequiredExp(pet.EntityType, pet.Statistics.Level);
			}
			bool legacyMilestones = pet.Statistics.MilestonesInformation != null &&
				pet.Statistics.MilestonesInformation.Length == 6 &&
				pet.Statistics.MilestonesInformation[0].MilestoneTableId == 10;
			MilestoneInfo[] expectedMilestones = CreateMilestones(pet.Rank);
			bool rankScheduleMismatch = pet.Statistics.MilestonesInformation != null &&
				(pet.Statistics.MilestonesInformation.Length != expectedMilestones.Length ||
				 !pet.Statistics.MilestonesInformation.Select(delegate(MilestoneInfo info) { return info.Level; })
					.SequenceEqual(expectedMilestones.Select(delegate(MilestoneInfo info) { return info.Level; })) ||
				 pet.Statistics.MilestonesInformation.Where(delegate(MilestoneInfo info) { return info.Acquired; }).Count() > expectedMilestones.Length);
			if (pet.Statistics.MilestonesInformation == null || pet.Statistics.MilestonesInformation.Length == 0 || legacyMilestones || rankScheduleMismatch)
			{
				pet.Statistics.MilestonesInformation = RebuildMilestonesForRank(pet.Statistics.MilestonesInformation, pet.Rank);
			}
			if (pet.Statistics.AvailableActiveSkill == null)
			{
				pet.Statistics.AvailableActiveSkill = new Messages.PetActiveSkill[0];
			}
			else
			{
				pet.Statistics.AvailableActiveSkill = pet.Statistics.AvailableActiveSkill.Where(
					delegate(Messages.PetActiveSkill skill)
					{
						return !string.IsNullOrEmpty(skill.SkillId) && PetActiveSkills.Get(skill.SkillId, skill.Rank) != null;
					}).ToArray();
			}
			// PetMilestonePickGroup only enters its active-skill reroll state when
			// RetryCost is present on the pet. Older offline saves never persisted it,
			// so the visible reset icon opened an unusable picker.
			if (pet.Statistics.AvailableActiveSkill.Length > 0 && pet.Stat.RetryCost == null)
			{
				pet.Stat.RetryCost = new Money?(new Money(0, Shared.Economy.Currency.TStone));
			}
			if (pet.Statistics.DerivedAbilities == null)
			{
				pet.Statistics.DerivedAbilities = new Dictionary<Derived, float>();
			}
			if (!pet.Statistics.DerivedAbilities.ContainsKey(Derived.Speed) || pet.Statistics.DerivedAbilities[Derived.Speed] < 10f)
			{
				CustomRein custom = GetCustomReinByEntityType(pet.EntityType);
				pet.Statistics.DerivedAbilities[Derived.Speed] = custom != null ? custom.speed : GetDefaultSpeed(pet.EntityType);
			}
			if (!pet.Statistics.DerivedAbilities.ContainsKey(Derived.InventoryCapacity) || pet.Statistics.DerivedAbilities[Derived.InventoryCapacity] < 10f)
			{
				CustomRein custom = GetCustomReinByEntityType(pet.EntityType);
				pet.Statistics.DerivedAbilities[Derived.InventoryCapacity] = custom != null ? custom.capacity : GetDefaultCapacity(pet.EntityType);
			}
			if (pet.Stat.Tags == null) pet.Stat.Tags = new Dictionary<string, int>();
			if (pet.Stat.EatableTags == null) pet.Stat.EatableTags = new string[0];
			if (pet.Stat.AgingSince <= 0.0) pet.Stat.AgingSince = Times.UnixTimeNow();
			if (pet.Stat.AgingUntil <= pet.Stat.AgingSince || pet.Stat.AgingUntil - pet.Stat.AgingSince > 315360000.0)
			{
				pet.Stat.AgingUntil = pet.Stat.AgingSince + 2592000.0;
			}
		}

		private static void LoadData(Durango.Offline.Player player)
		{
			try
			{
				PlayerContext context = GetContext(player);
				PlayerAnimalData data = GetData(player);
				string savePath = GetSavePath(context);

				if (!File.Exists(savePath))
				{
					SaveData(player);
					return;
				}

				data.SaveData = Json.Read<GenDict>(File.ReadAllBytes(savePath), false) ?? new GenDict();
				data.Pets = data.SaveData.PetList ?? new List<Messages.Pet>();
				if (data.SaveData.GrazedPetList == null)
				{
					List<Messages.Pet> legacyGrazed = GetWorld(player).GetGrazedPets();
					data.GrazedPets = legacyGrazed == null
						? new List<Messages.Pet>()
						: new List<Messages.Pet>(legacyGrazed);
					HashSet<string> grazedIds = new HashSet<string>(data.GrazedPets.Select(delegate(Messages.Pet pet) { return pet.EntityId; }));
					data.Pets.RemoveAll(delegate(Messages.Pet pet) { return grazedIds.Contains(pet.EntityId); });
				}
				else
				{
					data.GrazedPets = data.SaveData.GrazedPetList;
				}
				data.PetInventories = data.SaveData.PetInventories ??
					new Dictionary<string, List<Item>>();
				data.ActivePet = data.SaveData.ActivePet;
				data.PendingMilestones = data.SaveData.PendingMilestones ?? new Dictionary<string, string>();
				data.PendingRanks = data.SaveData.PendingRanks ?? new Dictionary<string, int>();
				data.Pets = data.Pets.Where(delegate(Messages.Pet pet) { return !string.IsNullOrEmpty(pet.EntityId); })
					.GroupBy(delegate(Messages.Pet pet) { return pet.EntityId; }).Select(delegate(IGrouping<string, Messages.Pet> group) { return group.First(); }).ToList();
				data.GrazedPets = data.GrazedPets.Where(delegate(Messages.Pet pet) { return !string.IsNullOrEmpty(pet.EntityId); })
					.GroupBy(delegate(Messages.Pet pet) { return pet.EntityId; }).Select(delegate(IGrouping<string, Messages.Pet> group) { return group.First(); }).ToList();
				HashSet<string> normalizedGrazedIds = new HashSet<string>(data.GrazedPets.Select(delegate(Messages.Pet pet) { return pet.EntityId; }));
				data.Pets.RemoveAll(delegate(Messages.Pet pet) { return normalizedGrazedIds.Contains(pet.EntityId); });

				for (int i = 0; i < data.Pets.Count; i++)
				{
					Messages.Pet pet = data.Pets[i];
					pet.IsSpawned = false;
					pet.IsBoarding = false;
					SanitizePet(ref pet);
					data.Pets[i] = pet;
				}

				for (int i = 0; i < data.GrazedPets.Count; i++)
				{
					Messages.Pet pet = data.GrazedPets[i];
					pet.IsSpawned = false;
					pet.IsBoarding = false;
					SanitizePet(ref pet);
					data.GrazedPets[i] = pet;
				}

				// Scene objects are recreated on every session. Keeping an old active pet
				// would make the first summon take the return/switch path incorrectly.
				data.ActivePet = default(AppearPet);
				SaveData(player);
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogException(exception);
			}
		}

		private static void SaveData(Durango.Offline.Player player)
		{
			try
			{
				PlayerContext context = GetContext(player);
				PlayerAnimalData data = GetData(player);
				data.SaveData.PlayerSlot = context.PlayerSlot;
				data.SaveData.PetList = data.Pets;
				data.SaveData.GrazedPetList = data.GrazedPets;
				data.SaveData.PetInventories = data.PetInventories;
				data.SaveData.ActivePet = data.ActivePet;
				data.SaveData.PendingMilestones = data.PendingMilestones;
				data.SaveData.PendingRanks = data.PendingRanks;
				File.WriteAllBytes(GetSavePath(context), Json.WriteToBytes(data.SaveData, true, null));
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogException(exception);
			}
		}

		[HarmonyPatch(typeof(Durango.Offline.Player), MethodType.Constructor, new Type[] { typeof(string), typeof(Durango.Offline.Connection), typeof(Durango.Offline.World), typeof(Durango.Offline.PlayerContext), typeof(bool) })]
		private static class PlayerConstructorPatch
		{
			private static void Postfix(
				Durango.Offline.Player __instance,
				Durango.Offline.Connection connection,
				World world,
				PlayerContext context,
				bool isLocalPlayer)
			{
				if (isLocalPlayer)
				{
					LocalPlayer = __instance;
				}

				connection.Recv<GetPetsInfo>(delegate(GetPetsInfo message, PacketHeader header)
				{
					SendPetsInfo(__instance, header.Seq);
				});

				connection.Recv<SpawnPet>(delegate(SpawnPet message, PacketHeader header)
				{
					if (SummonPet(__instance, message))
					{
						__instance.Send<OK>(default(OK), header.Seq);
					}
					else
					{
						__instance.Send<Abort>(default(Abort), header.Seq);
					}
				});

				connection.Recv<ReturnPet>(delegate(ReturnPet message, PacketHeader header)
				{
					if (DismissPet(__instance, message))
					{
						__instance.Send<OK>(default(OK), header.Seq);
					}
					else
					{
						__instance.Send<Abort>(default(Abort), header.Seq);
					}
				});

				connection.Recv<Mount>(delegate(Mount message, PacketHeader header)
				{
					if (!MountPlayer(__instance))
					{
						__instance.Send<Abort>(default(Abort), header.Seq);
					}
					else
					{
						__instance.Send<OK>(default(OK), header.Seq);
					}
				});

				connection.Recv<Unmount>(delegate(Unmount message, PacketHeader header)
				{
					DismountPlayer(__instance);
					__instance.Send<OK>(default(OK), header.Seq);
				});

				connection.Recv<GetPetInventory>(delegate(GetPetInventory message, PacketHeader header)
				{
					SendPetInventory(__instance, message, header.Seq);
				});

				connection.Recv<PutInItemsIntoPet>(delegate(PutInItemsIntoPet message, PacketHeader header)
				{
					if (PutItemsIntoPet(__instance, message))
					{
						__instance.Send<OK>(default(OK), header.Seq);
					}
					else
					{
						__instance.Send<Abort>(default(Abort), header.Seq);
					}
				});

				connection.Recv<TakeOutItemsFromPet>(delegate(TakeOutItemsFromPet message, PacketHeader header)
				{
					if (TakeItemsFromPet(__instance, message))
					{
						__instance.Send<OK>(default(OK), header.Seq);
					}
					else
					{
						__instance.Send<Abort>(default(Abort), header.Seq);
					}
				});

				connection.Recv<ReleasePet>(delegate(ReleasePet message, PacketHeader header)
				{
					ReleasePet(__instance, message, header.Seq);
				});

				connection.Recv<GrazePets>(delegate(GrazePets message, PacketHeader header)
				{
					if (SetGrazedPets(__instance, message))
					{
						__instance.Send<OK>(default(OK), header.Seq);
					}
					else
					{
						__instance.Send<Abort>(default(Abort), header.Seq);
					}
				});

				connection.Recv<GetGrazedPets>(delegate(GetGrazedPets message, PacketHeader header)
				{
					SendGrazedPets(__instance, header.Seq);
				});

				connection.Recv<RenamePet>(delegate(RenamePet message, PacketHeader header)
				{
					RenamePet(__instance, message, header.Seq);
				});

				connection.Recv<Feeding>(delegate(Feeding message, PacketHeader header)
				{
					if (FeedPet(__instance, message))
					{
						__instance.Send<OK>(default(OK), header.Seq);
					}
					else
					{
						__instance.Send<Abort>(default(Abort), header.Seq);
					}
				});

				connection.Recv<GetMilestoneCandidate>(delegate(GetMilestoneCandidate message, PacketHeader header)
				{
					GetMilestoneCandidates(__instance, message, header.Seq);
				});

				connection.Recv<PickMilestone>(delegate(PickMilestone message, PacketHeader header)
				{
					PickMilestone(__instance, message, header.Seq);
				});

				connection.Recv<PickMilestoneAgain>(delegate(PickMilestoneAgain message, PacketHeader header)
				{
					PickMilestoneAgain(__instance, message, header.Seq);
				});

				connection.Recv<AcceptMilestone>(delegate(AcceptMilestone message, PacketHeader header)
				{
					AcceptMilestone(__instance, message, header.Seq);
				});

				connection.Recv<DrawActiveSkill>(delegate(DrawActiveSkill message, PacketHeader header)
				{
					DrawActiveSkill(__instance, message, header.Seq);
				});

				connection.Recv<RedrawActiveSkill>(delegate(RedrawActiveSkill message, PacketHeader header)
				{
					RedrawActiveSkill(__instance, message, header.Seq);
				});

				connection.Recv<UsePetActiveSkill>(delegate(UsePetActiveSkill message, PacketHeader header)
				{
					if (UseActiveSkill(__instance, message))
					{
						__instance.Send<OK>(default(OK), header.Seq);
					}
					else
					{
						__instance.Send<PetActiveSkillCanceled>(new PetActiveSkillCanceled { SkillId = message.SkillId }, 0U);
						__instance.Send<Abort>(default(Abort), header.Seq);
					}
				});

				connection.Recv<RevertPetRank>(delegate(RevertPetRank message, PacketHeader header)
				{
					RevertRank(__instance, message, header.Seq);
				});

				connection.Recv<AcceptPetRank>(delegate(AcceptPetRank message, PacketHeader header)
				{
					AcceptRank(__instance, message, header.Seq);
				});

				connection.Recv<GetPreviewPet>(delegate(GetPreviewPet message, PacketHeader header)
				{
					SendPreviewPet(__instance, message, header.Seq);
				});

				connection.Recv<ResurrectPet>(delegate(ResurrectPet message, PacketHeader header)
				{
					if (Resurrect(__instance, message)) __instance.Send<OK>(default(OK), header.Seq);
					else __instance.Send<Abort>(default(Abort), header.Seq);
				});

				connection.Recv<ReinifyPet>(delegate(ReinifyPet message, PacketHeader header)
				{
					if (Reinify(__instance, message)) __instance.Send<OK>(default(OK), header.Seq);
					else __instance.Send<Abort>(default(Abort), header.Seq);
				});

				connection.Recv<PutInCage>(delegate(PutInCage message, PacketHeader header)
				{
					if (PutPetInCage(__instance, message)) __instance.Send<OK>(default(OK), header.Seq);
					else __instance.Send<Abort>(default(Abort), header.Seq);
				});

				connection.Recv<TakeOutFromCage>(delegate(TakeOutFromCage message, PacketHeader header)
				{
					if (TakePetOutOfCage(__instance, message)) __instance.Send<OK>(default(OK), header.Seq);
					else __instance.Send<Abort>(default(Abort), header.Seq);
				});

				connection.Recv<FeedInCage>(delegate(FeedInCage message, PacketHeader header)
				{
					if (FeedCagedPet(__instance, message)) __instance.Send<OK>(default(OK), header.Seq);
					else __instance.Send<Abort>(default(Abort), header.Seq);
				});

				connection.Recv<GetAvailableTask>(delegate(GetAvailableTask message, PacketHeader header)
				{
					SendAvailableTasks(__instance, message, header.Seq);
				});

				connection.Recv<StartPetTask>(delegate(StartPetTask message, PacketHeader header)
				{
					if (StartTask(__instance, message)) __instance.Send<OK>(default(OK), header.Seq);
					else __instance.Send<Abort>(default(Abort), header.Seq);
				});

				connection.Recv<CancelPetTask>(delegate(CancelPetTask message, PacketHeader header)
				{
					if (CancelTask(__instance, message)) __instance.Send<OK>(default(OK), header.Seq);
					else __instance.Send<Abort>(default(Abort), header.Seq);
				});

				connection.Recv<FinishPetTask>(delegate(FinishPetTask message, PacketHeader header)
				{
					if (FinishTask(__instance, message)) __instance.Send<OK>(default(OK), header.Seq);
					else __instance.Send<Abort>(default(Abort), header.Seq);
				});

				connection.Recv<UseTamingAction>(delegate(UseTamingAction message, PacketHeader header)
				{
					if (TameAnimal(__instance, message)) __instance.Send<OK>(default(OK), header.Seq);
					else __instance.Send<Abort>(default(Abort), header.Seq);
				});

				LoadData(__instance);
			}
		}

		[HarmonyPatch(typeof(Durango.Logic.Item.Inventory), "CheckEnableUseType")]
		private static class EnableTamingActionPatch
		{
			private static void Postfix(UseType useType, ref bool __result)
			{
				if (useType == UseType.Taming)
				{
					__result = true;
				}
			}
		}

		[HarmonyPatch(typeof(InventoryContainerBase), "ItemAction")]
		private static class TamingMaterialActionPatch
		{
			private static bool Prefix(UseType type, IList<ItemData> items)
			{
				if (type != UseType.Taming && type != UseType.Imprint && type != UseType.Grazing)
				{
					return true;
				}
				if (LocalPlayer == null || items == null || items.Count == 0)
				{
					return false;
				}

				// Check if this item is a taming material/crate
				PlayerContext context = GetContext(LocalPlayer);
				int itemIndex = context.InventoryItems.FindIndex(delegate(Item item) { return item.Id == items[0].Id; });
				if (itemIndex >= 0)
				{
					PerformanceYaml.Rein rein = PerformanceYaml.GetRein(context.InventoryItems[itemIndex].Prototype);
					if (rein != null)
					{
						ShowTameConfirmation(LocalPlayer, items[0].Id);
						return false; // Bypass original ItemAction (Show custom confirmation dialog)
					}
				}

				return true;
			}
		}

		private static void ShowTameConfirmation(Durango.Offline.Player player, string itemId)
		{
			PlayerContext context = GetContext(player);
			int itemIndex = context.InventoryItems.FindIndex(delegate(Item item) { return item.Id == itemId; });
			if (itemIndex < 0)
			{
				UIManager.SystemMsg("Animal Handling", "Taming Material was not found.", 3f);
				return;
			}

			Item material = context.InventoryItems[itemIndex];
			PerformanceYaml.Rein rein = PerformanceYaml.GetRein(material.Prototype);
			if (rein == null)
			{
				UIManager.SystemMsg("Animal Handling", "This item has no animal data.", 3f);
				return;
			}

			// Render 3D animal model preview in dialog box
			try
			{
				Yaml.Pet pet = SingletonDict<int, Yaml.Pet>.Get((int)rein.PetEntityType, null);
				string path = (pet != null) ? AnimalYaml.GetPrefabPath(pet.VehicleEntityType) : null;
				if (!string.IsNullOrEmpty(path))
				{
					MessageBox msgBox = UIManager.MessageBox;
					UIWidget modelViewer = msgBox.ModelViewer;
					UIModelViewer componentInChildren = modelViewer.GetComponentInChildren<UIModelViewer>(true);
					componentInChildren.SetPlainModel(path, new UIModelViewer.Arguments
					{
						CameraAngle = 35f,
						Rotation = 140f,
						Loaded = componentInChildren.DefaultAnimalPlay("idle", "stand", false)
					});
					msgBox.SetCustomWidget(modelViewer, MessageBox.Position.Top);
				}
			}
			catch (Exception ex)
			{
				UnityEngine.Debug.LogWarning("Failed to render 3D animal model: " + ex);
			}

			UIManager.MessageBox.Show(
				string.Format("Do you wish to bond with {0}?", rein.PetName),
				"Once a bond is formed, an animal cannot be sold or given to others until that bond is broken.",
				delegate(int index)
				{
					if (index == 0)
					{
						AddPetFromTamingMaterial(player, itemId);
					}
				},
				new MessageBox.Button[]
				{
					new MessageBox.Button { Text = "Yes", Style = PresetButton.Style.Solid },
					new MessageBox.Button { Text = "No", Style = PresetButton.Style.Border }
				});
		}

		private static void AddPetFromTamingMaterial(Durango.Offline.Player player, string itemId)
		{
			PlayerContext context = GetContext(player);
			PlayerAnimalData data = GetData(player);
			int itemIndex = context.InventoryItems.FindIndex(delegate(Item item) { return item.Id == itemId; });
			if (itemIndex < 0 || data.Pets.Any(delegate(Messages.Pet pet) { return pet.EntityId == itemId; }))
			{
				return;
			}
			int maxPets = (int)GameSystem<StatisticsSystem>.Instance().GetDeriveds(Derived.MaxTamingPet, 99f);
			if (data.Pets.Count >= Mathf.Max(1, maxPets))
			{
				UIManager.SystemMsg("Animal Handling", "The animal handling slots are full.", 3f);
				return;
			}

			Item material = context.InventoryItems[itemIndex];
			PerformanceYaml.Rein rein = PerformanceYaml.GetRein(material.Prototype);
			if (rein == null)
			{
				return;
			}

			float lifeValue = UnityEngine.Random.Range(15420f, 38219f);
			Gauge life = new Gauge(lifeValue, 0f, new GaugeNode[]
			{
				new GaugeNode { Time = 0.0, Value = lifeValue }
			});
			Messages.Reins? materialReins = null;
			if (material.Ext is Messages.Reins)
			{
				materialReins = new Messages.Reins?((Messages.Reins)material.Ext);
			}
			CustomRein customRein = GetCustomRein(material.Prototype, material.Level);
			float petSpeed = customRein != null ? customRein.speed : GetDefaultSpeed(rein.PetEntityType);
			float petCapacity = customRein != null ? customRein.capacity : GetDefaultCapacity(rein.PetEntityType);
			int petSize = customRein != null ? customRein.size : (materialReins == null ? 100 : materialReins.Value.Size);
			float hungryMax = customRein != null && customRein.hungry_max > 0f ? customRein.hungry_max : 300f;
			float hungryVelocity = customRein != null && customRein.hungry_velocity < 0f ? customRein.hungry_velocity : -0.05f;

			Dictionary<Derived, float> abilities = new Dictionary<Derived, float>();
			abilities[Derived.Speed] = petSpeed;
			abilities[Derived.InventoryCapacity] = petCapacity;
			abilities[Derived.Attack] = UnityEngine.Random.Range(456f, 1234f);
			abilities[Derived.Defense] = UnityEngine.Random.Range(456f, 1234f);
			abilities[Derived.Accuracy] = UnityEngine.Random.Range(456f, 1234f);
			abilities[Derived.LifeMax] = lifeValue;
			abilities[Derived.AnimalProductQuantity] = 1f;

			Messages.Pet petData;
			PetRank initialRank = GetInitialRank(rein.PetEntityType);
			if (materialReins != null && materialReins.Value.Pet != null)
			{
				petData = materialReins.Value.Pet.Value;
				petData.EntityId = itemId;
				petData.TamerEntityId = context.AppearPlayer.EntityId;
				petData.IsSpawned = false;
				petData.IsBoarding = false;
				SanitizePet(ref petData);
			}
			else
			{
				petData = new Messages.Pet
				{
					EntityId = itemId,
					EntityType = (ushort)rein.PetEntityType,
					TamerEntityId = context.AppearPlayer.EntityId,
					Name = rein.PetName,
					Rank = initialRank,
					Stat = new PetStats
					{
						PlaybackRate = rein.PlaybackRate,
						Size = petSize,
						Life = life,
						IsOld = false,
						Hungry = CreateGauge(hungryMax, hungryMax, hungryVelocity),
						EatableTags = new string[0],
						Tags = new Dictionary<string, int>(),
						AgingSince = Times.UnixTimeNow(),
						AgingUntil = Times.UnixTimeNow() + 2592000.0
					},
					Statistics = new PetStatistics
					{
						Level = Mathf.Max(1, material.Level),
						Exp = 0,
						RequiredExp = GetRequiredExp(rein.PetEntityType, material.Level),
						DerivedAbilities = abilities,
						MilestonesInformation = CreateMilestones(initialRank),
						AvailableActiveSkill = new Messages.PetActiveSkill[0]
					}
				};
			}

			data.Pets.Add(petData);
			data.PetInventories[itemId] = new List<Item>();
			context.InventoryItems.RemoveAt(itemIndex);
			player.Send<InventoryUpdated>(new InventoryUpdated
			{
				EntityId = context.AppearPlayer.EntityId,
				RemovedItemIds = new string[] { itemId }
			}, 0U);
			player.Send<Messages.Pets>(new Messages.Pets { Data = data.Pets.ToArray() }, 0U);
			InvokeOnContextChanged(player);
			context.Save();
			SaveData(player);

			SoundManager.PlayEvent("ui_button_animal_bind");
			UIManager.Alarm.ShowNotify(
				string.Format("{0} was added to Animal Handling.", rein.PetName),
				"act_domesticate_1",
				true,
				1.8f,
				null,
				null,
				null);
			PetGroup petGroup = UIManager.FindScript<PetGroup>();
			if (petGroup != null)
			{
				petGroup.Open(petData.EntityId);
			}
		}

		private static float GetOptionalReinValue(PerformanceYaml.Rein rein, string fieldName, float fallback)
		{
			FieldInfo field = AccessTools.Field(rein.GetType(), fieldName);
			if (field == null)
			{
				return fallback;
			}
			object value = field.GetValue(rein);
			return value == null ? fallback : Convert.ToSingle(value);
		}

		private static float GetDefaultSpeed(int entityType)
		{
			string name = Yaml.AnimalYaml.GetName(entityType).ToLower();
			if (name.Contains("raptor") || name.Contains("deino") || name.Contains("ostrich") || name.Contains("zebra"))
			{
				return 420f;
			}
			if (name.Contains("megaloceros") || name.Contains("elk") || name.Contains("deer") || name.Contains("horse"))
			{
				return 380f;
			}
			if (name.Contains("compy") || name.Contains("carnivore") || name.Contains("wolf") || name.Contains("smilodon"))
			{
				return 350f;
			}
			if (name.Contains("stego") || name.Contains("tri") || name.Contains("anky") || name.Contains("brachio") || name.Contains("mammoth"))
			{
				return 280f;
			}
			return 350f;
		}

		private static float GetDefaultCapacity(int entityType)
		{
			string name = Yaml.AnimalYaml.GetName(entityType).ToLower();
			if (name.Contains("stego") || name.Contains("tri") || name.Contains("anky") || name.Contains("brachio") || name.Contains("mammoth"))
			{
				return 400f;
			}
			if (name.Contains("megaloceros") || name.Contains("elk") || name.Contains("deer") || name.Contains("horse"))
			{
				return 200f;
			}
			if (name.Contains("raptor") || name.Contains("deino") || name.Contains("compy"))
			{
				return 100f;
			}
			return 150f;
		}

		private static Dictionary<string, Dictionary<string, CustomRein>> LoadedCustomReins;
		private static Dictionary<string, Dictionary<string, CustomPetFood>> LoadedPetFoods;
		private static Dictionary<string, CustomPetDefinition> LoadedPetDefinitions;

		private static void LoadCustomReins()
		{
			if (LoadedCustomReins != null) return;
			try
			{
				TextAsset asset = Resources.Load<TextAsset>("offline/assets/performance");
				if (asset != null)
				{
					var wrapper = Newtonsoft.Json.JsonConvert.DeserializeObject<CustomPerformanceWrapper>(asset.text);
					if (wrapper != null)
					{
						LoadedCustomReins = wrapper.ReinsDict;
						LoadedPetFoods = wrapper.PetFoodDict;
					}
				}
			}
			catch (Exception ex)
			{
				UnityEngine.Debug.LogWarning("Failed to load custom reins: " + ex);
			}
			if (LoadedCustomReins == null)
			{
				LoadedCustomReins = new Dictionary<string, Dictionary<string, CustomRein>>();
			}
			if (LoadedPetFoods == null)
			{
				LoadedPetFoods = new Dictionary<string, Dictionary<string, CustomPetFood>>();
			}
		}

		private static CustomRein GetCustomRein(string prototypeId)
		{
			return GetCustomRein(prototypeId, 1);
		}

		private static bool LevelMatches(string range, int level)
		{
			if (string.IsNullOrEmpty(range)) return true;
			string cleaned = range.Replace("[", string.Empty).Replace("]", string.Empty).Replace(" ", string.Empty);
			string[] parts = cleaned.Split(',');
			int min;
			int max;
			return parts.Length == 2 && int.TryParse(parts[0], out min) && int.TryParse(parts[1], out max)
				&& level >= min && level <= max;
		}

		private static CustomRein GetCustomRein(string prototypeId, int level)
		{
			LoadCustomReins();
			Dictionary<string, CustomRein> levelDict;
			if (LoadedCustomReins.TryGetValue(prototypeId, out levelDict))
			{
				foreach (var kvp in levelDict)
				{
					if (LevelMatches(kvp.Key, level)) return kvp.Value;
				}
			}
			return null;
		}

		private static CustomPetFood GetCustomPetFood(string prototypeId, int level)
		{
			LoadCustomReins();
			Dictionary<string, CustomPetFood> levelDict;
			if (LoadedPetFoods.TryGetValue(prototypeId, out levelDict))
			{
				foreach (var kvp in levelDict)
				{
					if (LevelMatches(kvp.Key, level)) return kvp.Value;
				}
			}
			return null;
		}

		private static void LoadPetDefinitions()
		{
			if (LoadedPetDefinitions != null) return;
			try
			{
				TextAsset asset = Resources.Load<TextAsset>("offline/assets/pet/pets_for_client");
				if (asset != null)
				{
					LoadedPetDefinitions = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, CustomPetDefinition>>(asset.text);
				}
			}
			catch (Exception ex)
			{
				UnityEngine.Debug.LogWarning("Failed to load pet definitions: " + ex);
			}
			if (LoadedPetDefinitions == null)
			{
				LoadedPetDefinitions = new Dictionary<string, CustomPetDefinition>();
			}
		}

		private static CustomPetDefinition GetPetDefinition(int entityType)
		{
			LoadPetDefinitions();
			CustomPetDefinition definition;
			return LoadedPetDefinitions.TryGetValue(entityType.ToString(), out definition) ? definition : null;
		}

		private static PetRank GetInitialRank(int entityType)
		{
			int[] ranks = GetRankPool(entityType);
			return (PetRank)ranks[UnityEngine.Random.Range(0, ranks.Length)];
		}

		private static int[] GetRankPool(int entityType)
		{
			CustomPetDefinition definition = GetPetDefinition(entityType);
			if (definition == null || definition.available_ranks == null || definition.available_ranks.Length == 0)
			{
				return new int[] { (int)PetRank.C, (int)PetRank.B, (int)PetRank.A, (int)PetRank.S };
			}
			// A singleton is the catalogue/default rank, not a useful reset pool.
			// This also applies to crafted and event animals; otherwise their reset
			// button can only return the same B/A rank forever.
			if (definition.available_ranks.Length == 1)
			{
				return new int[] { (int)PetRank.C, (int)PetRank.B, (int)PetRank.A, (int)PetRank.S };
			}
			int[] configured = definition.available_ranks.Distinct().Where(delegate(int rank)
			{
				return rank >= (int)PetRank.C && rank <= (int)PetRank.S;
			}).ToArray();
			return configured.Length == 0 ? new int[] { (int)PetRank.C, (int)PetRank.B, (int)PetRank.A, (int)PetRank.S } : configured;
		}

		private static int GetMilestoneAllocationCount(PetRank rank)
		{
			switch (rank)
			{
			case PetRank.S: return 5;
			case PetRank.A: return 4;
			case PetRank.B: return 3;
			case PetRank.C: return 2;
			case PetRank.D: return 1;
			default: return 1;
			}
		}

		private static MilestoneInfo[] CreateMilestones(PetRank rank)
		{
			int allocationCount = GetMilestoneAllocationCount(rank);
			Dictionary<int, int[][]> table = Yaml.Util.Singleton<Constants>.Instance.Pet.MilestoneLevel;
			int[][] rows;
			if (table == null || !table.TryGetValue(allocationCount, out rows) || rows == null || rows.Length == 0)
			{
				rows = new int[][] { new int[] { 60, 6 } };
			}
			List<MilestoneInfo> result = new List<MilestoneInfo>();
			foreach (int[] row in rows)
			{
				if (row == null || row.Length < 2) continue;
				result.Add(new MilestoneInfo
				{
					Level = row[0],
					MilestoneTableId = row[1],
					TagId = null,
					Acquired = false
				});
			}
			return result.ToArray();
		}

		private static MilestoneInfo[] RebuildMilestonesForRank(MilestoneInfo[] previous, PetRank rank)
		{
			MilestoneInfo[] rebuilt = CreateMilestones(rank);
			if (previous == null || previous.Length == 0) return rebuilt;
			List<MilestoneInfo> acquired = previous.Where(delegate(MilestoneInfo info) { return info.Acquired; })
				.OrderBy(delegate(MilestoneInfo info) { return info.Level; }).ToList();
			for (int i = 0; i < acquired.Count && i < rebuilt.Length; i++)
			{
				rebuilt[i].Acquired = true;
				rebuilt[i].TagId = acquired[i].TagId;
			}
			return rebuilt;
		}

		private static CustomRein GetCustomReinByEntityType(int entityType)
		{
			LoadCustomReins();
			foreach (var kvp in LoadedCustomReins)
			{
				string protoId = kvp.Key;
				PerformanceYaml.Rein rein = PerformanceYaml.GetRein(protoId);
				bool matches = rein != null && rein.PetEntityType == entityType;
				if (!matches)
				{
					matches = kvp.Value.Any(delegate(KeyValuePair<string, CustomRein> value)
					{
						return value.Value != null && value.Value.pet_entity_type == entityType;
					});
				}
				if (matches)
				{
					foreach (var subKvp in kvp.Value)
					{
						return subKvp.Value;
					}
				}
			}
			return null;
		}

		public class CustomRein
		{
			public float speed;
			public float capacity;
			public int size;
			public float hungry_max;
			public float hungry_velocity;
			public int pet_entity_type;
			public int vehicle_entity_type;
		}

		public class CustomPetFood
		{
			public float vigor;
		}

		public class CustomPetDefinition
		{
			public int family_level;
			public int[] available_ranks;
			public bool is_craft;
			public bool is_ridable;
			public bool is_fightable;
			public bool is_reinifiable;
			public string rein_id;
			public int vehicle_entity_type;
		}

		public class CustomPerformanceWrapper
		{
			[JsonProperty("reins")]
			public Dictionary<string, Dictionary<string, CustomRein>> ReinsDict;

			[JsonProperty("pet_food")]
			public Dictionary<string, Dictionary<string, CustomPetFood>> PetFoodDict;
		}

		private static string GetGameObjectPath(GameObject obj)
		{
			string path = "/" + obj.name;
			while (obj.transform.parent != null)
			{
				obj = obj.transform.parent.gameObject;
				path = "/" + obj.name + path;
			}
			return path;
		}

		private static int FindPetIndex(PlayerAnimalData data, string petId)
		{
			return data.Pets.FindIndex(delegate(Messages.Pet p) { return p.EntityId == petId; });
		}

		private static int FindGrazedPetIndex(PlayerAnimalData data, string petId)
		{
			return data.GrazedPets.FindIndex(delegate(Messages.Pet p) { return p.EntityId == petId; });
		}

		private static Gauge CreateGauge(float max, float value, float velocity)
		{
			max = Mathf.Max(1f, max);
			value = Mathf.Clamp(value, 0f, max);
			double now = Times.UnixTimeNow();
			if (velocity < 0f && value > 0f)
			{
				return new Gauge(max, 0f, new GaugeNode[]
				{
					new GaugeNode { Time = now, Value = value },
					new GaugeNode { Time = now + value / -velocity, Value = 0f }
				});
			}
			return new Gauge(max, 0f, new GaugeNode[]
			{
				new GaugeNode { Time = now, Value = value }
			});
		}

		private static int GetRequiredExp(int entityType, int level)
		{
			PetExpTable table = SingletonDict<int, PetExpTable>.Get(entityType, null);
			int required = table == null ? 0 : table.GetRequiredExp(Mathf.Max(1, level));
			return Mathf.Max(1, required);
		}

		private static void UpdateActivePetData(PlayerAnimalData data, Messages.Pet pet)
		{
			if (data.ActivePet.PetData != null && data.ActivePet.PetData.Value.EntityId == pet.EntityId)
			{
				data.ActivePet.PetData = new Messages.Pet?(pet);
			}
		}

		private static bool TryGetAnyPet(PlayerAnimalData data, string petId, out Messages.Pet pet, out bool isGrazed)
		{
			int index = FindPetIndex(data, petId);
			if (index >= 0)
			{
				pet = data.Pets[index];
				isGrazed = false;
				return true;
			}
			index = FindGrazedPetIndex(data, petId);
			if (index >= 0)
			{
				pet = data.GrazedPets[index];
				isGrazed = true;
				return true;
			}
			pet = default(Messages.Pet);
			isGrazed = false;
			return false;
		}


		private static void GetMilestoneCandidates(Durango.Offline.Player player, GetMilestoneCandidate message, uint replyOf)
		{
			PlayerAnimalData data = GetData(player);
			int index = FindPetIndex(data, message.PetId);
			if (index < 0)
			{
				player.Send<Abort>(default(Abort), replyOf);
				return;
			}

			Messages.Pet pet = data.Pets[index];

			MilestoneCandidates candidates = new MilestoneCandidates();
			candidates.Result = new Pair<string, float>[]
			{
				new Pair<string, float>("attack_plus_10", 1f),
				new Pair<string, float>("defense_plus_10", 1f),
				new Pair<string, float>("accuracy_plus_10", 1f),
				new Pair<string, float>("speed_plus_10", 1f),
				new Pair<string, float>("inventory_plus_10", 1f)
			};
			candidates.Original = new Pair<string, float>[]
			{
				new Pair<string, float>("attack_plus_10", pet.Stat.Tags == null ? 0f : pet.Stat.Tags.Get("attack_plus_10", 0)),
				new Pair<string, float>("defense_plus_10", pet.Stat.Tags == null ? 0f : pet.Stat.Tags.Get("defense_plus_10", 0)),
				new Pair<string, float>("accuracy_plus_10", pet.Stat.Tags == null ? 0f : pet.Stat.Tags.Get("accuracy_plus_10", 0)),
				new Pair<string, float>("speed_plus_10", pet.Stat.Tags == null ? 0f : pet.Stat.Tags.Get("speed_plus_10", 0)),
				new Pair<string, float>("inventory_plus_10", pet.Stat.Tags == null ? 0f : pet.Stat.Tags.Get("inventory_plus_10", 0))
			};

			player.Send<MilestoneCandidates>(candidates, replyOf);
		}

		private static void RollMilestoneChoice(Durango.Offline.Player player, string petId, uint replyOf)
		{
			PlayerAnimalData data = GetData(player);
			int index = FindPetIndex(data, petId);
			if (index < 0)
			{
				player.Send<Abort>(default(Abort), replyOf);
				return;
			}

			Messages.Pet pet = data.Pets[index];
			string[] tags = { "attack_plus_10", "defense_plus_10", "accuracy_plus_10", "speed_plus_10", "inventory_plus_10" };
			string selectedTag = tags[UnityEngine.Random.Range(0, tags.Length)];

			data.PendingMilestones[petId] = selectedTag;
			SaveData(player);

			var originalStat = new Dictionary<Derived, float>();
			var newStat = new Dictionary<Derived, float>();

			originalStat[Derived.LifeMax] = pet.Stat.Life.Max();
			originalStat[Derived.Attack] = pet.Statistics.DerivedAbilities.Get(Derived.Attack, 0f);
			originalStat[Derived.Defense] = pet.Statistics.DerivedAbilities.Get(Derived.Defense, 0f);
			originalStat[Derived.Accuracy] = pet.Statistics.DerivedAbilities.Get(Derived.Accuracy, 0f);
			originalStat[Derived.Speed] = pet.Statistics.DerivedAbilities.Get(Derived.Speed, 0f);
			originalStat[Derived.InventoryCapacity] = pet.Statistics.DerivedAbilities.Get(Derived.InventoryCapacity, 0f);

			newStat[Derived.LifeMax] = originalStat[Derived.LifeMax];
			newStat[Derived.Attack] = originalStat[Derived.Attack] * (selectedTag == "attack_plus_10" ? 1.05f : 1f);
			newStat[Derived.Defense] = originalStat[Derived.Defense] * (selectedTag == "defense_plus_10" ? 1.05f : 1f);
			newStat[Derived.Accuracy] = originalStat[Derived.Accuracy] * (selectedTag == "accuracy_plus_10" ? 1.05f : 1f);
			newStat[Derived.Speed] = originalStat[Derived.Speed] * (selectedTag == "speed_plus_10" ? 1.05f : 1f);
			newStat[Derived.InventoryCapacity] = originalStat[Derived.InventoryCapacity] + (selectedTag == "inventory_plus_10" ? 10f : 0f);

			MilestoneResult result = new MilestoneResult
			{
				SelectedTagId = selectedTag,
				OriginalStat = originalStat,
				NewStat = newStat,
				RetryCost = new Money(0, Shared.Economy.Currency.TStone)
			};

			player.Send<MilestoneResult>(result, replyOf);
		}

		private static void PickMilestone(Durango.Offline.Player player, PickMilestone message, uint replyOf)
		{
			RollMilestoneChoice(player, message.PetId, replyOf);
		}

		private static void PickMilestoneAgain(Durango.Offline.Player player, PickMilestoneAgain message, uint replyOf)
		{
			RollMilestoneChoice(player, message.PetId, replyOf);
		}

		private static void SetPrivateField(object obj, string fieldName, object value)
		{
			FieldInfo field = obj.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			field.SetValue(obj, value);
		}

		private static void AcceptMilestone(Durango.Offline.Player player, AcceptMilestone message, uint replyOf)
		{
			PlayerAnimalData data = GetData(player);
			int index = FindPetIndex(data, message.PetId);
			if (index < 0)
			{
				player.Send<Abort>(default(Abort), replyOf);
				return;
			}

			Messages.Pet pet = data.Pets[index];
			string selectedTag;
			if (!data.PendingMilestones.TryGetValue(message.PetId, out selectedTag))
			{
				player.Send<Abort>(default(Abort), replyOf);
				return;
			}

			float attackIncrement = selectedTag == "attack_plus_10" ? pet.Statistics.DerivedAbilities.Get(Derived.Attack, 0f) * 0.05f : 0f;
			float defenseIncrement = selectedTag == "defense_plus_10" ? pet.Statistics.DerivedAbilities.Get(Derived.Defense, 0f) * 0.05f : 0f;
			float accuracyIncrement = selectedTag == "accuracy_plus_10" ? pet.Statistics.DerivedAbilities.Get(Derived.Accuracy, 0f) * 0.05f : 0f;
			float speedIncrement = selectedTag == "speed_plus_10" ? pet.Statistics.DerivedAbilities.Get(Derived.Speed, 0f) * 0.05f : 0f;
			float inventoryIncrement = selectedTag == "inventory_plus_10" ? 10f : 0f;

			var originalStat = new Dictionary<Derived, float>();
			var newStat = new Dictionary<Derived, float>();

			originalStat[Derived.LifeMax] = pet.Stat.Life.Max();
			originalStat[Derived.Attack] = pet.Statistics.DerivedAbilities.Get(Derived.Attack, 0f);
			originalStat[Derived.Defense] = pet.Statistics.DerivedAbilities.Get(Derived.Defense, 0f);
			originalStat[Derived.Accuracy] = pet.Statistics.DerivedAbilities.Get(Derived.Accuracy, 0f);
			originalStat[Derived.Speed] = pet.Statistics.DerivedAbilities.Get(Derived.Speed, 0f);
			originalStat[Derived.InventoryCapacity] = pet.Statistics.DerivedAbilities.Get(Derived.InventoryCapacity, 0f);

			if (attackIncrement > 0f) pet.Statistics.DerivedAbilities[Derived.Attack] = originalStat[Derived.Attack] + attackIncrement;
			if (defenseIncrement > 0f) pet.Statistics.DerivedAbilities[Derived.Defense] = originalStat[Derived.Defense] + defenseIncrement;
			if (accuracyIncrement > 0f) pet.Statistics.DerivedAbilities[Derived.Accuracy] = originalStat[Derived.Accuracy] + accuracyIncrement;
			if (speedIncrement > 0f) pet.Statistics.DerivedAbilities[Derived.Speed] = originalStat[Derived.Speed] + speedIncrement;
			if (inventoryIncrement > 0f) pet.Statistics.DerivedAbilities[Derived.InventoryCapacity] = pet.Statistics.DerivedAbilities.Get(Derived.InventoryCapacity, 0f) + inventoryIncrement;
			if (pet.Stat.Tags == null) pet.Stat.Tags = new Dictionary<string, int>();
			pet.Stat.Tags[selectedTag] = pet.Stat.Tags.Get(selectedTag, 0) + 1;

			newStat[Derived.LifeMax] = pet.Stat.Life.Max();
			newStat[Derived.Attack] = pet.Statistics.DerivedAbilities.Get(Derived.Attack, 0f);
			newStat[Derived.Defense] = pet.Statistics.DerivedAbilities.Get(Derived.Defense, 0f);
			newStat[Derived.Accuracy] = pet.Statistics.DerivedAbilities.Get(Derived.Accuracy, 0f);
			newStat[Derived.Speed] = pet.Statistics.DerivedAbilities.Get(Derived.Speed, 0f);
			newStat[Derived.InventoryCapacity] = pet.Statistics.DerivedAbilities.Get(Derived.InventoryCapacity, 0f);

			if (pet.Statistics.MilestonesInformation != null)
			{
				for (int i = 0; i < pet.Statistics.MilestonesInformation.Length; i++)
				{
					if (pet.Statistics.Level >= pet.Statistics.MilestonesInformation[i].Level && !pet.Statistics.MilestonesInformation[i].Acquired)
					{
						pet.Statistics.MilestonesInformation[i].Acquired = true;
						pet.Statistics.MilestonesInformation[i].TagId = selectedTag;
						break;
					}
				}
			}
			pet.Stat.LastMilestoneAccepted = true;

			data.Pets[index] = pet;
			data.PendingMilestones.Remove(message.PetId);
			
			if (data.ActivePet.PetData != null && data.ActivePet.PetData.Value.EntityId == message.PetId)
			{
				data.ActivePet.PetData = new Messages.Pet?(pet);
			}
			SaveData(player);

			MilestoneResult result = new MilestoneResult
			{
				SelectedTagId = selectedTag,
				OriginalStat = originalStat,
				NewStat = newStat,
				RetryCost = new Money(0, Shared.Economy.Currency.TStone)
			};
			player.Send<MilestoneResult>(result, replyOf);
		}

		private static void DrawActiveSkill(Durango.Offline.Player player, DrawActiveSkill message, uint replyOf)
		{
			PlayerAnimalData data = GetData(player);
			int index = FindPetIndex(data, message.PetId);
			if (index < 0)
			{
				player.Send<Abort>(default(Abort), replyOf);
				return;
			}

			Messages.Pet pet = data.Pets[index];
			Messages.PetActiveSkill selectedSkill = SelectActiveSkill(pet);
			if (string.IsNullOrEmpty(selectedSkill.SkillId))
			{
				player.Send<Abort>(default(Abort), replyOf);
				return;
			}

			pet.Statistics.AvailableActiveSkill = new Messages.PetActiveSkill[] { selectedSkill };
			pet.Stat.RetryCost = new Money?(new Money(0, Shared.Economy.Currency.TStone));
			data.Pets[index] = pet;
			
			if (data.ActivePet.PetData != null && data.ActivePet.PetData.Value.EntityId == message.PetId)
			{
				data.ActivePet.PetData = new Messages.Pet?(pet);
			}
			SaveData(player);

			DrawSkillResult result = new DrawSkillResult
			{
				Skill = selectedSkill,
				RetryCost = new Money(0, Shared.Economy.Currency.TStone)
			};
			player.Send<DrawSkillResult>(result, replyOf);
		}

		private static void RedrawActiveSkill(Durango.Offline.Player player, RedrawActiveSkill message, uint replyOf)
		{
			PlayerAnimalData data = GetData(player);
			int index = FindPetIndex(data, message.PetId);
			if (index < 0)
			{
				player.Send<Abort>(default(Abort), replyOf);
				return;
			}

			Messages.Pet pet = data.Pets[index];
			Messages.PetActiveSkill selectedSkill = SelectActiveSkill(pet);
			if (string.IsNullOrEmpty(selectedSkill.SkillId))
			{
				player.Send<Abort>(default(Abort), replyOf);
				return;
			}

			pet.Statistics.AvailableActiveSkill = new Messages.PetActiveSkill[] { selectedSkill };
			pet.Stat.RetryCost = new Money?(new Money(0, Shared.Economy.Currency.TStone));
			data.Pets[index] = pet;

			if (data.ActivePet.PetData != null && data.ActivePet.PetData.Value.EntityId == message.PetId)
			{
				data.ActivePet.PetData = new Messages.Pet?(pet);
			}
			SaveData(player);

			DrawSkillResult result = new DrawSkillResult
			{
				Skill = selectedSkill,
				RetryCost = new Money(0, Shared.Economy.Currency.TStone)
			};
			player.Send<DrawSkillResult>(result, replyOf);
		}

		private static Messages.PetActiveSkill SelectActiveSkill(Messages.Pet pet)
		{
			List<Pair<Messages.PetActiveSkill, float>> candidates = PetUtil.GetActiveSkillCandidates(pet);
			if (candidates != null && candidates.Count > 0)
			{
				float total = candidates.Sum(delegate(Pair<Messages.PetActiveSkill, float> pair) { return Mathf.Max(0f, pair.Item2); });
				float roll = UnityEngine.Random.Range(0f, Mathf.Max(1f, total));
				foreach (Pair<Messages.PetActiveSkill, float> candidate in candidates)
				{
					roll -= Mathf.Max(0f, candidate.Item2);
					if (roll <= 0f && !string.IsNullOrEmpty(candidate.Item1.SkillId)) return candidate.Item1;
				}
			}
			List<Messages.PetActiveSkill> learnable = new List<Messages.PetActiveSkill>();
			PetUtil.FindLearnableSkills(learnable, pet.EntityType, true);
			return learnable.Count == 0 ? default(Messages.PetActiveSkill) : learnable[UnityEngine.Random.Range(0, learnable.Count)];
		}

		private static bool UseActiveSkill(Durango.Offline.Player player, UsePetActiveSkill message)
		{
			PlayerAnimalData data = GetData(player);
			if (data.ActivePet.PetData == null || string.IsNullOrEmpty(message.SkillId)) return false;
			Messages.Pet pet = data.ActivePet.PetData.Value;
			if (pet.Statistics.AvailableActiveSkill == null ||
				!pet.Statistics.AvailableActiveSkill.Any(delegate(Messages.PetActiveSkill skill) { return skill.SkillId == message.SkillId; }))
			{
				return false;
			}
			player.Send<PetActiveSkillUsed>(new PetActiveSkillUsed
			{
				SkillId = message.SkillId,
				ClipName = null
			}, 0U);
			return true;
		}

		private static void RevertRank(Durango.Offline.Player player, RevertPetRank message, uint replyOf)
		{
			PlayerAnimalData data = GetData(player);
			int index = FindPetIndex(data, message.PetId);
			if (index < 0)
			{
				player.Send<Abort>(default(Abort), replyOf);
				return;
			}
			Messages.Pet pet = data.Pets[index];
			int[] ranks = GetRankPool(pet.EntityType);
			int selected = ranks[UnityEngine.Random.Range(0, ranks.Length)];
			data.PendingRanks[message.PetId] = selected;
			SaveData(player);
			player.Send<RevertPetRankCandidate>(new RevertPetRankCandidate
			{
				Rank = (PetRank)selected,
				Tag = null
			}, replyOf);
		}

		private static void AcceptRank(Durango.Offline.Player player, AcceptPetRank message, uint replyOf)
		{
			PlayerAnimalData data = GetData(player);
			int index = FindPetIndex(data, message.PetId);
			int rank;
			if (index < 0 || !data.PendingRanks.TryGetValue(message.PetId, out rank))
			{
				player.Send<Abort>(default(Abort), replyOf);
				return;
			}
			Messages.Pet pet = data.Pets[index];
			PetRank acceptedRank = (PetRank)rank;
			pet.Statistics.MilestonesInformation = RebuildMilestonesForRank(pet.Statistics.MilestonesInformation, acceptedRank);
			pet.Rank = acceptedRank;
			pet.Stat.LastMilestoneAccepted = !pet.Statistics.MilestonesInformation.Any(
				delegate(MilestoneInfo info) { return !info.Acquired && info.Level <= pet.Statistics.Level; });
			data.Pets[index] = pet;
			UpdateActivePetData(data, pet);
			data.PendingRanks.Remove(message.PetId);
			player.Send<Messages.Pet>(pet, 0U);
			player.Send<OK>(default(OK), replyOf);
			SaveData(player);
		}

		private static Messages.Pet CreatePreviewPet(int entityType, PetRank rank, int level)
		{
			CustomRein rein = GetCustomReinByEntityType(entityType);
			float lifeMax = 1000f + Mathf.Max(1, level) * 250f;
			float hungryMax = rein != null && rein.hungry_max > 0f ? rein.hungry_max : 300f;
			float hungryVelocity = rein != null && rein.hungry_velocity < 0f ? rein.hungry_velocity : -0.05f;
			Dictionary<Derived, float> abilities = new Dictionary<Derived, float>();
			abilities[Derived.Speed] = rein != null && rein.speed > 0f ? rein.speed : GetDefaultSpeed(entityType);
			abilities[Derived.InventoryCapacity] = rein != null && rein.capacity > 0f ? rein.capacity : GetDefaultCapacity(entityType);
			abilities[Derived.Attack] = 400f + level * 12f;
			abilities[Derived.Defense] = 400f + level * 12f;
			abilities[Derived.Accuracy] = 400f + level * 12f;
			abilities[Derived.LifeMax] = lifeMax;
			abilities[Derived.AnimalProductQuantity] = 1f;
			return new Messages.Pet
			{
				EntityId = "preview-" + Guid.NewGuid().ToString("N"),
				EntityType = (ushort)entityType,
				Name = AnimalYaml.GetName(entityType),
				Rank = rank,
				Stat = new PetStats
				{
					PlaybackRate = 1f,
					Size = rein == null ? 100 : rein.size,
					Life = CreateGauge(lifeMax, lifeMax, 0f),
					Hungry = CreateGauge(hungryMax, hungryMax, hungryVelocity),
					EatableTags = new string[0],
					Tags = new Dictionary<string, int>(),
					AgingSince = Times.UnixTimeNow(),
					AgingUntil = Times.UnixTimeNow() + 2592000.0
				},
				Statistics = new PetStatistics
				{
					Level = Mathf.Max(1, level),
					Exp = 0,
					RequiredExp = GetRequiredExp(entityType, level),
					DerivedAbilities = abilities,
					MilestonesInformation = CreateMilestones(rank),
					AvailableActiveSkill = new Messages.PetActiveSkill[0]
				}
			};
		}

		private static void SendPreviewPet(Durango.Offline.Player player, GetPreviewPet message, uint replyOf)
		{
			if (SingletonDict<int, Yaml.Pet>.Get(message.PetEntityType, null) == null)
			{
				player.Send<Abort>(default(Abort), replyOf);
				return;
			}
			player.Send<Messages.Pet>(CreatePreviewPet(message.PetEntityType, message.Rank, message.Level), replyOf);
		}

		private static bool Resurrect(Durango.Offline.Player player, ResurrectPet message)
		{
			PlayerAnimalData data = GetData(player);
			int index = FindPetIndex(data, message.PetId);
			if (index < 0) return false;
			Messages.Pet pet = data.Pets[index];
			float maxLife = pet.Stat.Life == null ? 1000f : pet.Stat.Life.Max();
			pet.Stat.Life = CreateGauge(maxLife, maxLife, 0f);
			data.Pets[index] = pet;
			UpdateActivePetData(data, pet);
			player.Send<Messages.Pet>(pet, 0U);
			if (data.ActivePet.PetData != null && data.ActivePet.EntityId == pet.EntityId)
			{
				AppearPet appear = data.ActivePet;
				appear.IsAlive = true;
				appear.Survival.Life = pet.Stat.Life;
				appear.PetData = new Messages.Pet?(pet);
				data.ActivePet = appear;
				player.Send<AppearPet>(appear, 0U);
			}
			SaveData(player);
			return true;
		}

		private static bool Reinify(Durango.Offline.Player player, ReinifyPet message)
		{
			PlayerAnimalData data = GetData(player);
			int index = FindPetIndex(data, message.PetId);
			PlayerContext context = GetContext(player);
			int catalystIndex = context.InventoryItems.FindIndex(delegate(Item item) { return item.Id == message.ItemId; });
			if (index < 0 || catalystIndex < 0) return false;
			Messages.Pet pet = data.Pets[index];
			CustomPetDefinition definition = GetPetDefinition(pet.EntityType);
			if (definition == null || !definition.is_reinifiable || string.IsNullOrEmpty(definition.rein_id)) return false;
			Item? created = Cheats.MakeItem(definition.rein_id, Mathf.Max(1, pet.Statistics.Level));
			if (created == null) return false;
			if (data.ActivePet.EntityId == message.PetId) DismissActivePet(player, data);

			Item reinItem = created.Value;
			Messages.Reins reins = new Messages.Reins
			{
				PetEntityType = pet.EntityType,
				VehicleEntityType = (ushort)definition.vehicle_entity_type,
				Size = (ushort)Mathf.Clamp(pet.Stat.Size, 0, ushort.MaxValue),
				Pet = new Messages.Pet?(pet),
				Domesticated = true
			};
			reinItem.Ext = reins;
			context.InventoryItems.RemoveAt(catalystIndex);
			context.InventoryItems.Add(reinItem);
			List<Item> bag;
			if (data.PetInventories.TryGetValue(message.PetId, out bag) && bag.Count > 0)
			{
				context.InventoryItems.AddRange(bag);
			}
			data.PetInventories.Remove(message.PetId);
			data.Pets.RemoveAt(index);
			data.PendingMilestones.Remove(message.PetId);
			data.PendingRanks.Remove(message.PetId);
			player.Send<InventoryUpdated>(new InventoryUpdated
			{
				EntityId = context.AppearPlayer.EntityId,
				RemovedItemIds = new string[] { message.ItemId },
				Items = new List<Item>(new Item[] { reinItem }).Concat(bag ?? new List<Item>()).ToArray()
			}, 0U);
			player.Send<Messages.Pets>(new Messages.Pets { Data = data.Pets.ToArray() }, 0U);
			context.Save();
			SaveData(player);
			return true;
		}

		private static bool TryGetGrowCage(Durango.Offline.Player player, string entityId, out AppearArtifact artifact, out GrowCage cage)
		{
			AppearArtifact? value = GetWorld(player).ArtifactManager.Get(entityId);
			if (value == null || !(value.Value.States.Cage is GrowCage))
			{
				artifact = default(AppearArtifact);
				cage = default(GrowCage);
				return false;
			}
			artifact = value.Value;
			cage = (GrowCage)artifact.States.Cage;
			if (cage.Pets.Data == null) cage.Pets.Data = new Messages.Pet[0];
			if (cage.Tasks == null) cage.Tasks = new Dictionary<string, TaskStatus>();
			return true;
		}

		private static void SaveGrowCage(Durango.Offline.Player player, AppearArtifact artifact, GrowCage cage)
		{
			World world = GetWorld(player);
			artifact.States.EntityId = artifact.EntityId;
			artifact.States.Cage = cage;
			Dictionary<string, AppearArtifact> artifacts =
				(Dictionary<string, AppearArtifact>)AccessTools.Field(typeof(ArtifactManager), "_artifacts").GetValue(world.ArtifactManager);
			artifacts[artifact.EntityId] = artifact;
			world.BroadCast<ArtifactState>(artifact.States);
			world.Save();
		}

		private static bool PutPetInCage(Durango.Offline.Player player, PutInCage message)
		{
			PlayerAnimalData data = GetData(player);
			int index = FindPetIndex(data, message.PetId);
			AppearArtifact artifact;
			GrowCage cage;
			if (index < 0 || !TryGetGrowCage(player, message.EntityId, out artifact, out cage)) return false;
			Messages.Pet pet = data.Pets[index];
			int requiredSize = Mathf.Max(1, pet.Stat.Size);
			if (cage.RemainSize < requiredSize) return false;
			if (data.ActivePet.EntityId == pet.EntityId) DismissActivePet(player, data);
			List<Messages.Pet> pets = cage.Pets.Data.ToList();
			if (pets.Any(delegate(Messages.Pet value) { return value.EntityId == pet.EntityId; })) return false;
			pet.IsSpawned = false;
			pet.IsBoarding = false;
			pet.CageInfo = new CageInfo?(new CageInfo { RegionId = string.Empty, RegionName = string.Empty, Tile = message.Tile });
			pets.Add(pet);
			cage.Pets.Data = pets.ToArray();
			cage.RemainSize = (byte)Mathf.Max(0, cage.RemainSize - requiredSize);
			data.Pets.RemoveAt(index);
			SaveGrowCage(player, artifact, cage);
			player.Send<Messages.Pets>(new Messages.Pets { Data = data.Pets.ToArray() }, 0U);
			SaveData(player);
			return true;
		}

		private static bool TakePetOutOfCage(Durango.Offline.Player player, TakeOutFromCage message)
		{
			AppearArtifact artifact;
			GrowCage cage;
			if (!TryGetGrowCage(player, message.EntityId, out artifact, out cage)) return false;
			List<Messages.Pet> pets = cage.Pets.Data.ToList();
			int index = pets.FindIndex(delegate(Messages.Pet value) { return value.EntityId == message.PetId; });
			if (index < 0) return false;
			Messages.Pet pet = pets[index];
			pets.RemoveAt(index);
			cage.Pets.Data = pets.ToArray();
			cage.RemainSize = (byte)Mathf.Min(cage.Size, cage.RemainSize + Mathf.Max(1, pet.Stat.Size));
			cage.Tasks.Remove(pet.EntityId);
			pet.CageInfo = null;
			PlayerAnimalData data = GetData(player);
			if (FindPetIndex(data, pet.EntityId) < 0) data.Pets.Add(pet);
			SaveGrowCage(player, artifact, cage);
			player.Send<Messages.Pets>(new Messages.Pets { Data = data.Pets.ToArray() }, 0U);
			SaveData(player);
			return true;
		}

		private static bool FeedCagedPet(Durango.Offline.Player player, FeedInCage message)
		{
			AppearArtifact artifact;
			GrowCage cage;
			if (!TryGetGrowCage(player, message.EntityId, out artifact, out cage) || message.ItemIds == null) return false;
			List<Messages.Pet> pets = cage.Pets.Data.ToList();
			int index = pets.FindIndex(delegate(Messages.Pet value) { return value.EntityId == message.PetId; });
			if (index < 0) return false;
			PlayerContext context = GetContext(player);
			HashSet<string> ids = new HashSet<string>(message.ItemIds);
			List<Item> foods = context.InventoryItems.FindAll(delegate(Item item) { return ids.Contains(item.Id); });
			if (foods.Count != ids.Count) return false;
			float vigor = 0f;
			foreach (Item food in foods)
			{
				CustomPetFood foodData = GetCustomPetFood(food.Prototype, food.Level);
				if (foodData == null || foodData.vigor <= 0f) return false;
				vigor += foodData.vigor;
			}
			Messages.Pet pet = pets[index];
			CustomRein rein = GetCustomReinByEntityType(pet.EntityType);
			float max = pet.Stat.Hungry == null ? 300f : pet.Stat.Hungry.Max();
			float current = pet.Stat.Hungry == null ? 0f : pet.Stat.Hungry.Get();
			float velocity = rein != null && rein.hungry_velocity < 0f ? rein.hungry_velocity : -0.05f;
			pet.Stat.Hungry = CreateGauge(max, current + vigor, velocity);
			pets[index] = pet;
			cage.Pets.Data = pets.ToArray();
			context.InventoryItems.RemoveAll(delegate(Item item) { return ids.Contains(item.Id); });
			player.Send<InventoryUpdated>(new InventoryUpdated { EntityId = context.AppearPlayer.EntityId, RemovedItemIds = message.ItemIds }, 0U);
			player.Send<FeedingSuccess>(new FeedingSuccess { PetId = message.PetId }, 0U);
			SaveGrowCage(player, artifact, cage);
			context.Save();
			return true;
		}

		private static void SendAvailableTasks(Durango.Offline.Player player, GetAvailableTask message, uint replyOf)
		{
			AppearArtifact artifact;
			GrowCage cage;
			if (!TryGetGrowCage(player, message.EntityId, out artifact, out cage))
			{
				player.Send<Abort>(default(Abort), replyOf);
				return;
			}
			Messages.Pet pet = cage.Pets.Data.FirstOrDefault(delegate(Messages.Pet value) { return value.EntityId == message.PetId; });
			if (string.IsNullOrEmpty(pet.EntityId))
			{
				player.Send<Abort>(default(Abort), replyOf);
				return;
			}
			List<string> tasks = new List<string>();
			foreach (KeyValuePair<string, PetTask> pair in SingletonDict<string, PetTask>.Instance)
			{
				if (pair.Value != null && pair.Value.UnlockLevel <= pet.Statistics.Level) tasks.Add(pair.Key);
			}
			player.Send<AvailableTask>(new AvailableTask { Tasks = tasks.ToArray() }, replyOf);
		}

		private static bool StartTask(Durango.Offline.Player player, StartPetTask message)
		{
			AppearArtifact artifact;
			GrowCage cage;
			if (!TryGetGrowCage(player, message.EntityId, out artifact, out cage) || cage.Tasks.ContainsKey(message.PetId)) return false;
			List<Messages.Pet> pets = cage.Pets.Data.ToList();
			int index = pets.FindIndex(delegate(Messages.Pet value) { return value.EntityId == message.PetId; });
			PetTask task = SingletonDict<string, PetTask>.Get(message.TaskId, null);
			if (index < 0 || task == null || task.UnlockLevel > pets[index].Statistics.Level) return false;
			Messages.Pet pet = pets[index];
			float hungry = pet.Stat.Hungry == null ? 0f : pet.Stat.Hungry.Get();
			if (hungry < task.HungryRequired) return false;
			float max = pet.Stat.Hungry.Max();
			CustomRein rein = GetCustomReinByEntityType(pet.EntityType);
			float velocity = rein != null && rein.hungry_velocity < 0f ? rein.hungry_velocity : -0.05f;
			pet.Stat.Hungry = CreateGauge(max, hungry - task.HungryRequired, velocity);
			pets[index] = pet;
			cage.Pets.Data = pets.ToArray();
			double now = Times.UnixTimeNow();
			cage.Tasks[message.PetId] = new TaskStatus { TaskId = message.TaskId, Since = now, Until = now + task.Duration };
			SaveGrowCage(player, artifact, cage);
			return true;
		}

		private static bool CancelTask(Durango.Offline.Player player, CancelPetTask message)
		{
			AppearArtifact artifact;
			GrowCage cage;
			if (!TryGetGrowCage(player, message.EntityId, out artifact, out cage) || !cage.Tasks.Remove(message.PetId)) return false;
			SaveGrowCage(player, artifact, cage);
			return true;
		}

		private static bool FinishTask(Durango.Offline.Player player, FinishPetTask message)
		{
			AppearArtifact artifact;
			GrowCage cage;
			if (!TryGetGrowCage(player, message.EntityId, out artifact, out cage)) return false;
			TaskStatus status;
			if (!cage.Tasks.TryGetValue(message.PetId, out status) || status.Until > Times.UnixTimeNow()) return false;
			PetTask task = SingletonDict<string, PetTask>.Get(status.TaskId, null);
			List<Messages.Pet> pets = cage.Pets.Data.ToList();
			int index = pets.FindIndex(delegate(Messages.Pet value) { return value.EntityId == message.PetId; });
			if (task == null || index < 0) return false;
			Messages.Pet pet = pets[index];
			pet.Statistics.Exp += task.Exp;
			while (pet.Statistics.Level < 60 && pet.Statistics.Exp >= pet.Statistics.RequiredExp)
			{
				pet.Statistics.Exp -= pet.Statistics.RequiredExp;
				pet.Statistics.Level++;
				pet.Statistics.RequiredExp = GetRequiredExp(pet.EntityType, pet.Statistics.Level);
			}
			pets[index] = pet;
			cage.Pets.Data = pets.ToArray();
			cage.Tasks.Remove(message.PetId);

			List<Item> createdItems = new List<Item>();
			List<Messages.RewardItem> rewardItems = new List<Messages.RewardItem>();
			if (task.Type == PetTaskType.Production && task.ProducedPrototype != null && task.ProducedPrototype.Count > 0)
			{
				string prototype = ChooseWeightedPrototype(task.ProducedPrototype);
				int quantity = Mathf.Clamp(task.GetProductQuantity(
					pet.Statistics.DerivedAbilities.Get(Derived.AnimalProductQuantity, 1f)), 1, 20);
				for (int i = 0; i < quantity; i++)
				{
					Item? item = Cheats.MakeItem(prototype, pet.Statistics.Level);
					if (item != null) createdItems.Add(item.Value);
				}
				if (createdItems.Count > 0)
				{
					rewardItems.Add(new Messages.RewardItem { PrototypeId = prototype, Level = pet.Statistics.Level, Count = createdItems.Count });
				}
			}
			if (createdItems.Count > 0)
			{
				PlayerContext context = GetContext(player);
				context.InventoryItems.AddRange(createdItems);
				player.Send<InventoryUpdated>(new InventoryUpdated { EntityId = context.AppearPlayer.EntityId, Items = createdItems.ToArray() }, 0U);
				context.Save();
			}
			SaveGrowCage(player, artifact, cage);
			player.Send<Messages.Pet>(pet, 0U);
			player.Send<Rewarded>(new Rewarded
			{
				Effect = new PetTaskFinishedEffect
				{
					Type = Shared.System.RewardEffect.PetTaskFinished,
					TaskId = status.TaskId,
					PetExp = task.Exp
				},
				Reward = new RewardInfo { Items = rewardItems.ToArray() }
			}, 0U);
			return true;
		}

		private static string ChooseWeightedPrototype(Dictionary<string, float[]> candidates)
		{
			float total = 0f;
			foreach (KeyValuePair<string, float[]> pair in candidates)
			{
				total += pair.Value == null || pair.Value.Length == 0 ? 1f : Mathf.Max(0f, pair.Value[0]);
			}
			float roll = UnityEngine.Random.Range(0f, Mathf.Max(1f, total));
			foreach (KeyValuePair<string, float[]> pair in candidates)
			{
				roll -= pair.Value == null || pair.Value.Length == 0 ? 1f : Mathf.Max(0f, pair.Value[0]);
				if (roll <= 0f) return pair.Key;
			}
			return candidates.Keys.First();
		}

		private static bool TryFindReinForVehicle(int vehicleEntityType, out string prototypeId, out CustomRein rein)
		{
			LoadCustomReins();
			foreach (KeyValuePair<string, Dictionary<string, CustomRein>> pair in LoadedCustomReins)
			{
				foreach (KeyValuePair<string, CustomRein> ranged in pair.Value)
				{
					if (ranged.Value != null && ranged.Value.vehicle_entity_type == vehicleEntityType)
					{
						prototypeId = pair.Key;
						rein = ranged.Value;
						return true;
					}
				}
			}
			prototypeId = null;
			rein = null;
			return false;
		}

		private static bool TameAnimal(Durango.Offline.Player player, UseTamingAction message)
		{
			PlayerContext context = GetContext(player);
			if (context.InventoryItems.FindIndex(delegate(Item item) { return item.Id == message.ToolItemId; }) < 0) return false;
			AnimalBehavior animal = Durango.Utils.Singleton<AnimalManager>.Instance().GetAnimal(message.EntityId);
			if (animal == null) return false;
			string prototypeId;
			CustomRein reinData;
			if (!TryFindReinForVehicle(animal.EntityTypeId, out prototypeId, out reinData)) return false;
			int level = Mathf.Max(1, animal.Level);
			Item? created = Cheats.MakeItem(prototypeId, level);
			if (created == null) return false;
			Item reinItem = created.Value;
			reinItem.Ext = new Messages.Reins
			{
				PetEntityType = (ushort)reinData.pet_entity_type,
				VehicleEntityType = (ushort)reinData.vehicle_entity_type,
				Size = (ushort)Mathf.Clamp(reinData.size, 0, ushort.MaxValue),
				Pet = null,
				Domesticated = true,
				DomesticateDuration = 0f,
				DomesticateSuccessRate = 1f
			};
			context.InventoryItems.Add(reinItem);
			player.Send<InventoryUpdated>(new InventoryUpdated
			{
				EntityId = context.AppearPlayer.EntityId,
				Items = new Item[] { reinItem }
			}, 0U);
			player.Send<DisappearEntity>(new DisappearEntity { EntityId = message.EntityId }, 0U);
			player.Send<Rewarded>(new Rewarded
			{
				Effect = new TamingCompletedEffect
				{
					Type = Shared.System.RewardEffect.AnimalTamed,
					AnimalEntityId = message.EntityId,
					AnimalEntityType = animal.EntityTypeId,
					ReinsId = reinItem.Id
				},
				Reward = default(RewardInfo)
			}, 0U);
			context.Save();
			return true;
		}

		private static void SendPetsInfo(Durango.Offline.Player player, uint replyOf)
		{
			PlayerAnimalData data = GetData(player);
			PetsInfo info = default(PetsInfo);
			info.Pets.Data = data.Pets.ToArray();
			info.GrazedPets.Data = data.GrazedPets.ToArray();
			info.GrazableCount = 5;
			player.Send<PetsInfo>(info, replyOf);
		}

		private static void SendGrazedPets(Durango.Offline.Player player, uint replyOf)
		{
			PlayerAnimalData data = GetData(player);
			player.Send<GrazedPets>(new GrazedPets { Data = data.GrazedPets.ToArray() }, replyOf);
		}

		private static bool SummonPet(Durango.Offline.Player player, SpawnPet message)
		{
			PlayerAnimalData data = GetData(player);
			int selectedIndex = FindPetIndex(data, message.PetId);
			if (selectedIndex < 0)
			{
				return false;
			}

			if (data.ActivePet.PetData != null)
			{
				if (data.ActivePet.EntityId == message.PetId)
				{
					return true;
				}
				DismissActivePet(player, data);
			}

			if (!data.PetInventories.ContainsKey(message.PetId))
			{
				data.PetInventories.Add(message.PetId, new List<Item>());
			}

			Messages.Pet selectedPet = data.Pets[selectedIndex];
			selectedPet.IsSpawned = true;
			selectedPet.IsBoarding = false;
			data.Pets[selectedIndex] = selectedPet;

			Survival survival = default(Survival);
			survival.Life = selectedPet.Stat.Life;

			AppearPet appearPet = new AppearPet
			{
				EntityId = selectedPet.EntityId,
				IsAlive = true,
				PetData = new Messages.Pet?(selectedPet),
				Move = null,
				EntityType = selectedPet.EntityType,
				Survival = survival
			};

			player.Send<AppearPet>(appearPet, 0U);
			data.ActivePet = appearPet;
			InvokeOnContextChanged(player);
			SaveData(player);
			return true;
		}

		private static bool MountPlayer(Durango.Offline.Player player)
		{
			PlayerContext context = GetContext(player);
			PlayerAnimalData data = GetData(player);
			if (data.ActivePet.PetData == null)
			{
				return false;
			}
			Yaml.Pet petInfo = SingletonDict<int, Yaml.Pet>.Get(data.ActivePet.EntityType, null);
			if (petInfo != null && !petInfo.IsRidable)
			{
				return false;
			}
			Messages.Pet activePet = data.ActivePet.PetData.Value;
			activePet.IsBoarding = true;
			int index = FindPetIndex(data, activePet.EntityId);
			if (index >= 0) data.Pets[index] = activePet;
			data.ActivePet.PetData = new Messages.Pet?(activePet);
			context.AppearPlayer.Display.BoardingOn = BoardingOn.Pet;
			context.AppearPlayer.Display.VehicleEntityId = data.ActivePet.EntityId;
			player.Send<PlayerDisplay>(context.AppearPlayer.Display, 0U);
			InvokeOnContextChanged(player);
			SaveData(player);
			return true;
		}

		private static void DismountPlayer(Durango.Offline.Player player)
		{
			PlayerContext context = GetContext(player);
			context.AppearPlayer.Display.BoardingOn = BoardingOn.None;
			context.AppearPlayer.Display.VehicleEntityId = null;
			player.Send<PlayerDisplay>(context.AppearPlayer.Display, 0U);
			PlayerAnimalData data = GetData(player);
			if (data.ActivePet.PetData != null)
			{
				Messages.Pet activePet = data.ActivePet.PetData.Value;
				activePet.IsBoarding = false;
				int index = FindPetIndex(data, activePet.EntityId);
				if (index >= 0) data.Pets[index] = activePet;
				data.ActivePet.PetData = new Messages.Pet?(activePet);
			}
			InvokeOnContextChanged(player);
			SaveData(player);
		}

		private static void DismissActivePet(Durango.Offline.Player player, PlayerAnimalData data)
		{
			if (data.ActivePet.PetData == null) return;
			string tamerEntityId = null;
			Messages.Pet activePet = data.ActivePet.PetData.Value;
			tamerEntityId = activePet.TamerEntityId;
			activePet.IsSpawned = false;
			activePet.IsBoarding = false;
			int index = FindPetIndex(data, activePet.EntityId);
			if (index >= 0) data.Pets[index] = activePet;

			player.Send<DisappearPet>(new DisappearPet
			{
				TamerEntityId = tamerEntityId,
				EntityId = activePet.EntityId
			}, 0U);

			data.ActivePet = default(AppearPet);
			PlayerContext context = GetContext(player);
			if (context.AppearPlayer.Display.BoardingOn == BoardingOn.Pet)
			{
				context.AppearPlayer.Display.BoardingOn = BoardingOn.None;
				context.AppearPlayer.Display.VehicleEntityId = null;
				player.Send<PlayerDisplay>(context.AppearPlayer.Display, 0U);
			}
		}

		private static bool DismissPet(Durango.Offline.Player player, ReturnPet message)
		{
			PlayerAnimalData data = GetData(player);
			if (data.ActivePet.PetData == null || data.ActivePet.EntityId != message.PetId)
			{
				return false;
			}
			DismissActivePet(player, data);
			InvokeOnContextChanged(player);
			SaveData(player);
			return true;
		}

		private static void ReleasePet(Durango.Offline.Player player, ReleasePet message, uint sequence)
		{
			PlayerAnimalData data = GetData(player);
			if (data.ActivePet.EntityId == message.PetId)
			{
				DismissActivePet(player, data);
			}
			int removed = data.Pets.RemoveAll(delegate(Messages.Pet pet) { return pet.EntityId == message.PetId; });
			removed += data.GrazedPets.RemoveAll(delegate(Messages.Pet pet) { return pet.EntityId == message.PetId; });
			if (removed == 0)
			{
				player.Send<Abort>(default(Abort), sequence);
				return;
			}
			List<Item> releasedItems;
			if (data.PetInventories.TryGetValue(message.PetId, out releasedItems) && releasedItems.Count > 0)
			{
				PlayerContext context = GetContext(player);
				context.InventoryItems.AddRange(releasedItems);
				player.Send<InventoryUpdated>(new InventoryUpdated
				{
					EntityId = context.AppearPlayer.EntityId,
					Items = releasedItems.ToArray()
				}, 0U);
				context.Save();
			}
			data.PetInventories.Remove(message.PetId);
			data.PendingMilestones.Remove(message.PetId);
			player.Send<OK>(default(OK), sequence);
			player.Send<Messages.Pets>(new Messages.Pets { Data = data.Pets.ToArray() }, 0U);
			player.Send<GrazedPets>(new GrazedPets { Data = data.GrazedPets.ToArray() }, 0U);
			SaveData(player);
		}

		private static void SendPetInventory(Durango.Offline.Player player, GetPetInventory message, uint seq)
		{
			PlayerAnimalData data = GetData(player);
			Messages.Pet selectedPet = default(Messages.Pet);
			bool isGrazed;
			if (!TryGetAnyPet(data, message.EntityId, out selectedPet, out isGrazed))
			{
				player.Send<Abort>(default(Abort), seq);
				return;
			}

			List<Item> items;
			if (!data.PetInventories.TryGetValue(message.EntityId, out items))
			{
				items = new List<Item>();
				data.PetInventories[message.EntityId] = items;
			}

			float capacity = selectedPet.Statistics.DerivedAbilities.Get(Derived.InventoryCapacity, 200f);
			PetInventory inventory = default(PetInventory);
			inventory.Inven.EntityId = message.EntityId;
			inventory.Inven.InventoryInfos.EntityId = message.EntityId;
			inventory.Inven.InventoryItems.EntityId = message.EntityId;
			inventory.Inven.InventoryItems.Items = items.ToArray();
			inventory.Inven.InventoryInfos.MaxSize = (int)capacity;
			player.Send<PetInventory>(inventory, seq);
		}

		private static bool SetGrazedPets(Durango.Offline.Player player, GrazePets message)
		{
			PlayerAnimalData data = GetData(player);
			string[] requestedIds = message.PetIdsToGraze ?? new string[0];
			HashSet<string> desired = new HashSet<string>(requestedIds.Where(delegate(string id) { return !string.IsNullOrEmpty(id); }));
			if (desired.Count > 5)
			{
				return false;
			}

			Dictionary<string, Messages.Pet> allPets = new Dictionary<string, Messages.Pet>();
			foreach (Messages.Pet pet in data.Pets) allPets[pet.EntityId] = pet;
			foreach (Messages.Pet pet in data.GrazedPets) allPets[pet.EntityId] = pet;
			if (desired.Any(delegate(string id) { return !allPets.ContainsKey(id); }))
			{
				return false;
			}

			if (data.ActivePet.PetData != null && desired.Contains(data.ActivePet.EntityId))
			{
				DismissActivePet(player, data);
			}

			List<Messages.Pet> owned = new List<Messages.Pet>();
			List<Messages.Pet> grazed = new List<Messages.Pet>();
			double now = Times.UnixTimeNow();
			foreach (Messages.Pet source in allPets.Values)
			{
				Messages.Pet pet = source;
				pet.IsSpawned = data.ActivePet.PetData != null && data.ActivePet.EntityId == pet.EntityId;
				pet.IsBoarding = pet.IsSpawned && pet.IsBoarding;
				if (desired.Contains(pet.EntityId))
				{
					if (pet.Stat.GrazedAt == null) pet.Stat.GrazedAt = new double?(now);
					grazed.Add(pet);
				}
				else
				{
					pet.Stat.GrazedAt = null;
					owned.Add(pet);
				}
			}
			data.Pets = owned;
			data.GrazedPets = grazed;
			data.SaveData.PetList = data.Pets;
			data.SaveData.GrazedPetList = data.GrazedPets;
			player.Send<Messages.Pets>(new Messages.Pets { Data = data.Pets.ToArray() }, 0U);
			player.Send<GrazedPets>(new GrazedPets { Data = data.GrazedPets.ToArray() }, 0U);
			InvokeOnContextChanged(player);
			SaveData(player);
			return true;
		}

		private static bool PutItemsIntoPet(Durango.Offline.Player player, PutInItemsIntoPet message)
		{
			PlayerContext context = GetContext(player);
			PlayerAnimalData data = GetData(player);
			Messages.Pet pet;
			bool isGrazed;
			if (!TryGetAnyPet(data, message.PetId, out pet, out isGrazed) || isGrazed || message.ItemIds == null)
			{
				return false;
			}
			List<Item> petItems;
			if (!data.PetInventories.TryGetValue(message.PetId, out petItems))
			{
				petItems = new List<Item>();
				data.PetInventories[message.PetId] = petItems;
			}
			HashSet<string> requested = new HashSet<string>(message.ItemIds);
			List<Item> movingItems = context.InventoryItems.FindAll(delegate(Item item) { return requested.Contains(item.Id); });
			if (movingItems.Count != requested.Count)
			{
				return false;
			}
			int capacity = (int)pet.Statistics.DerivedAbilities.Get(Derived.InventoryCapacity, 0f);
			if (capacity <= 0 || petItems.Count + movingItems.Count > capacity)
			{
				return false;
			}

			petItems.AddRange(movingItems);
			context.InventoryItems.RemoveAll(delegate(Item item) { return requested.Contains(item.Id); });

			player.Send<InventoryUpdated>(new InventoryUpdated
			{
				EntityId = context.AppearPlayer.EntityId,
				RemovedItemIds = message.ItemIds
			}, 0U);
			player.Send<InventoryUpdated>(new InventoryUpdated
			{
				EntityId = message.PetId,
				Items = petItems.ToArray()
			}, 0U);

			UpdatePetInventoryUsage(player, message.PetId, petItems.Count);
			InvokeOnContextChanged(player);
			context.Save();
			SaveData(player);
			return true;
		}

		private static bool TakeItemsFromPet(Durango.Offline.Player player, TakeOutItemsFromPet message)
		{
			PlayerContext context = GetContext(player);
			PlayerAnimalData data = GetData(player);
			Messages.Pet pet;
			bool isGrazed;
			if (!TryGetAnyPet(data, message.PetId, out pet, out isGrazed) || message.ItemIds == null)
			{
				return false;
			}
			List<Item> petItems;
			if (!data.PetInventories.TryGetValue(message.PetId, out petItems))
			{
				petItems = new List<Item>();
				return false;
			}
			HashSet<string> requested = new HashSet<string>(message.ItemIds);
			List<Item> movingItems = petItems.FindAll(delegate(Item item) { return requested.Contains(item.Id); });
			if (movingItems.Count != requested.Count)
			{
				return false;
			}

			context.InventoryItems.AddRange(movingItems);
			petItems.RemoveAll(delegate(Item item) { return requested.Contains(item.Id); });

			player.Send<InventoryUpdated>(new InventoryUpdated
			{
				EntityId = message.PetId,
				RemovedItemIds = message.ItemIds
			}, 0U);
			player.Send<InventoryUpdated>(new InventoryUpdated
			{
				EntityId = context.AppearPlayer.EntityId,
				Items = context.InventoryItems.ToArray()
			}, 0U);

			UpdatePetInventoryUsage(player, message.PetId, petItems.Count);
			InvokeOnContextChanged(player);
			context.Save();
			SaveData(player);
			return true;
		}

		private static void UpdatePetInventoryUsage(Durango.Offline.Player player, string petId, int count)
		{
			PlayerAnimalData data = GetData(player);
			int index = data.Pets.FindIndex(delegate(Messages.Pet pet) { return pet.EntityId == petId; });
			if (index < 0)
			{
				return;
			}

			Messages.Pet updatedPet = data.Pets[index];
			updatedPet.Stat.InventoryUsage = count;
			data.Pets[index] = updatedPet;
			UpdateActivePetData(data, updatedPet);
			player.Send<Messages.Pet>(updatedPet, 0U);
		}

		private static bool FeedPet(Durango.Offline.Player player, Feeding message)
		{
			PlayerAnimalData data = GetData(player);
			int index = FindPetIndex(data, message.PetId);
			if (index < 0 || message.FoodIds == null || message.FoodIds.Length == 0)
			{
				return false;
			}
			PlayerContext context = GetContext(player);
			HashSet<string> foodIds = new HashSet<string>(message.FoodIds);
			List<Item> foods = context.InventoryItems.FindAll(delegate(Item item) { return foodIds.Contains(item.Id); });
			if (foods.Count != foodIds.Count)
			{
				return false;
			}

			float vigor = 0f;
			foreach (Item food in foods)
			{
				CustomPetFood foodData = GetCustomPetFood(food.Prototype, food.Level);
				if (foodData == null || foodData.vigor <= 0f)
				{
					return false;
				}
				vigor += foodData.vigor;
			}

			Messages.Pet pet = data.Pets[index];
			CustomRein reinData = GetCustomReinByEntityType(pet.EntityType);
			float hungryMax = pet.Stat.Hungry == null ? 300f : pet.Stat.Hungry.Max();
			float hungryNow = pet.Stat.Hungry == null ? 0f : pet.Stat.Hungry.Get();
			float hungryVelocity = reinData != null && reinData.hungry_velocity < 0f ? reinData.hungry_velocity : -0.05f;
			pet.Stat.Hungry = CreateGauge(hungryMax, hungryNow + vigor, hungryVelocity);
			data.Pets[index] = pet;
			UpdateActivePetData(data, pet);
			context.InventoryItems.RemoveAll(delegate(Item item) { return foodIds.Contains(item.Id); });
			player.Send<InventoryUpdated>(new InventoryUpdated
			{
				EntityId = context.AppearPlayer.EntityId,
				RemovedItemIds = message.FoodIds
			}, 0U);
			player.Send<Messages.Pet>(pet, 0U);
			player.Send<FeedingSuccess>(new FeedingSuccess { PetId = message.PetId }, 0U);
			context.Save();
			SaveData(player);
			return true;
		}

		private static void RenamePet(Durango.Offline.Player player, RenamePet message, uint sequence)
		{
			PlayerAnimalData data = GetData(player);
			int index = data.Pets.FindIndex(delegate(Messages.Pet pet) { return pet.EntityId == message.PetId; });
			if (index >= 0)
			{
				Messages.Pet updatedPet = data.Pets[index];
				updatedPet.Name = message.Name;
				data.Pets[index] = updatedPet;
				player.Send<Messages.Pet>(updatedPet, 0U);
			}

			player.Send<OK>(default(OK), sequence);
			InvokeOnContextChanged(player);
			SaveData(player);
		}

		[HarmonyPatch(typeof(PetGroup), "Start")]
		private static class PetGroupStartPatch
		{
			private static bool Prefix(PetGroup __instance)
			{
				// Let the original Start wire every event and refresh callback. The old
				// replacement skipped StatisticsUpdated and WalletUpdated subscriptions.
				return true;
				#pragma warning disable 0162
				AccessTools.Field(typeof(PetGroup), "_openCloseSound").SetValue(__instance, UISound.GroupType.Pet);

				NestedPrefabLinker tabLinker = (NestedPrefabLinker)AccessTools.Field(
					typeof(PetGroup), "_petListTabLinker").GetValue(__instance);
				HorizontalTabList tabs = tabLinker.Object.GetComponent<HorizontalTabList>();
				AccessTools.Field(typeof(PetGroup), "_petListTabs").SetValue(__instance, tabs);
				tabs.BeginLoad();
				PetGroup.PetOwnType[] ownTypes = Enums<PetGroup.PetOwnType>.All();
				for (int i = 0; i < ownTypes.Length; i++)
				{
					tabs.AddText(ownTypes[i].GetName());
				}
				tabs.EndLoadByFitOnWidget();
				tabs.Clicked += CreateDelegate<Action<int>>(__instance, "SelectTab");

				UITitle title = (UITitle)AccessTools.Field(typeof(PetGroup), "_titleWidget").GetValue(__instance);
				title.Object.SetTitle(T._("Animal Handling"));

				PetListWidget petList = (PetListWidget)AccessTools.Field(typeof(PetGroup), "_petList").GetValue(__instance);
				petList.PetSelected += CreateDelegate<Action<Messages.Pet>>(__instance, "OnPetSelect");

				PetInfoWidget petInfo = (PetInfoWidget)AccessTools.Field(typeof(PetGroup), "_petInfoWidget").GetValue(__instance);
				petInfo.PetActionClicked += CreateDelegate<Action<PetInfoWidget.PetAction, Messages.Pet>>(
					__instance, "OnPetActionClick");

				PetPreviewWidget preview = (PetPreviewWidget)AccessTools.Field(typeof(PetGroup), "_petPreview").GetValue(__instance);
				preview.Renamed += CreateDelegate<Action<Messages.Pet>>(__instance, "RenamePet");
				preview.MilestonePicked += CreateDelegate<Action<Messages.Pet, int>>(__instance, "PetMilestonePick");
				preview.ActiveSkillPicked += CreateDelegate<Action<Messages.Pet>>(__instance, "PetActiveSkillPick");
				preview.MilestoneHelpClicked += CreateDelegate<Action<Messages.Pet>>(__instance, "ShowPetMilestonHelp");

				Durango.Utils.Singleton<PetManager>.Instance().PetActiveSkillUsed +=
					CreateDelegate<Action<PetActiveSkillUsed>>(__instance, "OnPetActiveSkillUsed");
				__instance.OnOpenSucceed += CreateDelegate<Action>(__instance, "Opened");



				GameObject[] helpButtons = (GameObject[])AccessTools.Field(
					typeof(PetGroup), "_cardInfoButtons").GetValue(__instance);
				if (helpButtons != null)
				{
					UIEventListener.VoidDelegate helpHandler =
						CreateDelegate<UIEventListener.VoidDelegate>(__instance, "OnClickHelpButton");
					foreach (GameObject button in helpButtons)
					{
						if (button != null)
						{
							UIEventListener listener = UIEventListener.Get(button);
							listener.onClick = (UIEventListener.VoidDelegate)Delegate.Combine(listener.onClick, helpHandler);
						}
					}
				}



				MethodInfo setChildrenActive = typeof(UIBase).GetMethod(
					"SetChildrenActive",
					BindingFlags.NonPublic | BindingFlags.Instance);
				setChildrenActive.Invoke(__instance, new object[] { false });
				return false;
				#pragma warning restore 0162
			}



			private static T CreateDelegate<T>(object target, string methodName) where T : class
			{
				MethodInfo method = target.GetType().GetMethod(
					methodName,
					BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				return Delegate.CreateDelegate(typeof(T), target, method) as T;
			}

			private static void SetButtonHandler(PetGroup group, string fieldName, string methodName)
			{
				GameObject button = (GameObject)AccessTools.Field(typeof(PetGroup), fieldName).GetValue(group);
				MethodInfo method = typeof(PetGroup).GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				UIEventListener.Get(button).onClick = (UIEventListener.VoidDelegate)Delegate.CreateDelegate(
					typeof(UIEventListener.VoidDelegate), method);
			}
		}

		[HarmonyPatch(typeof(PetGroup), "Opened")]
		private static class PetGroupOpenedPatch
		{
			private static void Postfix(PetGroup __instance)
			{
				try
				{
					UITitle title = (UITitle)AccessTools.Field(typeof(PetGroup), "_titleWidget").GetValue(__instance);
					if (title != null && title.Object != null) title.Object.SetTitle(T._("Animal Handling"));
					GameObject petCountBtn = (GameObject)AccessTools.Field(typeof(PetGroup), "_petCountButton").GetValue(__instance);
					GameObject grazedBtn = (GameObject)AccessTools.Field(typeof(PetGroup), "_grazedPetCountButton").GetValue(__instance);
					GameObject voucherBtn = (GameObject)AccessTools.Field(typeof(PetGroup), "_petVoucherButton").GetValue(__instance);

					if (petCountBtn != null) petCountBtn.SetActive(false);
					if (grazedBtn != null) grazedBtn.SetActive(false);
					if (voucherBtn != null) voucherBtn.SetActive(false);

					Transform uiTitle = __instance.transform.Find("Container/UITitle");
					if (uiTitle != null)
					{
						Transform mobileCurrency = uiTitle.Find("Currency");
						if (mobileCurrency != null)
						{
							mobileCurrency.gameObject.SetActive(false);
						}

						UITitle titleWidget = (UITitle)AccessTools.Field(typeof(PetGroup), "_titleWidget").GetValue(__instance);
						if (titleWidget != null && titleWidget.Object != null)
						{
							UITitleWidget pcTitleWidget = titleWidget.Object;
							GameObject[] pcCurrencies = (GameObject[])AccessTools.Field(pcTitleWidget.GetType(), "_currencies").GetValue(pcTitleWidget);
							if (pcCurrencies != null)
							{
								if (pcCurrencies.Length > 0 && pcCurrencies[0] != null) pcCurrencies[0].SetActive(false);
								if (pcCurrencies.Length > 1 && pcCurrencies[1] != null) pcCurrencies[1].SetActive(true);
								if (pcCurrencies.Length > 2 && pcCurrencies[2] != null) pcCurrencies[2].SetActive(true);
							}
						}
					}
				}
				catch (Exception ex)
				{
					UnityEngine.Debug.LogException(ex);
				}
			}
		}

		[HarmonyPatch(typeof(PetGroup), "RefreshPetCountLabel")]
		private static class PetGroupRefreshPetCountLabelPatch
		{
			private static void Postfix(PetGroup __instance)
			{
				try
				{
					UITitle titleWidget = (UITitle)AccessTools.Field(typeof(PetGroup), "_titleWidget").GetValue(__instance);
					if (titleWidget == null || titleWidget.Object == null) return;

					UITitleWidget pcTitleWidget = titleWidget.Object;
					UILabel pcPetCountLabel = (UILabel)AccessTools.Field(typeof(UITitleWidget_PC), "_petCountLabel").GetValue(pcTitleWidget);
					if (pcPetCountLabel == null) return;

					PetsInfo? info = (PetsInfo?)AccessTools.Field(typeof(PetGroup), "_info").GetValue(__instance);
					PetGroup.PetOwnType currentTabType = (PetGroup.PetOwnType)AccessTools.Field(typeof(PetGroup), "_currentTabType").GetValue(__instance);

					if (currentTabType == PetGroup.PetOwnType.Holding)
					{
						int num = (info != null) ? KUtility.GetSize<Messages.Pet>(info.Value.Pets.Data) : 0;
						int num2 = (int)GameSystem<StatisticsSystem>.Instance().GetDeriveds(Derived.MaxTamingPet, 99f);
						pcPetCountLabel.text = string.Format("<em>{0}</em> <weak>/ {1}</weak>", num, num2);
					}
					else if (currentTabType == PetGroup.PetOwnType.Grazing)
					{
						int num3 = (info != null) ? KUtility.GetSize<Messages.Pet>(info.Value.GrazedPets.Data) : 0;
						int num4 = (info != null) ? info.Value.GrazableCount : 0;
						pcPetCountLabel.text = string.Format("<em>{0}</em> <weak>/ {1}</weak>", num3, num4);
					}
				}
				catch (Exception ex)
				{
					UnityEngine.Debug.LogException(ex);
				}
			}
		}

		[HarmonyPatch(typeof(UITitleWidget_PC), "UpdatePetCount")]
		private static class TitleWidgetPCUpdatePetCountPatch
		{
			private static void Postfix(UITitleWidget_PC __instance)
			{
				try
				{
					PetGroup petGroup = UIManager.FindScript<PetGroup>();
					if (petGroup != null && petGroup.IsOpened)
					{
						MethodInfo refreshMethod = typeof(PetGroup).GetMethod("RefreshPetCountLabel", BindingFlags.NonPublic | BindingFlags.Instance);
						refreshMethod.Invoke(petGroup, null);
					}
				}
				catch (Exception ex)
				{
					UnityEngine.Debug.LogException(ex);
				}
			}
		}

		[HarmonyPatch(typeof(PetManager), "TrySetTamerPlayer")]
		private static class PetManagerTrySetTamerPlayerPatch
		{
			private static bool Prefix(PetAI petAi, Messages.Pet msg, ref bool __result)
			{
				try
				{
					PlayerBehavior localPlayer = PlayerBehavior.LocalPlayer;
					if (localPlayer == null)
					{
						__result = false;
						return false;
					}
					
					if (localPlayer.Driver.IsVehicleKindOf<VehicleAirBalloon>())
					{
						__result = false;
						return false;
					}
					VehicleBase vehicle = petAi.GetComponent<VehicleBase>();
					if (vehicle != null)
					{
						localPlayer.Driver.SetVehicle(vehicle, true);
						if (vehicle.MoveSpeed < 10f)
						{
							CustomRein custom = GetCustomReinByEntityType(msg.EntityType);
							vehicle.MoveSpeed = custom != null ? custom.speed : GetDefaultSpeed(msg.EntityType);
						}
					}
					petAi.SetMaster(localPlayer.gameObject, msg.IsBoarding);
					
					__result = true;
					return false;
				}
				catch (Exception ex)
				{
					UnityEngine.Debug.LogException(ex);
					__result = false;
					return true;
				}
			}
		}
	}

	public class GenDict
	{
		public int PlayerSlot;
		public List<Messages.Pet> PetList;
		public List<Messages.Pet> GrazedPetList;
		public Dictionary<string, List<Item>> PetInventories;
		public AppearPet ActivePet;
		public Dictionary<string, string> PendingMilestones;
		public Dictionary<string, int> PendingRanks;
	}
}
