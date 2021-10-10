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
    public class SkillJob
    {
        public SkillJob(SkillDef skill, Job job)
        {
            this.skill = skill;
            this.job = job;
        }

        public SkillDef skill;
        public Job job;
    }
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

        public static HashSet<string> fireExtinguisherDefnames = new HashSet<string>()
        {
            "VWE_Gun_FireExtinguisher",
        };
        public static HashSet<ThingDef> fireExtinguishers = new HashSet<ThingDef>();

        public static bool IsTool(this ThingDef thingDef)
        {
            return toolDefs.Contains(thingDef) || fireExtinguishers.Contains(thingDef);
        }

        public static Func<Pawn, Thing, ToolPolicy, bool> baseToolValidator = delegate (Pawn p, Thing x, ToolPolicy policy)
        {
            if (policy != null && !policy.SatisfiedBy(x))
            {
                return false;
            }
            if (!p.CanReserveAndReach(x, PathEndMode.OnCell, Danger.Deadly))
            {
                return false;
            }
            return true;
        };

        private static Func<Pawn, Thing, bool> toolValidator = delegate (Pawn p, Thing x)
        {
            if (!toolDefs.Contains(x.def))
            {
                return false;
            }
            var policy = p.GetCurrentToolPolicy();
            if (!policy[x.def].takeAsTool)
            {
                return false;
            }
            if (!baseToolValidator(p, x, policy))
            {
                return false;
            }
            return true;
        };

        private static Func<Pawn, Thing, bool> fireExtinguisherValidator = delegate (Pawn p, Thing x)
        {
            if (!fireExtinguishers.Contains(x.def))
            {
                return false;
            }
            var policy = p.GetCurrentToolPolicy();
            if (!policy[x.def].takeAsTool)
            {
                return false;
            }
            if (!baseToolValidator(p, x, policy))
            {
                return false;
            }
            return true;
        };

        static ToolSearchUtility()
        {
            foreach (var thingDef in DefDatabase<ThingDef>.AllDefs)
            {
                if (thingDef?.equippedStatOffsets?.Any(x => x?.value > 0) ?? false && thingDef.comps != null)
                {
                    foreach (var comp in thingDef.comps)
                    {
                        if (comp.compClass != null && typeof(CompEquippable).IsAssignableFrom(comp.compClass))
                        {
                            toolDefs.Add(thingDef);
                            break;
                        }
                        if (comp.compClass is null)
                        {
                            Log.Error(comp + " has a missing comp class. It will lead to bugs.");
                        }
                    }
                }

                if (ModCompatUtility.survivalToolsLoaded && thingDef.IsSurvivalTool())
                {
                    toolDefs.Add(thingDef);
                }
            }
            foreach (var defName in fireExtinguisherDefnames)
            {
                var tool = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (tool != null)
                {
                    fireExtinguishers.Add(tool);
                }
            }
        }
        public static ThingWithComps FindToolFor(Pawn pawn, Job job, SkillDef skillDef, out ToolAction toolAction)
        {
            if (job.def == ATS_DefOf.ATS_BeatFireAdv)
            {
                return FindToolForInt(pawn, new SkillJob(skillDef, job), fireExtinguisherValidator, fireExtinguishers, out toolAction);
            }
            else
            {
                return FindToolForInt(pawn, new SkillJob(skillDef, job), toolValidator, toolDefs, out toolAction);
            }
        }
        private static ThingWithComps FindToolForInt(Pawn pawn, SkillJob skillJob, Func<Pawn, Thing, bool> validator, HashSet<ThingDef> toolThingDefs, out ToolAction toolAction)
        {
            toolAction = ToolAction.DoNothing;
            var equippedThings = pawn.equipment?.AllEquipmentListForReading.Where(x => validator(pawn, x));
            var inventoryThings = pawn.inventory?.innerContainer.OfType<ThingWithComps>().Where(x => validator(pawn, x));
            var outsideThings = new List<ThingWithComps>();
            foreach (var def in toolThingDefs)
            {
                foreach (var tool in pawn.Map.listerThings.ThingsOfDef(def).OfType<ThingWithComps>())
                {
                    if (validator(pawn, tool))
                    {
                        outsideThings.Add(tool);
                    }
                }
            }
            var equippedThingsScored = GetToolsScoredFor(equippedThings, skillJob);
            var inventoryThingsScored = GetToolsScoredFor(inventoryThings, skillJob);
            var outsideThingsScored = GetToolsScoredFor(outsideThings, skillJob);
            return GetScoredTool(pawn, equippedThingsScored, inventoryThingsScored, outsideThingsScored, out toolAction);
        }

        private static ThingWithComps GetScoredTool(Pawn pawn, Dictionary<float, List<ThingWithComps>> equippedThingsScored, Dictionary<float, List<ThingWithComps>> inventoryThingsScored, 
            Dictionary<float, List<ThingWithComps>> outsideThingsScored, out ToolAction toolAction)
        {
            toolAction = ToolAction.DoNothing;
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

                    if (equippedMaxScore.HasValue && (!inventoryMaxScore.HasValue || equippedMaxScore.Value >= inventoryMaxScore.Value)
                        && (!outsideMaxScore.HasValue || equippedMaxScore.Value >= outsideMaxScore))
                    {
                        return equippedThingsScored.RandomElement().Value.RandomElement();
                    }
                    else if (inventoryMaxScore.HasValue && (!equippedMaxScore.HasValue || inventoryMaxScore.Value > equippedMaxScore.Value)
                        && (!outsideMaxScore.HasValue || inventoryMaxScore.Value >= outsideMaxScore.Value))
                    {
                        toolAction = ToolAction.EquipFromInventory;
                        return inventoryThingsScored[inventoryMaxScore.Value].RandomElement();
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
                            return tool;
                        }
                        else
                        {
                            outsideThingsScored.Remove(outsideMaxScore.Value);
                        }
                    }
                    else if (!equippedMaxScore.HasValue && !inventoryMaxScore.HasValue && !outsideMaxScore.HasValue)
                    {
                        return null;
                    }
                }
            }
            return null;
        }

        private static Dictionary<float, List<ThingWithComps>> GetToolsScoredFor(IEnumerable<ThingWithComps> things, SkillJob skillJob)
        {
            if (things.Any())
            {
                return GetScoredThings(things, skillJob);
            }
            return null;
        }
        private static Dictionary<float, List<ThingWithComps>> GetScoredThings(IEnumerable<ThingWithComps> things, SkillJob skillJob)
        {
            Dictionary<float, List<ThingWithComps>> toolsByScores = new Dictionary<float, List<ThingWithComps>>();
            foreach (var thing in things)
            {
                if (thing.TryGetScore(skillJob, out var score))
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
        public static bool TryGetScore(this ThingWithComps thing, SkillJob skillJob, out float result)
        {
            bool isUseful = false;
            result = 0;
            if (skillJob.skill != null)
            {
                if (thing.def.equippedStatOffsets != null)
                {
                    foreach (var stat in thing.def.equippedStatOffsets)
                    {
                        if (stat.AffectsSkill(skillJob.skill))
                        {
                            if (stat.value > 0)
                            {
                                isUseful = true;
                            }
                            result += stat.value;
                        }
                    }
                }
            }
            if (skillJob.job != null) // maybe we should add scores for tools here
            {
                if (skillJob.job.def == ATS_DefOf.ATS_BeatFireAdv && fireExtinguishers.Contains(thing.def))
                {
                    result += 1f;
                    isUseful = true;
                }

                if (skillJob.job.bill?.recipe?.workSpeedStat != null)
                {
                    if (thing.def.equippedStatOffsets != null)
                    {
                        foreach (var stat in thing.def.equippedStatOffsets)
                        {
                            if (stat.stat == skillJob.job.bill.recipe.workSpeedStat)
                            {
                                if (stat.value > 0)
                                {
                                    isUseful = true;
                                }
                                result += stat.value;
                            }
                        }
                    }
                }
            }

            if (ModCompatUtility.survivalToolsLoaded)
            {
                var score = ModCompatUtility.GetScoreFromSurvivalTool(thing, skillJob, ref isUseful);
                if (score != 0)
                {
                    result *= score;
                }
            }
            return isUseful;
        }

        public static bool AffectsSkill(this StatModifier statModifier, SkillDef skill)
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

        public static SkillDef GetActiveSkill(this Job job, Pawn pawn)
        {
            var driver = pawn.jobs.curDriver;
            var toils = Traverse.Create(driver).Field("toils").GetValue<List<Toil>>();
            return GetActiveSkill(job, toils);
        }

        public static SkillDef GetActiveSkill(Job job, List<Toil> toils)
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
                    catch (Exception ex)
                    {

                    };
                }
            }

            if (job != null)
            {
                if (job.def == JobDefOf.FinishFrame)
                {
                    return SkillDefOf.Construction;
                }
            }
            return null;
        }
    }
}
