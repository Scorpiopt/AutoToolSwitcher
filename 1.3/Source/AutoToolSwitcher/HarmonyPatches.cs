using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace AutoToolSwitcher
{
    [DefOf]
    public static class ATS_DefOf
    {
        public static JobDef ATS_BeatFireAdv;
        public static ToolPolicyDef ATS_Unrestricted;
    }

    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            var harmony = new Harmony("AutoToolSwitcher.Mod");
            harmony.Patch(AccessTools.Method(typeof(JobGiver_OptimizeApparel), "TryGiveJob"),
                new HarmonyMethod(typeof(HarmonyPatches), nameof(TryFindGunJobPrefix)));

            var workTagIsDisabledMeth = AccessTools.Method(typeof(Pawn), "WorkTagIsDisabled");
            var workTagIsDisabledPostfix = AccessTools.Method(typeof(HarmonyPatches), "WorkTagIsDisabledPostfix");
            harmony.Patch(workTagIsDisabledMeth, null, new HarmonyMethod(workTagIsDisabledPostfix));

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
                catch (Exception ex)
                {

                }
            }

            harmony.PatchAll();
        }

        public static void WorkTagIsDisabledPostfix(WorkTags w, ref Pawn __instance, ref bool __result)
        {
            if (w != WorkTags.Violent)
            {
                return;
            }

            if (__result == false)
            {
                return;
            }

            if (__instance.IsUsingTool())
            {
                __result = false;
            }
        }

        [HarmonyPatch(typeof(PawnRenderer), "DrawEquipment")]
        public static class Patch_DrawEquipment
        {
            struct Values
            {
                public bool alwaysShowWeapon;
                public bool neverShowWeapon;
            }
            private static void Prefix(Pawn ___pawn, out Values? __state)
            {
                var job = ___pawn.CurJob;
                if (job != null && ___pawn.IsUsingTool())
                {
                    __state = new Values
                    {
                        neverShowWeapon = job.def.neverShowWeapon,
                        alwaysShowWeapon = job.def.alwaysShowWeapon
                    };
                    job.def.neverShowWeapon = false;
                    job.def.alwaysShowWeapon = true;
                }
                else
                {
                    __state = null;
                }
            }

            private static void Postfix(Pawn ___pawn, Values? __state)
            {
                if (__state.HasValue)
                {
                    var job = ___pawn.CurJob;
                    job.def.alwaysShowWeapon = __state.Value.alwaysShowWeapon;
                    job.def.neverShowWeapon = __state.Value.neverShowWeapon;
                }
            }

            // failed transpiler attempt here
            //public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilg)
            //{
            //    var neverShowWeaponField = AccessTools.Field(typeof(JobDef), "neverShowWeapon");
            //    var pawnField = AccessTools.Field(typeof(PawnRenderer), "pawn");
            //    var isUsingTool = AccessTools.Method(typeof(HarmonyPatches), "IsUsingTool");
            //    Label label = ilg.DefineLabel();
            //    var codes = instructions.ToList();
            //    for (var i = 0; i < codes.Count; i++)
            //    {
            //        var instr = codes[i];
            //        yield return instr;
            //        if (instr.OperandIs(neverShowWeaponField))
            //        {
            //            codes[i].labels.Add(label);
            //
            //            yield return new CodeInstruction(OpCodes.Ldarg_0);
            //            yield return new CodeInstruction(OpCodes.Ldfld, pawnField);
            //            yield return new CodeInstruction(OpCodes.Call, isUsingTool);
            //            yield return new CodeInstruction(OpCodes.And, null);
            //            yield return new CodeInstruction(OpCodes.Brfalse_S, label);
            //        }
            //    }
            //}
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

        [HarmonyPatch(typeof(Pawn_MeleeVerbs), "TryMeleeAttack")]
        public static class Patch_TryMeleeAttackVerb
        {
            public static void Prefix(Pawn_MeleeVerbs __instance, Thing target)
            {
                var pawn = __instance.Pawn;
                if ((pawn.equipment?.Primary?.def.IsRangedWeapon ?? false) && target.Position.DistanceTo(pawn.Position) <= 1.42f)
                {
                    var meleeWeapon = WeaponSearchUtility.PickBestMeleeWeaponFor(pawn);
                    if (meleeWeapon != null && meleeWeapon.GetStatValue(StatDefOf.MeleeWeapon_AverageDPS, true) > pawn.equipment.Primary.GetStatValue(StatDefOf.MeleeWeapon_AverageDPS, true))
                    {
                        EquipTool(pawn, meleeWeapon);
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
            if (!WeaponSearchUtility.CanLookForWeapon(pawn))
            {
                return true;
            }
            var weapon = WeaponSearchUtility.PickBestWeaponFor(pawn, out var secondaryWeapon);
            if (weapon == null && secondaryWeapon == null)
            {
                return true;
            }
            if (weapon != null && weapon.def != pawn.equipment.Primary?.def)
            {
                if (pawn.inventory.innerContainer.Contains(weapon))
                {
                    EquipTool(pawn, weapon as ThingWithComps);
                }
                else
                {
                    __result = JobMaker.MakeJob(JobDefOf.Equip, weapon);
                }
            }
            else if (secondaryWeapon != null && !pawn.inventory.innerContainer.Any(x => x.def == secondaryWeapon.def))
            {
                if (pawn.equipment.Primary == secondaryWeapon)
                {
                    if (secondaryWeapon.holdingOwner != null)
                    {
                        secondaryWeapon.holdingOwner.Remove(secondaryWeapon);
                    }
                    pawn.inventory.TryAddItemNotForSale(secondaryWeapon);
                }
                else
                {
                    __result = JobMaker.MakeJob(JobDefOf.TakeInventory, secondaryWeapon);
                    __result.count = 1;
                }
            }

            if (__result != null)
            {
                return false;
            }
            return true;
        }

        private static Dictionary<Job, ThingWithComps> cachedToolsByJobs = new Dictionary<Job, ThingWithComps>();
        private static void AddEquipToolToilsPostfix(ref IEnumerable<Toil> __result, JobDriver __instance)
        {
            var pawn = __instance.pawn;
            if (pawn?.RaceProps?.Humanlike ?? false)
            {
                var list = __result.ToList();
                var skill = ToolSearchUtility.GetActiveSkill(pawn.CurJob, list);
                if (pawn.Map != null)
                {
                    ToolAction toolAction;
                    var tool = skill != null ? ToolSearchUtility.FindToolFor(pawn, skill, out toolAction) : ToolSearchUtility.FindToolFor(pawn, pawn.CurJob, out toolAction);
                    if (tool is null)
                    {
                        Log.Message("1 Null: " + pawn.CurJob + " - " + tool);
                    }
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
                //else if (Scribe.mode == LoadSaveMode.PostLoadInit && Current.ProgramState == ProgramState.MapInitializing)
                //// if a save was reloaded, pawn map will be null when setting up jobdriver. in this case we insert tool finder inside actions.
                //// And we can't use job target fields because they might be filled
                //{
                //    //Toil goToTool = new Toil();
                //    //goToTool.initAction = delegate
                //    //{
                //    //    ToolAction toolAction;
                //    //    var tool = skill != null ? ToolSearchUtility.FindToolFor(pawn, skill, out toolAction) : ToolSearchUtility.FindToolFor(pawn, pawn.CurJob, out toolAction);
                //    //    cachedToolsByJobs[pawn.CurJob] = tool; // we do that so we retrieve this tool later rather than finding it again
                //    //    if (tool != null)
                //    //    {
                //    //        if (toolAction == ToolAction.GoAndEquipTool)
                //    //        {
                //    //            if (!pawn.Reserve(tool, pawn.CurJob, 1, 1))
                //    //            {
                //    //                pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                //    //            }
                //    //            else
                //    //            {
                //    //                pawn.pather.StartPath(tool, PathEndMode.OnCell);
                //    //            }
                //    //        }
                //    //    }
                //    //};
                //    //goToTool.defaultCompleteMode = ToilCompleteMode.Instant;
                //    //goToTool.AddEndCondition(delegate
                //    //{
                //    //    if (!cachedToolsByJobs.TryGetValue(pawn.CurJob, out var tool))
                //    //    {
                //    //        ToolAction toolAction;
                //    //        tool = skill != null ? ToolSearchUtility.FindToolFor(pawn, skill, out toolAction) : ToolSearchUtility.FindToolFor(pawn, pawn.CurJob, out toolAction);
                //    //        cachedToolsByJobs[pawn.CurJob] = tool;
                //    //    }
                //    //    return tool != null && tool.Spawned && tool.Map == pawn.Map ? JobCondition.Ongoing : JobCondition.Incompletable;
                //    //});
                //    //Toil equipTool = new Toil();
                //    //equipTool.initAction = delegate
                //    //{
                //    //    if (!cachedToolsByJobs.TryGetValue(pawn.CurJob, out var tool))
                //    //    {
                //    //        ToolAction toolAction;
                //    //        tool = skill != null ? ToolSearchUtility.FindToolFor(pawn, skill, out toolAction) : ToolSearchUtility.FindToolFor(pawn, pawn.CurJob, out toolAction);
                //    //        cachedToolsByJobs[pawn.CurJob] = tool;
                //    //    }
                //    //    if (tool != null)
                //    //    {
                //    //        EquipTool(pawn, tool);
                //    //    }
                //    //};
                //    //equipTool.defaultCompleteMode = ToilCompleteMode.Instant;
                //    list.Insert(0, new Toil());
                //    list.Insert(1, new Toil());
                //}
                __result = list;
            }
        }

        private static void EquipTool(Pawn pawn, ThingWithComps tool)
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
                    Log.Message("Active skill: " + activeSkill + " - " + job + " pawn: " + pawn);
                    if (activeSkill != null && tool.TryGetScore(new SkillJob(activeSkill), out var result) && result != 0)
                    {
                        cachedToolsByJobs[job] = tool;
                    }
                    else if (tool.TryGetScore(new SkillJob(job.def), out var result2) && result2 != 0)
                    {
                        cachedToolsByJobs[job] = tool;
                    }
                    else
                    {
                        Log.Message("2 Null: " + job + " - " + tool);
                        cachedToolsByJobs[job] = null;
                    }
                }
            }
            else
            {
                Log.Error(pawn + " couldn't equip " + tool);
            }
        }

        private static bool IsUsingTool(this Pawn pawn)
        {
            var job = pawn.CurJob;
            var eq = pawn.equipment?.Primary;
            if (job != null && eq != null)
            {
                if (!cachedToolsByJobs.TryGetValue(job, out var toolUsed))
                {
                    var activeSkill = ToolSearchUtility.GetActiveSkill(job, pawn);
                    if (activeSkill != null && eq.TryGetScore(new SkillJob(activeSkill), out var result) && result != 0)
                    {
                        cachedToolsByJobs[job] = toolUsed = eq;
                    }
                    else if (eq.TryGetScore(new SkillJob(job.def), out var result2) && result2 != 0)
                    {
                        cachedToolsByJobs[job] = toolUsed = eq;
                    }
                    else
                    {
                        Log.Message("3 Null: " + job + " - " + toolUsed);
                        cachedToolsByJobs[job] = toolUsed = null;
                    }
                }
                return toolUsed == pawn.equipment.Primary;
            }
            return false;
        }


    }
}
