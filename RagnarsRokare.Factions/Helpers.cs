using UnityEngine;

namespace RagnarsRokare.Factions
{
    internal class Helpers
    {
        public static Tameable GetOrAddTameable(GameObject gameObject)
        {
            var tameable = gameObject.GetComponent<Tameable>();
            if (tameable == null)
            {
                tameable = gameObject.AddComponent<Tameable>();
            }

            return tameable;
        }
        public static string GetOrCreateUniqueId(ZNetView ___m_nview)
        {
            var uniqueId = ___m_nview.GetZDO().GetString(Constants.Z_UniqueId);
            if (string.IsNullOrEmpty(uniqueId))
            {
                uniqueId = ___m_nview.GetZDO().GetString(Constants.Z_CharacterId);
                if (string.IsNullOrEmpty(uniqueId))
                {
                    uniqueId = System.Guid.NewGuid().ToString();
                }
                ___m_nview.GetZDO().Set(Constants.Z_UniqueId, uniqueId);
            }
            return uniqueId;
        }
    }
}
