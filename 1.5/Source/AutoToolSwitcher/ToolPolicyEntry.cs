using Verse;

namespace AutoToolSwitcher
{
	public class ToolPolicyEntry : IExposable
	{
		public ThingDef tool;
		public bool takeAsTool;

		public void CopyFrom(ToolPolicyEntry other)
		{
			tool = other.tool;
			takeAsTool = other.takeAsTool;
		}

        public void MakeUnrestrictedDefault()
        {
            if (this.tool.IsTool())
            {
                this.takeAsTool = true;
            }
        }

        public void ExposeData()
		{
			Scribe_Defs.Look(ref tool, "tool");
			Scribe_Values.Look(ref takeAsTool, "takeAsTool");
		}
	}
}
