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
    public enum ToolAction
    {
        DoNothing,
        EquipFromInventory,
        GoAndEquipTool
    }

    [StaticConstructorOnStartup]
    public static class ToolSearchUtility
    {
        public static HashSet<ThingDef> toolDefs = new HashSet<ThingDef>();
        static ToolSearchUtility()
        {
            foreach (var thingDef in DefDatabase<ThingDef>.AllDefs)
            {
                if (thingDef.equippedStatOffsets?.Any() ?? false && thingDef.comps != null)
                {
                    foreach (var comp in thingDef.comps)
                    {
                        if (typeof(CompEquippable).IsAssignableFrom(comp.compClass))
                        {
                            toolDefs.Add(thingDef);
                            break;
                        }
                    }
                }
            }
        }

        public static ThingWithComps FindToolFor(Pawn pawn, SkillDef skill, out ToolAction toolAction)
        {
            toolAction = ToolAction.DoNothing;
            var equippedThings = pawn.equipment?.AllEquipmentListForReading.Where(x => toolValidator(x));
            var inventoryThings = pawn.inventory?.innerContainer.OfType<ThingWithComps>().Where(x => toolValidator(x));
            var outsideThings = new List<ThingWithComps>();
            foreach (var def in toolDefs)
            {
                foreach (var tool in pawn.Map.listerThings.ThingsOfDef(def).OfType<ThingWithComps>())
                {
                    outsideThings.Add(tool);
                }
            }

            var equippedThingsScored = GetToolsScoredFor(equippedThings, skill);
            var inventoryThingsScored = GetToolsScoredFor(inventoryThings, skill);
            var outsideThingsScored = GetToolsScoredFor(outsideThings, skill);

            while (true)
            {
                if ((!equippedThingsScored?.Any() ?? false) && (!inventoryThingsScored?.Any() ?? false) && (!outsideThingsScored?.Any() ?? false))
                {
                    break;
                }
                else
                {
                    var equippedMaxScore = (equippedThingsScored != null && equippedThingsScored.Any()) ? equippedThingsScored?.MaxBy(x => x.Key).Key : null;
                    var inventoryMaxScore = (inventoryThingsScored != null && inventoryThingsScored.Any()) ? inventoryThingsScored?.MaxBy(x => x.Key).Key : null;
                    var outsideMaxScore = (outsideThingsScored != null && outsideThingsScored.Any()) ? outsideThingsScored?.MaxBy(x => x.Key).Key : null;

                    if (!equippedMaxScore.HasValue && !inventoryMaxScore.HasValue && !outsideMaxScore.HasValue
                        || equippedMaxScore.HasValue && (!inventoryMaxScore.HasValue || equippedMaxScore.Value >= inventoryMaxScore.Value)
                        && (!outsideMaxScore.HasValue || equippedMaxScore.Value >= outsideMaxScore))
                    {
                        break;
                    }
                    else if (inventoryMaxScore.HasValue && (!equippedMaxScore.HasValue || inventoryMaxScore.Value > equippedMaxScore.Value)
                        && (!outsideMaxScore.HasValue || inventoryMaxScore.Value >= outsideMaxScore.Value))
                    {
                        var tool = inventoryThingsScored[inventoryMaxScore.Value].RandomElement();
                        toolAction = ToolAction.EquipFromInventory;
                        Log.Message("Found " + tool + " for " + skill + " - score: " + inventoryMaxScore.Value);
                        return tool;
                    }
                    else if (outsideMaxScore.HasValue && (!equippedMaxScore.HasValue || outsideMaxScore.Value > equippedMaxScore)
                        && (!inventoryMaxScore.HasValue || outsideMaxScore.Value > inventoryMaxScore.Value))
                    {
                        var tools = outsideThingsScored[outsideMaxScore.Value];
                        var tool = GenClosest.ClosestThing_Global_Reachable(pawn.Position, pawn.Map, tools, PathEndMode.OnCell, TraverseParms.For(pawn),
                            9999, (Thing x) => !x.IsForbidden(pawn)) as ThingWithComps;
                        if (tool != null)
                        {
                            toolAction = ToolAction.GoAndEquipTool;
                            Log.Message("Found " + tool + " for " + skill + " - score: " + outsideMaxScore.Value);
                            return tool;
                        }
                        else
                        {
                            outsideThingsScored.Remove(outsideMaxScore.Value);
                        }
                    }
                }
            }
            return null;
        }

        private static Predicate<Thing> toolValidator = delegate (Thing x)
        {
            if (toolDefs.Contains(x.def))
            {
                return true;
            }
            return false;
        };

        private static Dictionary<float, List<ThingWithComps>> GetToolsScoredFor(IEnumerable<ThingWithComps> things, SkillDef skillDef)
        {
            if (things.Any())
            {
                return GetScoredThings(things, skillDef);
            }
            return null;
        }
        private static Dictionary<float, List<ThingWithComps>> GetScoredThings(IEnumerable<ThingWithComps> things, SkillDef skillDef)
        {
            Dictionary<float, List<ThingWithComps>> toolsByScores = new Dictionary<float, List<ThingWithComps>>();
            foreach (var thing in things)
            {
                if (thing.TryGetScore(skillDef, out var score))
                {
                    if (toolsByScores.TryGetValue(score, out var toolList))
                    {
                        toolList.Add(thing);
                    }
                    else
                    {
                        toolsByScores[score] = new List<ThingWithComps> { thing };
                    }
                }
            }
            return toolsByScores;
        }
        private static bool TryGetScore(this ThingWithComps thing, SkillDef skill, out float result)
        {
            bool affectsSkill = false;
            result = 0;
            if (thing.def.equippedStatOffsets != null)
            {
                foreach (var stat in thing.def.equippedStatOffsets)
                {
                    if (stat.AffectsSkill(skill))
                    {
                        affectsSkill = true;
                        result += stat.value;
                    }
                }
            }
            return affectsSkill;
        }

        private static bool AffectsSkill(this StatModifier statModifier, SkillDef skill)
        {
            if (statModifier.stat.skillNeedOffsets != null)
            {
                foreach (var skillNeed in statModifier.stat.skillNeedOffsets)
                {
                    if (skill == skillNeed.skill)
                    {
                        return true;
                    }
                }
            }

            if (statModifier.stat.skillNeedFactors != null)
            {
                foreach (var skillNeed in statModifier.stat.skillNeedFactors)
                {
                    if (skill == skillNeed.skill)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static SkillDef GetActiveSkill(List<Toil> toils)
        {
            foreach (var toil in toils)
            {
                if (toil?.activeSkill != null)
                {
                    try
                    {
                        var skill = toil.activeSkill();
                        if (skill != null)
                        {
                            return skill;
                        }
                    }
                    catch { };
                }
            }
            return null;
        }
    }
}
