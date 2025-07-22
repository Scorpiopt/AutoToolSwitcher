using RimWorld;
using Verse.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
namespace AutoToolSwitcher
{
    public static class WeaponSearchUtility
    {
        public static bool CanLookForWeapon(this Pawn pawn)
        {
            if (pawn.Map == null || pawn.equipment == null || pawn.Faction != Faction.OfPlayer || pawn.IsQuestLodger()
                || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) || pawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                return false;
            }
            if (ModCompatUtility.combatExtendedLoaded)
            {
                ThingWithComps primary = pawn.equipment.Primary;
                if (ModCompatUtility.HasActiveInCELoadout(pawn, primary, out bool hasActiveWeaponsInLoadout))
                {
                    return false;
                }
                if (hasActiveWeaponsInLoadout)
                {
                    return false;
                }
            }
            return true;
        }
        public static ThingWithComps PickBestWeapon(Pawn pawn, Func<Pawn, ThingWithComps, bool> validator)
        {
            ThingWithComps thing = null;
            float maxValue = 0f;
            List<ThingWithComps> weapons = pawn.inventory.innerContainer.InnerListForReading.OfType<ThingWithComps>().Where(x => validator(pawn, x)).ToList();
            for (int j = 0; j < weapons.Count; j++)
            {
                ThingWithComps weapon = weapons[j];
                if (!weapon.IsBurning() && (!CompBiocodable.IsBiocoded(weapon) || CompBiocodable.IsBiocodedFor(weapon, pawn))
                        && EquipmentUtility.CanEquip(weapon, pawn) && pawn.CanReserveAndReach(weapon, PathEndMode.OnCell, pawn.NormalMaxDanger()))
                {
                    ToolPolicy policy = pawn.GetCurrentToolPolicy();
                    float weaponScore = WeaponScoreGain(weapon);
                    if (!(weaponScore < 0.05f) && !(weaponScore < maxValue))
                    {
                        thing = weapon;
                        maxValue = weaponScore;
                    }
                }
            }
            return thing;
        }
        public static bool MainWeaponValidator(Thing t, Pawn pawn, bool isBrawler, ToolPolicy policy)
        {
            if (!t.def.IsWeapon)
            {
                //Log.Message("False 1");
                return false;
            }
            if (t.IsForbidden(pawn))
            {
                //Log.Message("False 2");

                return false;
            }
            if (isBrawler && t.def.IsRangedWeapon)
            {
                //Log.Message("False 3");

                return false;
            }
            if (t.def.weaponTags != null && t.def.weaponTags.Where(x => x.ToLower().Contains("grenade")).Any())
            {
                //Log.Message("False 4");

                return false;
            }
            if (t.def.IsRangedWeapon && t.def.Verbs.Where(x => x.verbClass == typeof(Verb_ShootOneUse)).Any())
            {
                //Log.Message("False 5");

                return false;
            }

            if (ToolSearchUtility.fireExtinguishers.Contains(t.def))
            {
                //Log.Message("False 6");

                return false;
            }
            if (ModCompatUtility.combatExtendedLoaded && !ModCompatUtility.IsUsableForCE(pawn, t))
            {
                //Log.Message("False 7");

                return false;
            }
            if (!ToolSearchUtility.baseEquipmentValidator(pawn, t, policy))
            {
                //Log.Message("False 8");

                return false;
            }
            return true;
        }

