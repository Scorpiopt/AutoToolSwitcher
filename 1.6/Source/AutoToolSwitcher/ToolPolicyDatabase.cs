using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace AutoToolSwitcher
{
	public class ToolPolicyDatabase : IExposable
	{
		private List<ToolPolicy> policies = new List<ToolPolicy>();
		public List<ToolPolicy> AllPolicies => policies;
		public ToolPolicyDatabase()
		{
			GenerateStartingToolPolicies();
		}
		public void ExposeData()
		{
			Scribe_Collections.Look(ref policies, "policies", LookMode.Deep);
		}

		public ToolPolicy DefaultToolPolicy()
		{
			if (policies.Count == 0)
			{
				MakeNewToolPolicy();
			}
			return policies[0];
		}

		public void MakePolicyDefault(ToolPolicyDef policyDef)
		{
			if (DefaultToolPolicy().sourceDef != policyDef)
			{
				ToolPolicy ToolPolicy = policies.FirstOrDefault((ToolPolicy x) => x.sourceDef == policyDef);
				if (ToolPolicy != null)
				{
					policies.Remove(ToolPolicy);
					policies.Insert(0, ToolPolicy);
				}
			}
		}

		public AcceptanceReport TryDelete(ToolPolicy policy)
		{
			foreach (Pawn item in PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_FreeColonists_NoLodgers)
			{
				var curToolPolicy = item.GetCurrentToolPolicy();
				if (curToolPolicy != null && curToolPolicy == policy)
				{
					return new AcceptanceReport("ATS.ToolPolicyInUse".Translate(item));
				}
			}
			foreach (Pawn item2 in PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead)
			{
				var tracker = item2.GetPawnToolTracker();
				if (tracker != null && tracker.CurrentPolicy != null && tracker.CurrentPolicy == policy)
				{
					tracker.CurrentPolicy = null;
				}
			}
			policies.Remove(policy);
			return AcceptanceReport.WasAccepted;
		}

		public ToolPolicy MakeNewToolPolicy()
		{
			int uniqueId = ((!policies.Any()) ? 1 : (policies.Max((ToolPolicy o) => o.uniqueId) + 1));
			ToolPolicy toolPolicy = new ToolPolicy(uniqueId, "ATS.ToolPolicy".Translate() + " " + uniqueId.ToString());
			policies.Add(toolPolicy);
			return toolPolicy;
		}

		public ToolPolicy NewToolPolicyFromDef(ToolPolicyDef def)
		{
			ToolPolicy toolPolicy = MakeNewToolPolicy();
			toolPolicy.sourceDef = def;
			toolPolicy.label = def.LabelCap;
			if (def == ATS_DefOf.ATS_Unrestricted)
			{
				for (var i = 0; i < toolPolicy.Count; i++)
                {
                    toolPolicy[i].MakeUnrestrictedDefault();
                }
            }

			if (def.entries != null)
			{
				for (int j = 0; j < def.entries.Count; j++)
				{
					toolPolicy[def.entries[j].tool].CopyFrom(def.entries[j]);
				}
			}
			return toolPolicy;
		}

        private void GenerateStartingToolPolicies()
		{
			foreach (ToolPolicyDef allDef in DefDatabase<ToolPolicyDef>.AllDefs)
			{
				NewToolPolicyFromDef(allDef);
			}
		}
	}
}
