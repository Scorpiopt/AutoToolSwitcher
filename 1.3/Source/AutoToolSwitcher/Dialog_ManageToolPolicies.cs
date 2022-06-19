using RimWorld;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using Verse;

namespace AutoToolSwitcher
{
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
	public class HotSwappableAttribute : Attribute
	{
	}

	[HotSwappableAttribute]
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
			num += 10;

			var nameRect = new Rect(num, 0, 130, 30);
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(nameRect, "ATS.ToolPolicyName".Translate());
			Text.Anchor = TextAnchor.UpperLeft;

			Rect rect4 = new Rect(0f, 40f, inRect.width, inRect.height - Window.CloseButSize.y).ContractedBy(10f);
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
				num += 130;
				var nameInputRect = new Rect(num, 0f, 200f, 30f);
				DoNameInputRect(nameInputRect, ref SelectedPolicy.label);

				GUI.BeginGroup(rect4);
				var searchInputRect = new Rect(0f, 0f, 200f, 30f);
				searchString = Widgets.TextField(searchInputRect, searchString);

				Rect rect5 = new Rect(0f, 0f, rect4.width, rect4.height - 10f);
				DoPolicyConfigArea(rect5);
				GUI.EndGroup();
			}
			DoTogglesAndSliders(inRect);
			Text.Font = GameFont.Small;

			Text.Font = GameFont.Small;
			var x = inRect.width / 2f - CloseButSize.x / 2f;
			var y = inRect.height - 50f;

			if (Widgets.ButtonText(new Rect(x - CloseButSize.x - 10, y, CloseButSize.x, CloseButSize.y), "ATS.ForceRefresh".Translate()))
			{
				foreach (var kvp in GameComponent_ToolTracker.Instance.trackers)
                {
					var pawn = kvp.Key;
					var tracker = kvp.Value;
					if (SelectedPolicy != null && tracker?.CurrentPolicy == SelectedPolicy && pawn.IsColonistPlayerControlled && pawn.Spawned)
                    {
						var job = WeaponSearchUtility.SearchForWeapon(pawn);
						//Log.Message("Checking: " + pawn + " - " + SelectedPolicy + " job: " + job);
						if (job != null)
                        {
							pawn.jobs.TryTakeOrderedJob(job);
                        }
                    }
                }
			}
			if (Widgets.ButtonText(new Rect(x, y, CloseButSize.x, CloseButSize.y), "CloseButton".Translate()))
			{
				Close();
			}
		}
		private void DoTogglesAndSliders(Rect rect)
        {
            if (SelectedPolicy != null)
            {
				var toggleEquipSoundRect = new Rect(rect.x + 10, rect.y + 615, 190, 24);
				Widgets.CheckboxLabeled(toggleEquipSoundRect, "ATS.ToggleEquipSound".Translate(), ref SelectedPolicy.toggleEquipSound);
				var toggleAutoMeleeRect = new Rect(toggleEquipSoundRect.x, toggleEquipSoundRect.yMax, toggleEquipSoundRect.width, toggleEquipSoundRect.height);
				Widgets.CheckboxLabeled(toggleAutoMeleeRect, "ATS.ToggleAutoMelee".Translate(), ref SelectedPolicy.toggleAutoMelee);

				var minQualitySliderLabel = new Rect(toggleEquipSoundRect.xMax + 300, toggleEquipSoundRect.y, 180, 50);
				Widgets.Label(minQualitySliderLabel, "ATS.MinQualityForWeaponsTools".Translate());
				var minQualitySliderRect = new Rect(minQualitySliderLabel.xMax, minQualitySliderLabel.y, 150, 25);
				SelectedPolicy.minQuality = (QualityCategory)Widgets.HorizontalSlider(minQualitySliderRect, (float)SelectedPolicy.minQuality, 0,
					(float)QualityCategory.Legendary, true, SelectedPolicy.minQuality.GetLabel());
			}
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

			float height = ToolCount(SelectedPolicy);
			Rect viewRect = new Rect(0f, 0f, rect3.width - 16f, height);
			Widgets.BeginScrollView(rect3, ref scrollPosition, viewRect);
			ToolPolicy selectedPolicy = SelectedPolicy;
			var num = 0;
			for (int i = 0; i < selectedPolicy.Count; i++)
			{
				if (!searchString.NullOrEmpty() && selectedPolicy[i].tool != null && !selectedPolicy[i].tool.label.ToLower().Contains(searchString.ToLower()))
				{
					continue;
				}

				Rect rect5 = new Rect(0f, (float)num * 35f, viewRect.width, 35f);
				DoEntryRow(rect5, selectedPolicy[i]);
				num++;
			}
			Widgets.EndScrollView();
		}

		public float ToolCount(ToolPolicy toolPolicy)
        {
			float num = 0;
			for (int i = 0; i < toolPolicy.Count; i++)
            {
				if (!searchString.NullOrEmpty() && toolPolicy[i].tool != null && !toolPolicy[i].tool.label.ToLower().Contains(searchString.ToLower()))
				{
					continue;
				}
				num += 35f;
			}
			return num;
		}

		public static float toolIconWidth = 27f;
		public static float test = 80;
		public static float test2 = 80;
		public static float toolNameWidth = 110;
		public static float takeToInventoryWidth = 80;
		public static float test4 = 70;
		public static float test3 = 55;

		public string searchString;


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
			Rect rect3 = new Rect(x, rect.y, takeToInventoryWidth, 50);
			Widgets.Label(rect3, "ATS.TakeAsTool".Translate());
			TooltipHandler.TipRegionByKey(rect3, "ATS.TakeAsToolDesc");

			var prevAnchor = Text.Anchor;
			Text.Anchor = TextAnchor.MiddleLeft;

			var selectAllToggleRect = new Rect(rect3.x + 20, rect3.yMax - 28, 54, 25);
			bool? selectedAll = null;
			if (SelectedPolicy.entriesInt.TrueForAll(opt => opt.takeAsTool))
            {
				selectedAll = true;
			}
			else if (SelectedPolicy.entriesInt.TrueForAll(opt => !opt.takeAsTool))
            {
				selectedAll = false;
			}

			if (selectedAll != null)
            {
				var value = selectedAll.Value;
				Widgets.CheckboxLabeled(selectAllToggleRect, "ATS.SelectAll".Translate(), ref value);
				if (value != selectedAll.Value)
                {
					SelectedPolicy.entriesInt.ForEach(opt => opt.takeAsTool = value);
				}
			}
			else
            {
				var labelRect = selectAllToggleRect;
				labelRect.width -= 24;
				var checkBoxRect = selectAllToggleRect;
				checkBoxRect.x = labelRect.xMax;
				checkBoxRect.width = 24;

				Widgets.Label(labelRect, "ATS.SelectAll".Translate());
				var state = Widgets.CheckboxMulti(checkBoxRect, MultiCheckboxState.Partial);
				if (state == MultiCheckboxState.On)
                {
					SelectedPolicy.entriesInt.ForEach(opt => opt.takeAsTool = true);
				}
				else if (state == MultiCheckboxState.Off)
                {
					SelectedPolicy.entriesInt.ForEach(opt => opt.takeAsTool = false);
				}
			}
			Text.Anchor = prevAnchor;


			x += test;
			Rect rect5 = new Rect(x, rect.y, takeToInventoryWidth, 50);
			Widgets.Label(rect5, "ATS.Equip".Translate());
			TooltipHandler.TipRegionByKey(rect5, "ATS.EquipDesc");

			Text.Anchor = TextAnchor.MiddleLeft;
			var selectAllEquipWeaponToggleRect = new Rect(rect5.x + 20, rect5.yMax - 28, 54, 25);
			bool? selectedEquipWeaponAll = null;
			if (SelectedPolicy.entriesInt.TrueForAll(opt => opt.equipAsWeapon))
			{
				selectedEquipWeaponAll = true;
			}
			else if (SelectedPolicy.entriesInt.TrueForAll(opt => !opt.equipAsWeapon))
			{
				selectedEquipWeaponAll = false;
			}

			if (selectedEquipWeaponAll != null)
			{
				var value = selectedEquipWeaponAll.Value;
				Widgets.CheckboxLabeled(selectAllEquipWeaponToggleRect, "ATS.SelectAll".Translate(), ref value);
				if (value != selectedEquipWeaponAll.Value)
				{
					SelectedPolicy.entriesInt.ForEach(opt => opt.equipAsWeapon = value);
				}
			}
			else
			{
				var labelRect = selectAllEquipWeaponToggleRect;
				labelRect.width -= 24;
				var checkBoxRect = selectAllEquipWeaponToggleRect;
				checkBoxRect.x = labelRect.xMax;
				checkBoxRect.width = 24;

				Widgets.Label(labelRect, "ATS.SelectAll".Translate());
				var state = Widgets.CheckboxMulti(checkBoxRect, MultiCheckboxState.Partial);
				if (state == MultiCheckboxState.On)
				{
					SelectedPolicy.entriesInt.ForEach(opt => opt.equipAsWeapon = true);
				}
				else if (state == MultiCheckboxState.Off)
				{
					SelectedPolicy.entriesInt.ForEach(opt => opt.equipAsWeapon = false);
				}
			}

			Text.Anchor = prevAnchor;

			x += test2;
			Rect rect6 = new Rect(x, rect.y, takeToInventoryWidth, 50);
			Widgets.Label(rect6, "ATS.EquipAsSecondaryWeapon".Translate());
			TooltipHandler.TipRegionByKey(rect6, "ATS.EquipAsSecondaryWeaponDesc");

			Text.Anchor = TextAnchor.MiddleLeft;
			var selectAllSecondaryToggleRect = new Rect(rect6.x + 20, rect6.yMax - 28, 54, 25);
			bool? selectedSecondaryAll = null;
			if (SelectedPolicy.entriesInt.TrueForAll(opt => opt.takeAsSecondary))
			{
				selectedSecondaryAll = true;
			}
			else if (SelectedPolicy.entriesInt.TrueForAll(opt => !opt.takeAsSecondary))
			{
				selectedSecondaryAll = false;
			}

			if (selectedSecondaryAll != null)
			{
				var value = selectedSecondaryAll.Value;
				Widgets.CheckboxLabeled(selectAllSecondaryToggleRect, "ATS.SelectAll".Translate(), ref value);
				if (value != selectedSecondaryAll.Value)
				{
					SelectedPolicy.entriesInt.ForEach(opt => opt.takeAsSecondary = value);
				}
			}
			else
			{
				var labelRect = selectAllSecondaryToggleRect;
				labelRect.width -= 24;
				var checkBoxRect = selectAllSecondaryToggleRect;
				checkBoxRect.x = labelRect.xMax;
				checkBoxRect.width = 24;

				Widgets.Label(labelRect, "ATS.SelectAll".Translate());
				var state = Widgets.CheckboxMulti(checkBoxRect, MultiCheckboxState.Partial);
				if (state == MultiCheckboxState.On)
				{
					SelectedPolicy.entriesInt.ForEach(opt => opt.takeAsSecondary = true);
				}
				else if (state == MultiCheckboxState.Off)
				{
					SelectedPolicy.entriesInt.ForEach(opt => opt.takeAsSecondary = false);
				}
			}

			Text.Anchor = prevAnchor;


			var selectedPolicy = SelectedPolicy;
			var firstWeaponType = new Rect(rect6.xMax + 10, rect6.y, 120, 30);
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(firstWeaponType, "ATS.FirstCombatWeaponChoice".Translate());
			Text.Anchor = TextAnchor.UpperLeft;
			if (Widgets.RadioButtonLabeled(new Rect(firstWeaponType.xMax, rect6.y, 75f, 30f), "ATS.Range".Translate(), selectedPolicy.combatMode == CombatMode.Range))
			{
				selectedPolicy.combatMode = CombatMode.Range;
			}
			else if (Widgets.RadioButtonLabeled(new Rect(firstWeaponType.xMax + 90f, rect6.y, 75f, 30f), "ATS.Melee".Translate(), selectedPolicy.combatMode == CombatMode.Melee))
			{
				selectedPolicy.combatMode = CombatMode.Melee;
			}
			else if (Widgets.RadioButtonLabeled(new Rect(firstWeaponType.xMax + 180f, rect6.y, 75f, 30f), "ATS.None".Translate(), selectedPolicy.combatMode == CombatMode.None))
			{
				selectedPolicy.combatMode = CombatMode.None;
			}
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
			TooltipHandler.TipRegion(new Rect(x, rect.y, 24, 24), "ATS.TakeAsToolDesc".Translate());

			x += test;
			Widgets.Checkbox(x, rect.y, ref entry.equipAsWeapon, 24f, paintable: true);
			TooltipHandler.TipRegion(new Rect(x, rect.y, 24, 24), "ATS.EquipDesc".Translate());

			x += test2;
			Widgets.Checkbox(x, rect.y, ref entry.takeAsSecondary, 24f, paintable: true);
			TooltipHandler.TipRegion(new Rect(x, rect.y, 24, 24), "ATS.EquipAsSecondaryWeaponDesc".Translate());

			Text.Anchor = TextAnchor.UpperLeft;
		}
	}
}
