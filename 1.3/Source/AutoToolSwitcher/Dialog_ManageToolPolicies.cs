using RimWorld;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using Verse;

namespace AutoToolSwitcher
{
	[StaticConstructorOnStartup]
	public class Dialog_ManageToolPolicies : Window
	{
		private Vector2 scrollPosition;

		private ToolPolicy selPolicy;

		private static readonly Regex ValidNameRegex = Outfit.ValidNameRegex;
		private ToolPolicy SelectedPolicy
		{
			get
			{
				return selPolicy;
			}
			set
			{
				CheckSelectedPolicyHasName();
				selPolicy = value;
			}
		}

		public override Vector2 InitialSize => new Vector2(900f, 700f);

		private void CheckSelectedPolicyHasName()
		{
			if (SelectedPolicy != null && SelectedPolicy.label.NullOrEmpty())
			{
				SelectedPolicy.label = "Unnamed";
			}
		}

		public Dialog_ManageToolPolicies(ToolPolicy selectedAssignedTools)
		{
			forcePause = true;
			doCloseX = true;
			doCloseButton = true;
			closeOnClickedOutside = false;
			absorbInputAroundWindow = false;
			SelectedPolicy = selectedAssignedTools;
		}

		public override void DoWindowContents(Rect inRect)
		{
			Text.Font = GameFont.Small;
			float num = 0f;
			Rect rect = new Rect(0f, 0f, 150f, 35f);
			num += 150f;
			if (Widgets.ButtonText(rect, "ATS.SelectToolPolicy".Translate()))
			{
				List<FloatMenuOption> list = new List<FloatMenuOption>();
				foreach (ToolPolicy allPolicy in GameComponent_ToolTracker.Instance.toolPolicyDatabase.AllPolicies)
				{
					ToolPolicy localAssignedTools = allPolicy;
					list.Add(new FloatMenuOption(localAssignedTools.label, delegate
					{
						SelectedPolicy = localAssignedTools;
					}));
				}
				Find.WindowStack.Add(new FloatMenu(list));
			}
			num += 10f;
			Rect rect2 = new Rect(num, 0f, 150f, 35f);
			num += 150f;
			if (Widgets.ButtonText(rect2, "ATS.NewToolPolicy".Translate()))
			{
				SelectedPolicy = GameComponent_ToolTracker.Instance.toolPolicyDatabase.MakeNewToolPolicy();
			}
			num += 10f;
			Rect rect3 = new Rect(num, 0f, 150f, 35f);
			num += 150f;
			if (Widgets.ButtonText(rect3, "ATS.DeleteToolPolicy".Translate()))
			{
				List<FloatMenuOption> list2 = new List<FloatMenuOption>();
				foreach (ToolPolicy allPolicy2 in GameComponent_ToolTracker.Instance.toolPolicyDatabase.AllPolicies)
				{
					ToolPolicy localAssignedTools2 = allPolicy2;
					list2.Add(new FloatMenuOption(localAssignedTools2.label, delegate
					{
						AcceptanceReport acceptanceReport = GameComponent_ToolTracker.Instance.toolPolicyDatabase.TryDelete(localAssignedTools2);
						if (!acceptanceReport.Accepted)
						{
							Messages.Message(acceptanceReport.Reason, MessageTypeDefOf.RejectInput, historical: false);
						}
						else if (localAssignedTools2 == SelectedPolicy)
						{
							SelectedPolicy = null;
						}
					}));
				}
				Find.WindowStack.Add(new FloatMenu(list2));
			}
			Rect rect4 = new Rect(0f, 40f, inRect.width, inRect.height - 40f - Window.CloseButSize.y).ContractedBy(10f);
			if (SelectedPolicy == null)
			{
				GUI.color = Color.grey;
				Text.Anchor = TextAnchor.MiddleCenter;
				Widgets.Label(rect4, "ATS.NoToolPolicySelected".Translate());
				Text.Anchor = TextAnchor.UpperLeft;
				GUI.color = Color.white;
			}
			else
			{
				GUI.BeginGroup(rect4);
				DoNameInputRect(new Rect(0f, 0f, 200f, 30f), ref SelectedPolicy.label);
				Rect rect5 = new Rect(0f, 40f, rect4.width, rect4.height - 45f - 10f);
				DoPolicyConfigArea(rect5);
				GUI.EndGroup();
			}
			Text.Font = GameFont.Small;
		}
		public override void PreClose()
		{
			base.PreClose();
			CheckSelectedPolicyHasName();
		}

