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
        public static Harmony harmony;
        static HarmonyPatches()
        {
            harmony = new Harmony("AutoToolSwitcher.Mod");
            harmony.Patch(AccessTools.Method(typeof(JobGiver_OptimizeApparel), "TryGiveJob"),
                new HarmonyMethod(typeof(HarmonyPatches), nameof(TryFindGunJobPrefix)));

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

        [HarmonyPatch(typeof(PawnRenderer), "DrawEquipment")]
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
            private static void Prefix(Pawn ___pawn)
            {
                var job = ___pawn.CurJob;
                if (job != null && ___pawn.IsUsingTool())
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
            public static Predicate<ThingWithComps> meleeValidator = delegate (ThingWithComps t)
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
            public static Predicate<ThingWithComps> rangeValidator = delegate (ThingWithComps t)
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
                        if (meleeWeapon != null && meleeWeapon.WeaponScoreGain() > pawn.equipment.Primary.WeaponScoreGain())
                        {
                            EquipTool(pawn, meleeWeapon);
                        }
                    }
                }
                else
                {
                    var toolPolicy = pawn.GetCurrentToolPolicy();
                    if (toolPolicy != null)
                    {
                        if (toolPolicy.combatMode == CombatMode.Range)
                        {
                            var rangeWeapon = WeaponSearchUtility.PickBestWeapon(pawn, rangeValidator);
                            if (rangeWeapon != null && (pawn.equipment.Primary is null || (!rangeValidator(pawn.equipment.Primary)
                                || rangeWeapon.WeaponScoreGain() > pawn.equipment.Primary.WeaponScoreGain())))
                            {
                                EquipTool(pawn, rangeWeapon);
                            }
                        }
                        else if (toolPolicy.combatMode == CombatMode.Melee)
                        {
                            var meleeWeapon = WeaponSearchUtility.PickBestWeapon(pawn, meleeValidator);
                            if (meleeWeapon != null && (pawn.equipment.Primary is null || (!meleeValidator(pawn.equipment.Primary) ||
                                meleeWeapon.WeaponScoreGain() > pawn.equipment.Primary.WeaponScoreGain())))
                            {
                                EquipTool(pawn, meleeWeapon);
                            }
                        }
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
            if (pawn.CanLookForWeapon())
            {
                var weapon = WeaponSearchUtility.PickBestWeaponFor(pawn, out var secondaryWeapon);
                if (weapon != null || secondaryWeapon != null)
                {
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
                }
            }

            if (__result is null)
            {
                var toolPolicy = pawn.GetCurrentToolPolicy();
                if (toolPolicy != null)
                {
                    var primary = pawn.equipment?.Primary;
                    if (primary != null && primary.def.IsTool())
                    {
                        var curPolicy = toolPolicy[primary.def];
                        if (!curPolicy.equipAsWeapon && !curPolicy.takeAsTool 
                            && (!ModCompatUtility.combatExtendedLoaded || !ModCompatUtility.HasActiveInCELoadout(pawn, primary, out _)))
                        {
                            __result = HaulTool(pawn, primary);
                        }
                    }

                    if (__result is null && pawn.inventory != null)
                    {
                        var things = pawn.inventory.innerContainer.ToList();
                        foreach (var thing in things)
                        {
                            if (thing.def.IsTool())
                            {
                                var curPolicy = toolPolicy[thing.def];
                                if (!curPolicy.takeAsTool && (!ModCompatUtility.combatExtendedLoaded || !ModCompatUtility.HasActiveInCELoadout(pawn, thing, out _)))
                                {
                                    __result = HaulTool(pawn, thing);
                                }
                            }
                        }
                    }
                }
            }

            if (__result != null)
            {
                return false;
            }
            return true;
        }

        private static Job HaulTool(Pawn pawn, Thing thing)
        {
            Thing droppedThing;
            if (thing.holdingOwner.TryDrop(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near, 1, out droppedThing))
            {
                if (droppedThing != null)
                {
                    return HaulAIUtility.HaulToStorageJob(pawn, droppedThing);
                }
            }
            return null;
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
                if (!ignoredJobs.Contains(__instance.job.def) && (pawn?.RaceProps?.Humanlike ?? false))
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
            catch { }
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
                    if (activeSkill != null && tool.TryGetScore(new SkillJob(activeSkill, job), out var result) && result != 0)
                    {
                        cachedToolsByJobs[job] = tool;
                    }
                    else if (tool.TryGetScore(new SkillJob(activeSkill, job), out var result2) && result2 != 0)
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
                if (!cachedToolsByJobs.TryGetValue(job, out var toolUsed))
                {
                    var activeSkill = ToolSearchUtility.GetActiveSkill(job, pawn);
                    if (activeSkill != null && eq.TryGetScore(new SkillJob(activeSkill, job), out var result) && result != 0)
                    {
                        cachedToolsByJobs[job] = toolUsed = eq;
                    }
                    else if (eq.TryGetScore(new SkillJob(activeSkill, job), out var result2) && result2 != 0)
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


    }
}