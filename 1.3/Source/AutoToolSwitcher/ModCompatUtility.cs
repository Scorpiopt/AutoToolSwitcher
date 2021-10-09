using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace AutoToolSwitcher
{

    [StaticConstructorOnStartup]
    public static class ModCompatUtility
    {
        public static bool survivalToolsLoaded;
        public static bool combatExtendedLoaded;
        static ModCompatUtility()
        {
            survivalToolsLoaded = ModLister.HasActiveModWithName("Survival Tools");
            combatExtendedLoaded = ModLister.HasActiveModWithName("Combat Extended");
            if (combatExtendedLoaded)
            {
                DoCEPatches();
            }
        }

        private static void DoCEPatches()
        {
            var holdTracker = AccessTools.TypeByName("CombatExtended.Utility_HoldTracker");
            var combatExcessPrefix = new HarmonyMethod(AccessTools.Method(typeof(ModCompatUtility), "CombatExcessPrefix"));
            var combatExcessPostfix = new HarmonyMethod(AccessTools.Method(typeof(ModCompatUtility), "CombatExcessPostfix"));

            HarmonyPatches.harmony.Patch(AccessTools.Method(holdTracker, "GetAnythingForDrop"), combatExcessPrefix, combatExcessPostfix);
            HarmonyPatches.harmony.Patch(AccessTools.Method(holdTracker, "GetExcessThing"), combatExcessPrefix, combatExcessPostfix);
            HarmonyPatches.harmony.Patch(AccessTools.Method(holdTracker, "GetExcessEquipment"), combatExcessPrefix, combatExcessPostfix);

            var method = AccessTools.Method(typeof(CombatExtended.Utility_Loadouts), "IsItemQuestLocked");
            HarmonyPatches.harmony.Patch(method, new HarmonyMethod(typeof(ModCompatUtility), "IsItemQuestLockedPrefix"));
        }

        public static bool HasActiveInCELoadout(this Pawn pawn, Thing thing, out bool hasActiveWeaponsInLoadout)
        {
            hasActiveWeaponsInLoadout = false;
            CombatExtended.Loadout loadout = CombatExtended.Utility_Loadouts.GetLoadout(pawn);
            if (loadout != null)
            {
                foreach (var slot in loadout.Slots)
                {
                    if (slot.thingDef != null)
                    {
                        if (slot.thingDef.IsWeapon)
                        {
                            hasActiveWeaponsInLoadout = true;
                        }
                        if (slot.thingDef == thing?.def)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        private static Pawn lookedPawn;
        public static void CombatExcessPrefix(Pawn pawn)
        {
            lookedPawn = pawn;
        }

        public static void CombatExcessPostfix(Pawn pawn)
        {
            lookedPawn = null;
        }
        public static bool IsItemQuestLockedPrefix(Pawn pawn, Thing thing, ref bool __result)
        {
            if (pawn == lookedPawn && (thing?.def?.IsTool() ?? false))
            {
                var policy = pawn.GetCurrentToolPolicy();
                if (policy != null && policy[thing.def].takeAsTool)
                {
                    __result = true;
                    return false;
                }
            }
            return true;
        }

        public static bool IsSurvivalTool(this ThingDef tool)
        {
            return typeof(SurvivalTools.SurvivalTool).IsAssignableFrom(tool.thingClass);
        }
        public static float GetScoreFromSurvivalTool(Thing tool, SkillJob skillJob, ref bool isUseful)
        {
            float result = 0f;
            if (tool is SurvivalTools.SurvivalTool survivalTool)
            {
                foreach (var statFactor in survivalTool.WorkStatFactors)
                {
                    if (skillJob.skill != null)
                    {
                        if (statFactor.AffectsSkill(skillJob.skill))
                        {
                            if (statFactor.value > 0)
                            {
                                isUseful = true;
                            }
                            result += statFactor.value;
                        }
                    }
                    if (skillJob.job != null) // maybe we should add scores for tools here
                    {
                        if (skillJob.job.bill?.recipe?.workSpeedStat != null)
                        {
                            if (statFactor.stat == skillJob.job.bill.recipe.workSpeedStat)
                            {
                                if (statFactor.value > 0)
                                {
                                    isUseful = true;
                                }
                                result += statFactor.value;
                            }
                        }
                    }
                }
            }
            return result;
        }
    }
}
