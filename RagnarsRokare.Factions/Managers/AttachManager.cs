using System;
using System.Collections.Generic;
using UnityEngine;

namespace RagnarsRokare.Factions
{
    public static class AttachManager
    {
        public static void Init()
        {
            On.Character.GetRelativePosition += Character_GetRelativePosition;
        }

        private static Dictionary<ZDOID, Transform> m_attachPoints = new Dictionary<ZDOID, Transform>();

        public static void RegisterAttachpoint(ZDOID zdoId, Transform attachPoint)
        {
            if (m_attachPoints.ContainsKey(zdoId))
            {
                m_attachPoints.Remove(zdoId);
            }
            if (m_attachPoints.ContainsValue(attachPoint))
            {
                throw new Exception("Attachpoint in use by other npc");
            }
            m_attachPoints.Add(zdoId, attachPoint);
        }

        public static bool IsAttachPointInUse(Transform attachPoint)
        {
            return m_attachPoints.ContainsValue(attachPoint);
        }

        public static Transform GetAttachPoint(ZDOID zdoid)
        {
            return m_attachPoints[zdoid];
        }

        public static bool IsAttached(ZDOID zdoid)
        {
            return m_attachPoints.ContainsKey(zdoid);
        }

        public static void UnregisterAttachPoint(ZDOID zdoid)
        {
            if (m_attachPoints.ContainsKey(zdoid))
            {
                m_attachPoints.Remove(zdoid);
            }
        }

        private static bool Character_GetRelativePosition(On.Character.orig_GetRelativePosition orig, Character self, out ZDOID parent, out string attachJoint, out UnityEngine.Vector3 relativePos, out UnityEngine.Vector3 relativeVel)
        {
            if (self.gameObject.name.Contains("NPC") && IsAttached(GetZdoId(self)))
            {
                var attachPoint = m_attachPoints[GetZdoId(self)];
                ZNetView componentInParent = attachPoint.GetComponentInParent<ZNetView>();
                if ((bool)componentInParent && componentInParent.IsValid())
                {
                    parent = componentInParent.GetZDO().m_uid;
                    if (componentInParent.GetComponent<Character>() != null)
                    {
                        attachJoint = attachPoint.name;
                        relativePos = Vector3.zero;
                    }
                    else
                    {
                        attachJoint = "";
                        relativePos = componentInParent.transform.InverseTransformPoint(self.transform.position);
                    }
                    relativeVel = Vector3.zero;
                    return true;
                }
            }
            return orig(self, out parent, out attachJoint, out relativePos, out relativeVel);
        }

        private static ZDOID GetZdoId(Character self)
        {
            var nview = self.GetComponent<ZNetView>();
            return nview.GetZDO().m_uid;
        }
    }
}
