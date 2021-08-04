using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
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
    }

    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            var harmony = new Harmony("AutoToolSwitcher.Mod");
            harmony.Patch(AccessTools.Method(typeof(JobGiver_OptimizeApparel), "TryGiveJob"),
                new HarmonyMethod(typeof(HarmonyPatches), nameof(TryFindGunJobPrefix)));
            harmony.PatchAll();
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
        }

        //private static void DrawEquipment(Pawn pawn, Vector3 rootLoc, Rot4 pawnRotation, PawnRenderFlags flags)
        //{
        //    if (pawn.Dead || !pawn.Spawned)
        //    {
        //        return;
        //    }
        //    if (pawn.equipment == null || pawn.equipment.Primary == null)
        //    {
        //        return;
        //    }
        //    if (pawn.CurJob == null || (pawn.CurJob.def.neverShowWeapon && !IsUsingTool(pawn)))
        //    {
        //        return;
        //    }
        //}
        //
        //[HarmonyPatch(typeof(PawnRenderer), "DrawEquipment")]
        //public static class Patch_DrawEquipment
        //{
        //    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilg)
        //    {
        //        var neverShowWeaponField = AccessTools.Field(typeof(JobDef), "neverShowWeapon");
        //        var pawnField = AccessTools.Field(typeof(PawnRenderer), "pawn");
        //        var isUsingTool = AccessTools.Method(typeof(HarmonyPatches), "IsUsingTool");
        //        Label label = ilg.DefineLabel();
        //        var codes = instructions.ToList();
        //        for (var i = 0; i < codes.Count; i++)
        //        {
        //            var instr = codes[i];
        //            yield return instr;
        //            if (instr.OperandIs(neverShowWeaponField))
        //            {
        //                codes[i].labels.Add(label);
        //
        //                yield return new CodeInstruction(OpCodes.Ldarg_0);
        //                yield return new CodeInstruction(OpCodes.Ldfld, pawnField);
        //                yield return new CodeInstruction(OpCodes.Call, isUsingTool);
        //                yield return new CodeInstruction(OpCodes.And, null);
        //                yield return new CodeInstruction(OpCodes.Brfalse_S, label);
        //            }
        //        }
        //    }
        //}
        //
        //[HarmonyPatch(typeof(PawnRenderer), "CarryWeaponOpenly")]
        //public static class Patch_CarryWeaponOpenly
        //{
        //    public static void Postfix(ref bool __result, Pawn ___pawn)
        //    {
        //        Log.Message(___pawn + " - " + ___pawn.IsUsingTool());
        //        if (!__result && ___pawn.IsUsingTool())
        //        {
        //            __result = true;
        //        }
        //    }
        //}

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
                        Log.Message("DEF SWAPPED: " + def);
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

        private static bool TryFindGunJobPrefix(ref Job __result, Pawn pawn)
        {
            if (!WeaponSearchUtility.CanLookForWeapon(pawn))
            {
                Log.Message("Can't search for weapon for " + pawn);
                return true;
            }
            var weapon = WeaponSearchUtility.PickBestWeaponFor(pawn, out var secondaryWeapon);
            if (weapon == null && secondaryWeapon == null)
            {
                Log.Message("Couldn't find a weapon for " + pawn);
                return true;
            }
            if (weapon != null && weapon.def != pawn.equipment.Primary?.def)
            {
                __result = JobMaker.MakeJob(JobDefOf.Equip, weapon);
            }
            else if (secondaryWeapon != null && !pawn.inventory.innerContainer.Any(x => x.def == secondaryWeapon.def))
            {
                __result = JobMaker.MakeJob(JobDefOf.TakeInventory, secondaryWeapon);
                __result.count = 1;
            }
            if (__result != null)
            {
                Log.Message(pawn + " - TryFindGunJobPrefix: " + __result);
            }
            return false;
        }

        private static Dictionary<Job, ThingWithComps> cachedToolsByJobs = new Dictionary<Job, ThingWithComps>();
        private static void AddEquipToolToilsPostfix(ref IEnumerable<Toil> __result, JobDriver __instance)
        {
            var pawn = __instance.pawn;

            if (pawn?.RaceProps?.Humanlike ?? false)
            {
                pawn.jobs.debugLog = true;
                var list = __result.ToList();
                var skill = ToolSearchUtility.GetActiveSkill(list);
                Log.Message(pawn + " - " + __instance.job + " - " + skill);

                if (pawn.Map != null)
                {
                    ToolAction toolAction;
                    var tool = skill != null ? ToolSearchUtility.FindToolFor(pawn, skill, out toolAction) : ToolSearchUtility.FindToolFor(pawn, pawn.CurJob, out toolAction);
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
                else if (Scribe.mode == LoadSaveMode.PostLoadInit && Current.ProgramState == ProgramState.MapInitializing)
                // if a save was reloaded, pawn map will be null when setting up jobdriver. in this case we insert tool finder inside actions.
                // And we can't use job target fields because they might be filled
                {
                    Toil goToTool = new Toil();
                    goToTool.initAction = delegate
                    {
                        ToolAction toolAction;
                        var tool = skill != null ? ToolSearchUtility.FindToolFor(pawn, skill, out toolAction) : ToolSearchUtility.FindToolFor(pawn, pawn.CurJob, out toolAction);
                        if (tool != null)
                        {
                            cachedToolsByJobs[pawn.CurJob] = tool; // we do that so we retrieve this tool later rather than finding it again
                            if (toolAction == ToolAction.GoAndEquipTool)
                            {
                                if (!pawn.Reserve(tool, pawn.CurJob, 1, 1))
                                {
                                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                                }
                                else
                                {
                                    Log.Message(pawn + " starting path");
                                    pawn.pather.StartPath(tool, PathEndMode.OnCell);
                                }

                            }
                            else
                            {

                            }
                        }
                    };
                    goToTool.defaultCompleteMode = ToilCompleteMode.PatherArrival;
                
                    goToTool.AddEndCondition(delegate
                    {
                        if (!cachedToolsByJobs.TryGetValue(pawn.CurJob, out var tool))
                        {
                            ToolAction toolAction;
                            tool = skill != null ? ToolSearchUtility.FindToolFor(pawn, skill, out toolAction) : ToolSearchUtility.FindToolFor(pawn, pawn.CurJob, out toolAction);
                            cachedToolsByJobs[pawn.CurJob] = tool;
                        }
                        return tool != null && tool.Spawned && tool.Map == pawn.Map ? JobCondition.Ongoing : JobCondition.Incompletable;
                    });
                    Toil equipTool = new Toil();
                    equipTool.initAction = delegate
                    {
                        if (!cachedToolsByJobs.TryGetValue(pawn.CurJob, out var tool))
                        {
                            ToolAction toolAction;
                            tool = skill != null ? ToolSearchUtility.FindToolFor(pawn, skill, out toolAction) : ToolSearchUtility.FindToolFor(pawn, pawn.CurJob, out toolAction);
                            cachedToolsByJobs[pawn.CurJob] = tool;
                        }
                        if (tool != null)
                        {
                            EquipTool(pawn, tool);
                        }
                    };
                    list.Insert(0, goToTool);
                    list.Insert(1, equipTool);
                }
                __result = list;
            }
        }

        private static void EquipTool(Pawn pawn, ThingWithComps tool)
        {
            if (pawn.equipment.Primary != null && pawn.inventory != null)
            {
                pawn.inventory.innerContainer.TryAddOrTransfer(pawn.equipment.Primary);
            }
            Log.Message("Equipping tool: " + tool);
            if (pawn.equipment.GetDirectlyHeldThings().TryAdd(tool.SplitOff(1)))
            {
                if (tool.def.soundInteract != null)
                {
                    tool.def.soundInteract.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                }
            }
            else
            {
                Log.Error(pawn + " couldn't equip " + tool);
            }
        }

        private static bool IsUsingTool(this Pawn pawn)
        {
            return false;
            if (!cachedToolsByJobs.TryGetValue(pawn.CurJob, out var toolUsed))
            {
                var eq = pawn.equipment.Primary;
                var driver = pawn.CurJob.GetCachedDriver(pawn);
                var toils = Traverse.Create(driver).Field("toils").GetValue<List<Toil>>();
                var activeSkill = ToolSearchUtility.GetActiveSkill(toils);
                if (activeSkill != null && eq.TryGetScore(new SkillJob(activeSkill), out var result) && result != 0)
                {
                    cachedToolsByJobs[pawn.CurJob] = toolUsed = eq;
                }
                else
                {
                    cachedToolsByJobs[pawn.CurJob] = toolUsed = null;
                }
            }
            return toolUsed == pawn.equipment.Primary;
        }
    }
}
