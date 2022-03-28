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
        private float m_triggerTimer;
        private float m_stuckInIdleTimer;
        private float m_calculateComfortTimer;

        // Behaviours
        readonly EatingBehaviour eatingBehaviour;
        readonly SearchForItemsBehaviour searchForItemsBehaviour;

        // Triggers
        readonly StateMachine<string, string>.TriggerWithParameters<float> UpdateTrigger;

        private Vector3 m_startPosition;
        public MaxStack<Container> m_containers;
        readonly NpcAIConfig m_config;

        public const float CalculateComfortLevelInterval = 10f;

        public class State
        {
            public const string Idle = "Idle";
            public const string Follow = "Follow";
            public const string Fight = "Fight";
            public const string Flee = "Flee";
            public const string Sorting = "Sorting";
            public const string SearchForItems = "SearchForItems";
            public const string Root = "Root";
            public const string Hungry = "Hungry";
        }

        private class Trigger
        {
            public const string Update = "Update";
            public const string TakeDamage = "TakeDamage";
            public const string Hungry = "Hungry";
            public const string SearchForItems = "SearchForItems";
        }

        public int HungerLevel
        {
            get
            {
                if (int.TryParse(NView.GetZDO()?.GetString(Misc.Constants.Z_HungerLevel), out int result))
                {
                    return result;
                }
                return 0;
            }
            set
            {
                var zdo = NView.GetZDO();
                if (zdo?.IsOwner() ?? false)
                {
                    Jotunn.Logger.LogDebug($"Set hungerlevel to {value}");
                    zdo.Set(Misc.Constants.Z_HungerLevel, value);
                }
            }
        }

        public int ComfortLevel
        {
            get
            {
                if (int.TryParse(NView.GetZDO()?.GetString(Misc.Constants.Z_ComfortLevel), out int result))
                {
                    return result;
                }
                return 0;
            }
            set
            {
                var zdo = NView.GetZDO();
                if (zdo?.IsOwner() ?? false)
                {
                    Jotunn.Logger.LogDebug($"Set comfortlevel to {value}");
                    zdo.Set(Misc.Constants.Z_ComfortLevel, value);
                }
            }
        }


        /// <summary>
        /// Used by MobAILib.MobManager to find MobAIInfo
        /// </summary>
        public NpcAI() : base()
        { }

        public NpcAI(MonsterAI vanillaAI, NpcAIConfig config) : base(vanillaAI, State.Idle, config)
        {
            UpdateTrigger = Brain.SetTriggerParameters<float>(Trigger.Update);
            m_containers = new MaxStack<Container>(Intelligence);
            m_config = config;

            eatingBehaviour = new EatingBehaviour
            {
                HungryTimeout = 10f,
                SearchForItemsState = State.SearchForItems,
                SuccessState = State.Idle,
                FailState = State.Idle,
                HealPercentageOnConsume = 0.2f
            }; 
            eatingBehaviour.Configure(this, Brain, State.Hungry);

            searchForItemsBehaviour = new SearchForItemsBehaviour();
            searchForItemsBehaviour.Configure(this, Brain, State.SearchForItems);


            ConfigureRoot();
            ConfigureHungry();
            ConfigureIdle();
            ConfigureSearchForItems();
        }

        private void ConfigureRoot()
        {
            Brain.Configure(State.Root)
                .InitialTransition(State.Idle)
                .PermitIf(Trigger.TakeDamage, State.Fight, () => !Brain.IsInState(State.Flee) && !Brain.IsInState(State.Fight) && (TimeSinceHurt < 20.0f || Common.Alarmed(Instance, 1)));
        }

        private void ConfigureHungry()
        {
            Brain.Configure(State.Hungry)
                .SubstateOf(State.Root)
                .OnExit(t =>
                {
                    HungerLevel = eatingBehaviour.FailedToFindFood;
                });
        }

        private void ConfigureIdle()
        {
            Brain.Configure(State.Idle)
                .SubstateOf(State.Root)
                .PermitIf(Trigger.Hungry, eatingBehaviour.StartState, () => eatingBehaviour.IsHungry(IsHurt))
                .OnEntry(t =>
                {
                    m_stuckInIdleTimer = 0;
                    UpdateAiStatus(State.Idle);
                });
        }

        private void ConfigureSearchForItems()
        {
            Brain.Configure(State.SearchForItems.ToString())
                .SubstateOf(State.Root)
                .Permit(Trigger.SearchForItems, searchForItemsBehaviour.StartState)
                .OnEntry(t =>
                {
                    Common.Dbgl($"{Character.GetHoverName()}:ConfigureSearchContainers Initiated", true, "NPC");
                    searchForItemsBehaviour.KnownContainers = m_containers;
                    searchForItemsBehaviour.Items = t.Parameters[0] as IEnumerable<ItemDrop.ItemData>;
                    searchForItemsBehaviour.AcceptedContainerNames = m_config.AcceptedContainers;
                    searchForItemsBehaviour.SuccessState = t.Parameters[1] as string;
                    searchForItemsBehaviour.FailState = t.Parameters[2] as string;
                    Brain.Fire(Trigger.SearchForItems.ToString());
                });
        }

        public override void UpdateAI(float dt)
        {
            base.UpdateAI(dt);
            m_triggerTimer += dt;
            if (m_triggerTimer < 0.1f) return;

            m_triggerTimer = 0f;

            // Update active behaviours
            eatingBehaviour.Update(this, dt);

            // Update runtime triggers
            Brain.Fire(Trigger.Hungry);

            if (Time.time > m_calculateComfortTimer)
            {
                var cl = CalculateComfortLevel();
                if (cl >= 0)
                {
                    ComfortLevel = cl;
                }
                m_calculateComfortTimer = Time.time + CalculateComfortLevelInterval;
            }

            if (Brain.IsInState(State.SearchForItems))
            {
                searchForItemsBehaviour.Update(this, dt);
                return;
            }

        }

        private int CalculateComfortLevel()
        {
            Jotunn.Logger.LogDebug($"{Character.m_name}:CalculateComfortLevel");
            var bedZDOId = NView.GetZDO().GetZDOID(Misc.Constants.Z_NpcBedOwnerId);
            if (bedZDOId == ZDOID.None) return 0; // No bed, no comfort

            var bedGO = ZNetScene.instance.FindInstance(bedZDOId);
            if (!bedGO || !bedGO.GetComponent<ZNetView>().IsValid()) return -1; // Bed has no instance, can't calculate, keep old value

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

            return Helpers.GetComfortFromNearbyPieces(bed.transform.position) + 2; 
        }

        public override void Follow(Player player)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }
    }
}
