using RagnarsRokare.MobAI;
using Stateless;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RagnarsRokare.Factions
{
    internal class SitBehaviour : IDynamicBehaviour
    {
        private const string Prefix = "RR_Sit";
        private Transform m_targetSeat;
        private Vector3 m_sitOnGroundPosition;
        private Vector3 m_fireplacePosition;
        private float m_sitTimer;
        private bool m_sitting;
        protected ZSyncAnimation m_zanim;
        private bool m_attached;
        private Transform m_attachPoint;
        private Vector3 m_detachOffset;
        private string m_attachAnimation;
        private Collider[] m_attachColliders;

        private StateDef State { get; set; }

        private sealed class StateDef
        {
            private readonly string prefix;

            public string Main { get { return $"{prefix}Main"; } }
            public string SelectSeat { get { return $"{prefix}SelectSeat"; } }
            public string WalkingToChair { get { return $"{prefix}WalkingToChair"; } }
            public string WalkingToFire { get { return $"{prefix}WalkingToFire"; } }
            public string TurningToFaceFire { get { return $"{prefix}TurningToFaceFire"; } }
            public string SitOnSeat { get { return $"{prefix}SitOnSeat"; } }
            public string SitOnGround { get { return $"{prefix}SitOnGround"; } }

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
            public string WalkToChair { get { return $"{prefix}WalkToChair"; } }
            public string WalkToFire { get { return $"{prefix}WalkToFire"; } }
            public string TurnToFaceFire { get { return $"{prefix}TurnToFaceFire"; } }
            public string SitDown { get { return $"{prefix}SitDown"; } }
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
        public float SitByFireChance { get; set; } = 0.25f;

        public void Abort()
        {
            m_sitting = false;
        }

        public void Configure(MobAIBase aiBase, StateMachine<string, string> brain, string parentState)
        {
            State = new StateDef(parentState + Prefix);
            Trigger = new TriggerDef(parentState + Prefix);
            m_zanim = aiBase.Instance.GetComponent<ZSyncAnimation>();

            brain.Configure(State.Main)
               .InitialTransition(State.SelectSeat)
               .SubstateOf(parentState)
               .PermitDynamic(Trigger.Abort, () => FailState)
               .OnEntry(t =>
               {
                   aiBase.UpdateAiStatus("Sit");
                   Common.Dbgl("Entered SitBehaviour", true, "NPC");
               });

            brain.Configure(State.SelectSeat)
                .SubstateOf(State.Main)
                .Permit(Trigger.WalkToFire, State.WalkingToFire)
                .Permit(Trigger.WalkToChair, State.WalkingToChair)
                .OnEntry(t =>
                {
                    if (UnityEngine.Random.value < SitByFireChance)
                    {
                        var pos = FindPositionNearFire(aiBase);
                        if (pos.Item1 != Vector3.zero)
                        {
                            m_sitOnGroundPosition = pos.Item1;
                            m_fireplacePosition = pos.Item2;
                            brain.Fire(Trigger.WalkToFire);
                            return;
                        }
                    }
                    m_targetSeat = FindClosestSeat(aiBase);
                    brain.Fire(Trigger.WalkToChair);
                    return;
                });

            brain.Configure(State.WalkingToFire)
                .SubstateOf(State.Main)
                .Permit(Trigger.TurnToFaceFire, State.TurningToFaceFire);

            brain.Configure(State.TurningToFaceFire)
                .SubstateOf(State.Main)
                .Permit(Trigger.SitDown, State.SitOnGround);

            brain.Configure(State.WalkingToChair)
                .SubstateOf(State.Main)
                .Permit(Trigger.SitDown, State.SitOnSeat);

            brain.Configure(State.SitOnSeat)
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

            brain.Configure(State.SitOnGround)
                .SubstateOf(State.Main)
                .Permit(Trigger.StandUp, SuccessState)
                .OnEntry(t =>
                {
                    m_sitTimer = Time.time + SitTime;
                    EmoteManager.StartEmote(aiBase.NView, EmoteManager.Emotes.Sit, false);
                })
                .OnExit(() =>
                {
                    EmoteManager.StopEmote(aiBase.NView);
                });
        }

        private (Vector3, Vector3) FindPositionNearFire(MobAIBase aiBase)
        {
            var posNearFire = Vector3.zero;
            var firePos = Vector3.zero;
            var allPieces = new List<Piece>();
            Piece.GetAllPiecesInRadius(aiBase.Character.transform.position, 20f, allPieces);
            var firepit = allPieces.Where(p => p.m_comfortGroup == Piece.ComfortGroup.Fire).Where(f => IsBurning(f)).RandomOrDefault();
            bool found = false;
            for (int i = 0; i < 100 && !found; i++)
            {
                var pos = GetRandomPointInRadius(firepit.transform.position, 2f, 2.5f);
                if (Pathfinding.instance.HavePath(aiBase.Character.transform.position, pos, Pathfinding.AgentType.Humanoid) &&
                    Cover.IsUnderRoof(firepit.transform.position) == Cover.IsUnderRoof(pos))
                {
                    found = true;
                    posNearFire = pos;
                    firePos = firepit.transform.position;
                }
            }
            return (posNearFire, firePos);
        }

        private bool IsBurning(Piece f)
        {
            var fire = f.GetComponent<Fireplace>();
            return fire?.IsBurning() ?? false;
        }

        private Vector3 GetRandomPointInRadius(Vector3 center, float minRadius, float maxRadius)
        {
            float f = UnityEngine.Random.value * (float)Math.PI * 2f;
            float num = UnityEngine.Random.Range(minRadius, maxRadius);
            return center + new Vector3(Mathf.Sin(f) * num, 0f, Mathf.Cos(f) * num);
        }

        private Transform FindClosestSeat(MobAIBase aiBase)
        {
            var allPieces = new List<Piece>();
            Piece.GetAllPiecesInRadius(aiBase.Character.transform.position, 20f, allPieces);
            var freeAttachPoints = new List<Transform>();
            foreach (var piece in allPieces.Where(p => p.m_comfortGroup == Piece.ComfortGroup.Chair).OrderByDescending(p => Vector3.Distance(p.transform.position, aiBase.Character.transform.position)))
            {
                var chairAttachPoints = GetPieceAttachPoints(piece);
                foreach (var attachPoint in chairAttachPoints)
                {
                    if (!AttachManager.IsAttachPointInUse(attachPoint))
                    {
                        freeAttachPoints.Add(attachPoint);
                    }
                }
            }
            var mostComfortable = freeAttachPoints.OrderByDescending(a => Helpers.GetComfortFromNearbyPieces(a.position)).FirstOrDefault();
            return mostComfortable;
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
                if (!(bool)m_targetSeat || AttachManager.IsAttachPointInUse(m_targetSeat))
                {
                    aiBase.Brain.Fire(Trigger.Abort);
                }
                else if (aiBase.MoveAndAvoidFire(m_targetSeat.position, dt, 2.5f))
                {
                    aiBase.Brain.Fire(Trigger.SitDown);
                }
                return;
            }

            if (aiBase.Brain.IsInState(State.WalkingToFire))
            {
                if ((aiBase.Instance as MonsterAI).MoveTo(dt, m_sitOnGroundPosition, 1.0f, false))
                {
                    aiBase.Brain.Fire(Trigger.TurnToFaceFire);
                }
                return;
            }

            if (aiBase.Brain.IsInState(State.TurningToFaceFire))
            {
                if (MobAI.Common.TurnToFacePosition(aiBase, m_fireplacePosition))
                {
                    aiBase.Brain.Fire(Trigger.SitDown);
                }
            }

            if (aiBase.Brain.IsInState(State.SitOnGround))
            {
                if (Time.time > m_sitTimer)
                {
                    aiBase.Brain.Fire(Trigger.StandUp);
                }
                return;
            }

            if (aiBase.Brain.IsInState(State.SitOnSeat))
            {
                if (!m_targetSeat)
                {
                    aiBase.Brain.Fire(Trigger.Abort);
                    return;
                }

                if (Time.time > m_sitTimer)
                {
                    aiBase.Brain.Fire(Trigger.StandUp);
                }
                return;
            }
        }

        public void AttachStart(MobAIBase npc, Transform attachPoint, GameObject colliderRoot, bool hideWeapons, bool isBed, bool onShip, string attachAnimation, Vector3 detachOffset)
        {
            if (m_attached)
            {
                return;
            }
            AttachManager.RegisterAttachpoint(npc.NView.GetZDO().m_uid, attachPoint);
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
            npc.Instance.StartCoroutine(UpdateAttach(npc));
            (npc.Character as Humanoid).ResetCloth();
        }

        private IEnumerator UpdateAttach(MobAIBase npc)
        {
            while (m_attached)
            {
                yield return new WaitForFixedUpdate();
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

        public void AttachStop(MobAIBase npc)
        {
            if (m_sitting || !m_attached)
            {
                return;
            }
            if (m_detachOffset != null)
            {
                npc.Character.transform.position += m_detachOffset;
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
            AttachManager.UnregisterAttachPoint(npc.NView.GetZDO().m_uid);
            npc.Character.ResetCloth();
            npc.Instance.StopCoroutine(UpdateAttach(npc));
        }
    }
}
