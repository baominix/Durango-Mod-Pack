using System;
using System.Reflection;
using Durango.Logic.Skill;
using Durango.UI;
using Durango.UI.Control;
using HarmonyLib;
using L10N;

namespace BaoX.DurangoOriginal.SkillSystemMod
{
    [HarmonyPatch(typeof(SkillNodeInfoWidget), "SetBottom")]
    internal static class SkillUntrainDependencyUiPatch
    {
        internal static readonly FieldInfo SkillField = AccessTools.Field(typeof(SkillNodeInfoWidget), "_skill");
        private static readonly FieldInfo LearnButtonField = AccessTools.Field(typeof(SkillNodeInfoWidget), "_learnButton");

        private static void Postfix(SkillNodeInfoWidget __instance)
        {
            Node skill = SkillField == null ? null : SkillField.GetValue(__instance) as Node;
            if (!ShouldDisableUntrain(skill))
            {
                return;
            }

            SelectableButton learnButton = LearnButtonField == null ? null : LearnButtonField.GetValue(__instance) as SelectableButton;
            if (learnButton == null)
            {
                return;
            }

            learnButton.Text = T._("습득함");
            learnButton.Disabled = true;
            learnButton.ShowLoadingRing(false, null);
        }

        internal static bool ShouldDisableUntrain(Node skill)
        {
            if (skill == null || skill.Parent == null)
            {
                return false;
            }

            if (skill.Level > skill.Parent.Level || skill.Level != skill.Parent.Level || skill.UntrainDisabled)
            {
                return false;
            }

            int parentLevelAfterUntrain = skill.Level - 1;
            if (OfflineSkillHandlers.HasLocalDependentBranch(skill.Id, skill.Sub, parentLevelAfterUntrain))
            {
                return true;
            }

            return HasDependentBranch(skill.Parent, parentLevelAfterUntrain);
        }

        private static bool HasDependentBranch(Skill parent, int parentLevelAfterUntrain)
        {
            if (parent == null || parent.Bundle == null || string.IsNullOrEmpty(parent.SubId))
            {
                return false;
            }

            bool parentIsBase = parent.Bundle.Base == parent;
            if (IsDependentSkill(parent, parent.Bundle.Base, parentLevelAfterUntrain, parentIsBase))
            {
                return true;
            }

            if (parent.Bundle.Sub == null)
            {
                return false;
            }

            for (int i = 0; i < parent.Bundle.Sub.Length; i++)
            {
                if (IsDependentSkill(parent, parent.Bundle.Sub[i], parentLevelAfterUntrain, parentIsBase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDependentSkill(Skill parent, Skill child, int parentLevelAfterUntrain, bool parentIsBase)
        {
            if (child == null || child.Level <= 0 || string.Equals(parent.SubId, child.SubId, StringComparison.Ordinal))
            {
                return false;
            }

            if (parentIsBase)
            {
                return parentLevelAfterUntrain <= 0;
            }

            if (!parentIsBase && !IsDependentSubSkill(parent.SubId, child.SubId))
            {
                return false;
            }

            return child.Level > parentLevelAfterUntrain;
        }

        private static bool IsDependentSubSkill(string parentSubId, string childSubId)
        {
            if (string.Equals(parentSubId, "__base__", StringComparison.Ordinal))
            {
                return true;
            }

            return childSubId.StartsWith(parentSubId + "_", StringComparison.Ordinal);
        }
    }

    [HarmonyPatch(typeof(SkillNodeInfoWidget), "OnClickLearnButton")]
    internal static class SkillUntrainDependencyClickPatch
    {
        private static bool Prefix(SkillNodeInfoWidget __instance)
        {
            Node skill = SkillUntrainDependencyUiPatch.SkillField == null ? null : SkillUntrainDependencyUiPatch.SkillField.GetValue(__instance) as Node;
            return !SkillUntrainDependencyUiPatch.ShouldDisableUntrain(skill);
        }
    }
}
