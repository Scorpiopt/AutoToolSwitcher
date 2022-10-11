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
    public class JobDriver_BeatFireAdv : JobDriver
    {
        protected Fire TargetFire => (Fire)job.targetA.Thing;
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected bool TryFindShootingPosition(Pawn pawn, Thing target, out IntVec3 dest)
        {
            bool allowManualCastWeapons = !pawn.IsColonist;
            Verb verb = pawn.TryGetAttackVerb(target, allowManualCastWeapons);
            if (verb == null)
            {
                dest = IntVec3.Invalid;
                return false;
            }
            CastPositionRequest newReq = default(CastPositionRequest);
            newReq.caster = pawn;
            newReq.target = target;
            newReq.verb = verb;
            newReq.maxRangeFromTarget = verb.verbProps.range;
            newReq.wantCoverFromTarget = false;
            return CastPositionFinder.TryFindCastPosition(newReq, out dest);
        }

        private bool cantUseFireExt;
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            Toil beat = new Toil();
            Toil approach = new Toil();
            approach.initAction = delegate
            {
                if (base.Map.reservationManager.CanReserve(pawn, TargetFire))
                {
                    pawn.Reserve(TargetFire, job);
                }
                if (ToolSearchUtility.fireExtinguishers.Contains(pawn.equipment?.Primary?.def) && TryFindShootingPosition(pawn, TargetFire, out var dest))
                {
                    pawn.pather.StartPath(dest, PathEndMode.OnCell);
                }
                else
                {
                    pawn.pather.StartPath(TargetFire, PathEndMode.Touch);
                }
            };
            approach.tickAction = delegate
            {
                if (pawn.pather.Moving && pawn.pather.nextCell != TargetFire.Position)
                {
                    StartBeatingFireIfAnyAt(pawn.pather.nextCell, beat);
                }
                if (pawn.Position != TargetFire.Position)
                {
                    StartBeatingFireIfAnyAt(pawn.Position, beat);
                }
            };
            approach.FailOnDespawnedOrNull(TargetIndex.A);
            approach.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            approach.atomicWithPrevious = true;
            yield return approach;
            beat.tickAction = delegate
            {
                if ((!ToolSearchUtility.fireExtinguishers.Contains(pawn.equipment?.Primary?.def) || cantUseFireExt) && !pawn.CanReachImmediate(TargetFire, PathEndMode.Touch))
                {
                    JumpToToil(approach);
                }
                else if (!(pawn.Position != TargetFire.Position) || !StartBeatingFireIfAnyAt(pawn.Position, beat))
                {
                    if (!cantUseFireExt && ToolSearchUtility.fireExtinguishers.Contains(pawn.equipment?.Primary?.def))
                    {
                        var verb = pawn.TryGetAttackVerb(TargetFire);
                        if (verb != null && !verb.CanHitTargetFrom(base.pawn.Position, TargetFire))
                        {
                            JumpToToil(approach);
                        }
                        else if (!pawn.stances.FullBodyBusy && !pawn.TryStartAttack(TargetFire))
                        {
                            cantUseFireExt = true;
                            JumpToToil(approach);
                        }
                    }
                    else
                    {
                        pawn.natives.TryBeatFire(TargetFire);
                    }

                    if (TargetFire.Destroyed)
                    {
                        pawn.records.Increment(RecordDefOf.FiresExtinguished);
                        pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
                    }
                }
            };
            beat.FailOnDespawnedOrNull(TargetIndex.A);
            beat.defaultCompleteMode = ToilCompleteMode.Never;
            yield return beat;
        }

        private bool StartBeatingFireIfAnyAt(IntVec3 cell, Toil nextToil)
        {
            List<Thing> thingList = cell.GetThingList(base.Map);
            for (int i = 0; i < thingList.Count; i++)
            {
                Fire fire = thingList[i] as Fire;
                if (fire != null && fire.parent == null)
                {
                    job.targetA = fire;
                    pawn.pather.StopDead();
                    JumpToToil(nextToil);
                    return true;
                }
            }
            return false;
        }
    }
}