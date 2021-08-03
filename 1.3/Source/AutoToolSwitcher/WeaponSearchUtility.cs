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
    public static class WeaponSearchUtility
    {
		public static bool CanLookForWeapon(Pawn pawn)
        {
			if (pawn.Map == null || pawn.equipment == null || pawn.Faction != Faction.OfPlayer || pawn.IsQuestLodger()
				|| !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) || pawn.WorkTagIsDisabled(WorkTags.Violent))
			{
				return false;
			}
			return true;
		}
		public static ThingWithComps PickBestMeleeWeaponFor(Pawn pawn)
        {
			Predicate<ThingWithComps> validator = delegate (ThingWithComps t)
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

			ThingWithComps thing = null;
			float maxValue = 0f;
			List<ThingWithComps> weapons = pawn.inventory.innerContainer.InnerListForReading.OfType<ThingWithComps>().Where(x => validator(x)).ToList();
			for (int j = 0; j < weapons.Count; j++)
			{
				ThingWithComps weapon = weapons[j];
				if (!weapon.IsBurning() && (!CompBiocodable.IsBiocoded(weapon) || CompBiocodable.IsBiocodedFor(weapon, pawn))
						&& EquipmentUtility.CanEquip(weapon, pawn) && pawn.CanReserveAndReach(weapon, PathEndMode.OnCell, pawn.NormalMaxDanger()))
				{
					float weaponScore = WeaponScoreGain(weapon, StatDefOf.AccuracyMedium);
					if (!(weaponScore < 0.05f) && !(weaponScore < maxValue))
					{
						thing = weapon;
						maxValue = weaponScore;
					}
				}
			}

			return thing;
		}


		public static Thing PickBestWeaponFor(Pawn pawn, out Thing secondaryWeapon)
		{
			bool isBrawler = pawn.story?.traits?.HasTrait(TraitDefOf.Brawler) ?? false;
			bool preferRanged = !isBrawler && (!pawn.equipment.Primary?.def.IsMeleeWeapon ?? false || pawn.equipment.Primary == null);
			secondaryWeapon = null;
			Predicate<Thing> validator = delegate (Thing t)
			{
				if (!t.def.IsWeapon)
				{
					return false;
				}
				if (t.IsForbidden(pawn))
				{
					return false;
				}
				if (isBrawler && t.def.IsRangedWeapon)
				{
					return false;
				}
				if (t.def.weaponTags != null && t.def.weaponTags.Where(x => x.ToLower().Contains("grenade")).Any())
				{
					return false;
				}
				if (t.def.IsRangedWeapon && t.def.Verbs.Where(x => x.verbClass == typeof(Verb_ShootOneUse)).Any())
				{
					return false;
				}
				return true;
			};

			Predicate<Thing> preferabilityValidator = delegate (Thing t)
			{
				if (preferRanged && t.def.IsMeleeWeapon)
				{
					return false;
				}
				if (!preferRanged && t.def.IsRangedWeapon)
				{
					return false;
				}
				return true;
			};

			Thing thing = null;
			float maxValue = 0f;
			List<Thing> list = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon).Where(x => validator(x)).ToList();

			List<Thing> weapons = pawn.inventory.innerContainer.InnerListForReading.Where(x => validator(x)).ToList();
			list.AddRange(weapons);
			if (pawn.equipment.Primary != null)
			{
				list.Add(pawn.equipment.Primary);
			}
			Dictionary<Thing, float> weaponsByScores = new Dictionary<Thing, float>();
			for (int j = 0; j < list.Count; j++)
			{
				Thing weapon = list[j];
				if (!weapon.IsBurning() && (!CompBiocodable.IsBiocoded(weapon) || CompBiocodable.IsBiocodedFor(weapon, pawn))
						&& EquipmentUtility.CanEquip(weapon, pawn) && pawn.CanReserveAndReach(weapon, PathEndMode.OnCell, pawn.NormalMaxDanger()))
				{
					float weaponScore = WeaponScoreGain(weapon, StatDefOf.AccuracyMedium);
					weaponsByScores[weapon] = weaponScore;
					if (!(weaponScore < 0.05f) && !(weaponScore < maxValue) && preferabilityValidator(weapon))
					{
						thing = weapon;
						maxValue = weaponScore;
					}
				}
			}

			if (thing != null)
            {
				secondaryWeapon = weaponsByScores.OrderByDescending(x => x.Value).FirstOrDefault(x => x.Key.def.IsRangedWeapon != thing.def.IsRangedWeapon).Key;
			}
			return thing;
		}
		public static float WeaponScoreGain(Thing weapon)
		{
			if (weapon?.def != null)
			{
				if (weapon.def.IsRangedWeapon)
				{
					var verbProperties = weapon.def.Verbs?.Where(x => x.range > 0).FirstOrDefault();
					if (verbProperties?.defaultProjectile?.projectile != null)
					{
						double num = (verbProperties.defaultProjectile.projectile.GetDamageAmount(weapon, null) * (float)verbProperties.burstShotCount);
						float num2 = (StatExtension.GetStatValue(weapon, StatDefOf.RangedWeapon_Cooldown, true) + verbProperties.warmupTime) * 60f;
						float num3 = (verbProperties.burstShotCount * verbProperties.ticksBetweenBurstShots);
						float num4 = (num2 + num3) / 60f;
						var dps = (float)Math.Round(num / num4, 2);
						return (float)Math.Round(dps, 1);
					}
				}
				else if (weapon.def.IsMeleeWeapon)
				{
					return StatExtension.GetStatValue(weapon, StatDefOf.MeleeWeapon_AverageDPS, true);
				}
			}
			return 0f;
		}
		private static float WeaponScoreGain(Thing weapon, StatDef accuracyDef)
		{
			if (weapon.def.IsRangedWeapon)
			{
				var verbProperties = weapon.def.Verbs.Where(x => x.range > 0).First();
				double num = (verbProperties.defaultProjectile.projectile.GetDamageAmount(weapon, null) * (float)verbProperties.burstShotCount);
				float num2 = (StatExtension.GetStatValue(weapon, StatDefOf.RangedWeapon_Cooldown, true) + verbProperties.warmupTime) * 60f;
				float num3 = (verbProperties.burstShotCount * verbProperties.ticksBetweenBurstShots);
				float num4 = (num2 + num3) / 60f;
				var dps = (float)Math.Round(num / num4, 2);
				var accuracy = StatExtension.GetStatValue(weapon, accuracyDef, true) * 100f;
				return (float)Math.Round(dps * accuracy / 100f, 1);
			}
			else if (weapon.def.IsMeleeWeapon)
			{
				return StatExtension.GetStatValue(weapon, StatDefOf.MeleeWeapon_AverageDPS, true);
			}
			return 0f;
		}
	}
}
