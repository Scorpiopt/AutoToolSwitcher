using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace AutoToolSwitcher
{
	[HotSwappable]
	[StaticConstructorOnStartup]
	public static class HarmonyPatches
	{
		public static Harmony harmony;
		static HarmonyPatches()
		{
			//foreach (var stat in DefDatabase<StatDef>.AllDefs)
			//{
			//    if (stat.showOnPawns)
			//    {
			//        foreach (var skill in DefDatabase<SkillDef>.AllDefs)
			//        {
			//            var statModifier = new StatModifier
			//            {
			//                stat = stat,
			//                value = 1f
			//            };
			//            Log.Message(stat.defName + " affects skill: " + skill.defName + " - " + ToolSearchUtility.AffectsSkill(statModifier, skill));
			//            Log.ResetMessageCount();
			//        }
			//    }
			//}

			harmony = new Harmony("AutoToolSwitcher.Mod");

			var workTagIsDisabledMeth = AccessTools.Method(typeof(Pawn), "WorkTagIsDisabled");
			var workTagIsDisabledPostfix = AccessTools.Method(typeof(HarmonyPatches), "WorkTagIsDisabledPostfix");
			harmony.Patch(workTagIsDisabledMeth, null, new HarmonyMethod(workTagIsDisabledPostfix));

			if (ModLister.AllInstalledMods.FirstOrDefault(x => x.Active && x.PackageIdPlayerFacing == "com.yayo.combat3") != null)
			{
				var yayoMethod = AccessTools.Method("yayoCombat.patch_DrawEquipment:Prefix");
				harmony.Patch(yayoMethod, new HarmonyMethod(AccessTools.Method(typeof(Patch_DrawEquipment), "PrefixForYayo")),
					new HarmonyMethod(AccessTools.Method(typeof(Patch_DrawEquipment), "Postfix")));
			}
			var methodPostfix = AccessTools.Method(typeof(HarmonyPatches), nameof(AddEquipToolToilsPostfix));
			foreach (var type in typeof(JobDriver).AllSubclasses())
			{
				try
				{
					var makeNewToilsMethod = AccessTools.Method(type, "MakeNewToils");
					if (makeNewToilsMethod != null)
					{
						harmony.Patch(makeNewToilsMethod, null, new HarmonyMethod(methodPostfix, priority: Priority.Last));
					}
				}
				catch { }
			}

			var equipWeaponMethod = AccessTools.GetDeclaredMethods(typeof(JobDriver_Equip))
				.FirstOrDefault(x => x.Name.Contains("<MakeNewToils>") && x.ReturnType == typeof(void) && x.GetParameters().Length == 0);
			harmony.Patch(equipWeaponMethod, transpiler: new HarmonyMethod(AccessTools.Method(typeof(HarmonyPatches), "EquipWeaponTranspiler")));

			harmony.PatchAll();
		}

		public static IEnumerable<CodeInstruction> EquipWeaponTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilg)
		{
			var shouldSkip = AccessTools.Method(typeof(HarmonyPatches), "IsSoundEquipAllowed");
			var codes = instructions.ToList();
			for (var i = 0; i < codes.Count; i++)
			{
				if (i > 0 && codes[i - 1].opcode == OpCodes.Brfalse_S && codes[i].opcode == OpCodes.Ldloc_0)
				{
					yield return new CodeInstruction(OpCodes.Ldarg_0);
					yield return new CodeInstruction(OpCodes.Call, shouldSkip);
					yield return new CodeInstruction(OpCodes.Brfalse_S, codes[i - 1].operand);
				}
				yield return codes[i];
			}
		}

		private static bool IsSoundEquipAllowed(JobDriver_Equip jobDriver_Equip)
		{
			var pawn = jobDriver_Equip.pawn;
			var policy = pawn.GetCurrentToolPolicy();
			if (policy != null && !policy.toggleEquipSound)
			{
				return false;
			}
			return true;
		}

		public static void WorkTagIsDisabledPostfix(WorkTags w, ref Pawn __instance, ref bool __result)
		{
			if (!__result && w != WorkTags.Violent)
			{
				return;
			}

			if (__instance.IsUsingTool())
			{
				__result = false;
			}
		}

		[HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAndApparelExtras))]
		public static class Patch_DrawEquipment
		{
			public struct Values
			{
				public bool alwaysShowWeapon;
				public bool neverShowWeapon;
				public Job job;
			}

			public static Values? __state;
			private static void PrefixForYayo(Pawn __2)
			{
				Prefix(__2);
			}
			private static void Prefix(Pawn pawn)
			{
				var job = pawn.CurJob;
				if (job != null && pawn.IsUsingTool())
				{
					__state = new Values
					{
						neverShowWeapon = job.def.neverShowWeapon,
						alwaysShowWeapon = job.def.alwaysShowWeapon,
						job = job,
					};
					job.def.neverShowWeapon = false;
					job.def.alwaysShowWeapon = true;
				}
				else
				{
					__state = null;
				}
			}

			private static void Postfix()
			{
				if (__state.HasValue)
				{
					var job = __state.Value.job;
					job.def.alwaysShowWeapon = __state.Value.alwaysShowWeapon;
					job.def.neverShowWeapon = __state.Value.neverShowWeapon;
				}
			}
		}

		[HarmonyPatch(typeof(JobMaker), "MakeJob", new Type[] { typeof(JobDef), typeof(LocalTargetInfo) })]
		public static class Patch_MakeJob
		{
			public static void Prefix(ref JobDef def, LocalTargetInfo targetA)
			{
				if (def == JobDefOf.BeatFire)
				{
					if (ToolSearchUtility.fireExtinguishers.Any())
					{
						def = ATS_DefOf.ATS_BeatFireAdv;
					}
				}
			}
		}

		[HarmonyPatch(typeof(PawnComponentsUtility), "AddAndRemoveDynamicComponents")]
		public static class Patch_AddAndRemoveDynamicComponents
		{
			public static void Postfix(Pawn pawn)
			{
				if (pawn.Faction != null && pawn.Faction.IsPlayer && pawn.RaceProps.Humanlike)
				{
					if (!GameComponent_ToolTracker.Instance.trackers.ContainsKey(pawn))
					{
						GameComponent_ToolTracker.Instance.trackers[pawn] = new Pawn_ToolPolicyTracker(pawn);
					}
				}
			}
		}
		private static bool TryFindGunJobPrefix(ref Job __result, Pawn pawn)
		{
			if (pawn is null || pawn.RaceProps.Animal)
			{
				return false;
			}
			return true;
		}

		private static HashSet<JobDef> ignoredJobs = new HashSet<JobDef>
		{
			JobDefOf.GotoWander,
			JobDefOf.Ingest,
			JobDefOf.LayDown,
			JobDefOf.Wait_MaintainPosture,
			JobDefOf.Wait,
			JobDefOf.HaulToCell,
			JobDefOf.TakeInventory,
			JobDefOf.Wait_Downed,
			JobDefOf.Wait_Wander,
			JobDefOf.FleeAndCower,
			JobDefOf.Goto,
			JobDefOf.Wait_Combat,
		};
		private static Dictionary<Job, ThingWithComps> cachedToolsByJobs = new Dictionary<Job, ThingWithComps>();
		private static void AddEquipToolToilsPostfix(ref IEnumerable<Toil> __result, JobDriver __instance)
		{
			try
			{
				var pawn = __instance.pawn;
				if (pawn != null && pawn.RaceProps.Humanlike && pawn.IsColonist && __instance.job != null && !ignoredJobs.Contains(__instance.job.def))
				{
					var list = __result.ToList();
					var skill = ToolSearchUtility.GetActiveSkill(pawn.CurJob, list);
					if (pawn.Map != null)
					{
						ToolAction toolAction;
						var tool = ToolSearchUtility.FindToolFor(pawn, __instance.job, skill, out toolAction);
						cachedToolsByJobs[pawn.CurJob] = tool; // we do that so we retrieve this tool later rather than finding it again
						if (tool != null)
						{
							Toil goToTool = new Toil();
							goToTool.initAction = delegate
							{
								if (!pawn.Reserve(tool, pawn.CurJob, 1, 1))
								{
									pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
								}
								else
								{
									pawn.pather.StartPath(tool, PathEndMode.OnCell);
								}
							};
							goToTool.defaultCompleteMode = ToilCompleteMode.PatherArrival;
							goToTool.AddEndCondition(delegate
							{
								return (tool != null && tool.Spawned && tool.Map == pawn.Map) ? JobCondition.Ongoing : JobCondition.Incompletable;
							});
							Toil equipTool = new Toil();
							equipTool.initAction = delegate
							{
								EquipTool(pawn, tool);
							};
							switch (toolAction)
							{
								case ToolAction.GoAndEquipTool:
									list.Insert(0, goToTool);
									list.Insert(1, equipTool);
									break;
								case ToolAction.EquipFromInventory:
									list.Insert(0, equipTool);
									break;
							}
						}
					}
					__result = list;
				}
			}
			catch (Exception ex)
			{
				Log.Error("Exception found: " + ex);
			}
		}

		public static void EquipTool(Pawn pawn, ThingWithComps tool)
		{
			if (pawn.equipment.Primary != null && pawn.inventory != null)
			{
				pawn.inventory.innerContainer.TryAddOrTransfer(pawn.equipment.Primary);
			}
			if (pawn.equipment.GetDirectlyHeldThings().TryAdd(tool.SplitOff(1)))
			{
				if (tool.def.soundInteract != null)
				{
					tool.def.soundInteract.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
				}
				var job = pawn.CurJob;
				if (job != null)
				{
					var activeSkill = ToolSearchUtility.GetActiveSkill(job, pawn);
					if (tool.TryGetScore(new SkillJob(activeSkill, job), out var result) && result != 0)
					{
						cachedToolsByJobs[job] = tool;
					}
					else
					{
						cachedToolsByJobs[job] = null;
					}
				}
			}
		}

		private static bool IsUsingTool(this Pawn pawn)
		{
			var job = pawn.CurJob;
			var eq = pawn.equipment?.Primary;
			if (job != null && eq != null)
			{
				if (!cachedToolsByJobs.TryGetValue(job, out var toolUsed) || true)
				{
					var activeSkill = ToolSearchUtility.GetActiveSkill(job, pawn);
					if (eq.TryGetScore(new SkillJob(activeSkill, job), out var result) && result != 0)
					{
						cachedToolsByJobs[job] = toolUsed = eq;
					}
					else
					{
						cachedToolsByJobs[job] = toolUsed = null;
					}
				}
				return toolUsed == pawn.equipment.Primary;
			}
			return false;
		}


		[HarmonyPatch(typeof(Pawn), "TryGetAttackVerb")]
		public static class Patch_TryGetAttackVerb
		{
			public static void Prefix(Pawn __instance, Thing target, bool allowManualCastWeapons = false)
			{
				Patch_TryMeleeAttackVerb.TrySwitchWeapon(__instance, target);
			}
		}

		[HarmonyPatch(typeof(Pawn_MeleeVerbs), "TryMeleeAttack")]
		public static class Patch_TryMeleeAttackVerb
		{
			public static Func<Pawn, ThingWithComps, bool> meleeValidator = delegate (Pawn pawn, ThingWithComps t)
			{
				if (!t.def.IsWeapon)
				{
					return false;
				}
				if (t.def.IsRangedWeapon)
				{
					return false;
				}
				if (t.def.weaponTags != null && t.def.weaponTags.Where(x => x.ToLower().Contains("grenade")).Any())
				{
					return false;
				}
				if (t.def.Verbs.Where(x => x.verbClass == typeof(Verb_ShootOneUse)).Any())
				{
					return false;
				}

				return true;
			};

			public static Func<Pawn, ThingWithComps, bool> rangeValidator = delegate (Pawn pawn, ThingWithComps t)
			{
				if (!t.def.IsWeapon)
				{
					return false;
				}
				if (!t.def.IsRangedWeapon)
				{
					return false;
				}
				if (t.def.weaponTags != null && t.def.weaponTags.Where(x => x.ToLower().Contains("grenade")).Any())
				{
					return false;
				}
				if (t.def.Verbs.Where(x => x.verbClass == typeof(Verb_ShootOneUse)).Any())
				{
					return false;
				}
				if (ModCompatUtility.combatExtendedLoaded && !ModCompatUtility.IsUsableForCE(pawn, t))
				{
					return false;
				}
				return true;
			};
			public static void Prefix(Pawn_MeleeVerbs __instance, Thing target)
			{
				TrySwitchWeapon(__instance.Pawn, target);
			}

			public static void TrySwitchWeapon(Pawn pawn, Thing target)
			{
				if (target != null && target.Position.DistanceTo(pawn.Position) <= 1.42f)
				{
					var toolPolicy = pawn.GetCurrentToolPolicy();
					if (toolPolicy != null && !toolPolicy.toggleAutoMelee)
					{
						return;
					}
					if ((pawn.equipment?.Primary?.def.IsRangedWeapon ?? false))
					{
						var meleeWeapon = WeaponSearchUtility.PickBestWeapon(pawn, meleeValidator);
						if (meleeWeapon != null)
						{
							EquipTool(pawn, meleeWeapon);
						}
					}
				}
			}
		}
	}
}