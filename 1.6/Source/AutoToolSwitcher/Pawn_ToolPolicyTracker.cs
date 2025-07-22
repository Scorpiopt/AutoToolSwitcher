using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace AutoToolSwitcher
{
	public class Pawn_ToolPolicyTracker : IExposable
	{
		public Pawn pawn;

		private ToolPolicy curPolicy;
		public ToolPolicy CurrentPolicy
		{
			get
			{
				if (curPolicy == null)
				{
					curPolicy = GameComponent_ToolTracker.Instance.toolPolicyDatabase.DefaultToolPolicy();
				}
				return curPolicy;
			}
			set
			{
				if (curPolicy != value)
				{
					curPolicy = value;
				}
			}
		}
		public Pawn_ToolPolicyTracker()
		{

		}
		public Pawn_ToolPolicyTracker(Pawn pawn)
		{
			this.pawn = pawn;
		}

		public void ExposeData()
		{
			Scribe_References.Look(ref curPolicy, "curAssignedTools");
		}

		public bool AllowedToTakeToInventory(ThingDef thingDef)
		{
			if (!thingDef.IsTool())
			{
				return false;
			}
			ToolPolicyEntry toolPolicyEntry = CurrentPolicy[thingDef];
			if (toolPolicyEntry.takeAsTool)
			{
				return !pawn.inventory.innerContainer.Contains(thingDef);
			}
			return false;
		}
	}
}
