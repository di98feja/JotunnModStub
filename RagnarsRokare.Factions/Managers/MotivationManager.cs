using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RagnarsRokare.Factions
{
    internal static class MotivationManager
    {
        internal static float CalculateMotivation(ZDO zdo, int comfortLevel)
        {
            float sum = 0f + comfortLevel;

            return Math.Max(sum, 0f);
        }
    }
}