		public static void DoNameInputRect(Rect rect, ref string name)
		{
			name = Widgets.TextField(rect, name, 30, ValidNameRegex);
		}

		private void DoPolicyConfigArea(Rect rect)
		{
			Rect rect2 = rect;
			rect2.height = 54f;
			Rect rect3 = rect;
			rect3.yMin = rect2.yMax;
			rect3.height -= 50f;
			Rect rect4 = rect;
			rect4.yMin = rect4.yMax - 50f;
			DoColumnLabels(rect2);
			Widgets.DrawMenuSection(rect3);
			if (SelectedPolicy.Count == 0)
			{
				GUI.color = Color.grey;
				Text.Anchor = TextAnchor.MiddleCenter;
				Widgets.Label(rect3, "ATS.NoTools".Translate());
				Text.Anchor = TextAnchor.UpperLeft;
				GUI.color = Color.white;
				return;
			}
			float height = (float)SelectedPolicy.Count * 35f;
			Rect viewRect = new Rect(0f, 0f, rect3.width - 16f, height);
			Widgets.BeginScrollView(rect3, ref scrollPosition, viewRect);
			ToolPolicy selectedPolicy = SelectedPolicy;
			for (int i = 0; i < selectedPolicy.Count; i++)
			{
				Rect rect5 = new Rect(0f, (float)i * 35f, viewRect.width, 35f);
				DoEntryRow(rect5, selectedPolicy[i]);
			}
			Widgets.EndScrollView();
		}


		public static float toolIconWidth = 27f;
		public static float test = 80;
		public static float test2 = 80;
		public static float toolNameWidth = 110;
		public static float takeToInventoryWidth = 80;
		public static float test4 = 70;
		public static float test3 = 55;
		private void DoColumnLabels(Rect rect)
		{
			rect.width -= 16f;
			float x = rect.x;
			Text.Anchor = TextAnchor.LowerCenter;
			Rect rect2 = new Rect(x + 4f, rect.y, toolNameWidth + toolIconWidth, rect.height);
			Widgets.Label(rect2, "ATS.ToolColumnLabel".Translate());
			TooltipHandler.TipRegionByKey(rect2, "ATS.ToolNameColumnDesc");
			Text.Anchor = TextAnchor.UpperCenter;

			x += toolNameWidth + toolIconWidth + test4;
			Rect rect3 = new Rect(x, rect.y, takeToInventoryWidth, rect.height);
			Widgets.Label(rect3, "ATS.TakeAsTool".Translate());
			TooltipHandler.TipRegionByKey(rect3, "ATS.TakeAsToolDesc");

			x += test;
			Rect rect5 = new Rect(x, rect.y, takeToInventoryWidth, rect.height);
			Widgets.Label(rect5, "ATS.Equip".Translate());
			TooltipHandler.TipRegionByKey(rect5, "ATS.EquipDesc");

			x += test2;
			Rect rect6 = new Rect(x, rect.y, takeToInventoryWidth, rect.height);
			Widgets.Label(rect6, "ATS.EquipAsSecondaryWeapon".Translate());
			TooltipHandler.TipRegionByKey(rect6, "ATS.EquipAsSecondaryWeaponDesc");
		}


		private void DoEntryRow(Rect rect, ToolPolicyEntry entry)
		{
			Text.Anchor = TextAnchor.MiddleLeft;
			float x = rect.x + 5f;
			float num = (rect.height - toolIconWidth) / 2f;
			Widgets.ThingIcon(new Rect(x, rect.y + num, toolIconWidth, toolIconWidth), entry.tool);
			x += toolIconWidth + 10f;
			var labelRect = new Rect(x, rect.y, toolNameWidth, rect.height);
			Widgets.Label(labelRect, entry.tool.LabelCap);
			Widgets.InfoCardButton(labelRect.xMax + 5f, rect.y + (rect.height - 24f) / 2f, entry.tool);

			x += toolNameWidth + toolIconWidth + test3;
			Widgets.Checkbox(x, rect.y, ref entry.takeAsTool, 24f, paintable: true);

			x += test;
			Widgets.Checkbox(x, rect.y, ref entry.equipAsWeapon, 24f, paintable: true);

			x += test2;
			Widgets.Checkbox(x, rect.y, ref entry.takeAsSecondary, 24f, paintable: true);

			Text.Anchor = TextAnchor.UpperLeft;
		}
	}
}
