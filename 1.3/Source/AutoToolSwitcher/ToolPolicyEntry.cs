using Verse;

namespace AutoToolSwitcher
{
	public class ToolPolicyEntry : IExposable
	{
		public ThingDef tool;

		public bool takeAsTool;
		public bool equipAsWeapon;
		public bool takeAsSecondary;

		public void CopyFrom(ToolPolicyEntry other)
		{
			tool = other.tool;
			takeAsTool = other.takeAsTool;
			equipAsWeapon = other.equipAsWeapon;
			takeAsSecondary = other.takeAsSecondary;
		}
		public void ExposeData()
		{
			Scribe_Defs.Look(ref tool, "tool");
			Scribe_Values.Look(ref takeAsTool, "takeAsTool");
			Scribe_Values.Look(ref equipAsWeapon, "equipAsWeapon");
			Scribe_Values.Look(ref takeAsSecondary, "takeAsSecondary");
		}
	}
}
