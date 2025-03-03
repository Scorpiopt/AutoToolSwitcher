﻿using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace AutoToolSwitcher
{
	public enum CombatMode
	{
		Range,
		Melee,
		None
	}
	public class ToolPolicy : IExposable, ILoadReferenceable
	{
		public int uniqueId;

		public string label;

		public ToolPolicyDef sourceDef;

		public List<ToolPolicyEntry> entriesInt;
		public int Count => entriesInt.Count;

		public CombatMode combatMode = CombatMode.Range;

		public bool toggleEquipSound = true;
		public bool toggleAutoMelee = false;
		
		public ToolPolicyEntry this[int index]
		{
			get
			{
				return entriesInt[index];
			}
			set
			{
				entriesInt[index] = value;
			}
		}

		public ToolPolicyEntry this[ThingDef ToolDef]
		{
			get
			{
				for (int i = 0; i < entriesInt.Count; i++)
				{
					if (entriesInt[i].tool == ToolDef)
					{
						return entriesInt[i];
					}
				}
				throw new ArgumentException();
			}
		}

		public ToolPolicy()
		{
		}

		public ToolPolicy(int uniqueId, string label)
		{
			this.uniqueId = uniqueId;
			this.label = label;
			InitializeIfNeeded();
		}

		public bool SatisfiedBy(Thing tool)
		{
			return true;
		}
		public void InitializeIfNeeded(bool overwriteExisting = true)
		{
			if (overwriteExisting)
			{
				if (entriesInt != null)
				{
					return;
				}
				entriesInt = new List<ToolPolicyEntry>();
			}
			List<ThingDef> thingDefs = DefDatabase<ThingDef>.AllDefsListForReading;
			int i;
			for (i = 0; i < thingDefs.Count; i++)
			{
				entriesInt.RemoveAll(x => x.tool is null || x.tool.IsTool() is false);
				if (thingDefs[i].IsTool())
				{
					bool missing = !entriesInt.Any((ToolPolicyEntry x) => x.tool == thingDefs[i]);
					if (overwriteExisting || missing)
					{
						ToolPolicyEntry toolPolicyEntry = new ToolPolicyEntry();
						toolPolicyEntry.tool = thingDefs[i];
						if (missing && (this.sourceDef == ATS_DefOf.ATS_Unrestricted 
							|| this.label == ATS_DefOf.ATS_Unrestricted.LabelCap))
						{
							toolPolicyEntry.MakeUnrestrictedDefault();
						}
						entriesInt.RemoveAll(x => x.tool == thingDefs[i]);
						entriesInt.Add(toolPolicyEntry);
					}
				}
				entriesInt.SortBy((ToolPolicyEntry e) => e.tool.label);
			}
		}

		public void ExposeData()
		{
			Scribe_Values.Look(ref uniqueId, "uniqueId", 0);
			Scribe_Values.Look(ref label, "label");
			Scribe_Collections.Look(ref entriesInt, "Tools", LookMode.Deep);
			Scribe_Defs.Look(ref sourceDef, "sourceDef");
			Scribe_Values.Look(ref combatMode, "combatMode", CombatMode.Range);
			Scribe_Values.Look(ref toggleEquipSound, "toggleEquipSound", true);
			Scribe_Values.Look(ref toggleAutoMelee, "toggleAutoMelee", false);
			if (Scribe.mode == LoadSaveMode.PostLoadInit && entriesInt != null)
			{
				if (entriesInt.RemoveAll((ToolPolicyEntry x) => x == null || x.tool == null) != 0)
				{
					Log.Error("Some ToolPolicyEntries were null after loading.");
				}
				InitializeIfNeeded(overwriteExisting: false);
			}
		}

		public string GetUniqueLoadID()
		{
			return "ToolPolicy_" + label + uniqueId;
		}

		public override string ToString()
		{
			return "ToolPolicy_" + label + uniqueId;
		}
	}
}