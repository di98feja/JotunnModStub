using RagnarsRokare.MobAI;
using Stateless;
using UnityEngine;

namespace RagnarsRokare.Factions
{
    internal class SleepBehaviour : IDynamicBehaviour
    {
        private const string Prefix = "RR_Sleep";
        private Vector3 m_targetPosition;
        private float m_sleepTimer;
        private bool m_sleeping;
        protected ZSyncAnimation m_zanim;
        private bool m_attached;
        private Transform m_attachPoint;
        private Vector3 m_detachOffset;
        private string m_attachAnimation;
        private Collider[] m_attachColliders;
		
		private sealed class State
		{

			private readonly string name;
			private readonly int value;

			public static readonly State Main = new State("Main");
			public static readonly State WalkingToBed = new State("WalkingToBed");
			public static readonly State Sleeping = new State("Sleeping");

			private State(string name)
			{
				this.name = name;
			}

			public string ToString(string prefix)
			{
				return prefix + name;
			}

		}
        private class Trigger
        {
            public const string Abort = Prefix + "Abort";
            public const string WalkToBed = Prefix + "WalkToBed";
            public const string LieDown = Prefix + "LieDown";
            public const string Sleep = Prefix + "Sleep";
            public const string StandUp = Prefix + "StandUp";
        }

		// Settings
		public string StartState => State.Main.ToString(Prefix);
        public string SuccessState { get; set; }
        public string FailState { get; set; }
        public float SleepTime { get; set; }

        public void Abort()
        {
        }

        public void Configure(MobAIBase aiBase, StateMachine<string, string> brain, string parentState)
        {
            m_zanim = aiBase.Instance.GetComponent<ZSyncAnimation>();

            brain.Configure(State.Main.ToString())
               .InitialTransition(State.WalkingToBed.ToString(Prefix))
               .SubstateOf(parentState)
               .PermitDynamic(Trigger.Abort, () => FailState)
               .OnEntry(t =>
               {
                   aiBase.UpdateAiStatus("Sleep");
                   Common.Dbgl("Entered SleepBehaviour", true, "NPC");
               });

            brain.Configure(State.WalkingToBed.ToString(Prefix))
                .SubstateOf(State.Main.ToString(Prefix))
                .Permit(Trigger.LieDown, State.Sleeping.ToString(Prefix))
                .OnEntry(t =>
                {
                    m_targetPosition = aiBase.HomePosition;
                })
                .OnExit(t =>
                {
                });

            brain.Configure(State.Sleeping.ToString(Prefix))
                .SubstateOf(State.Main.ToString(Prefix))
                .Permit(Trigger.StandUp, SuccessState)
                .OnEntry(t =>
                {
                    Jotunn.Logger.LogDebug($"{aiBase.Character.GetHoverName()} going to sleep");
					// Trigger lay down animation
					var bedZDOId = aiBase.NView.GetZDO().GetZDOID(Misc.Constants.Z_NpcBedOwnerId);
					if (bedZDOId == ZDOID.None)
					{
						aiBase.Brain.Fire(Trigger.Abort);
						return; // No bed, no sleep
					}
					m_sleepTimer = Time.time + SleepTime;
					var bedGO = ZNetScene.instance.FindInstance(bedZDOId);

					AttachStart(aiBase, bedGO.transform, bedGO, hideWeapons: true, isBed: true, onShip: false, "attach_bed", new Vector3(0f, 0.1f, 0f));

				})
                .OnExit(t =>
                {
                    Jotunn.Logger.LogDebug($"{aiBase.Character.GetHoverName()} getting out of bed");
					// Trigger stand up animation
					m_sleeping = false;
					AttachStop(aiBase);
				});
        }

        public void Update(MobAIBase aiBase, float dt)
        {
            if (aiBase.Brain.IsInState(State.WalkingToBed.ToString(Prefix)))
            {
                if (aiBase.MoveAndAvoidFire(m_targetPosition, dt, 1.5f))
                {
                   aiBase.Brain.Fire(Trigger.LieDown);
                }
                return;
            }
            if (aiBase.Brain.IsInState(State.Sleeping.ToString(Prefix)))
            {
                if (Time.time > m_sleepTimer)
                {
                    aiBase.Brain.Fire(Trigger.StandUp);
                }
				//UpdateAttach(aiBase);
                return;
            }
        }


		public void AttachStart(MobAIBase npc, Transform attachPoint, GameObject colliderRoot, bool hideWeapons, bool isBed, bool onShip, string attachAnimation, Vector3 detachOffset)
		{
			if (m_attached)
			{
				return;
			}
			m_attached = true;
			m_attachPoint = attachPoint;
			m_detachOffset = detachOffset;
			m_attachAnimation = attachAnimation;
			m_zanim.SetBool(attachAnimation, value: true);
			npc.NView.GetZDO().Set("inBed", isBed);
			if (colliderRoot != null)
			{
				var attachColliders = colliderRoot.GetComponentsInChildren<Collider>();
				m_attachColliders = attachColliders;
				ZLog.Log("Ignoring " + attachColliders.Length + " colliders");
				var npcCollider = npc.NView.gameObject.GetComponent<CapsuleCollider>();
				foreach (Collider collider in attachColliders)
				{
					Physics.IgnoreCollision(npcCollider, collider, ignore: true);
				}
			}
			if (hideWeapons)
			{
				(npc.Character as Humanoid).HideHandItems();
			}
			UpdateAttach(npc);
			(npc.Character as Humanoid).ResetCloth();
		}

		private void UpdateAttach(MobAIBase npc)
		{
			if (m_attached)
			{
				if (m_attachPoint != null)
				{
					npc.Character.transform.position = m_attachPoint.position;
					npc.Character.transform.rotation = m_attachPoint.rotation;
					Rigidbody componentInParent = m_attachPoint.GetComponentInParent<Rigidbody>();
					npc.Character.m_body.useGravity = false;
					npc.Character.m_body.velocity = (componentInParent ? componentInParent.GetPointVelocity(npc.Character.transform.position) : Vector3.zero);
					npc.Character.m_body.angularVelocity = Vector3.zero;
					npc.Character.m_maxAirAltitude = npc.Character.transform.position.y;
				}
				else
				{
					AttachStop(npc);
				}
			}
		}

		public bool IsAttached()
		{
			return m_attached;
		}

		public bool InBed(ZNetView nview)
		{
			if (!nview.IsValid())
			{
				return false;
			}
			return nview.GetZDO().GetBool("inBed");
		}

		public void AttachStop(MobAIBase npc)
		{
			if (m_sleeping || !m_attached)
			{
				return;
			}
			if (m_attachPoint != null)
			{
				npc.Character.transform.position = m_attachPoint.TransformPoint(m_detachOffset);
			}
			if (m_attachColliders != null)
			{
				Collider[] attachColliders = m_attachColliders;
				foreach (Collider collider in attachColliders)
				{
					if ((bool)collider)
					{
						var npcCollider = npc.NView.gameObject.GetComponent<CapsuleCollider>();
						Physics.IgnoreCollision(npcCollider, collider, ignore: false);
					}
				}
				m_attachColliders = null;
			}
			npc.Character.m_body.useGravity = true;
			m_attached = false;
			m_attachPoint = null;
			m_zanim.SetBool(m_attachAnimation, value: false);
			npc.NView.GetZDO().Set("inBed", value: false);
			npc.Character.ResetCloth();
		}

	}
}
