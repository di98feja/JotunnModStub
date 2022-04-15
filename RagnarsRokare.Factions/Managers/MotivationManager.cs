using System;

namespace RagnarsRokare.Factions
{
    internal static class MotivationManager
    {
        internal static float CalculateMotivation(ZDO zdo, int comfortLevel)
        {
            float sum = 0f + comfortLevel;

            return Math.Max(sum, 0f);
        }

        public static float GetMotivation(ZDO npcZdo)
        {
            return npcZdo.GetFloat(Misc.Constants.Z_MotivationLevel);
        }
    }
}