        public static bool PreferabilityValidator(bool preferRanged, Thing t)
        {
            return (!preferRanged && t.def.IsMeleeWeapon) || (preferRanged && t.def.IsRangedWeapon);
        }
        public static Thing PickBestWeaponFor(List<Thing> allWeapons, Pawn pawn)
        {
            ThingWithComps primary = pawn.equipment?.Primary;
            bool isBrawler = pawn.story?.traits?.HasTrait(TraitDefOf.Brawler) ?? false;
            bool preferRanged = !isBrawler;
            ToolPolicy policy = pawn.GetCurrentToolPolicy();
            Thing thing = null;
            float maxValue = 0f;
            Dictionary<Thing, float> weaponsByScores = new Dictionary<Thing, float>();
            for (int j = 0; j < allWeapons.Count; j++)
            {
                Thing weapon = allWeapons[j];
                //Log.Message("Checking weapon: " + weapon + " - " + (policyEntry.equipAsWeapon || policyEntry.takeAsSecondary)
                //	+ " - " + MainWeaponValidator(weapon, pawn, isBrawler, policy) + " policy: " + policy.label);
                if (MainWeaponValidator(weapon, pawn, isBrawler, policy)
                    && !weapon.IsBurning() && (!CompBiocodable.IsBiocoded(weapon) || CompBiocodable.IsBiocodedFor(weapon, pawn))
                    && EquipmentUtility.CanEquip(weapon, pawn))
                {
                    float weaponScore = WeaponScoreGain(weapon);
                    //Log.Message("Can look into " + weapon + " - score: " + weaponScore + " - maxValue: " + maxValue + " - PreferabilityValidator: " + PreferabilityValidator(preferRanged, weapon));
                    weaponsByScores[weapon] = weaponScore;
                    if (weaponScore > maxValue && PreferabilityValidator(preferRanged, weapon))
                    {
                        thing = weapon;
                        maxValue = weaponScore;
                    }
                }
            }
            return thing;
        }

        private static readonly Dictionary<Thing, float> cachedResults = new Dictionary<Thing, float>();
        public static float WeaponScoreGain(this Thing weapon)
        {
            if (!cachedResults.TryGetValue(weapon, out float result))
            {
                cachedResults[weapon] = result = WeaponScoreGainInt(weapon);
            }
            return result;
        }
        private static float WeaponScoreGainInt(Thing weapon)
        {
            if (weapon.def.IsRangedWeapon)
            {
                VerbProperties verbProperties = weapon.def.Verbs.FirstOrDefault(x => x.range > 0
                && x.defaultProjectile?.projectile != null);
                if (verbProperties != null)
                {
                    double num = verbProperties.defaultProjectile.projectile.GetDamageAmount(weapon, null)
                        * (float)verbProperties.burstShotCount;
                    float num2 = (StatExtension.GetStatValue(weapon, StatDefOf.RangedWeapon_Cooldown, true)
                        + verbProperties.warmupTime) * 60f;
                    float num3 = verbProperties.burstShotCount * verbProperties.ticksBetweenBurstShots;
                    float num4 = (num2 + num3) / 60f;
                    float dps = (float)Math.Round(num / num4, 2);
                    float accuracy = StatExtension.GetStatValue(weapon, StatDefOf.AccuracyMedium, true) * 100f;
                    return (float)Math.Round(dps * accuracy / 100f, 1);
                }
            }
            else if (weapon.def.IsMeleeWeapon)
            {
                return StatExtension.GetStatValue(weapon, StatDefOf.MeleeWeapon_AverageDPS, true);
            }
            return 0f;
        }

        public static Job SearchForWeapon(Pawn pawn)
        {
            if (pawn.CanLookForWeapon())
            {
                List<Thing> allWeapons = new List<Thing>();
                allWeapons.AddRange(pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon));
                allWeapons.AddRange(pawn.inventory.innerContainer.Where(x => x.def.IsWeapon).ToList());
                ThingWithComps primary = pawn.equipment?.Primary;
                if (primary != null)
                {
                    allWeapons.Add(primary);
                }
                Thing weapon = PickBestWeaponFor(allWeapons, pawn);
                if (weapon != null && weapon.def != pawn.equipment.Primary?.def)
                {
                    if (pawn.inventory.innerContainer.Contains(weapon))
                    {
                        HarmonyPatches.EquipTool(pawn, weapon as ThingWithComps);
                        return null;
                    }
                    else
                    {
                        return JobMaker.MakeJob(JobDefOf.Equip, weapon);
                    }
                }
            }
            return null;
        }

        private static Job HaulTool(Pawn pawn, Thing thing)
        {
            if (thing.holdingOwner.TryDrop(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near, 1, out Thing droppedThing))
            {
                if (droppedThing != null)
                {
                    return HaulAIUtility.HaulToStorageJob(pawn, droppedThing, false);
                }
            }
            return null;
        }
    }
}
