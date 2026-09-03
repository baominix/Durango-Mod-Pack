using Durango.Render.Particle;
using Durango.Utils;
using HarmonyLib;
using UnityEngine;

namespace Baominix.DurangoOriginal.CombatSystem.Presentation
{
    // FX_Targeting_Common_01 is emitted as a child of the selected entity,
    // but every ParticleSystem in the original prefab has
    // moveWithTransform=0 (world-space simulation). During a large root
    // motion, already-emitted ring particles remain at an older world
    // position while the entity and emitter move on. This presents as the
    // red base sliding in front of Zebraceratops even though its logical base
    // is correct. Change only the combat selection effect to local-space;
    // attack telegraphs and every other particle keep their original mode.
    [HarmonyPatch(typeof(ParticleManager), "EmitFollow")]
    internal static class TargetSelectionParticlePatch
    {
        private const string TargetSelectionEffect =
            "Particle/FX_Targeting_Common_01.prefab";

        [HarmonyPostfix]
        private static void Postfix(
            string assetPath,
            Transform followingParent,
            bool useLocalPosition,
            int __result)
        {
            if (__result == 0 || followingParent == null ||
                !useLocalPosition ||
                assetPath != TargetSelectionEffect ||
                !Singleton<ParticleManager>.HasInstance())
            {
                return;
            }

            Singleton<ParticleManager>.Instance().RegisterAction(
                __result,
                delegate(GameObject effect)
                {
                    AnchorSelectionEffect(effect, followingParent);
                });
        }

        private static void AnchorSelectionEffect(
            GameObject effect,
            Transform logicalRoot)
        {
            if (effect == null || logicalRoot == null)
            {
                return;
            }

            // ParticleManager has already parented the loaded/reused effect
            // when this callback runs. Bind it again explicitly because the
            // pool preserves the previous instance transform until the new
            // emit has finished.
            TargetSelectionLogicalAnchor anchor =
                effect.GetComponent<TargetSelectionLogicalAnchor>();
            if (anchor == null)
            {
                anchor = effect.AddComponent<
                    TargetSelectionLogicalAnchor>();
            }
            anchor.Bind(logicalRoot);

            // Custom simulation space makes every already-emitted segment
            // use the entity's logical root directly. This is stricter than
            // Local: a two-hit clip may alter the presentation hierarchy,
            // but it cannot move the target ring away from CurrentPosition.
            effect.transform.localPosition = Vector3.zero;
            ParticleSystem[] systems =
                effect.GetComponentsInChildren<ParticleSystem>(true);
            int i;
            for (i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i];
                if (system == null)
                {
                    continue;
                }
                ParticleSystem.MainModule main = system.main;
                main.simulationSpace =
                    ParticleSystemSimulationSpace.Custom;
                main.customSimulationSpace = logicalRoot;
                system.Clear(true);
                system.Play(true);
            }
        }
    }

    // The selection effect is pooled and outlives individual attacks. Keep
    // only this visual at the entity root; attack telegraphs continue to use
    // their authoritative per-hit centers.
    internal sealed class TargetSelectionLogicalAnchor : MonoBehaviour
    {
        private Transform _logicalRoot;

        internal void Bind(Transform logicalRoot)
        {
            _logicalRoot = logicalRoot;
            if (_logicalRoot == null)
            {
                enabled = false;
                return;
            }
            transform.SetParent(_logicalRoot, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            enabled = true;
        }

        private void LateUpdate()
        {
            if (_logicalRoot == null)
            {
                enabled = false;
                return;
            }
            if (transform.parent != _logicalRoot)
            {
                transform.SetParent(_logicalRoot, false);
            }
            transform.localPosition = Vector3.zero;
        }

        private void OnDisable()
        {
            _logicalRoot = null;
        }
    }
}
