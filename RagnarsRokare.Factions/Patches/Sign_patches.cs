using HarmonyLib;
using RagnarsRokare.MobAI;
using System;
using System.Linq;
using UnityEngine;

namespace RagnarsRokare.Factions.Patches
{
    public static class Sign_patches
    {
        [HarmonyPatch(typeof(Sign), nameof(Sign.GetHoverText))]
        static class Sign_GetHoverText_Patch
        {
            static void Postfix(Sign __instance, ref string __result)
            {
                if (!PrivateArea.CheckAccess(__instance.transform.position, 0f, flash: false))
                {
                    return;
                }

                __result += Localization.instance.Localize($"\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] Update from container");
            }
        }

        internal static void UpdateSignFromContainer()
        {
            var hoveringCollider = Player.m_localPlayer.m_hovering;
            if (!(bool)hoveringCollider) return;

            var sign = hoveringCollider.GetComponentInParent<Sign>();
            if (!(bool)sign) return;

            var container = Common.FindClosestContainer(sign.transform.position, 1.5f);
            if (!(bool)container) return;

            var inventory = string.Join(",", container.GetInventory().GetAllItems().Select(i => i.m_shared.m_name).Distinct());
            string translatedList = Localization.instance.Localize(inventory);
            sign.SetText(translatedList.Substring(0, Math.Min(translatedList.Length, 50)));
        }


        [HarmonyPatch(typeof(Sign), nameof(Sign.Interact))]
        static class Sign_Interact_Patch
        {
            static bool Prefix(Sign __instance, Humanoid character, bool hold, bool alt)
            {
                if (!PrivateArea.CheckAccess(__instance.transform.position, 0f, flash: false))
                {
                    return false;
                }
                if (alt)
                {
                    Common.Dbgl($"UpdateSignFormContainer command", true, "");
                    UpdateSignFromContainer();
                    return false;
                }
                return true;
            }
        }

    }
}
