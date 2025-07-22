using System;
using System.Collections.Generic;
using Verse;

namespace AutoToolSwitcher
{
	public static class ToolPolicyUtils
	{
		public static Pawn_ToolPolicyTracker GetPawnToolTracker(this Pawn pawn)
        {
			var trackers = GameComponent_ToolTracker.Instance.trackers;
			if (trackers.TryGetValue(pawn, out var pawn_ToolPolicyTracker))
			{
				return pawn_ToolPolicyTracker;
			}
			return null;
        }

		public static ToolPolicy GetCurrentToolPolicy(this Pawn pawn)
		{
			return pawn.GetPawnToolTracker()?.CurrentPolicy;
		}
	}
}
