using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace AutoToolSwitcher
{
	[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
	public static class Pawn_GetGizmos_Patch
	{
		public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, Pawn __instance)
		{
			foreach (var gizmo in gizmos)
				yield return gizmo;
			if (__instance.IsColonistPlayerControlled)
			{
				var weapons = __instance.inventory.innerContainer.Where(x => x.def.IsWeapon);
				foreach (var weapon in weapons.OfType<ThingWithComps>())
				{
					var gizmo = new Command_Action
					{
						defaultLabel = "Equip".Translate(weapon.def.label),
						defaultDesc = weapon.DescriptionDetailed,
						icon = weapon.def.uiIcon,
						action = () => HarmonyPatches.EquipTool(__instance, weapon)
					};
					yield return gizmo;
				}
			}
		}
	}
}