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
	[HarmonyPatch(typeof(WorkGiver_HunterHunt), "HasHuntingWeapon")]
	public static class WorkGiver_HunterHunt_HasHuntingWeapon_Patch
	{
		public static void Postfix(ref bool __result, Pawn p)
		{
			if (__result is false && ToolSearchUtility.FindToolFor(p, JobMaker.MakeJob(JobDefOf.Hunt, p),
			 SkillDefOf.Animals, out _) is not null)
			{
				__result = true;
			}
		}
	}

	[HotSwappable]
	[StaticConstructorOnStartup]
	public static class ToolSearchUtility
	{
		public static HashSet<ThingDef> toolDefs = new HashSet<ThingDef>();
		public static HashSet<string> fireExtinguisherDefnames = new HashSet<string>()
		{
			"VWE_Gun_FireExtinguisher",
			"Mosi_FirefoamLauncher",
			"Mosi_GrenadeFirefoam",
			"Mosi_FirefoamSprayer"
		};
		
		public static HashSet<ThingDef> fireExtinguishers = new HashSet<ThingDef>();
		private static HashSet<ThingDef> huntingWeapons = new HashSet<ThingDef>();
		public static bool IsTool(this ThingDef thingDef)
		{
			return toolDefs.Contains(thingDef) || fireExtinguishers.Contains(thingDef);
		}

		public static Func<Pawn, Thing, ToolPolicy, bool> baseEquipmentValidator = delegate (Pawn p, Thing x, ToolPolicy policy)
		{
			if (policy != null && !policy.SatisfiedBy(x))
			{
				//Log.Message("Not satisfied 1");
				return false;
			}
			if (!p.CanReserveAndReach(x, PathEndMode.OnCell, Danger.Deadly))
			{
				//Log.Message("Not satisfied 2");
				return false;
			}
			return true;
		};

		private static Func<Pawn, Thing, bool> baseToolValidator = delegate (Pawn p, Thing x)
		{
			if (!toolDefs.Contains(x.def))
			{
				return false;
			}
			var policy = p.GetCurrentToolPolicy();
			if (policy != null)
			{
				if (!policy[x.def].takeAsTool)
				{
					return false;
				}
				if (!baseEquipmentValidator(p, x, policy))
				{
					return false;
				}
				return true;
			}
			return false;
		};

		private static Func<Pawn, Thing, bool> huntingWeaponsValidator = delegate (Pawn p, Thing x)
		{
			return x.def.IsRangedWeapon;
		};

		private static Func<Pawn, Thing, bool> fireExtinguisherValidator = delegate (Pawn p, Thing x)
		{
			if (!fireExtinguishers.Contains(x.def))
			{
				return false;
			}
			var policy = p.GetCurrentToolPolicy();
			if (policy != null)
			{
				if (!policy[x.def].takeAsTool)
				{
					return false;
				}
				if (!baseEquipmentValidator(p, x, policy))
				{
					return false;
				}
				return true;
			}
			return false;
		};
		public static Dictionary<JobDef, List<StatDef>> jobRelatedStats = new Dictionary<JobDef, List<StatDef>>();
		public static HashSet<StatDef> workRelatedStats = new HashSet<StatDef>();
		static ToolSearchUtility()
		{
			foreach (var statDef in DefDatabase<StatDef>.AllDefs)
			{
				foreach (var skillDef in DefDatabase<SkillDef>.AllDefs)
				{
					if (statDef.AffectsSkill(skillDef))
					{
						workRelatedStats.Add(statDef);
					}
				}
			}
			foreach (var recipeDef in DefDatabase<RecipeDef>.AllDefs)
            {
                if (recipeDef.workSpeedStat != null)
                {
                    workRelatedStats.Add(recipeDef.workSpeedStat);
                }
            }

			foreach (var stat in workRelatedStats.ToList())
			{
				if (stat.statFactors != null)
				{
					foreach (var factor in stat.statFactors)
					{
						workRelatedStats.Add(factor);
					}
				}
			}


            workRelatedStats.Add(StatDefOf.CleaningSpeed);
			jobRelatedStats[JobDefOf.Clean] = new List<StatDef>() { StatDefOf.CleaningSpeed };

			foreach (var thingDef in DefDatabase<ThingDef>.AllDefs)
			{
				if (thingDef.equippedStatOffsets?.Any(x => x?.value > 0
					&& workRelatedStats.Contains(x.stat)) ?? false && thingDef.comps != null)
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
				
				if (thingDef.IsRangedWeapon && thingDef.Verbs.Any(x => typeof(Verb_LaunchProjectile).IsAssignableFrom(x.verbClass) && x.range > 5))
				{
					huntingWeapons.Add(thingDef);
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
			if (job.def == JobDefOf.Hunt)
			{
				return FindToolForInt(pawn, new SkillJob(skillDef, job), huntingWeaponsValidator, huntingWeapons, out toolAction);
			}
			else if (job.def == ATS_DefOf.ATS_BeatFireAdv)
			{
				return FindToolForInt(pawn, new SkillJob(skillDef, job), fireExtinguisherValidator, fireExtinguishers, out toolAction);
			}
			else
			{
				return FindToolForInt(pawn, new SkillJob(skillDef, job), baseToolValidator, toolDefs, out toolAction);
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
				foreach (var tool in pawn.Map.listerThings.ThingsOfDef(def))
				{
					if (pawn.equipment.Primary?.def != tool.def && !pawn.inventory.innerContainer.Any(x => x.def == tool.def)
						&& validator(pawn, tool))
					{
						outsideThings.Add(tool as ThingWithComps);
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

		public static bool TryGetScore(this ThingWithComps tool, SkillJob skillJob, out float result)
		{
			bool isUseful = false;
			result = 0;
			var processedStats = new HashSet<StatDef>();

			if (skillJob.job.def == JobDefOf.Hunt)
			{
				if (tool.def.IsRangedWeapon)
				{
					result += tool.def.verbs.Max(x => x.range);
					isUseful = true;
				}
				return isUseful;
			}
			
			if (skillJob.skill != null)
			{
				if (tool.def.equippedStatOffsets != null)
				{
					foreach (var statOffset in tool.def.equippedStatOffsets)
					{
						if (!processedStats.Contains(statOffset.stat) && statOffset.stat.AffectsSkill(skillJob.skill))
						{
							if (statOffset.value > 0)
							{
								isUseful = true;
							}
							result += statOffset.value;
							processedStats.Add(statOffset.stat);
						}
					}
				}
			}

			if (skillJob.job != null)
			{
				if (skillJob.job.def == ATS_DefOf.ATS_BeatFireAdv && fireExtinguishers.Contains(tool.def))
				{
					result += 1f;
					isUseful = true;
				}

				if (skillJob.job.bill?.recipe?.workSpeedStat != null)
				{
					if (tool.def.equippedStatOffsets != null)
					{
						foreach (var stat in tool.def.equippedStatOffsets)
						{
							if (!processedStats.Contains(stat.stat) && (stat.stat == skillJob.job.bill.recipe.workSpeedStat 
								|| skillJob.job.bill.recipe.workSpeedStat.statFactors != null && skillJob.job.bill.recipe.workSpeedStat.statFactors.Contains(stat.stat)))
							{
								if (stat.value > 0)
								{
									isUseful = true;
								}
								result += stat.value;
								processedStats.Add(stat.stat);
							}
						}
					}
				}

				if (tool.def.equippedStatOffsets != null)
				{
					if (jobRelatedStats.TryGetValue(skillJob.job.def, out var stats))
					{
						foreach (var stat in tool.def.equippedStatOffsets)
						{
							if (!processedStats.Contains(stat.stat) && stats.Contains(stat.stat))
							{
								if (stat.value > 0)
								{
									isUseful = true;
								}
								result += stat.value;
								processedStats.Add(stat.stat);
							}
						}
					}
				}
			}

			if (ModCompatUtility.survivalToolsLoaded)
			{
				var score = ModCompatUtility.GetScoreFromSurvivalTool(tool, skillJob, ref isUseful);
				if (score > 1)
				{
					result += score;
				}
			}

			return isUseful;
		}

		public static Dictionary<(StatDef stat, SkillDef skill), bool> affectedStats = new();

		public static bool AffectsSkill(this StatDef stat, SkillDef skill)
		{
			var key = (stat, skill);
			if (!affectedStats.TryGetValue(key, out var result))
			{
				affectedStats[key] = result = stat.AffectsSkillInt(skill);
			}
			return result;
		}

		private static bool AffectsSkillInt(this StatDef stat, SkillDef skill)
		{
			if (stat.skillNeedOffsets != null)
			{
				foreach (var skillNeed in stat.skillNeedOffsets)
				{
					if (skill == skillNeed.skill)
					{
						return true;
					}
				}
			}

			if (stat.skillNeedFactors != null)
			{
				foreach (var skillNeed in stat.skillNeedFactors)
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
			return GetActiveSkill(job, pawn.jobs.curDriver.toils);
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

			if (job.workGiverDef != null)
			{
				return job.workGiverDef?.workType?.relevantSkills?.FirstOrDefault();
			}
			return null;
		}
	}
}
