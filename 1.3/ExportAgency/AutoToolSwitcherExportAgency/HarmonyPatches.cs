using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoToolSwitcher;
using ExportAgency;
using HarmonyLib;
using Verse;

namespace AutoToolSwitcherExportAgency
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            Log.Message("Loaded");
            Harmony harmony = new Harmony(id: "rimworld.taranchuk.exportAgencyToolSwitcher");
            harmony.Patch(original: AccessTools.Method(type: typeof(Dialog_ManageToolPolicies), name: nameof(Dialog_ManageToolPolicies.PreClose)), 
                postfix: new HarmonyMethod(methodType: typeof(HarmonyPatches), methodName: nameof(ToolPolicyDialogClosePostfix)));
            harmony.Patch(original: AccessTools.Constructor(type: typeof(ToolPolicyDatabase)), postfix: new HarmonyMethod(methodType: typeof(HarmonyPatches),
                methodName: nameof(ToolPolicyDatabasePostfix)));

            //harmony.Patch(original: AccessTools.Method(type: typeof(Dialog_ManageDrugPolicies), name: nameof(Dialog_ManageDrugPolicies.DoWindowContents)),
            //    transpiler: new HarmonyMethod(methodType: typeof(ExportAgency), methodName: nameof(DrugPolicyManageTranspiler)));
            //
            //harmony.Patch(original: AccessTools.Method(type: typeof(DrugPolicyDatabase), name: nameof(DrugPolicyDatabase.DefaultDrugPolicy)),
            //    postfix: new HarmonyMethod(methodType: typeof(ExportAgency), methodName: nameof(DefaultDrugPolicyPostfix)));

        }
        public static readonly ExportType exportType = (ExportType)4;
        public static void ToolPolicyDatabasePostfix(ToolPolicyDatabase __instance)
        {
            if (!ExportAgencyMod.Settings.dictionary.ContainsKey(key: exportType))
                return;
            __instance.AllPolicies.Clear();
            foreach (ExposableList<IExposable> li in ExportAgencyMod.Settings.dictionary[key: exportType])
            {
                ToolPolicy policy = __instance.MakeNewToolPolicy();
                int x = 0;
                for (int i = 0; i < li.Count; i++)
                    if (li[index: i].exposable is ToolPolicyEntry dpe)
                        if (dpe.tool != null && policy.Count < x - 1)
                            policy[index: x++] = dpe;
                policy.label = li.Name;
            }
        }

        public static void ToolPolicyDialogClosePostfix()
        {
            if (ExportAgencyMod.Settings.dictionary.ContainsKey(key: exportType))
                ExportAgencyMod.Settings.dictionary[key: exportType].Clear();
            
            foreach (ToolPolicy policy in Current.Game.GetComponent<GameComponent_ToolTracker>().toolPolicyDatabase.AllPolicies)
                ExportAgency.ExportAgency.Export(key: exportType,
                    list: Enumerable.Range(start: 0, count: policy.Count).Select(selector: index => policy[index: index] as IExposable), name: policy.label);
        }
    }


}
