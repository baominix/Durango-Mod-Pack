using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using Durango.Logic;
using Durango.Logic.Party;
using Durango.Player;
using Durango.Render.Camera;
using Durango.UI;
using Durango.UI.Control;
using Durango.UI.Popup;
using Durango.Utils;
using HarmonyLib;
using InteractionData;
using Messages;
using Shared.Player;
using UnityEngine;

namespace PartySystemPlugin
{
	[BepInPlugin("com.antigravity.partysystem", "Party System Plugin", "2.0.0")]
	public class PartySystemPlugin : BaseUnityPlugin
	{
		public static PartySystemPlugin Instance;

		// ================================================================
		// NPC K in-world state
		// ================================================================
		private GameObject _kObject;
		private Animation _kAnimation;
		private AnimalBehavior _kAnimal;
		private float _stuckTimer = 0f;
		private bool _kIsMoving = false;
		private float _kRunRetryTimer = 0f;
		private GameObject _kNameObject;
		private float _kNameBottomOffset = 0f;

		// NPC Charlie in-world state
		private GameObject _charlieObject;
		private Animation _charlieAnimation;
		private AnimalBehavior _charlieAnimal;
		private float _charlieStuckTimer = 0f;
		private bool _charlieIsMoving = false;
		private float _charlieRunRetryTimer = 0f;
		private GameObject _charlieNameObject;
		private float _charlieNameBottomOffset = 0f;

		// ================================================================
		// PetAI-style follow constants
		// ================================================================
		// Durango client-space coordinates are roughly hundreds of units per
		// character length. These values mirror VehiclePet/NpcAIK scale.
		private const float FOLLOW_DIST = 450f;
		private const float FOLLOW_THRESHOLD = 200f;
		private const float TELEPORT_DIST = 3200f;
		private const float RUN_SPEED = 500f;
		private const float STUCK_TIMEOUT = 10f;
		// K variants have different child-mesh facing offsets. Compensate at
		// the AnimalBehavior root so PetAI movement still visually faces travel.
		private const float K_STORY_MODEL_FORWARD_YAW = 39f;
		private const float K_INDOOR_MODEL_FORWARD_YAW = -47f;
		private const string K_STORY_PREFAB_PATH = "Models/NPC/F_NPC_K_Story.prefab";
		private const string K_INDOOR_PREFAB_PATH = "Models/NPC/F_NPC_K_Indoor.prefab";
		private const string K_PORTRAIT_PRESET = "todo_icon_npc_TheFirm";
		private const string K_INDOOR_PORTRAIT_PRESET = "todo_icon_npc_RescueTf";
		private const string CHARLIE_PORTRAIT_PRESET = "todo_icon_npc_Optimistic";
		private const string NPC_RESCUE_MENU_ID = "party_npc_rescue";
		private const float NPC_RESCUE_STOP_DISTANCE = 45f;
		private const float NPC_RESCUE_TIMEOUT = 6f;
		private const float NPC_RESCUE_CPR_DURATION = 12.6f;
		private const float NPC_RESCUE_REVIVE_LIFE_RATIO = 0.35f;
		private const float NPC_RESCUE_BEFORE_DIALOG_DELAY = 2f;
		private const float NPC_RESCUE_AFTER_DIALOG_DELAY = 6f;
		private const float NPC_RESCUE_BEFORE_CPR_DELAY = 2f;
		private const float NPC_RESCUE_POST_DIALOG_DELAY = 4f;
		private const float NPC_RESCUE_BUBBLE_DURATION = 5f;
		private const float NPC_RESCUE_OFFLINE_AFTER_FINAL_BUBBLE_DELAY = 6f;
		private const float NPC_RESCUE_OFFLINE_DURATION = 60f;
		private const float CHARLIE_AMBIENT_BUBBLE_DURATION = 4f;
		private const float CHARLIE_AMBIENT_MIN_DELAY = 30f;
		private const float CHARLIE_AMBIENT_MAX_DELAY = 180f;
		private static readonly string[] K_RESCUE_START_LINES = new string[]
		{
			"Hold on. I'm coming.",
			"Stay still. I'll get you back up.",
			"Don't move. I can handle this."
		};
		private static readonly string[] K_RESCUE_FINISH_LINES = new string[]
		{
			"You're breathing again. Stay close.",
			"You're up. Don't make me do that twice.",
			"Good. Now stay behind me for a moment."
		};
		private static readonly string[] K_INDOOR_AMBIENT_LINES = new string[]
		{
			"듀랑고에서는 말이에요.",
			"캠프에서 많은 일이 이뤄져요.",
			"캠프에 익숙해지는 게 좋아요.",
			"저기 휠체어에 앉은 사람도 기차를 탔다던데,",
			"아는 사람이에요?",
			"그 기차 부식된지 수십년은 됐을 거예요.",
			"워프는 직선으로 흐르지 않죠.",
			"다른 생존자들도 찾아볼게요."
		};
		private static readonly string[] CHARLIE_RESCUE_START_LINES = new string[]
		{
			"Don't worry. Think of this as a very short break.",
			"Resting is important, but this is a little too much.",
			"Charlie. Charlie. Rescue Charlie? No—rescuing you."
		};
		private static readonly string[] CHARLIE_RESCUE_FINISH_LINES = new string[]
		{
			"There you go. Break time is over.",
			"See? Knowing when to rest also means knowing when to get up.",
			"You're back. Let's stay optimistic, okay?"
		};
		private static readonly string[] CHARLIE_AMBIENT_LINES = new string[]
		{
			"여기 모닥불이 있지.",
			"낙관주의는 최후의 날 마음인 거 알어?",
			"찰리. 찰리. 찰리.",
			"쉬는 법을 알아야, 안 쉬는 법도 알지요."
		};

		private enum NpcPartyMember
		{
			None,
			K,
			Charlie
		}

		private bool _npcRescueRunning = false;
		private NpcPartyMember _npcRescueMember = NpcPartyMember.None;
		private Coroutine _kAmbientDialogCoroutine;
		private Coroutine _charlieAmbientDialogCoroutine;
		private bool _kWasOffline = false;
		private bool _charlieWasOffline = false;
		private static string _selectedKPrefabPath = null;

		// ================================================================
		// Mock Party State
		// ================================================================
		public static class MockPartyState
		{
			public static bool IsInParty = false;
			public static string LeaderEntityId = "";
			public static bool KInvited = false;
			public static bool KJoined = false;
			public static float KInviteTimer = 0f;
			public static float KOfflineUntil = 0f;
			public static bool CharlieInvited = false;
			public static bool CharlieJoined = false;
			public static float CharlieInviteTimer = 0f;
			public static float CharlieOfflineUntil = 0f;
		}

		// ================================================================
		// Lifecycle
		// ================================================================

		private void Awake()
		{
			Instance = this;
			new Harmony("com.antigravity.partysystem").PatchAll(Assembly.GetExecutingAssembly());
			Logger.LogInfo("PartySystemPlugin v2.0 loaded.");
		}

		private void Update()
		{
			try
			{
				PlayerBehavior localPlayer = PlayerBehavior.LocalPlayer;
				if (localPlayer == null) return;

				PartySystem partySystem = GameSystem<PartySystem>.Instance();
				if (partySystem == null) return;

				UpdateNpcOfflineTransitions(partySystem);

				// K invite acceptance timer (2.5s delay)
				if (MockPartyState.KInvited && !MockPartyState.KJoined)
				{
					MockPartyState.KInviteTimer += Time.deltaTime;
					if (MockPartyState.KInviteTimer >= 2.5f)
					{
						MockPartyState.KJoined = true;
						MockPartyState.KInvited = false;
						MockPartyState.KInviteTimer = 0f;
						TriggerOnParty(partySystem);
						UIManager.SystemMsg("K has joined the party.", 3f);
						SoundManager.PlayEvent("ui_party_join");
						Logger.LogInfo("K joined the party.");
					}
				}

				// Charlie invite acceptance timer (same simulated delay as K)
				if (MockPartyState.CharlieInvited && !MockPartyState.CharlieJoined)
				{
					MockPartyState.CharlieInviteTimer += Time.deltaTime;
					if (MockPartyState.CharlieInviteTimer >= 2.5f)
					{
						MockPartyState.CharlieJoined = true;
						MockPartyState.CharlieInvited = false;
						MockPartyState.CharlieInviteTimer = 0f;
						TriggerOnParty(partySystem);
						UIManager.SystemMsg("Charlie has joined the party.", 3f);
						SoundManager.PlayEvent("ui_party_join");
						Logger.LogInfo("Charlie joined the party.");
					}
				}

				// K follow logic (PetAI style)
				if (MockPartyState.KJoined)
				{
					if (IsNpcOffline(NpcPartyMember.K))
					{
						if (_kObject != null)
						{
							DespawnNPCK();
						}
					}
					else if (_kObject == null)
					{
						SpawnNPCK(localPlayer);
					}
					else if (!_npcRescueRunning)
					{
						UpdateKFollow(localPlayer, Time.deltaTime);
					}
					UpdateKNameLabel();
				}
				else if (_kObject != null)
				{
					DespawnNPCK();
				}

				// Charlie follows with the same PetAI-style movement as K.
				if (MockPartyState.CharlieJoined)
				{
					if (IsNpcOffline(NpcPartyMember.Charlie))
					{
						if (_charlieObject != null)
						{
							DespawnNPCCharlie();
						}
					}
					else if (_charlieObject == null)
					{
						SpawnNPCCharlie(localPlayer);
					}
					else if (!_npcRescueRunning)
					{
						UpdateCharlieFollow(localPlayer, Time.deltaTime);
					}
					UpdateCharlieNameLabel();
				}
				else if (_charlieObject != null)
				{
					DespawnNPCCharlie();
				}
			}
			catch (Exception ex)
			{
				UnityEngine.Debug.LogException(ex);
			}
		}

		private static string GetOrChooseKPrefabPath()
		{
			if (string.IsNullOrEmpty(_selectedKPrefabPath))
			{
				_selectedKPrefabPath = (UnityEngine.Random.value < 0.5f)
					? K_STORY_PREFAB_PATH
					: K_INDOOR_PREFAB_PATH;
				if (Instance != null)
				{
					Instance.Logger.LogInfo("Selected K prefab 50/50: " + _selectedKPrefabPath);
				}
			}
			return _selectedKPrefabPath;
		}

		private static bool IsSelectedKIndoorPrefab()
		{
			return string.Equals(GetOrChooseKPrefabPath(), K_INDOOR_PREFAB_PATH, StringComparison.Ordinal);
		}

		private static string GetSelectedKPortraitPreset()
		{
			return IsSelectedKIndoorPrefab() ? K_INDOOR_PORTRAIT_PRESET : K_PORTRAIT_PRESET;
		}

		private static float GetSelectedKModelForwardYaw()
		{
			return IsSelectedKIndoorPrefab() ? K_INDOOR_MODEL_FORWARD_YAW : K_STORY_MODEL_FORWARD_YAW;
		}

		private static void ResetKPrefabSelection()
		{
			if (!string.IsNullOrEmpty(_selectedKPrefabPath) && Instance != null)
			{
				Instance.Logger.LogInfo("Reset K prefab selection.");
			}
			_selectedKPrefabPath = null;
		}

		// ================================================================
		// NPC K Spawn / Despawn (local GameObject, no server)
		// ================================================================

