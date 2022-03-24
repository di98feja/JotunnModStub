using Jotunn.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RagnarsRokare.Factions.Managers
{
    internal static class LocationsManager
    {
        public static void SetupNpcLocations()
        {
            var woodhouse1 = ZoneManager.Instance.GetZoneLocation("WoodHouse1");
            NpcManager.CreateRandomizedNpc(woodhouse1.m_prefab.transform, new UnityEngine.Vector3(1f,0f,0f));
            
        }
    }
}
