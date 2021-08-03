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
                var list = __result.ToList();
                var skill = ToolSearchUtility.GetActiveSkill(list);
                if (skill != null)
                {
                    if (pawn.Map != null)
                    {
                        var tool = ToolSearchUtility.FindToolFor(pawn, skill, out var toolAction);
                        if (tool != null)
                        {
                            Toil goToTool = new Toil();
                            goToTool.initAction = delegate
                            {
                                pawn.pather.StartPath(tool, PathEndMode.OnCell);
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
                    else // if a save was reloaded, pawn map will be null when setting up jobdriver. in this case we insert tool finder inside actions. And we can't use job target fields because they might be filled
                    {
                        Log.Message(Scribe.mode + " - " + Current.ProgramState);
                        Toil goToTool = new Toil();
                        goToTool.initAction = delegate
                        {
                            var tool = ToolSearchUtility.FindToolFor(pawn, skill, out var toolAction);
                            if (tool != null)
                            {
                                cachedToolsByJobs[pawn.CurJob] = tool; // we do that so we retrieve this tool later rather than finding it again
                                if (toolAction == ToolAction.GoAndEquipTool)
                                {
                                    pawn.pather.StartPath(tool, PathEndMode.OnCell);
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
                                cachedToolsByJobs[pawn.CurJob] = tool = ToolSearchUtility.FindToolFor(pawn, skill, out var toolAction);
                            }
                            return tool != null && tool.Spawned && tool.Map == pawn.Map ? JobCondition.Ongoing : JobCondition.Incompletable;
                        });
                        Toil equipTool = new Toil();
                        equipTool.initAction = delegate
                        {
                            if (!cachedToolsByJobs.TryGetValue(pawn.CurJob, out var tool))
                            {
                                cachedToolsByJobs[pawn.CurJob] = tool = ToolSearchUtility.FindToolFor(pawn, skill, out var toolAction);
                            }
                            if (tool != null)
                            {
                                EquipTool(pawn, tool);
                            }
                        };
                        list.Insert(0, goToTool);
                        list.Insert(1, equipTool);
                    } 
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

    }
}