		private void SpawnNPCK(PlayerBehavior localPlayer)
		{
			try
			{
				Vector3 spawnPos = localPlayer.transform.position - localPlayer.transform.forward * FOLLOW_DIST;
				spawnPos.y = localPlayer.transform.position.y;

				_kObject = new GameObject("NPC_K");
				_kObject.transform.position = spawnPos;
				_kObject.transform.rotation = localPlayer.transform.rotation;
				_stuckTimer = 0f;
				_kIsMoving = false;
				_kRunRetryTimer = 0f;
				_kAnimation = null;
				_kAnimal = null;
				string kPrefabPath = GetOrChooseKPrefabPath();

				// Load K model prefab asynchronously
				Singleton<AssetBundleManager>.Instance().RequestAsset(
					kPrefabPath,
					typeof(GameObject),
					delegate(UnityEngine.Object asset)
					{
						if (asset != null && _kObject != null)
						{
							GameObject holder = _kObject;
							GameObject kModel = (GameObject)UnityEngine.Object.Instantiate(asset);
							kModel.name = "NPC_K";
							kModel.transform.position = holder.transform.position;
							kModel.SetActive(true);

							_kAnimal = kModel.GetComponent<AnimalBehavior>();
							_kAnimation = kModel.GetComponentInChildren<Animation>();
							_kObject = kModel;
							// This story prefab has an autonomous actor that randomly plays
							// K_Stand/K_Idle every 6 seconds. Our follower drives movement
							// itself, so disable the competing animation controller.
							ClientAnimalActor clientActor = kModel.GetComponent<ClientAnimalActor>();
							if (clientActor != null) clientActor.enabled = false;
							ClientActorChat actorChat = kModel.GetComponent<ClientActorChat>();
							if (actorChat != null) actorChat.enabled = false;
							if (_kAnimal != null)
							{
								_kAnimal.EntityId = "npc_k";
								_kAnimal.CurrentPosition = holder.transform.position;
								// Keep AnimalBehavior's internal yaw in sync with the spawn
								// rotation before smooth chase turning begins.
								float spawnYaw = Mathf.Repeat(
									Maths.CalcYaw(localPlayer.transform) - GetSelectedKModelForwardYaw(),
									360f);
								_kAnimal.TurnToYaw(spawnYaw, true);
							}
							UnityEngine.Object.Destroy(holder);

							TryAttachLoadedClip(_kAnimation, "F_Barehand_Run");
							SetKAnimation("idle");
							CreateKNameLabel();
							StartKAmbientDialog();
							Logger.LogInfo("K model loaded from " + kPrefabPath + ". AnimalBehavior=" + (_kAnimal != null)
								+ ", Animation=" + (_kAnimation != null));
						}
						else
						{
							Logger.LogWarning("Failed to load K model prefab.");
						}
					}
				);

				Logger.LogInfo("NPC K spawned at " + spawnPos);
			}
			catch (Exception ex)
			{
				Logger.LogError("SpawnNPCK error: " + ex);
			}
		}

		private void DespawnNPCK()
		{
			if (_kObject != null)
			{
				StopKAmbientDialog();
				UnityEngine.Object.Destroy(_kObject);
				_kObject = null;
				_kAnimation = null;
				_kAnimal = null;
				_stuckTimer = 0f;
				_kIsMoving = false;
				_kRunRetryTimer = 0f;
				DestroyKNameLabel();
				Logger.LogInfo("NPC K despawned.");
			}
		}

		private void CreateKNameLabel()
		{
			DestroyKNameLabel();
			try
			{
				PlayerFloatingGroup group = UIManager.FindScript<PlayerFloatingGroup>();
				if (group == null) return;

				FieldInfo templateField = typeof(PlayerFloatingGroup).GetField("_floatingUIBase", BindingFlags.NonPublic | BindingFlags.Instance);
				GameObject template = (templateField != null) ? templateField.GetValue(group) as GameObject : null;
				if (template == null) return;

				GameObject nameObject = group.gameObject.AddChild(template);
				PlayerFloatingControl control = nameObject.GetComponent<PlayerFloatingControl>();
				if (control == null)
				{
					UnityEngine.Object.Destroy(nameObject);
					return;
				}

				control.Target = null;
				control.SetName("K");
				control.SetNameColor(Color.white);
				control.SetTitle(string.Empty);
				control.SetFloatingIcon(string.Empty);
				control.SetDrawIconVisible(false);

				FieldInfo clanField = typeof(PlayerFloatingControl).GetField("_clantagLabel", BindingFlags.NonPublic | BindingFlags.Instance);
				UILabel clanLabel = (clanField != null) ? clanField.GetValue(control) as UILabel : null;
				if (clanLabel != null) clanLabel.gameObject.SetActive(false);

				FieldInfo separatorField = typeof(PlayerFloatingControl).GetField("_separator", BindingFlags.NonPublic | BindingFlags.Instance);
				UISprite separator = (separatorField != null) ? separatorField.GetValue(control) as UISprite : null;
				if (separator != null) separator.enabled = false;

				FieldInfo bottomField = typeof(PlayerFloatingControl).GetField("_bottomOffset", BindingFlags.NonPublic | BindingFlags.Instance);
				_kNameBottomOffset = (bottomField != null) ? (float)bottomField.GetValue(control) : 0f;
				_kNameObject = nameObject;
				_kNameObject.name = "K_PlayerFloatingControl";
				_kNameObject.SetActive(true);
				UpdateKNameLabel();
			}
			catch (Exception ex)
			{
				Logger.LogWarning("Create K name label failed: " + ex.Message);
				DestroyKNameLabel();
			}
		}

		private void UpdateKNameLabel()
		{
			if (_kAnimal == null) return;
			if (_kNameObject == null)
			{
				CreateKNameLabel();
				if (_kNameObject == null) return;
			}

			Vector3 worldPosition = _kAnimal.CurrentPosition + Vector3.down * _kNameBottomOffset;
			_kNameObject.transform.localPosition = MainCamera.WorldToNGUIPos(worldPosition, null);
			if (!_kNameObject.activeSelf) _kNameObject.SetActive(true);
		}

		private void DestroyKNameLabel()
		{
			if (_kNameObject != null)
			{
				UnityEngine.Object.Destroy(_kNameObject);
			}
			_kNameObject = null;
			_kNameBottomOffset = 0f;
		}

		private void StartKAmbientDialog()
		{
			StopKAmbientDialog();
			if (_kObject != null && _kAnimal != null && MockPartyState.KJoined && !IsNpcOffline(NpcPartyMember.K))
			{
				_kAmbientDialogCoroutine = StartCoroutine(CoKAmbientDialog());
			}
		}

		private void StopKAmbientDialog()
		{
			if (_kAmbientDialogCoroutine != null)
			{
				StopCoroutine(_kAmbientDialogCoroutine);
				_kAmbientDialogCoroutine = null;
			}
			try
			{
				if (_kAnimal != null)
				{
					ChatBubbleGroup group = UIManager.FindScript<ChatBubbleGroup>();
					if (group != null) group.Hide(_kAnimal.EntityId);
				}
			}
			catch
			{
				// Best-effort cleanup only.
			}
		}

		private IEnumerator CoKAmbientDialog()
		{
			int cursor = 0;
			yield return null;

			while (_kObject != null && _kAnimal != null && MockPartyState.KJoined && !IsNpcOffline(NpcPartyMember.K))
			{
				PlayerBehavior localPlayer = PlayerBehavior.LocalPlayer;
				if (_npcRescueRunning || localPlayer == null || !IsKInAmbientChatRange(localPlayer))
				{
					yield return new WaitForSeconds(1f);
					continue;
				}

				string rawLine = K_INDOOR_AMBIENT_LINES[cursor % K_INDOOR_AMBIENT_LINES.Length];
				cursor++;
				ShowNpcRescueBubble(
					NpcPartyMember.K,
					LocalizeLine(rawLine),
					GetSelectedKPortraitPreset(),
					CHARLIE_AMBIENT_BUBBLE_DURATION);

				yield return new WaitForSeconds(CHARLIE_AMBIENT_BUBBLE_DURATION);

				float nextDelay = UnityEngine.Random.Range(CHARLIE_AMBIENT_MIN_DELAY, CHARLIE_AMBIENT_MAX_DELAY);
				float waitUntil = Time.time + nextDelay;
				while (Time.time < waitUntil && _kObject != null && MockPartyState.KJoined && !IsNpcOffline(NpcPartyMember.K))
				{
					yield return new WaitForSeconds(1f);
				}
			}

			_kAmbientDialogCoroutine = null;
		}

		private bool IsKInAmbientChatRange(PlayerBehavior localPlayer)
		{
			if (localPlayer == null || _kAnimal == null) return false;
			Vector3 diff = localPlayer.transform.position - _kAnimal.CurrentPosition;
			diff.y = 0f;
			return diff.magnitude <= 600f;
		}

		// ================================================================
		// PetAI-style Follow Logic
		// ================================================================

		private void UpdateKFollow(PlayerBehavior localPlayer, float dt)
		{
			if (_kObject == null || localPlayer == null || dt <= 0f) return;

			Vector3 kPos = (_kAnimal != null) ? _kAnimal.CurrentPosition : _kObject.transform.position;
			Vector3 pPos = localPlayer.transform.position;

			// 2D distance (ignore Y, same as PetAI Maths.Make2D)
			Vector3 diff = pPos - kPos;
			diff.y = 0f;
			float distance = diff.magnitude;

			// PetAI only starts chasing after FollowDistance + threshold, then
			// continues until it reaches FollowDistance. This avoids jitter.
			float chaseThreshold = _kIsMoving ? FOLLOW_DIST : FOLLOW_DIST + FOLLOW_THRESHOLD;
			if (distance <= chaseThreshold)
			{
				if (_kIsMoving)
				{
					SetKAnimation("idle");
					_kIsMoving = false;
					_kRunRetryTimer = 0f;
				}
				_stuckTimer = 0f;
				return;
			}

			// --- Zone 3: Too far -> teleport (like PetAI.SpawnNearMaster) ---
			if (distance > TELEPORT_DIST)
			{
				Vector3 teleportPos = pPos - localPlayer.transform.forward * FOLLOW_DIST;
				teleportPos.y = pPos.y;
				if (_kAnimal != null) _kAnimal.CurrentPosition = teleportPos;
				else _kObject.transform.position = teleportPos;
				_stuckTimer = 0f;
				Logger.LogInfo("K teleported near player (distance was " + distance + ")");
				return;
			}

			// --- Zone 2: Chase (like PetAI.ChaseDoing) ---

			// Match NpcAIK.ChaseDoing: run directly toward the player. Using a
			// moving point behind the player made K circle or strafe when the
			// player rotated in place.
			Vector3 destPos = pPos;

			// Direction to target (2D)
			Vector3 dir = destPos - kPos;
			dir.y = 0f;
			if (dir.sqrMagnitude < 0.001f) return;
			dir.Normalize();

			// K always uses the NpcAIK run speed.
			float speed = RUN_SPEED;

			// Movement with collision sliding (like PetAI.ProcessCollisionWithSliding)
			Vector3 velocity = dir * speed;
			Vector3 moveDelta = velocity * dt;
			Vector3 nextPos;

			try
			{
				// Use game's collision system
				if (moveDelta != Vector3.zero)
				{
					CollisionParam param = Collisions.CreateCollisionParam(kPos, moveDelta);
					Vector3 slidDelta = Collisions.ProcessSimpleSliding(param);
					nextPos = kPos + slidDelta;

					// Stuck detection (like PetAI: < 70% target speed)
					float actualSpeed = slidDelta.magnitude / dt;
					if (speed > 0f && actualSpeed / speed < 0.7f)
					{
						_stuckTimer += dt;
						if (_stuckTimer > STUCK_TIMEOUT)
						{
							// Teleport if stuck too long
							Vector3 unstuckPos = pPos - localPlayer.transform.forward * FOLLOW_DIST;
							unstuckPos.y = pPos.y;
							if (_kAnimal != null) _kAnimal.CurrentPosition = unstuckPos;
							else _kObject.transform.position = unstuckPos;
							_stuckTimer = 0f;
							return;
						}
					}
					else
					{
						_stuckTimer = 0f;
					}
				}
				else
				{
					return;
				}
			}
			catch
			{
				// Fallback: direct movement without collision
				nextPos = kPos + moveDelta;
				_stuckTimer = 0f;
			}

			// Apply position (match player Y level)
			nextPos.y = pPos.y;
			if (_kAnimal != null)
			{
				// Match the native NpcAIK/PetAI convention exactly. Use the
				// post-collision movement direction so K faces the path she
				// actually travels instead of strafing while sliding.
				Vector3 actualDirection = nextPos - kPos;
				actualDirection.y = 0f;
				if (actualDirection.sqrMagnitude > 0.001f)
				{
					float movementYaw = Mathf.Repeat(
						Maths.CalcYaw(actualDirection.normalized) - GetSelectedKModelForwardYaw(),
						360f);
					_kAnimal.TurnToYaw(movementYaw, false);
				}
				_kAnimal.CurrentPosition = nextPos;
			}
			else _kObject.transform.position = nextPos;

			_kRunRetryTimer -= dt;
			bool runPlaying = _kAnimation != null && _kAnimation.IsPlaying("F_Barehand_Run");
			if (!_kIsMoving || (!runPlaying && _kRunRetryTimer <= 0f))
			{
				SetKAnimation("run");
				_kIsMoving = true;
				_kRunRetryTimer = 0.5f;
			}
		}

