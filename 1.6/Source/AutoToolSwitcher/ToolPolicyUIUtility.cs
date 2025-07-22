using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AutoToolSwitcher
{
	public static class ToolPolicyUIUtility
	{
		public static void DoAssignToolPolicyButtons(Rect rect, Pawn pawn)
		{
			int num = Mathf.FloorToInt((rect.width - 4f) * 0.714285731f);
			int num2 = Mathf.FloorToInt((rect.width - 4f) * 0.2857143f);
			float x = rect.x;
			Rect rect2 = new Rect(x, rect.y + 2f, num, rect.height - 4f);
			string text = pawn.GetCurrentToolPolicy().label;

			Widgets.Dropdown(rect2, pawn, (Pawn p) => p.GetCurrentToolPolicy(), Button_GenerateMenu, text.Truncate(rect2.width), null, pawn.GetCurrentToolPolicy().label, null, null, paintable: true);
			x += (float)num;
			x += 4f;
			Rect rect3 = new Rect(x, rect.y + 2f, num2, rect.height - 4f);
			if (Widgets.ButtonText(rect3, "AssignTabEdit".Translate()))
			{
				Find.WindowStack.Add(new Dialog_ManageToolPolicies(pawn.GetCurrentToolPolicy()));
			}
			UIHighlighter.HighlightOpportunity(rect2, "ATS.ButtonAssignTools");
			UIHighlighter.HighlightOpportunity(rect3, "ATS.ButtonAssignTools");
			x += (float)num2;
		}

		private static IEnumerable<Widgets.DropdownMenuElement<ToolPolicy>> Button_GenerateMenu(Pawn pawn)
		{
			foreach (ToolPolicy assignedTools in GameComponent_ToolTracker.Instance.toolPolicyDatabase.AllPolicies)
			{
				yield return new Widgets.DropdownMenuElement<ToolPolicy>
				{
					option = new FloatMenuOption(assignedTools.label, delegate
					{
						pawn.GetPawnToolTracker().CurrentPolicy = assignedTools;
					}),
					payload = assignedTools
				};
			}
		}
	}
}