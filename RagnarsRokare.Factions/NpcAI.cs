using RagnarsRokare.MobAI;
using Stateless;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RagnarsRokare.Factions
{
    public class NpcAI : MobAI.MobAIBase, IMobAIType
    {
        // Timers
        private float m_calculateComfortTimer;
        private float m_dynamicBehaviourTimer;

        // Settings
        public float FleeTimeout { get; private set; } = 10f;
        public float DynamicBehaviourTime { get; set; } = 20f;
        // Behaviours
        private IDynamicBehaviour m_dynamicBehaviour;
        private ApathyBehaviour m_apathyBehaviour;
        private HopelessBehaviour m_hopelessBehaviour;

        // Triggers
        readonly StateMachine<string, string>.TriggerWithParameters<float> UpdateTrigger;

        private Vector3 m_startPosition;
        public MaxStack<Container> m_containers;
        readonly NpcAIConfig m_config;
        private Animator m_animator;

        private string m_emoteState = "";
        private int m_emoteID;
        public const float CalculateComfortLevelInterval = 10f;

        public class State
        {
            public const string Follow = "Follow";
            public const string Fight = "Fight";
            public const string Flee = "Flee";
            public const string Sorting = "Sorting";
            public const string SearchForItems = "SearchForItems";
            public const string Root = "Root";
            public const string Hungry = "Hungry";
            public const string DynamicBehaviour = "DynamicBehaviour";
        }

        private class Trigger
        {
            public const string Update = "Update";
            public const string TakeDamage = "TakeDamage";
            public const string Hungry = "Hungry";
            public const string SearchForItems = "SearchForItems";
            public const string Fight = "Fight";
            public const string Follow = "Follow";
            public const string ChangeDynamicBehaviour = "ChangeDynamicBehaviour";
            public const string StartDynamicBehaviour = "StartDynamicBehaviour";
            public const string StopDynamicBehaviour = "StopDynamicBehaviour";
        }

        public int ComfortLevel {get; set;} = 0;

        public float MotivationLevel 
        { 
            get
            {
                return NView.GetZDO().GetFloat(Misc.Constants.Z_MotivationLevel);
            }
            set
            {
                if (!NView.IsOwner() || !NView.IsValid()) return;
                NView.GetZDO().Set(Misc.Constants.Z_MotivationLevel, value);
            }
        }


        public bool HasBed()
        {
            return NView.GetZDO().GetZDOID(Misc.Constants.Z_NpcBedOwnerId) != ZDOID.None;
        }

        /// <summary>
        /// Used by MobAILib.MobManager to find MobAIInfo
        /// </summary>
        public NpcAI() : base()
        { }

        public NpcAI(MonsterAI vanillaAI, NpcAIConfig config) : base(vanillaAI, State.Root, config)
        {
            UpdateTrigger = Brain.SetTriggerParameters<float>(Trigger.Update);
            KnownContainers = new MaxStack<Container>(Intelligence);
            AcceptedContainerNames = new string[] { "piece_chest_wood" };
            m_config = config;
            m_animator = Character.GetComponentInChildren<Animator>();

            m_hopelessBehaviour = new HopelessBehaviour();
            m_hopelessBehaviour.SuccessState = State.Root;
            m_hopelessBehaviour.FailState = State.Root;
            m_hopelessBehaviour.Configure(this, Brain, State.DynamicBehaviour);

            m_apathyBehaviour = new ApathyBehaviour();
            m_apathyBehaviour.SuccessState = State.Root;
            m_apathyBehaviour.FailState = State.Root;
            m_apathyBehaviour.Configure(this, Brain, State.DynamicBehaviour);

            ConfigureRoot();
            ConfigureDynamicBehaviour();
        }

        private void ConfigureRoot()
        {
            Brain.Configure(State.Root)
                .InitialTransition(State.DynamicBehaviour)
                .PermitIf(Trigger.ChangeDynamicBehaviour, State.DynamicBehaviour, () => !Brain.IsInState(State.DynamicBehaviour));
        }

        private void ConfigureDynamicBehaviour()
        {
            Brain.Configure(State.DynamicBehaviour)
                .SubstateOf(State.Root)
                .PermitDynamic(Trigger.StartDynamicBehaviour, () => m_dynamicBehaviour.StartState)
                .Permit(Trigger.StopDynamicBehaviour, State.Root)
                .PermitReentry(Trigger.ChangeDynamicBehaviour)
                .OnEntry(t =>
                {
                    Jotunn.Logger.LogDebug("DynamicBehaviour.OnEntry()");
                    Debug.Log($"{Character.m_name}: Swithing Dynamicbehaviour to {m_dynamicBehaviour}.");
                    Brain.Fire(Trigger.StartDynamicBehaviour);
                });
        }

        private void SelectDynamicBehaviour(float motivation)
        {
            var selectedBehaviour = m_dynamicBehaviour;
            if (motivation < Misc.Constants.Motivation_Hopeless)
            {
                selectedBehaviour = m_apathyBehaviour;               
            }
            else if (motivation < Misc.Constants.Motivation_Hopefull)
            {
                selectedBehaviour = m_hopelessBehaviour;
            }

            if (selectedBehaviour == m_dynamicBehaviour) return;

            if (m_dynamicBehaviour != null)
            {
                var oldBehaviour = m_dynamicBehaviour;
                Jotunn.Logger.LogDebug($"{Character.m_name}: Swithing from {oldBehaviour}");
                m_dynamicBehaviour.Abort();
            }

            Jotunn.Logger.LogDebug($"{Character.m_name}: Swithing to {selectedBehaviour?.ToString() ?? "None"}");
            m_dynamicBehaviourTimer = Time.time + DynamicBehaviourTime;
            m_dynamicBehaviour = selectedBehaviour;
            Brain.Fire(Trigger.ChangeDynamicBehaviour);
            return;
        }

        public override void UpdateAI(float dt)
        {
            base.UpdateAI(dt);
            EmoteManager.UpdateEmote(NView, ref m_emoteState, ref m_emoteID, ref m_animator);

            if (Time.time > m_calculateComfortTimer || m_dynamicBehaviour == null)
            {
                var cl = CalculateComfortLevel();
                if (cl >= 0)
                {
                    ComfortLevel = cl;
                }
                m_calculateComfortTimer = Time.time + CalculateComfortLevelInterval;
                var motivation = MotivationManager.CalculateMotivation(NView.GetZDO(), ComfortLevel);
                Debug.Log($"Motivation = {motivation} ");
                if (MotivationLevel != motivation || m_dynamicBehaviour == null)
                {
                    MotivationLevel = motivation;
                    SelectDynamicBehaviour(motivation);
                }
            }
            m_dynamicBehaviour.Update(this, dt);
        }

        private int CalculateComfortLevel()
        {
            Jotunn.Logger.LogDebug($"{Character.m_name}:CalculateComfortLevel");
            var bedZDOId = NView.GetZDO().GetZDOID(Misc.Constants.Z_NpcBedOwnerId);
            if (bedZDOId == ZDOID.None) return 0; // No bed, no comfort

            var bedGO = ZNetScene.instance.FindInstance(bedZDOId);
            if (!bedGO || !bedGO.GetComponent<ZNetView>().IsValid()) return -1; // Bed has no instance, can't calculate, keep old value
            
            m_startPosition = bedGO.transform.position;

            var bed = bedGO.GetComponent<Bed>();
            
            var ownerNpcId = bed.GetOwner();
            if (ownerNpcId != NView.GetZDO().m_uid.id)
            {
                // We are no longer bound to this bed, remove reference
                NView.GetZDO().Set(Misc.Constants.Z_NpcBedOwnerId, ZDOID.None);
                return 0;
            }
            Cover.GetCoverForPoint(bed.transform.position, out var coverPercentage, out var underRoof);
            if (!underRoof) return 1; // Have beed but no roof

            if (!EffectArea.IsPointInsideArea(bed.transform.position, EffectArea.Type.Heat))
            {
                return 2; // Have roof but no fire
            }

            var comfort = Helpers.GetComfortFromNearbyPieces(bed.transform.position) + 2;
            var builder = Helpers.WhoBuiltMostPiecesNearPosition(bed.transform.position);
            if (builder != default && comfort >= Misc.Constants.Motivation_Hopeless)
            {
                var player = Player.GetPlayer(builder.builderId);
                if (player != null)
                {
                    var playerFaction = FactionManager.GetPlayerFaction(player);
                    if (StandingsManager.GetStandingTowards(NView.GetZDO(), playerFaction) == Misc.Constants.Standing_Suspicious)
                    {
                        StandingsManager.SetStandingTowards(NView.GetZDO(), playerFaction, Misc.Constants.Standing_MinimumInteraction);
                    }
                }
            }
            return comfort;
        }

        public override void Follow(Player player)
        {
            throw new NotImplementedException();
        }

        public void StartEmote(EmoteManager.Emotes emoteName, bool oneshot = true)
        {
            EmoteManager.StartEmote(NView, emoteName, oneshot);
        }

        public MobAIInfo GetMobAIInfo()
        {
            return new MobAIInfo
            {
                AIType = typeof(NpcAI),
                ConfigType = typeof(NpcAIConfig),
                Name = nameof(NpcAI)
            };
        }

        public override void GotShoutedAtBy(MobAIBase mob)
        {
            throw new NotImplementedException();
        }

        protected override void RPC_MobCommand(long sender, ZDOID playerId, string command)
        {

        }
    }
}