		private void SetKAnimation(string animName)
		{
			if (_kAnimation == null) return;
			try
			{
				string clipName = (animName == "idle") ? "K_Stand" : "F_Barehand_Run";
				AnimationClip clip = _kAnimation.GetClip(clipName);
				if (clip == null)
				{
					TryAttachLoadedClip(_kAnimation, clipName);
					clip = _kAnimation.GetClip(clipName);
				}

				if (clip == null)
				{
					// The story prefab always contains this clip, so K remains animated
					// even if the shared player run clip has not been loaded yet.
					clipName = (animName == "idle") ? "K_Stand" : "K_Idle";
				}

				_kAnimation.CrossFade(clipName, 0.15f);
				AnimationState state = _kAnimation[clipName];
				if (state != null)
				{
					state.wrapMode = WrapMode.Loop;
					state.speed = 1f;
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning("SetKAnimation failed: " + ex.Message);
			}
		}

		private void TryAttachLoadedClip(Animation animation, string clipName)
		{
			if (animation == null || animation.GetClip(clipName) != null) return;

			if (TryAttachPlayerClip(animation, clipName, clipName)) return;
			if (!clipName.StartsWith("F_", StringComparison.OrdinalIgnoreCase)
				&& TryAttachPlayerClip(animation, "F_" + clipName, clipName)) return;
			if (!clipName.StartsWith("M_", StringComparison.OrdinalIgnoreCase)
				&& TryAttachPlayerClip(animation, "M_" + clipName, clipName)) return;

			UnityEngine.Object[] loadedClips = Resources.FindObjectsOfTypeAll(typeof(AnimationClip));
			for (int i = 0; i < loadedClips.Length; i++)
			{
				AnimationClip clip = loadedClips[i] as AnimationClip;
				if (clip != null && string.Equals(clip.name, clipName, StringComparison.OrdinalIgnoreCase))
				{
					animation.AddClip(clip, clipName);
					Logger.LogInfo("Attached loaded animation clip: " + animation.name + " / " + clipName);
					return;
				}
			}
		}

		private bool TryAttachPlayerClip(Animation animation, string sourceClipName, string aliasClipName)
		{
			return TryAttachPlayerClip(animation, false, sourceClipName, aliasClipName)
				|| TryAttachPlayerClip(animation, true, sourceClipName, aliasClipName);
		}

		private bool TryAttachPlayerClip(Animation animation, bool male, string sourceClipName, string aliasClipName)
		{
			if (animation == null) return false;
			try
			{
				foreach (KeyValuePair<string, AnimationClip> clip in PlayerManager.GetPlayerClips(male))
				{
					if (clip.Value == null) continue;
					if (!string.Equals(clip.Key, sourceClipName, StringComparison.OrdinalIgnoreCase)
						&& !string.Equals(clip.Value.name, sourceClipName, StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}

					string addName = string.IsNullOrEmpty(aliasClipName) ? clip.Key : aliasClipName;
					animation.AddClip(clip.Value, addName);
					if (!string.Equals(addName, clip.Key, StringComparison.OrdinalIgnoreCase)
						&& animation.GetClip(clip.Key) == null)
					{
						animation.AddClip(clip.Value, clip.Key);
					}
					Logger.LogInfo("Attached player animation clip: " + animation.name
						+ " / " + sourceClipName + " as " + addName);
					return true;
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning("TryAttachPlayerClip failed: " + sourceClipName + " / " + ex.Message);
			}
			return false;
		}

		// ================================================================
		// NPC Charlie Spawn / Despawn / Follow
		// ================================================================

		private void SpawnNPCCharlie(PlayerBehavior localPlayer)
		{
			try
			{
				// Start Charlie on the other side of the player so two followers do
				// not initially occupy the same point.
				Vector3 spawnPos = localPlayer.transform.position
					- localPlayer.transform.forward * FOLLOW_DIST
					+ localPlayer.transform.right * 250f;
				spawnPos.y = localPlayer.transform.position.y;

				_charlieObject = new GameObject("NPC_Charlie_Loading");
				_charlieObject.transform.position = spawnPos;
				_charlieObject.transform.rotation = localPlayer.transform.rotation;
				_charlieStuckTimer = 0f;
				_charlieIsMoving = false;
				_charlieRunRetryTimer = 0f;
				_charlieAnimation = null;
				_charlieAnimal = null;

				Singleton<AssetBundleManager>.Instance().RequestAsset(
					"Models/Ancora/NPC/F_NPC_Player_First.prefab",
					typeof(GameObject),
					delegate(UnityEngine.Object asset)
					{
						if (asset != null && _charlieObject != null)
						{
							GameObject holder = _charlieObject;
							GameObject model = (GameObject)UnityEngine.Object.Instantiate(asset);
							model.name = "NPC_Charlie";
							model.transform.position = holder.transform.position;
							model.SetActive(true);

							_charlieAnimal = model.GetComponent<AnimalBehavior>();
							_charlieAnimation = model.GetComponentInChildren<Animation>();
							_charlieObject = model;

							ClientAnimalActor clientActor = model.GetComponent<ClientAnimalActor>();
							if (clientActor != null) clientActor.enabled = false;
							ClientInteractionQuest interaction = model.GetComponent<ClientInteractionQuest>();
							if (interaction != null) interaction.enabled = false;
							ClientActorChat actorChat = model.GetComponent<ClientActorChat>();
							if (actorChat != null) actorChat.enabled = false;

							if (_charlieAnimal != null)
							{
								_charlieAnimal.EntityId = "npc_charlie";
								_charlieAnimal.CurrentPosition = holder.transform.position;
								_charlieAnimal.TurnToYaw(Maths.CalcYaw(localPlayer.transform), true);
							}
							UnityEngine.Object.Destroy(holder);

							TryAttachLoadedClip(_charlieAnimation, "F_Barehand_Run");
							TryAttachLoadedClip(_charlieAnimation, "F_Barehand_Stand");
							SetCharlieAnimation("idle");
							CreateCharlieNameLabel();
							StartCharlieAmbientDialog();
							Logger.LogInfo("Charlie model loaded. AnimalBehavior=" + (_charlieAnimal != null)
								+ ", Animation=" + (_charlieAnimation != null));
						}
						else
						{
							Logger.LogWarning("Failed to load Charlie model prefab.");
						}
					}
				);

				Logger.LogInfo("NPC Charlie spawned at " + spawnPos);
			}
			catch (Exception ex)
			{
				Logger.LogError("SpawnNPCCharlie error: " + ex);
			}
		}

		private void DespawnNPCCharlie()
		{
			if (_charlieObject != null)
			{
				StopCharlieAmbientDialog();
				UnityEngine.Object.Destroy(_charlieObject);
				_charlieObject = null;
				_charlieAnimation = null;
				_charlieAnimal = null;
				_charlieStuckTimer = 0f;
				_charlieIsMoving = false;
				_charlieRunRetryTimer = 0f;
				DestroyCharlieNameLabel();
				Logger.LogInfo("NPC Charlie despawned.");
			}
		}

		private void CreateCharlieNameLabel()
		{
			DestroyCharlieNameLabel();
			try
			{
				PlayerFloatingGroup group = UIManager.FindScript<PlayerFloatingGroup>();
				if (group == null) return;

				FieldInfo templateField = typeof(PlayerFloatingGroup).GetField("_floatingUIBase", BindingFlags.NonPublic | BindingFlags.Instance);
				GameObject template = (templateField != null) ? templateField.GetValue(group) as GameObject : null;
				if (template == null) return;

				GameObject nameObject = group.gameObject.AddChild(template);
				PlayerFloatingControl control = nameObject.GetComponent<PlayerFloatingControl>();
				if (control == null)
				{
					UnityEngine.Object.Destroy(nameObject);
					return;
				}

				control.Target = null;
				control.SetName("Charlie");
				control.SetNameColor(Color.white);
				control.SetTitle(string.Empty);
				control.SetFloatingIcon(string.Empty);
				control.SetDrawIconVisible(false);

				FieldInfo clanField = typeof(PlayerFloatingControl).GetField("_clantagLabel", BindingFlags.NonPublic | BindingFlags.Instance);
				UILabel clanLabel = (clanField != null) ? clanField.GetValue(control) as UILabel : null;
				if (clanLabel != null) clanLabel.gameObject.SetActive(false);

				FieldInfo separatorField = typeof(PlayerFloatingControl).GetField("_separator", BindingFlags.NonPublic | BindingFlags.Instance);
				UISprite separator = (separatorField != null) ? separatorField.GetValue(control) as UISprite : null;
				if (separator != null) separator.enabled = false;

				FieldInfo bottomField = typeof(PlayerFloatingControl).GetField("_bottomOffset", BindingFlags.NonPublic | BindingFlags.Instance);
				_charlieNameBottomOffset = (bottomField != null) ? (float)bottomField.GetValue(control) : 0f;
				_charlieNameObject = nameObject;
				_charlieNameObject.name = "Charlie_PlayerFloatingControl";
				_charlieNameObject.SetActive(true);
				UpdateCharlieNameLabel();
			}
			catch (Exception ex)
			{
				Logger.LogWarning("Create Charlie name label failed: " + ex.Message);
				DestroyCharlieNameLabel();
			}
		}

		private void UpdateCharlieNameLabel()
		{
			if (_charlieAnimal == null) return;
			if (_charlieNameObject == null)
			{
				CreateCharlieNameLabel();
				if (_charlieNameObject == null) return;
			}

			Vector3 worldPosition = _charlieAnimal.CurrentPosition + Vector3.down * _charlieNameBottomOffset;
			_charlieNameObject.transform.localPosition = MainCamera.WorldToNGUIPos(worldPosition, null);
			if (!_charlieNameObject.activeSelf) _charlieNameObject.SetActive(true);
		}

		private void DestroyCharlieNameLabel()
		{
			if (_charlieNameObject != null)
			{
				UnityEngine.Object.Destroy(_charlieNameObject);
			}
			_charlieNameObject = null;
			_charlieNameBottomOffset = 0f;
		}

		private void StartCharlieAmbientDialog()
		{
			StopCharlieAmbientDialog();
			if (_charlieObject != null && _charlieAnimal != null && MockPartyState.CharlieJoined)
			{
				_charlieAmbientDialogCoroutine = StartCoroutine(CoCharlieAmbientDialog());
			}
		}

		private void StopCharlieAmbientDialog()
		{
			if (_charlieAmbientDialogCoroutine != null)
			{
				StopCoroutine(_charlieAmbientDialogCoroutine);
				_charlieAmbientDialogCoroutine = null;
			}
			try
			{
				if (_charlieAnimal != null)
				{
					ChatBubbleGroup group = UIManager.FindScript<ChatBubbleGroup>();
					if (group != null) group.Hide(_charlieAnimal.EntityId);
				}
			}
			catch
			{
				// Best-effort cleanup only.
			}
		}

		private IEnumerator CoCharlieAmbientDialog()
		{
			int cursor = 0;
			yield return null;

			while (_charlieObject != null && _charlieAnimal != null && MockPartyState.CharlieJoined)
			{
				PlayerBehavior localPlayer = PlayerBehavior.LocalPlayer;
				if (_npcRescueRunning || localPlayer == null || !IsCharlieInAmbientChatRange(localPlayer))
				{
					yield return new WaitForSeconds(1f);
					continue;
				}

				string rawLine = CHARLIE_AMBIENT_LINES[cursor % CHARLIE_AMBIENT_LINES.Length];
				cursor++;
				ShowNpcRescueBubble(
					NpcPartyMember.Charlie,
					LocalizeLine(rawLine),
					CHARLIE_PORTRAIT_PRESET,
					CHARLIE_AMBIENT_BUBBLE_DURATION);

				yield return new WaitForSeconds(CHARLIE_AMBIENT_BUBBLE_DURATION);

				float nextDelay = UnityEngine.Random.Range(CHARLIE_AMBIENT_MIN_DELAY, CHARLIE_AMBIENT_MAX_DELAY);
				float waitUntil = Time.time + nextDelay;
				while (Time.time < waitUntil && _charlieObject != null && MockPartyState.CharlieJoined)
				{
					yield return new WaitForSeconds(1f);
				}
			}

			_charlieAmbientDialogCoroutine = null;
		}

		private bool IsCharlieInAmbientChatRange(PlayerBehavior localPlayer)
		{
			if (localPlayer == null || _charlieAnimal == null) return false;
			Vector3 diff = localPlayer.transform.position - _charlieAnimal.CurrentPosition;
			diff.y = 0f;
			return diff.magnitude <= 600f;
		}

		private string LocalizeLine(string text)
		{
			if (string.IsNullOrEmpty(text)) return string.Empty;
			try
			{
				return LocalizeSystem.Get(text);
			}
			catch
			{
				return text;
			}
		}

		private void UpdateCharlieFollow(PlayerBehavior localPlayer, float dt)
		{
			if (_charlieObject == null || localPlayer == null || dt <= 0f) return;

			Vector3 npcPos = (_charlieAnimal != null) ? _charlieAnimal.CurrentPosition : _charlieObject.transform.position;
			Vector3 playerPos = localPlayer.transform.position;
			Vector3 diff = playerPos - npcPos;
			diff.y = 0f;
			float distance = diff.magnitude;

			float chaseThreshold = _charlieIsMoving ? FOLLOW_DIST : FOLLOW_DIST + FOLLOW_THRESHOLD;
			if (distance <= chaseThreshold)
			{
				if (_charlieIsMoving)
				{
					SetCharlieAnimation("idle");
					_charlieIsMoving = false;
					_charlieRunRetryTimer = 0f;
				}
				_charlieStuckTimer = 0f;
				return;
			}

			if (distance > TELEPORT_DIST)
			{
				Vector3 teleportPos = playerPos - localPlayer.transform.forward * FOLLOW_DIST
					+ localPlayer.transform.right * 250f;
				teleportPos.y = playerPos.y;
				if (_charlieAnimal != null) _charlieAnimal.CurrentPosition = teleportPos;
				else _charlieObject.transform.position = teleportPos;
				_charlieStuckTimer = 0f;
				Logger.LogInfo("Charlie teleported near player (distance was " + distance + ")");
				return;
			}

			Vector3 direction = playerPos - npcPos;
			direction.y = 0f;
			if (direction.sqrMagnitude < 0.001f) return;
			direction.Normalize();

			Vector3 moveDelta = direction * RUN_SPEED * dt;
			Vector3 nextPos;
			try
			{
				CollisionParam param = Collisions.CreateCollisionParam(npcPos, moveDelta);
				Vector3 slidDelta = Collisions.ProcessSimpleSliding(param);
				nextPos = npcPos + slidDelta;

				float actualSpeed = slidDelta.magnitude / dt;
				if (actualSpeed / RUN_SPEED < 0.7f)
				{
					_charlieStuckTimer += dt;
					if (_charlieStuckTimer > STUCK_TIMEOUT)
					{
						Vector3 unstuckPos = playerPos - localPlayer.transform.forward * FOLLOW_DIST
							+ localPlayer.transform.right * 250f;
						unstuckPos.y = playerPos.y;
						if (_charlieAnimal != null) _charlieAnimal.CurrentPosition = unstuckPos;
						else _charlieObject.transform.position = unstuckPos;
						_charlieStuckTimer = 0f;
						return;
					}
				}
				else
				{
					_charlieStuckTimer = 0f;
				}
			}
			catch
			{
				nextPos = npcPos + moveDelta;
				_charlieStuckTimer = 0f;
			}

			nextPos.y = playerPos.y;
			if (_charlieAnimal != null)
			{
				Vector3 actualDirection = nextPos - npcPos;
				actualDirection.y = 0f;
				if (actualDirection.sqrMagnitude > 0.001f)
				{
					_charlieAnimal.TurnToYaw(Mathf.Repeat(Maths.CalcYaw(actualDirection.normalized), 360f), false);
				}
				_charlieAnimal.CurrentPosition = nextPos;
			}
			else
			{
				_charlieObject.transform.position = nextPos;
			}

			_charlieRunRetryTimer -= dt;
			bool runPlaying = _charlieAnimation != null && _charlieAnimation.IsPlaying("F_Barehand_Run");
			if (!_charlieIsMoving || (!runPlaying && _charlieRunRetryTimer <= 0f))
			{
				SetCharlieAnimation("run");
				_charlieIsMoving = true;
				_charlieRunRetryTimer = 0.5f;
			}
		}

		private void SetCharlieAnimation(string animName)
		{
			if (_charlieAnimation == null) return;
			try
			{
				string clipName = (animName == "idle") ? "F_Barehand_Stand" : "F_Barehand_Run";
				AnimationClip clip = _charlieAnimation.GetClip(clipName);
				if (clip == null)
				{
					TryAttachLoadedClip(_charlieAnimation, clipName);
					clip = _charlieAnimation.GetClip(clipName);
				}
				if (clip == null)
				{
					clipName = "F_Barehand_Sit_F";
				}

				_charlieAnimation.CrossFade(clipName, 0.15f);
				AnimationState state = _charlieAnimation[clipName];
				if (state != null)
				{
					state.wrapMode = WrapMode.Loop;
					state.speed = 1f;
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning("SetCharlieAnimation failed: " + ex.Message);
			}
		}

		// ================================================================
		// NPC Party Rescue (local CPR-style revive)
		// ================================================================

		private static float GetNpcOfflineUntil(NpcPartyMember member)
		{
			switch (member)
			{
			case NpcPartyMember.K:
				return MockPartyState.KOfflineUntil;
			case NpcPartyMember.Charlie:
				return MockPartyState.CharlieOfflineUntil;
			default:
				return 0f;
			}
		}

		private static void SetNpcOfflineUntil(NpcPartyMember member, float offlineUntil)
		{
			switch (member)
			{
			case NpcPartyMember.K:
				MockPartyState.KOfflineUntil = offlineUntil;
				break;
			case NpcPartyMember.Charlie:
				MockPartyState.CharlieOfflineUntil = offlineUntil;
				break;
			}
		}

		private static bool IsNpcOffline(NpcPartyMember member)
		{
			float offlineUntil = GetNpcOfflineUntil(member);
			return offlineUntil > 0f && Time.time < offlineUntil;
		}

		private static bool IsNpcOfflineByEntityId(string entityId)
		{
			if (entityId == "npc_k") return IsNpcOffline(NpcPartyMember.K);
			if (entityId == "npc_charlie") return IsNpcOffline(NpcPartyMember.Charlie);
			return false;
		}

		private void UpdateNpcOfflineTransitions(PartySystem partySystem)
		{
			UpdateNpcOfflineTransition(NpcPartyMember.K, partySystem, ref _kWasOffline);
			UpdateNpcOfflineTransition(NpcPartyMember.Charlie, partySystem, ref _charlieWasOffline);
		}

		private void UpdateNpcOfflineTransition(NpcPartyMember member, PartySystem partySystem, ref bool wasOffline)
		{
			float offlineUntil = GetNpcOfflineUntil(member);
			bool isOffline = IsNpcOffline(member);
			if (isOffline)
			{
				wasOffline = true;
				return;
			}

			if (offlineUntil > 0f)
			{
				SetNpcOfflineUntil(member, 0f);
				if (wasOffline && IsNpcJoined(member))
				{
					TriggerOnParty(partySystem);
					string npcName = GetNpcDisplayName(member);
					UIManager.SystemMsg(npcName + " is online again.", 3f);
					Logger.LogInfo(npcName + " returned online after rescue cooldown.");
				}
			}
			wasOffline = false;
		}

		private void SetNpcOfflineAfterRescue(NpcPartyMember member)
		{
			if (!IsNpcJoined(member)) return;

			string npcName = GetNpcDisplayName(member);
			SetNpcOfflineUntil(member, Time.time + NPC_RESCUE_OFFLINE_DURATION);
			if (member == NpcPartyMember.K)
			{
				_kWasOffline = true;
				HideNpcBubble(member);
				DespawnNPCK();
			}
			else if (member == NpcPartyMember.Charlie)
			{
				_charlieWasOffline = true;
				HideNpcBubble(member);
				DespawnNPCCharlie();
			}

			PartySystem partySystem = GameSystem<PartySystem>.Instance();
			TriggerOnParty(partySystem);
			UIManager.SystemMsg(npcName + " went offline for 1 minute.", 3f);
			Logger.LogInfo(npcName + " went offline for " + NPC_RESCUE_OFFLINE_DURATION + " seconds after rescue.");
		}

		private void HideNpcBubble(NpcPartyMember member)
		{
			try
			{
				ChatBubbleGroup group = UIManager.FindScript<ChatBubbleGroup>();
				AnimalBehavior animal = GetNpcAnimal(member);
				if (group != null && animal != null)
				{
					group.Hide(animal.EntityId);
				}
			}
			catch
			{
				// Best-effort UI cleanup only.
			}
		}

		private bool CanOfferNpcPartyRescue()
		{
			PlayerBehavior player = PlayerBehavior.LocalPlayer;
			if (player == null || player.IsAlive) return false;
			return SelectNpcRescuer(player) != NpcPartyMember.None;
		}

		private NpcPartyMember SelectNpcRescuer(PlayerBehavior player)
		{
			bool kAvailable = MockPartyState.KJoined && !IsNpcOffline(NpcPartyMember.K);
			bool charlieAvailable = MockPartyState.CharlieJoined && !IsNpcOffline(NpcPartyMember.Charlie);
			if (!kAvailable && !charlieAvailable) return NpcPartyMember.None;
			if (kAvailable && !charlieAvailable) return NpcPartyMember.K;
			if (!kAvailable && charlieAvailable) return NpcPartyMember.Charlie;

			float kDistance = GetNpcDistanceSqr(NpcPartyMember.K, player);
			float charlieDistance = GetNpcDistanceSqr(NpcPartyMember.Charlie, player);
			return (charlieDistance < kDistance) ? NpcPartyMember.Charlie : NpcPartyMember.K;
		}

		private float GetNpcDistanceSqr(NpcPartyMember member, PlayerBehavior player)
		{
			if (player == null) return float.MaxValue;
			GameObject obj = GetNpcObject(member);
			if (obj == null) return float.MaxValue;

			Vector3 diff = GetNpcPosition(member) - player.transform.position;
			diff.y = 0f;
			return diff.sqrMagnitude;
		}

		private GameObject GetNpcObject(NpcPartyMember member)
		{
			switch (member)
			{
			case NpcPartyMember.K:
				return _kObject;
			case NpcPartyMember.Charlie:
				return _charlieObject;
			default:
				return null;
			}
		}

		private AnimalBehavior GetNpcAnimal(NpcPartyMember member)
		{
			switch (member)
			{
			case NpcPartyMember.K:
				return _kAnimal;
			case NpcPartyMember.Charlie:
				return _charlieAnimal;
			default:
				return null;
			}
		}

		private Animation GetNpcAnimation(NpcPartyMember member)
		{
			switch (member)
			{
			case NpcPartyMember.K:
				return _kAnimation;
			case NpcPartyMember.Charlie:
				return _charlieAnimation;
			default:
				return null;
			}
		}

		private string GetNpcDisplayName(NpcPartyMember member)
		{
			return (member == NpcPartyMember.Charlie) ? "Charlie" : "K";
		}

		private bool IsNpcReady(NpcPartyMember member)
		{
			return GetNpcObject(member) != null
				&& (GetNpcAnimal(member) != null || GetNpcAnimation(member) != null);
		}

		private Vector3 GetNpcPosition(NpcPartyMember member)
		{
			AnimalBehavior animal = GetNpcAnimal(member);
			if (animal != null) return animal.CurrentPosition;

			GameObject obj = GetNpcObject(member);
			return (obj != null) ? obj.transform.position : Vector3.zero;
		}

		private void SetNpcPosition(NpcPartyMember member, Vector3 position)
		{
			AnimalBehavior animal = GetNpcAnimal(member);
			if (animal != null)
			{
				animal.CurrentPosition = position;
				return;
			}

			GameObject obj = GetNpcObject(member);
			if (obj != null) obj.transform.position = position;
		}

		private void TurnNpcToward(NpcPartyMember member, Vector3 direction, bool immediate)
		{
			direction.y = 0f;
			if (direction.sqrMagnitude < 0.001f) return;

			float yaw = Mathf.Repeat(Maths.CalcYaw(direction.normalized)
				- ((member == NpcPartyMember.K) ? GetSelectedKModelForwardYaw() : 0f), 360f);

			AnimalBehavior animal = GetNpcAnimal(member);
			if (animal != null)
			{
				animal.TurnToYaw(yaw, immediate);
				return;
			}

			GameObject obj = GetNpcObject(member);
			if (obj != null) obj.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
		}

		private void SetNpcRunAnimation(NpcPartyMember member)
		{
			if (member == NpcPartyMember.K)
			{
				SetKAnimation("run");
				_kIsMoving = true;
				_kRunRetryTimer = 0.5f;
			}
			else if (member == NpcPartyMember.Charlie)
			{
				SetCharlieAnimation("run");
				_charlieIsMoving = true;
				_charlieRunRetryTimer = 0.5f;
			}
		}

		private void SetNpcIdleAnimation(NpcPartyMember member)
		{
			if (member == NpcPartyMember.K)
			{
				SetKAnimation("idle");
				_kIsMoving = false;
				_kRunRetryTimer = 0f;
			}
			else if (member == NpcPartyMember.Charlie)
			{
				SetCharlieAnimation("idle");
				_charlieIsMoving = false;
				_charlieRunRetryTimer = 0f;
			}
		}

		private void EnsureNpcSpawned(NpcPartyMember member, PlayerBehavior player)
		{
			if (IsNpcOffline(member)) return;
			if (member == NpcPartyMember.K && MockPartyState.KJoined && _kObject == null)
			{
				SpawnNPCK(player);
			}
			else if (member == NpcPartyMember.Charlie && MockPartyState.CharlieJoined && _charlieObject == null)
			{
				SpawnNPCCharlie(player);
			}
		}

		private bool IsNpcJoined(NpcPartyMember member)
		{
			switch (member)
			{
			case NpcPartyMember.K:
				return MockPartyState.KJoined;
			case NpcPartyMember.Charlie:
				return MockPartyState.CharlieJoined;
			default:
				return false;
			}
		}

		private string PickLine(string[] lines)
		{
			if (lines == null || lines.Length == 0) return string.Empty;
			return lines[UnityEngine.Random.Range(0, lines.Length)];
		}

		private void ShowRescueBubble(NpcPartyMember member, bool beforeCpr)
		{
			if (member == NpcPartyMember.K)
			{
				ShowNpcRescueBubble(NpcPartyMember.K,
					PickLine((!beforeCpr) ? K_RESCUE_FINISH_LINES : K_RESCUE_START_LINES),
					GetSelectedKPortraitPreset());
			}
			else if (member == NpcPartyMember.Charlie)
			{
				ShowNpcRescueBubble(NpcPartyMember.Charlie,
					PickLine((!beforeCpr) ? CHARLIE_RESCUE_FINISH_LINES : CHARLIE_RESCUE_START_LINES),
					CHARLIE_PORTRAIT_PRESET);
			}
		}

		private void ShowKRescueBubble(bool beforeCpr)
		{
			ShowRescueBubble(NpcPartyMember.K, beforeCpr);
		}

		private void ShowNpcRescueBubble(
			NpcPartyMember member,
			string text,
			string portraitPreset)
		{
			ShowNpcRescueBubble(member, text, portraitPreset, NPC_RESCUE_BUBBLE_DURATION);
		}

		private void ShowNpcRescueBubble(
			NpcPartyMember member,
			string text,
			string portraitPreset,
			float duration)
		{
			if (string.IsNullOrEmpty(text)) return;

			try
			{
				ChatBubbleGroup group = UIManager.FindScript<ChatBubbleGroup>();
				AnimalBehavior animal = GetNpcAnimal(member);
				if (group == null || animal == null)
				{
					UIManager.SystemMsg(text, 3f);
					return;
				}

				PortraitBuilder.Argument portrait = new PortraitBuilder.Argument
				{
					Preset = portraitPreset
				};
				group.Show(
					animal.ChatableBase,
					text,
					new PortraitBuilder.Argument?(portrait),
					string.Empty,
					Color.white,
					new ChatBubble.TargetPivot?(ChatBubble.TargetPivot.Up),
					new Vector3?(Vector3.up * 80f),
					true,
					new float?(duration),
					false);
			}
			catch (Exception ex)
			{
				Logger.LogWarning("Show NPC rescue bubble failed: " + ex.Message);
			}
		}

		private IEnumerator CoNpcPartyRescue()
		{
			if (_npcRescueRunning)
			{
				UIManager.SystemMsg("NPC party rescue is already in progress.", 3f);
				yield break;
			}

			_npcRescueRunning = true;
			_npcRescueMember = NpcPartyMember.None;

			IEnumerator routine = CoNpcPartyRescueImpl();
			while (true)
			{
				object current = null;
				bool moveNext = false;
				try
				{
					moveNext = routine.MoveNext();
					if (moveNext) current = routine.Current;
				}
				catch (Exception ex)
				{
					Logger.LogError("NPC party rescue error: " + ex);
					UIManager.SystemMsg("NPC party rescue failed.", 3f);
					break;
				}

				if (!moveNext) break;
				yield return current;
			}

			CleanupNpcPartyRescue();
		}

		private IEnumerator CoNpcPartyRescueImpl()
		{
			PlayerBehavior player = PlayerBehavior.LocalPlayer;
			if (player == null)
			{
				UIManager.SystemMsg("No local player found for NPC rescue.", 3f);
				yield break;
			}
			if (player.IsAlive)
			{
				yield break;
			}

			NpcPartyMember member = SelectNpcRescuer(player);
			if (member == NpcPartyMember.None)
			{
				UIManager.SystemMsg("No NPC party member is available to rescue you.", 3f);
				yield break;
			}

			_npcRescueMember = member;
			EnsureNpcSpawned(member, player);

			float readyUntil = Time.time + 5f;
			while (!IsNpcReady(member) && Time.time < readyUntil)
			{
				yield return null;
			}

			if (!IsNpcReady(member))
			{
				UIManager.SystemMsg(GetNpcDisplayName(member) + " is not ready to rescue you.", 3f);
				yield break;
			}

			string npcName = GetNpcDisplayName(member);
			if (member == NpcPartyMember.K || member == NpcPartyMember.Charlie)
			{
				float waitUntil = Time.time + NPC_RESCUE_BEFORE_DIALOG_DELAY;
				while (Time.time < waitUntil)
				{
					if (player == null || player.IsAlive || !IsNpcJoined(member))
					{
						yield break;
					}
					yield return null;
				}

				ShowRescueBubble(member, true);
				waitUntil = Time.time + NPC_RESCUE_AFTER_DIALOG_DELAY;
				while (Time.time < waitUntil)
				{
					if (player == null || player.IsAlive || !IsNpcJoined(member))
					{
						yield break;
					}
					yield return null;
				}
			}

			UIManager.SystemMsg(npcName + " is coming to rescue you.", 3f);

			yield return MoveNpcToRescuePosition(member, player);

			if (player == null || player.IsAlive)
			{
				yield break;
			}

			if (member == NpcPartyMember.K || member == NpcPartyMember.Charlie)
			{
				SetNpcIdleAnimation(member);
				float waitUntil = Time.time + NPC_RESCUE_BEFORE_CPR_DELAY;
				while (Time.time < waitUntil)
				{
					if (player == null || player.IsAlive || !IsNpcJoined(member))
					{
						yield break;
					}
					Vector3 facePlayer = player.transform.position - GetNpcPosition(member);
					TurnNpcToward(member, facePlayer, false);
					yield return null;
				}
			}

			Vector3 toPlayer = player.transform.position - GetNpcPosition(member);
			TurnNpcToward(member, toPlayer, true);
			PlayNpcCprAnimation(member);

			player.IsReceivingCPR = true;
			player.PlayMotionForcely("Barehand_CPR_Dead", 1f, false);

			float endAt = Time.time + NPC_RESCUE_CPR_DURATION;
			while (Time.time < endAt)
			{
				if (player == null || player.IsAlive)
				{
					yield break;
				}
				yield return null;
			}

			if (player != null && !player.IsAlive)
			{
				ReviveLocalPlayer(player, npcName);
				SetNpcIdleAnimation(member);

				if (member == NpcPartyMember.K || member == NpcPartyMember.Charlie)
				{
					float waitUntil = Time.time + NPC_RESCUE_POST_DIALOG_DELAY;
					while (Time.time < waitUntil)
					{
						if (!IsNpcJoined(member))
						{
							yield break;
						}
						yield return null;
					}
					ShowRescueBubble(member, false);
					waitUntil = Time.time + NPC_RESCUE_BUBBLE_DURATION + NPC_RESCUE_OFFLINE_AFTER_FINAL_BUBBLE_DELAY;
					while (Time.time < waitUntil)
					{
						if (!IsNpcJoined(member))
						{
							yield break;
						}
						yield return null;
					}
					SetNpcOfflineAfterRescue(member);
				}
			}
		}

		private IEnumerator MoveNpcToRescuePosition(NpcPartyMember member, PlayerBehavior player)
		{
			float timeoutAt = Time.time + NPC_RESCUE_TIMEOUT;
			while (player != null && !player.IsAlive && Time.time < timeoutAt)
			{
				Vector3 npcPos = GetNpcPosition(member);
				Vector3 dest = player.GetSidePos(true, 2f);
				dest.y = player.transform.position.y;

				Vector3 diff = dest - npcPos;
				diff.y = 0f;
				float distance = diff.magnitude;
				if (distance <= NPC_RESCUE_STOP_DISTANCE)
				{
					break;
				}

				if (distance > TELEPORT_DIST)
				{
					SetNpcPosition(member, dest);
					break;
				}

				Vector3 dir = diff.normalized;
				float dt = Mathf.Max(Time.deltaTime, 0.016f);
				Vector3 moveDelta = dir * RUN_SPEED * dt;
				Vector3 nextPos;

				try
				{
					CollisionParam param = Collisions.CreateCollisionParam(npcPos, moveDelta);
					Vector3 slidDelta = Collisions.ProcessSimpleSliding(param);
					nextPos = npcPos + slidDelta;
				}
				catch
				{
					nextPos = npcPos + moveDelta;
				}

				nextPos.y = player.transform.position.y;
				Vector3 actualDirection = nextPos - npcPos;
				TurnNpcToward(member, actualDirection, false);
				SetNpcPosition(member, nextPos);
				SetNpcRunAnimation(member);
				yield return null;
			}
		}

		private void PlayNpcCprAnimation(NpcPartyMember member)
		{
			if (TryPlayNpcAnimation(member, "F_Barehand_CPR", true)) return;
			if (TryPlayNpcAnimation(member, "Barehand_CPR", true)) return;
			if (TryPlayNpcAnimation(member, "F_Barehand_CPR_Idle", true)) return;
			if (TryPlayNpcAnimation(member, "Barehand_CPR_Idle", true)) return;
			if (TryPlayNpcAnimation(member, "M_Barehand_CPR", true)) return;
			if (TryPlayNpcAnimation(member, "M_Barehand_CPR_Idle", true)) return;
			if (member == NpcPartyMember.K && TryPlayNpcAnimation(member, "K_Bike_CPR", true)) return;
			SetNpcIdleAnimation(member);
		}

		private bool TryPlayNpcAnimation(NpcPartyMember member, string clipName, bool loop)
		{
			Animation animation = GetNpcAnimation(member);
			if (animation != null)
			{
				if (animation.GetClip(clipName) == null)
				{
					TryAttachLoadedClip(animation, clipName);
				}

				if (animation.GetClip(clipName) != null)
				{
					animation.CrossFade(clipName, 0.15f);
					AnimationState state = animation[clipName];
					if (state != null)
					{
						state.wrapMode = (!loop) ? WrapMode.Default : WrapMode.Loop;
						state.speed = 1f;
					}
					return true;
				}
			}

			AnimalBehavior animal = GetNpcAnimal(member);
			if (animal != null)
			{
				float length = animal.CrossFade(clipName, 0.15f, loop, 0f, 1f);
				return length > 0f;
			}

			return false;
		}

		private void ReviveLocalPlayer(PlayerBehavior player, string rescuerName)
		{
			float maxLife = GetSafeMaxLife(player);
			float revivedLife = Mathf.Clamp(maxLife * NPC_RESCUE_REVIVE_LIFE_RATIO, 1f, maxLife);
			Gauge life = new Gauge(maxLife, 0f, new GaugeNode[]
			{
				new GaugeNode(Gauge.CurrentTime, revivedLife)
			});

			player.SetSurvivalGauge(life, null);
			player.IsReceivingCPR = false;
			player.SetAlive(true, false);

			UIManager.SystemMsg(rescuerName + " rescued you.", 3f);
			Logger.LogInfo(rescuerName + " rescued local player with " + revivedLife + "/" + maxLife + " life.");
		}

		private float GetSafeMaxLife(PlayerBehavior player)
		{
			try
			{
				if (player != null && player.Life != null)
				{
					float max = player.Life.RealMax();
					if (max > 0f) return max;

					max = player.Life.Max();
					if (max > 0f) return max;
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning("Unable to read player max life: " + ex.Message);
			}

			return 100f;
		}

		private void CleanupNpcPartyRescue()
		{
			PlayerBehavior player = PlayerBehavior.LocalPlayer;
			if (player != null)
			{
				player.IsReceivingCPR = false;
			}

			NpcPartyMember member = _npcRescueMember;
			_npcRescueMember = NpcPartyMember.None;
			_npcRescueRunning = false;

			if (member != NpcPartyMember.None && GetNpcObject(member) != null)
			{
				SetNpcIdleAnimation(member);
			}
		}

		// ================================================================
		// Party System Helpers
		// ================================================================

		public static void TriggerOnParty(PartySystem partySystem)
		{
			if (partySystem == null) return;

			Messages.Party msg = new Messages.Party();
			msg.Id = MockPartyState.IsInParty ? "mock_party_id" : null;

			if (MockPartyState.IsInParty)
			{
				Messages.PartyInfo info = new Messages.PartyInfo();

				PlayerBehavior localPlayer = PlayerBehavior.LocalPlayer;
				info.LeaderRadioId = new Messages.RadioId
				{
					Name = localPlayer != null ? localPlayer.GetName() : "Player",
					Freq = 1234
				};
				info.LeaderStatus = new Messages.PartierStatus
				{
					EntityId = localPlayer != null ? localPlayer.EntityId : "player_entity",
					RegionId = (GameManager.Region != null) ? GameManager.Region.Id : "region_0",
					Tile = localPlayer != null ? localPlayer.CurrentTile : new Point2(0, 0),
					Health = new Vector2(
						localPlayer != null && localPlayer.Life != null ? localPlayer.Life.Get() : 100f,
						localPlayer != null && localPlayer.Life != null ? localPlayer.Life.RealMax() : 100f),
					Energy = new Vector2(
						localPlayer != null && localPlayer.Stamina != null ? localPlayer.Stamina.Get() : 100f,
						localPlayer != null && localPlayer.Stamina != null ? localPlayer.Stamina.RealMax() : 100f),
					Level = localPlayer != null ? localPlayer.Level : 60,
					IsOnline = true,
					ExpiresAt = 0.0
				};

				List<Pair<Messages.PartierStatus, bool>> members = new List<Pair<Messages.PartierStatus, bool>>();

				if (MockPartyState.KInvited || MockPartyState.KJoined)
				{
					bool kOnline = !IsNpcOffline(NpcPartyMember.K);
					Messages.PartierStatus kStatus = new Messages.PartierStatus
					{
						EntityId = "npc_k",
						RegionId = (GameManager.Region != null) ? GameManager.Region.Id : "region_0",
						Tile = localPlayer != null ? localPlayer.CurrentTile : new Point2(0, 0),
						Health = new Vector2(100f, 100f),
						Energy = new Vector2(100f, 100f),
						Level = 60,
						IsOnline = kOnline,
						// A mock member has no PlayerBehavior, so zero would make
						// Member.IsOffline become true immediately.
						ExpiresAt = kOnline ? double.MaxValue : 0.0
					};
					members.Add(new Pair<Messages.PartierStatus, bool>(kStatus, MockPartyState.KJoined));
				}

				if (MockPartyState.CharlieInvited || MockPartyState.CharlieJoined)
				{
					bool charlieOnline = !IsNpcOffline(NpcPartyMember.Charlie);
					Messages.PartierStatus charlieStatus = new Messages.PartierStatus
					{
						EntityId = "npc_charlie",
						RegionId = (GameManager.Region != null) ? GameManager.Region.Id : "region_0",
						Tile = localPlayer != null ? localPlayer.CurrentTile : new Point2(0, 0),
						Health = new Vector2(100f, 100f),
						Energy = new Vector2(100f, 100f),
						Level = 60,
						IsOnline = charlieOnline,
						ExpiresAt = charlieOnline ? double.MaxValue : 0.0
					};
					members.Add(new Pair<Messages.PartierStatus, bool>(charlieStatus, MockPartyState.CharlieJoined));
				}

				info.MemberStatus = members.ToArray();
				msg.Info = info;
			}
			else
			{
				msg.Info = null;
			}

			var method = typeof(PartySystem).GetMethod("OnParty", BindingFlags.NonPublic | BindingFlags.Instance);
			if (method != null)
			{
				method.Invoke(partySystem, new object[] { msg, null });
			}
		}

		private static IEnumerator DelayCallback(Action action)
		{
			yield return null;
			try { action(); }
			catch (Exception ex) { Instance.Logger.LogError("DelayCallback error: " + ex); }
		}

		private static IEnumerator DeliverLocalPlayerInfo(
			string key,
			Durango.Player.PlayerInfo cachedInfo,
			Action<string, Durango.Player.PlayerInfo> onResult)
		{
			PlayerBehavior localPlayer = null;
			// Party can request PlayerInfo while the new character is still being
			// assembled after login. Wait for the actual costume/display payload so
			// the temporary/default portrait is never cached by the party widget.
			for (int frame = 0; frame < 60; frame++)
			{
				yield return null;
				localPlayer = PlayerBehavior.LocalPlayer;
				if (localPlayer != null
					&& localPlayer.EntityId == key
					&& !string.IsNullOrEmpty(localPlayer.Display.Body))
				{
					break;
				}
			}

			try
			{
				localPlayer = PlayerBehavior.LocalPlayer;
				if (localPlayer == null || localPlayer.EntityId != key) yield break;

				Durango.Player.PlayerInfo localInfo = cachedInfo;
				if (localInfo == null || localInfo == PlayerInfoManager.EmptyPlayer)
				{
					localInfo = new Durango.Player.PlayerInfo();
				}

				localInfo.Valid = true;
				localInfo.EntityId = localPlayer.EntityId;
				localInfo.Name = localPlayer.GetName();
				localInfo.Level = localPlayer.Level;
				localInfo.Freq = localPlayer.Freq;
				localInfo.ClanId = localPlayer.Clan.ClanId ?? string.Empty;
				localInfo.ClanName = localPlayer.Clan.ClanName ?? string.Empty;
				localInfo.PersonalRegionId = string.Empty;
				localInfo.Display = localPlayer.Display;
				localInfo.Display.EntityId = localPlayer.EntityId;
				if (onResult != null) onResult(key, localInfo);
			}
			catch (Exception ex)
			{
				Instance.Logger.LogError("Deliver local player info error: " + ex);
			}
		}

		private static void ApplyKPortrait(UITexture texture)
		{
			ApplyPresetPortrait(texture, GetSelectedKPortraitPreset(), "K");
		}

		private static Material FindPortraitMaterial(string key)
		{
			try
			{
				PortraitMap map = ResourceSingleton<PortraitMap>.Instance();
				FieldInfo materialsField = typeof(PortraitMap).GetField("_materials", BindingFlags.NonPublic | BindingFlags.Instance);
				Array materials = (materialsField != null) ? materialsField.GetValue(map) as Array : null;
				if (materials == null) return null;

				foreach (object entry in materials)
				{
					if (entry == null) continue;
					Type entryType = entry.GetType();
					FieldInfo keyField = entryType.GetField("Key", BindingFlags.Public | BindingFlags.Instance);
					FieldInfo materialField = entryType.GetField("Material", BindingFlags.Public | BindingFlags.Instance);
					string entryKey = (keyField != null) ? keyField.GetValue(entry) as string : null;
					if (string.Equals(entryKey, key, StringComparison.Ordinal))
					{
						return (materialField != null) ? materialField.GetValue(entry) as Material : null;
					}
				}
			}
			catch (Exception ex)
			{
				Instance.Logger.LogWarning("Find portrait material failed: " + ex.Message);
			}
			return null;
		}

		private static void ApplyCharliePortrait(UITexture texture)
		{
			ApplyPresetPortrait(texture, CHARLIE_PORTRAIT_PRESET, "Charlie");
		}

		private static void ApplyPresetPortrait(UITexture texture, string preset, string label)
		{
			if (texture == null) return;

			ResetCustomPortrait(texture);

			PortraitBuilder.Argument argument = new PortraitBuilder.Argument
			{
				Preset = preset
			};
			PortraitBuilder.Set(argument, texture);
			texture.MarkAsChanged();

			if (texture.mainTexture == null && texture.material == null)
			{
				Instance.Logger.LogWarning(label + " portrait preset was not found: " + preset);
			}
		}

		private static void ResetPlayerInfoPortraitCache(Durango.Player.PlayerInfo playerInfo)
		{
			if (playerInfo == null) return;
			try
			{
				FieldInfo portraitArgumentField = typeof(Durango.Player.PlayerInfo).GetField("_portraitArgument", BindingFlags.NonPublic | BindingFlags.Instance);
				if (portraitArgumentField != null)
				{
					portraitArgumentField.SetValue(playerInfo, null);
				}
			}
			catch (Exception ex)
			{
				Instance.Logger.LogWarning("Reset player portrait cache failed: " + ex.Message);
			}
		}

		private static bool IsCustomPortraitMaterial(Material material)
		{
			return material != null
				&& (material.name.StartsWith("KPortrait_Runtime", StringComparison.Ordinal)
					|| material.name.StartsWith("CharliePortrait_Runtime", StringComparison.Ordinal));
		}

		private static void ResetCustomPortrait(UITexture texture)
		{
			if (texture == null) return;
			Material currentMaterial = texture.material;
			if (!IsCustomPortraitMaterial(currentMaterial)) return;

			texture.material = null;
			texture.mainTexture = null;
			texture.uvRect = new Rect(0f, 0f, 1f, 1f);
			texture.drawRegion = new Vector4(0f, 0f, 1f, 1f);
			texture.MarkAsChanged();
			UnityEngine.Object.Destroy(currentMaterial);
		}

		private static IEnumerator DeliverSocialResponse(SocialSystem socialSystem, Social social, Action<Social> onSocial)
		{
			// The real GetSocial response is asynchronous. Waiting two frames gives
			// FriendFollowList time to become active and subscribe to SocialUpdated.
			yield return null;
			yield return null;

			try
			{
				var setSocial = typeof(SocialSystem).GetMethod("SetSocial", BindingFlags.NonPublic | BindingFlags.Instance);
				if (setSocial == null)
				{
					throw new MissingMethodException(typeof(SocialSystem).FullName, "SetSocial");
				}

				setSocial.Invoke(socialSystem, new object[] { social, null });
				if (onSocial != null)
				{
					onSocial(social);
				}

				Instance.Logger.LogInfo("Mock social delivered. Following=" + social.FollowingEntityIds.Length
					+ ", friends=" + social.FriendEntities.Count);
			}
			catch (Exception ex)
			{
				Instance.Logger.LogError("DeliverSocialResponse error: " + ex);
			}
		}

		// ================================================================
		// HARMONY PATCHES — Party System
		// ================================================================

		// UITexture instances are pooled and reused. Remove K's temporary material
		// and crop before PortraitBuilder draws a normal player portrait.
		[HarmonyPatch(typeof(PortraitBuilder), "Set")]
		private static class PortraitBuilderSetPatch
		{
			private static void Prefix(UITexture tex)
			{
				ResetCustomPortrait(tex);
			}
		}

		// Add a local NPC-party rescue option to the normal dead radial menu.
		[HarmonyPatch(typeof(InteractionGroup), "ShowPlayerDeadInteractionMenu")]
		private static class InteractionGroupShowPlayerDeadInteractionMenuPatch
		{
			private static void Postfix()
			{
				try
				{
					if (Instance == null || !Instance.CanOfferNpcPartyRescue()) return;
					if (GameManager.Region == null || !GameManager.Region.CanRevive()) return;

					InteractionMenuList menuList = GameSystem<InteractionSystem>.Instance().MenuList;
					if (menuList.IndexOf(Interaction.Resurrect, NPC_RESCUE_MENU_ID) != -1)
					{
						return;
					}

					menuList.Add(new InteractionMenuData(Interaction.Resurrect)
					{
						Id = NPC_RESCUE_MENU_ID,
						Name = "Request Rescue With NPC Party",
						Icon = "act_Resurrect"
					});
					menuList.Name = string.Empty;
					GameSystem<InteractionSystem>.Instance().ShowClientMenuList(null);
					Instance.Logger.LogInfo("Added NPC party rescue menu.");
				}
				catch (Exception ex)
				{
					if (Instance != null) Instance.Logger.LogError("Add NPC party rescue menu error: " + ex);
					else UnityEngine.Debug.LogException(ex);
				}
			}
		}

		// Intercept only our tagged CPR menu. The native Resurrect action expects
		// a real target PlayerBehavior; our simulated party members revive locally.
		[HarmonyPatch(typeof(InteractionGroup), "OnClickInteractionMenu")]
		private static class InteractionGroupOnClickInteractionMenuPatch
		{
			private static bool Prefix(InteractionMenuData menu, bool selectAll)
			{
				if (menu.Action != Interaction.Resurrect || menu.Id != NPC_RESCUE_MENU_ID)
				{
					return true;
				}

				try
				{
					GameSystem<InteractionSystem>.Instance().SetInteractionTarget(null);
					if (Instance != null)
					{
						Instance.StartCoroutine(Instance.CoNpcPartyRescue());
					}
				}
				catch (Exception ex)
				{
					if (Instance != null) Instance.Logger.LogError("NPC party rescue click error: " + ex);
					else UnityEngine.Debug.LogException(ex);
				}

				return false;
			}
		}

		[HarmonyPatch(typeof(PartySystem), "MakeParty")]
		private static class PartySystemMakePartyPatch
		{
			private static bool Prefix(PartySystem __instance)
			{
				try
				{
					MockPartyState.IsInParty = true;
					MockPartyState.LeaderEntityId = PlayerBehavior.LocalPlayer.EntityId;
					TriggerOnParty(__instance);
					Instance.Logger.LogInfo("Party created.");
				}
				catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
				return false;
			}
		}

		[HarmonyPatch(typeof(PartySystem), "LeaveParty")]
		private static class PartySystemLeavePartyPatch
		{
			private static bool Prefix(PartySystem __instance)
			{
				try
				{
					MockPartyState.IsInParty = false;
					MockPartyState.LeaderEntityId = "";
					MockPartyState.KInvited = false;
					MockPartyState.KJoined = false;
					MockPartyState.KInviteTimer = 0f;
					MockPartyState.KOfflineUntil = 0f;
					ResetKPrefabSelection();
					MockPartyState.CharlieInvited = false;
					MockPartyState.CharlieJoined = false;
					MockPartyState.CharlieInviteTimer = 0f;
					MockPartyState.CharlieOfflineUntil = 0f;
					TriggerOnParty(__instance);
					Instance.Logger.LogInfo("Left party.");
				}
				catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
				return false;
			}
		}

		[HarmonyPatch(typeof(PartySystem), "InviteIntoParty")]
		private static class PartySystemInviteIntoPartyPatch
		{
			private static bool Prefix(PartySystem __instance, string entityId)
			{
				try
				{
					if (!MockPartyState.IsInParty)
					{
						MockPartyState.IsInParty = true;
						MockPartyState.LeaderEntityId = PlayerBehavior.LocalPlayer.EntityId;
					}

					if (entityId == "npc_k")
					{
						MockPartyState.KInvited = true;
						MockPartyState.KInviteTimer = 0f;
						TriggerOnParty(__instance);
						UIManager.SystemMsg("Party invitation sent to K.", 3f);
						Instance.Logger.LogInfo("Invited K to party.");
					}
					else if (entityId == "npc_charlie")
					{
						MockPartyState.CharlieInvited = true;
						MockPartyState.CharlieInviteTimer = 0f;
						TriggerOnParty(__instance);
						UIManager.SystemMsg("Party invitation sent to Charlie.", 3f);
						Instance.Logger.LogInfo("Invited Charlie to party.");
					}
				}
				catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
				return false;
			}
		}

		[HarmonyPatch(typeof(PartySystem), "KickMember")]
		private static class PartySystemKickMemberPatch
		{
			private static bool Prefix(PartySystem __instance, string entityId)
			{
				try
				{
					if (entityId == "npc_k")
					{
						MockPartyState.KJoined = false;
						MockPartyState.KInvited = false;
						MockPartyState.KInviteTimer = 0f;
						MockPartyState.KOfflineUntil = 0f;
						ResetKPrefabSelection();
						TriggerOnParty(__instance);
						UIManager.SystemMsg("K has been kicked from the party.", 3f);
						Instance.Logger.LogInfo("Kicked K from party.");
					}
					else if (entityId == "npc_charlie")
					{
						MockPartyState.CharlieJoined = false;
						MockPartyState.CharlieInvited = false;
						MockPartyState.CharlieInviteTimer = 0f;
						MockPartyState.CharlieOfflineUntil = 0f;
						TriggerOnParty(__instance);
						UIManager.SystemMsg("Charlie has been kicked from the party.", 3f);
						Instance.Logger.LogInfo("Kicked Charlie from party.");
					}
				}
				catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
				return false;
			}
		}

		[HarmonyPatch(typeof(PartySystem), "GetParty")]
		private static class PartySystemGetPartyPatch
		{
			private static bool Prefix(PartySystem __instance)
			{
				try { TriggerOnParty(__instance); }
				catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
				return false;
			}
		}

		// Allow invite for simulated friends via PlayerInfoPopup
		[HarmonyPatch(typeof(PartySystem), "CanInvite")]
		private static class PartySystemCanInvitePatch
		{
			private static bool Prefix(string entityId, ref bool __result)
			{
				if (entityId == "npc_k")
				{
					__result = !MockPartyState.KJoined && !MockPartyState.KInvited;
					return false;
				}
				if (entityId == "npc_charlie")
				{
					__result = !MockPartyState.CharlieJoined && !MockPartyState.CharlieInvited;
					return false;
				}
				return true;
			}
		}

		// ================================================================
		// HARMONY PATCHES — Social System (inject NPC K into friend list)
		// ================================================================

		[HarmonyPatch(typeof(SocialSystem), "GetSocial")]
		private static class SocialSystemGetSocialPatch
		{
			private static bool Prefix(SocialSystem __instance, Action<Social> onSocial)
			{
				try
				{
					Social social = __instance.Social;
					if (social.FollowingEntityIds == null) social.FollowingEntityIds = new string[0];
					if (social.FriendEntities == null) social.FriendEntities = new Dictionary<string, Shared.Player.FriendType>();
					if (social.ReceivedFriendRequests == null) social.ReceivedFriendRequests = new string[0];
					if (social.SentFriendRequests == null) social.SentFriendRequests = new string[0];
					if (social.BlockedEntityIds == null) social.BlockedEntityIds = new string[0];
					if (social.FavoriteRegionOwners == null) social.FavoriteRegionOwners = new string[0];

					// Inject the two simulated NPC friends and favorites.
					var following = new List<string>(social.FollowingEntityIds);
					if (!following.Contains("npc_k")) following.Add("npc_k");
					if (!following.Contains("npc_charlie")) following.Add("npc_charlie");
					social.FollowingEntityIds = following.ToArray();

					if (!social.FriendEntities.ContainsKey("npc_k"))
					{
						social.FriendEntities.Add("npc_k", Shared.Player.FriendType.JustFriend);
					}
					if (!social.FriendEntities.ContainsKey("npc_charlie"))
					{
						social.FriendEntities.Add("npc_charlie", Shared.Player.FriendType.JustFriend);
					}

					Instance.StartCoroutine(DeliverSocialResponse(__instance, social, onSocial));
				}
				catch (Exception ex)
				{
					Instance.Logger.LogError("SocialSystem.GetSocial error: " + ex);
				}
				return false;
			}
		}

		// ================================================================
		// HARMONY PATCHES — PlayerInfoManager (provide K's info)
		// ================================================================

		[HarmonyPatch(typeof(PlayerInfoManager), "RequestFunc")]
		private static class PlayerInfoManagerRequestFuncPatch
		{
			private static bool Prefix(string key, Durango.Player.PlayerInfo cachedInfo, Action<string, Durango.Player.PlayerInfo> onResult)
			{
				PlayerBehavior localPlayer = PlayerBehavior.LocalPlayer;
				if (localPlayer != null && key == localPlayer.EntityId)
				{
					Instance.StartCoroutine(DeliverLocalPlayerInfo(key, cachedInfo, onResult));
					return false;
				}

				if (key == "npc_k")
				{
					try
					{
						Durango.Player.PlayerInfo playerInfo = cachedInfo;
						if (playerInfo == null || playerInfo == PlayerInfoManager.EmptyPlayer)
						{
							playerInfo = new Durango.Player.PlayerInfo();
						}
						playerInfo.Valid = true;
						playerInfo.EntityId = "npc_k";
						playerInfo.Name = "K";
						playerInfo.Level = 60;
						playerInfo.Freq = 1234;
						playerInfo.ClanId = string.Empty;
						playerInfo.ClanName = string.Empty;
						playerInfo.PersonalRegionId = string.Empty;

						try
						{
							EditPlayerDisplayProxy.FillRandomPlayerDisplayData(false, Shared.Player.Job.Student, ref playerInfo.Display);
							playerInfo.Display.Body = ResourceSingleton<PlayerCostumeTable>.Instance().GetPlayerDefaultBodyModelAssetBundlePath(false, (int)Shared.Player.Job.Student, PlayerCostumeTable.ClothState.Normal);
							playerInfo.Display.DefaultBody = playerInfo.Display.Body;
							EditPlayerDisplayProxy.FillRandomPortrait(false, ref playerInfo.Display);
						}
						catch (Exception displayEx)
						{
							// Keep the row usable even if costume resources are not ready yet.
							if (PlayerBehavior.LocalPlayer != null)
							{
								playerInfo.Display = PlayerBehavior.LocalPlayer.Display;
							}
							Instance.Logger.LogWarning("K display fallback: " + displayEx.Message);
						}
						playerInfo.Display.EntityId = "npc_k";
						playerInfo.Display.PortraitIcon = GetSelectedKPortraitPreset();
						ResetPlayerInfoPortraitCache(playerInfo);

						Instance.StartCoroutine(DelayCallback(delegate()
						{
							if (onResult != null)
							{
								onResult(key, playerInfo);
							}
						}));
					}
					catch (Exception ex)
					{
						Instance.Logger.LogError("RequestFunc error: " + ex);
					}
					return false;
				}

				if (key == "npc_charlie")
				{
					try
					{
						Durango.Player.PlayerInfo playerInfo = cachedInfo;
						if (playerInfo == null || playerInfo == PlayerInfoManager.EmptyPlayer)
						{
							playerInfo = new Durango.Player.PlayerInfo();
						}
						playerInfo.Valid = true;
						playerInfo.EntityId = "npc_charlie";
						playerInfo.Name = "Charlie";
						playerInfo.Level = 60;
						playerInfo.Freq = 5678;
						playerInfo.ClanId = string.Empty;
						playerInfo.ClanName = string.Empty;
						playerInfo.PersonalRegionId = string.Empty;

						try
						{
							EditPlayerDisplayProxy.FillRandomPlayerDisplayData(false, Shared.Player.Job.Student, ref playerInfo.Display);
							playerInfo.Display.Body = ResourceSingleton<PlayerCostumeTable>.Instance().GetPlayerDefaultBodyModelAssetBundlePath(false, (int)Shared.Player.Job.Student, PlayerCostumeTable.ClothState.Normal);
							playerInfo.Display.DefaultBody = playerInfo.Display.Body;
							EditPlayerDisplayProxy.FillRandomPortrait(false, ref playerInfo.Display);
						}
						catch (Exception displayEx)
						{
							if (PlayerBehavior.LocalPlayer != null)
							{
								playerInfo.Display = PlayerBehavior.LocalPlayer.Display;
							}
							Instance.Logger.LogWarning("Charlie display fallback: " + displayEx.Message);
						}
						playerInfo.Display.EntityId = "npc_charlie";
						playerInfo.Display.PortraitIcon = CHARLIE_PORTRAIT_PRESET;
						ResetPlayerInfoPortraitCache(playerInfo);

						Instance.StartCoroutine(DelayCallback(delegate()
						{
							if (onResult != null) onResult(key, playerInfo);
						}));
					}
					catch (Exception ex)
					{
						Instance.Logger.LogError("Charlie RequestFunc error: " + ex);
					}
					return false;
				}
				return true;
			}
		}

		[HarmonyPatch(typeof(PlayerInfoManager), "GetPlayerConnected")]
		private static class PlayerInfoManagerGetPlayerConnectedPatch
		{
			private static bool Prefix(string entityId, Action<PlayerConnected> onResult)
			{
				if (entityId == "npc_k" || entityId == "npc_charlie")
				{
					Instance.StartCoroutine(DelayCallback(delegate()
					{
						if (onResult != null)
						{
							onResult(new PlayerConnected { Online = !IsNpcOfflineByEntityId(entityId) });
						}
					}));
					return false;
				}
				return true;
			}
		}

		// ================================================================
		// HARMONY PATCHES — Friend List UI Fix
		// ================================================================

		[HarmonyPatch(typeof(FriendFollowList), "Refresh", new Type[] { typeof(Social) })]
		private static class FriendFollowListRefreshPatch
		{
			private static void Postfix(FriendFollowList __instance)
			{
				try
				{
					Instance.StartCoroutine(ForceUIRebuild(__instance));
				}
				catch (Exception ex)
				{
					Instance.Logger.LogError("FriendFollowList Postfix error: " + ex);
				}
			}

			private static IEnumerator ForceUIRebuild(FriendFollowList instance)
			{
				// Wait 2 frames for NGUI to process widget cloning
				yield return null;
				yield return null;

				try
				{
					// Force scroll view layout update via reflection
					var scrollField = typeof(FriendFollowList).GetField("_scrollView", BindingFlags.NonPublic | BindingFlags.Instance);
					if (scrollField != null)
					{
						var scrollView = scrollField.GetValue(instance) as Component;
						if (scrollView != null && scrollView.gameObject.activeInHierarchy)
						{
							// Call UpdateLayout(false) on the scroll view
							var updateMethod = scrollView.GetType().GetMethod("UpdateLayout", BindingFlags.Public | BindingFlags.Instance);
							if (updateMethod != null)
							{
								updateMethod.Invoke(scrollView, new object[] { false });
							}

							// Mark all widgets as changed to force draw call rebuild
							foreach (var widget in scrollView.GetComponentsInChildren<UIWidget>(true))
							{
								widget.MarkAsChanged();
							}
						}
					}

					// Force SocialGroup panel alpha to 1 (may be mid-animation)
					SocialGroup socialGroup = instance.GetComponentInParent<SocialGroup>();
					if (socialGroup != null)
					{
						UIPanel sgPanel = socialGroup.GetComponent<UIPanel>();
						if (sgPanel != null && sgPanel.alpha < 0.95f)
						{
							sgPanel.alpha = 1f;
						}
					}
				}
				catch (Exception ex)
				{
					Instance.Logger.LogError("ForceUIRebuild error: " + ex);
				}
			}
		}

		// ================================================================
		// HARMONY PATCHES — PlayerInfoWidget (simplified logging)
		// ================================================================

		[HarmonyPatch(typeof(PlayerInfoWidget), "OnPlayer")]
		private static class PlayerInfoWidgetOnPlayerPatch
		{
			private static void Postfix(PlayerInfoWidget __instance, Durango.Player.PlayerInfo player)
			{
				if (player == null) return;
				FieldInfo portraitField = typeof(PlayerInfoWidget).GetField("_portraitTexture", BindingFlags.NonPublic | BindingFlags.Instance);
				UITexture portrait = (portraitField != null) ? portraitField.GetValue(__instance) as UITexture : null;
				if (player.EntityId == "npc_k")
				{
					ApplyKPortrait(portrait);
					Instance.Logger.LogInfo(string.Format("K widget: name={0}, active={1}",
						__instance.name,
						__instance.gameObject.activeInHierarchy));
				}
				else if (player.EntityId == "npc_charlie")
				{
					ApplyCharliePortrait(portrait);
				}
			}
		}

		[HarmonyPatch(typeof(PartyHudPlayerWidget), "UpdatePlayerInfo")]
		private static class PartyHudPlayerWidgetUpdatePlayerInfoPatch
		{
			private static void Postfix(PartyHudPlayerWidget __instance, Durango.Player.PlayerInfo info)
			{
				if (info == null) return;
				FieldInfo portraitField = typeof(PartyHudPlayerWidget).GetField("_portraitTexture", BindingFlags.NonPublic | BindingFlags.Instance);
				UITexture portrait = (portraitField != null) ? portraitField.GetValue(__instance) as UITexture : null;
				if (info.EntityId == "npc_k") ApplyKPortrait(portrait);
				else if (info.EntityId == "npc_charlie") ApplyCharliePortrait(portrait);
			}
		}

		[HarmonyPatch(typeof(PlayerInfoPopup), "FillUpperPane")]
		private static class PlayerInfoPopupFillUpperPanePatch
		{
			private static void Postfix(PlayerInfoPopup __instance)
			{
				FieldInfo infoField = typeof(PlayerInfoPopup).GetField("_playerInfo", BindingFlags.NonPublic | BindingFlags.Instance);
				Durango.Player.PlayerInfo info = (infoField != null) ? infoField.GetValue(__instance) as Durango.Player.PlayerInfo : null;
				FieldInfo portraitField = typeof(PlayerInfoPopup).GetField("_portraitTexture", BindingFlags.NonPublic | BindingFlags.Instance);
				UITexture portrait = (portraitField != null) ? portraitField.GetValue(__instance) as UITexture : null;
				if (info == null) return;
				if (info.EntityId == "npc_k") ApplyKPortrait(portrait);
				else if (info.EntityId == "npc_charlie") ApplyCharliePortrait(portrait);
			}
		}

		// The friend-list detail popup normally creates a human PlayerBehavior from
		// PlayerDisplay. Simulated NPC friends use their exact story prefabs.
		[HarmonyPatch(typeof(PlayerInfoPopup), "MakePreviewModel")]
		private static class PlayerInfoPopupMakePreviewModelPatch
		{
			private static readonly HashSet<int> Loading = new HashSet<int>();

			private static bool Prefix(PlayerInfoPopup __instance)
			{
				FieldInfo infoField = typeof(PlayerInfoPopup).GetField("_playerInfo", BindingFlags.NonPublic | BindingFlags.Instance);
				Durango.Player.PlayerInfo info = (infoField != null) ? infoField.GetValue(__instance) as Durango.Player.PlayerInfo : null;
				if (info == null)
				{
					return true;
				}

				string entityId = info.EntityId;
				string assetPath;
				string modelName;
				string idleMotion;
				if (entityId == "npc_k")
				{
					assetPath = GetOrChooseKPrefabPath();
					modelName = IsSelectedKIndoorPrefab() ? "K_Indoor_FriendPreview" : "K_FriendPreview";
					idleMotion = "K_Stand";
				}
				else if (entityId == "npc_charlie")
				{
					assetPath = "Models/Ancora/NPC/F_NPC_Player_First.prefab";
					modelName = "Charlie_FriendPreview";
					idleMotion = "F_Barehand_Sit_F";
				}
				else
				{
					return true;
				}

				try
				{
					FieldInfo renderField = typeof(PlayerInfoPopup).GetField("_uiModelRender", BindingFlags.NonPublic | BindingFlags.Instance);
					UIModelRender existingRender = (renderField != null) ? renderField.GetValue(__instance) as UIModelRender : null;
					int instanceId = __instance.GetInstanceID();
					if (existingRender != null || Loading.Contains(instanceId))
					{
						return false;
					}

					Loading.Add(instanceId);
					Singleton<AssetBundleManager>.Instance().RequestAsset(
						assetPath,
						typeof(GameObject),
						delegate(UnityEngine.Object asset)
						{
							Loading.Remove(instanceId);
							if (__instance == null || asset == null) return;

							Durango.Player.PlayerInfo currentInfo = (infoField != null) ? infoField.GetValue(__instance) as Durango.Player.PlayerInfo : null;
							if (currentInfo == null || currentInfo.EntityId != entityId) return;

							GameObject model = (GameObject)UnityEngine.Object.Instantiate(asset);
							model.name = modelName;
							model.SetActive(true);
							foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
							{
								collider.enabled = false;
							}
							ClientInteractionQuest interaction = model.GetComponent<ClientInteractionQuest>();
							if (interaction != null) interaction.enabled = false;
							ClientAnimalActor actor = model.GetComponent<ClientAnimalActor>();
							if (actor != null) actor.enabled = false;

							AnimalBehavior animal = model.GetComponent<AnimalBehavior>();
							if (animal != null)
							{
								animal.EntityId = entityId + "_friend_preview";
								animal.TurnToYaw(180f, true);
								animal.CrossFade(idleMotion, 0.1f, true, 0f, 1f);
							}

							FieldInfo previewField = typeof(PlayerInfoPopup).GetField("_previewTexture", BindingFlags.NonPublic | BindingFlags.Instance);
							UITexture preview = (previewField != null) ? previewField.GetValue(__instance) as UITexture : null;
							UIModelRender render = UIModelRenderBuilder.Make();
							if (renderField != null) renderField.SetValue(__instance, render);

							if (render != null && preview != null)
							{
								render.SetModel(model, 35f, 1f, null, 0f);
								render.FillTexture(preview);
								Instance.Logger.LogInfo(entityId + " friend popup preview loaded from NPC prefab.");
							}
							else
							{
								UnityEngine.Object.Destroy(model);
							}
						}
					);
				}
				catch (Exception ex)
				{
					Loading.Remove(__instance.GetInstanceID());
					Instance.Logger.LogError(entityId + " friend popup preview error: " + ex);
				}
				return false;
			}
		}

		// PartyPlayerInfoWidget normally builds a human preview from PlayerDisplay.
		// Simulated friends render the same exact NPC prefab used in-world.
		[HarmonyPatch(typeof(PartyPlayerInfoWidget), "SetPreviewModel")]
		private static class PartyPlayerInfoWidgetSetPreviewModelPatch
		{
			private static readonly HashSet<int> Loading = new HashSet<int>();
			private static readonly Dictionary<int, GameObject> Models = new Dictionary<int, GameObject>();

			private static bool Prefix(PartyPlayerInfoWidget __instance, Durango.Player.PlayerInfo info)
			{
				if (info == null)
				{
					return true;
				}

				string entityId = info.EntityId;
				string assetPath;
				string modelName;
				string idleMotion;
				if (entityId == "npc_k")
				{
					assetPath = GetOrChooseKPrefabPath();
					modelName = IsSelectedKIndoorPrefab() ? "K_Indoor_PartyPreview" : "K_PartyPreview";
					idleMotion = "K_Stand";
				}
				else if (entityId == "npc_charlie")
				{
					assetPath = "Models/Ancora/NPC/F_NPC_Player_First.prefab";
					modelName = "Charlie_PartyPreview";
					idleMotion = "F_Barehand_Sit_F";
				}
				else
				{
					return true;
				}

				try
				{
					int instanceId = __instance.GetInstanceID();
					GameObject existing;
					if (Models.TryGetValue(instanceId, out existing) && existing != null
						&& existing.name == modelName)
					{
						return false;
					}
					if (existing != null)
					{
						UnityEngine.Object.Destroy(existing);
						Models.Remove(instanceId);
					}
					if (Loading.Contains(instanceId))
					{
						return false;
					}

					Loading.Add(instanceId);
					Singleton<AssetBundleManager>.Instance().RequestAsset(
						assetPath,
						typeof(GameObject),
						delegate(UnityEngine.Object asset)
						{
							Loading.Remove(instanceId);
							if (__instance == null || __instance.EntityId != entityId || asset == null)
							{
								return;
							}

							GameObject model = (GameObject)UnityEngine.Object.Instantiate(asset);
							model.name = modelName;
							model.SetActive(true);
							foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
							{
								collider.enabled = false;
							}
							ClientInteractionQuest interaction = model.GetComponent<ClientInteractionQuest>();
							if (interaction != null) interaction.enabled = false;
							ClientAnimalActor clientActor = model.GetComponent<ClientAnimalActor>();
							if (clientActor != null) clientActor.enabled = false;

							AnimalBehavior animal = model.GetComponent<AnimalBehavior>();
							if (animal != null)
							{
								animal.EntityId = entityId + "_preview";
								animal.TurnToYaw(180f, true);
								animal.CrossFade(idleMotion, 0.1f, true, 0f, 1f);
							}

							FieldInfo renderField = typeof(PartyPlayerInfoWidget).GetField("_uiModelRender", BindingFlags.NonPublic | BindingFlags.Instance);
							FieldInfo previewField = typeof(PartyPlayerInfoWidget).GetField("_preview", BindingFlags.NonPublic | BindingFlags.Instance);
							UIModelRender render = (renderField != null) ? renderField.GetValue(__instance) as UIModelRender : null;
							UITexture preview = (previewField != null) ? previewField.GetValue(__instance) as UITexture : null;
							if (render == null)
							{
								render = UIModelRenderBuilder.Make();
								if (renderField != null) renderField.SetValue(__instance, render);
							}

							if (render != null && preview != null)
							{
								render.SetModel(model, 35f, 1f, null, 0f);
								render.FillTexture(preview);
								Models[instanceId] = model;
								Instance.Logger.LogInfo(entityId + " party preview loaded from NPC prefab.");
							}
							else
							{
								UnityEngine.Object.Destroy(model);
							}
						}
					);
				}
				catch (Exception ex)
				{
					Instance.Logger.LogError(entityId + " party preview error: " + ex);
				}
				return false;
			}
		}

		// ================================================================
		// HARMONY PATCHES — SocialGroup (minimal, no verbose logging)
		// ================================================================

		[HarmonyPatch(typeof(SocialGroup), "OnSocial")]
		private static class SocialGroupOnSocialPatch
		{
			private static void Prefix(SocialGroup __instance)
			{
				// Intentionally minimal — removed diagnostic flood
			}
		}
	}
}
