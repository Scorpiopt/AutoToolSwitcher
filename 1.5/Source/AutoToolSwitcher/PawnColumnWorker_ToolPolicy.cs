using RimWorld;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace AutoToolSwitcher
{
	public class PawnColumnWorker_ToolPolicy : PawnColumnWorker
	{
		private const int TopAreaHeight = 65;

		public const int ManageToolPoliciesButtonHeight = 32;

		public override void DoHeader(Rect rect, PawnTable table)
		{
			base.DoHeader(rect, table);
			MouseoverSounds.DoRegion(rect);
			Rect rect2 = new Rect(rect.x, rect.y + (rect.height - 65f), Mathf.Min(rect.width, 360f), 32f);
			if (Widgets.ButtonText(rect2, "ATS.ManageToolPolicies".Translate()))
			{
				Find.WindowStack.Add(new Dialog_ManageToolPolicies(null));
			}
			UIHighlighter.HighlightOpportunity(rect2, "ATS.ManageToolPolicies");
			UIHighlighter.HighlightOpportunity(rect2, "ATS.ButtonAssignTools");
		}

		public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
		{
			if (pawn.GetPawnToolTracker() != null)
			{
				ToolPolicyUIUtility.DoAssignToolPolicyButtons(rect, pawn);
			}
		}

		public override int GetMinWidth(PawnTable table)
		{
			return Mathf.Max(base.GetMinWidth(table), Mathf.CeilToInt(194f));
		}

		public override int GetOptimalWidth(PawnTable table)
		{
			return Mathf.Clamp(Mathf.CeilToInt(251f), GetMinWidth(table), GetMaxWidth(table));
		}

		public override int GetMinHeaderHeight(PawnTable table)
		{
			return Mathf.Max(base.GetMinHeaderHeight(table), 65);
		}

		public override int Compare(Pawn a, Pawn b)
		{
			return GetValueToCompare(a).CompareTo(GetValueToCompare(b));
		}

		private int GetValueToCompare(Pawn pawn)
		{
			var tracker = pawn.GetPawnToolTracker();
			if (tracker != null && tracker.CurrentPolicy != null)
			{
				return tracker.CurrentPolicy.uniqueId;
			}
			return int.MinValue;
		}
	}
}
