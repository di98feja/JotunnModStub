using RagnarsRokare.MobAI;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RagnarsRokare.Factions
{
    public class Human : HumanStub
    {
		private bool m_attached;
		private string m_attachAnimation = "";
		private bool m_sleeping;
		private bool m_attachedToShip;
		private Transform m_attachPoint;
		private Vector3 m_detachOffset = Vector3.zero;
		private Collider[] m_attachColliders;

		public override void Awake()
        {
			base.Awake();
			if (NpcManager.NeedsInit(gameObject))
			{
				NpcManager.InitNpc(gameObject);
			}

			var ai = GetBaseAI() as MonsterAI;
			var mobInfo = new NpcAIConfig
			{
				Agressiveness = UnityEngine.Random.Range(1, 10),
				Awareness = UnityEngine.Random.Range(1, 10),
				Intelligence = UnityEngine.Random.Range(1, 10),
				Mobility = UnityEngine.Random.Range(1, 10),
				EatInterval = 60
			};
			Tameable tameable = Helpers.GetOrAddTameable(gameObject);
			tameable.m_commandable = true;
			string uniqueId = Helpers.GetOrCreateUniqueId(m_nview);

			try
			{
				MobManager.RegisterMob(this, uniqueId, "NpcAI", mobInfo);
			}
			catch (ArgumentException e)
			{
				Jotunn.Logger.LogError($"Failed to register Mob AI. {e.Message}");
				return;
			}
			m_faction = Character.Faction.Players;
			ai.m_consumeItems.Clear();
			ai.m_consumeItems.AddRange(CreateDropItemList(new string[] { "Raspberry", "Honey", "Blueberries" }));
			ai.m_consumeSearchRange = mobInfo.Awareness * 5;
			ai.m_randomMoveRange = mobInfo.Mobility * 2;
			ai.m_randomMoveInterval = 15 - mobInfo.Mobility;
			string givenName = m_nview?.GetZDO()?.GetString(Constants.Z_GivenName);

			Jotunn.Logger.LogInfo($"{m_name} woke up");
		}

		public static IEnumerable<ItemDrop> CreateDropItemList(IEnumerable<string> itemNames)
		{
			foreach (var itemName in itemNames)
			{
				var item = ObjectDB.instance.GetItemByName(itemName);
				if (null == item)
				{
					Jotunn.Logger.LogWarning($"Cannot find item {itemName} in objectDB");
					continue;
				}
				yield return item;
			}
		}

		public override void AttachStart(Transform attachPoint, GameObject colliderRoot, bool hideWeapons, bool isBed, bool onShip, string attachAnimation, Vector3 detachOffset)
		{
			if (m_attached)
			{
				return;
			}
			m_attached = true;
			m_attachedToShip = onShip;
			m_attachPoint = attachPoint;
			m_detachOffset = detachOffset;
			m_attachAnimation = attachAnimation;
			m_zanim.SetBool(attachAnimation, value: true);
			m_nview.GetZDO().Set("inBed", isBed);
			if (colliderRoot != null)
			{
				m_attachColliders = colliderRoot.GetComponentsInChildren<Collider>();
				ZLog.Log("Ignoring " + m_attachColliders.Length + " colliders");
				Collider[] attachColliders = m_attachColliders;
				foreach (Collider collider in attachColliders)
				{
					Physics.IgnoreCollision(m_collider, collider, ignore: true);
				}
			}
			if (hideWeapons)
			{
				HideHandItems();
			}
			UpdateAttach();
			ResetCloth();
		}

		private void UpdateAttach()
		{
			if (m_attached)
			{
				if (m_attachPoint != null)
				{
					base.transform.position = m_attachPoint.position;
					base.transform.rotation = m_attachPoint.rotation;
					Rigidbody componentInParent = m_attachPoint.GetComponentInParent<Rigidbody>();
					m_body.useGravity = false;
					m_body.velocity = (componentInParent ? componentInParent.GetPointVelocity(base.transform.position) : Vector3.zero);
					m_body.angularVelocity = Vector3.zero;
					m_maxAirAltitude = base.transform.position.y;
				}
				else
				{
					AttachStop();
				}
			}
		}

		public override bool IsAttached()
		{
			return m_attached;
		}

		public override bool IsAttachedToShip()
		{
			if (m_attached)
			{
				return m_attachedToShip;
			}
			return false;
		}

		public override bool InBed()
		{
			if (!m_nview.IsValid())
			{
				return false;
			}
			return m_nview.GetZDO().GetBool("inBed");
		}

	}
}
