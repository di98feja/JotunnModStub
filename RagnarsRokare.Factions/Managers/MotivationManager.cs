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
        internal static float CalculateMotivation(ZDO zdo, int confortLevel)
        {
            float sum = 0f + confortLevel;

            return Math.Max(sum, 0f);
        }
    }
}
