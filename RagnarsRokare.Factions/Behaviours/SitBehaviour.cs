using RagnarsRokare.MobAI;
using Stateless;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RagnarsRokare.Factions
{
    internal class SitBehaviour : IDynamicBehaviour
    {
        private const string Prefix = "RR_Sit";
        private Transform m_targetSeat;
        private float m_sitTimer;
        private bool m_sitting;
        protected ZSyncAnimation m_zanim;
        private bool m_attached;
        private Transform m_attachPoint;
        private Vector3 m_detachOffset;
        private string m_attachAnimation;
        private Collider[] m_attachColliders;
        private Rigidbody m_attachRigidbody;

        private StateDef State { get; set; }

		private sealed class StateDef
		{
			private readonly string prefix;

			public string Main { get { return $"{prefix}Main"; } }
			public string WalkingToChair { get { return $"{prefix}WalkingToChair"; } }
			public string Sitting { get { return $"{prefix}Sitting"; } }

			public StateDef(string prefix)
			{
				this.prefix = prefix;
			}
		}

		private TriggerDef Trigger { get; set; }
		private sealed class TriggerDef
		{
			private readonly string prefix;

			public string Abort { get { return $"{prefix}Abort"; } }
			public string WalkToChait { get { return $"{prefix}WalkToChair"; } }
			public string SitDown { get { return $"{prefix}SitDown"; } }
			public string Sit { get { return $"{prefix}Sit"; } }
			public string StandUp { get { return $"{prefix}StandUp"; } }
			public TriggerDef(string prefix)
			{
				this.prefix = prefix;

			}
		}

		// Settings
		public string StartState => State.Main;
        public string SuccessState { get; set; }
        public string FailState { get; set; }
        public float SitTime { get; set; }

        public void Abort()
        {
			m_sitting = false;
		}

		public void Configure(MobAIBase aiBase, StateMachine<string, string> brain, string parentState)
        {
			Debug.Log($"Fixed Timestep {Time.fixedDeltaTime}");

			State = new StateDef(parentState + Prefix);
			Trigger = new TriggerDef(parentState + Prefix);
            m_zanim = aiBase.Instance.GetComponent<ZSyncAnimation>();

            brain.Configure(State.Main)
               .InitialTransition(State.WalkingToChair)
               .SubstateOf(parentState)
               .PermitDynamic(Trigger.Abort, () => FailState)
               .OnEntry(t =>
               {
                   aiBase.UpdateAiStatus("Sit");
                   Common.Dbgl("Entered SitBehaviour", true, "NPC");
               });

            brain.Configure(State.WalkingToChair)
                .SubstateOf(State.Main)
                .Permit(Trigger.SitDown, State.Sitting)
                .OnEntry(t =>
                {
					m_targetSeat = FindClosestSeat(aiBase);
                })
                .OnExit(t =>
                {
                });

            brain.Configure(State.Sitting)
                .SubstateOf(State.Main)
                .Permit(Trigger.StandUp, SuccessState)
                .OnEntry(t =>
                {
                    Debug.Log($"{aiBase.Character.GetHoverName()} sitting down");
					// Trigger sit down animation
					if (!m_targetSeat)
					{
						aiBase.Brain.Fire(Trigger.Abort);
						return; // No place to sit
					}
					m_sitTimer = Time.time + SitTime;
					var attachPoint = m_targetSeat;
					AttachStart(aiBase, attachPoint, null, hideWeapons: true, isBed: false, onShip: false, "attach_chair", new Vector3(0f, 0.5f, 0f));
				})
                .OnExit(t =>
                {
                    Jotunn.Logger.LogDebug($"{aiBase.Character.GetHoverName()} getting out of seat");
					// Trigger stand up animation
					m_sitting = false;
                    AttachStop(aiBase);
                });
        }

        private Transform FindClosestSeat(MobAIBase aiBase)
        {
			var allPieces = new List<Piece>();
			Piece.GetAllPiecesInRadius(aiBase.Character.transform.position, 20f, allPieces);
			var freeAttachPoints = new List<Transform>();
			foreach (var piece in allPieces.Where(p => p.m_comfortGroup == Piece.ComfortGroup.Chair).OrderByDescending(p => Vector3.Distance(p.transform.position, aiBase.Character.transform.position)))
            {
				var chairAttachPoints = GetPieceAttachPoints(piece);
				var allMobPositions = GetAllMobPoisitions();
				foreach (var attachPoint in chairAttachPoints)
                {
					if (!allMobPositions.Any(m => m == attachPoint.position))
                    {
						freeAttachPoints.Add(attachPoint);
                    }
                }
            }
			var mostComfortable = freeAttachPoints.OrderByDescending(a => Helpers.GetComfortFromNearbyPieces(a.position)).FirstOrDefault();
			return mostComfortable;
		}

		private IEnumerable<Vector3> GetAllMobPoisitions()
        {
			foreach (var mob in MobAI.MobManager.AliveMobs.Values)
            {
				yield return mob.Character.transform.position;
            }
			foreach (var player in ZNet.instance.GetPlayerList())
            {
				yield return player.m_position;
            }
        }

        private IEnumerable<Transform> GetPieceAttachPoints(Piece piece)
        {
			var chairs = new List<Chair>();
			chairs.AddRange(piece.GetComponents<Chair>());
			chairs.AddRange(piece.GetComponentsInChildren<Chair>());
			foreach (var chair in chairs)
			{
				yield return chair.m_attachPoint;
			}
		}

		public void Update(MobAIBase aiBase, float dt)
        {
            if (aiBase.Brain.IsInState(State.WalkingToChair))
            {
                if (aiBase.MoveAndAvoidFire(m_targetSeat.position, dt, 1.5f))
                {
                   aiBase.Brain.Fire(Trigger.SitDown);
                }
                return;
            }
            if (aiBase.Brain.IsInState(State.Sitting))
            {
                if (Time.time > m_sitTimer)
                {
                    aiBase.Brain.Fire(Trigger.StandUp);
                }
				UpdateAttach(aiBase);
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

		private float m_lastUpdate = 0f;

        private void UpdateAttach(MobAIBase npc)
		{
			if (m_attached)
			{
				if (m_attachPoint != null)
				{
					m_lastUpdate = Time.time;
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

		public void AttachStop(MobAIBase npc)
		{
			if (m_sitting || !m_attached)
			{
				return;
			}
			if (m_attachPoint != null)
			{
				npc.Character.transform.position = m_attachPoint.position;
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
			npc.Character.ResetCloth();
		}
	}
}
