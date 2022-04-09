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
        private float m_fleeTimer;
        private float m_dynamicBehaviourTimer;

        // Settings
        public float FleeTimeout { get; private set; } = 10f;
        public float DynamicBehaviourTime { get; set; } = 20f;
        // Behaviours
        readonly EatingBehaviour eatingBehaviour;
        readonly SearchForItemsBehaviour searchForItemsBehaviour;
        readonly IFightBehaviour fightBehaviour;
        private IDynamicBehaviour m_dynamicBehaviour;
        private SleepBehaviour m_sleepBehaviour;
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
            public const string Idle = "Idle";
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

        public int ComfortLevel {get; set;} = 0;

        public float MotivationLevel { get; set; } = 0;


        public bool HasBed()
        {
            return NView.GetZDO().GetZDOID(Misc.Constants.Z_NpcBedOwnerId) != ZDOID.None;
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
            m_animator = Character.GetComponentInChildren<Animator>();

            m_apathyBehaviour = new ApathyBehaviour();
            m_apathyBehaviour.SuccessState = State.Idle;
            m_apathyBehaviour.FailState = State.Idle;
            m_apathyBehaviour.Configure(this, Brain, State.DynamicBehaviour);

            m_hopelessBehaviour = new HopelessBehaviour();
            m_hopelessBehaviour.SuccessState = State.Idle;
            m_hopelessBehaviour.FailState = State.Idle;
            m_hopelessBehaviour.Configure(this, Brain, State.DynamicBehaviour);

            m_sleepBehaviour = new SleepBehaviour();
            m_sleepBehaviour.SuccessState = State.Idle;
            m_sleepBehaviour.FailState = State.Idle;
            m_sleepBehaviour.Configure(this, Brain, State.DynamicBehaviour);
            m_sleepBehaviour.SleepTime = 60f;

            eatingBehaviour = new EatingBehaviour
            {
                HungryTimeout = 60f,
                SearchForItemsState = State.SearchForItems,
                SuccessState = State.Idle,
                FailState = State.Idle,
                HealPercentageOnConsume = 0.2f
            };
            eatingBehaviour.Configure(this, Brain, State.Hungry);

            searchForItemsBehaviour = new SearchForItemsBehaviour();
            searchForItemsBehaviour.Configure(this, Brain, State.SearchForItems);
            fightBehaviour = Activator.CreateInstance(FightingBehaviourSelector.Invoke(this)) as IFightBehaviour;
            fightBehaviour.Configure(this, Brain, State.Fight);

            ConfigureRoot();
            ConfigureHungry();
            ConfigureIdle();
            ConfigureSearchForItems();
            ConfigureFight();
            ConfigureFlee();
            ConfigureDynamicBehaviour();
        }

        private void ConfigureRoot()
        {
            Brain.Configure(State.Root)
                .InitialTransition(State.Idle)
                .PermitIf(Trigger.TakeDamage, State.Fight, () => !Brain.IsInState(State.Flee) && !Brain.IsInState(State.Fight) && (TimeSinceHurt < 20.0f || Common.Alarmed(Instance, base.Mobility)) && MotivationManager.CalculateMotivation(NView.GetZDO(), ComfortLevel) > 2)
                .PermitIf(Trigger.TakeDamage, State.Flee, () => !Brain.IsInState(State.Flee) && !Brain.IsInState(State.Fight) && TimeSinceHurt < 20.0f && MotivationManager.CalculateMotivation(NView.GetZDO(), ComfortLevel) > 0 && MotivationManager.CalculateMotivation(NView.GetZDO(), ComfortLevel) <= 2)
                .PermitIf(Trigger.ChangeDynamicBehaviour, State.DynamicBehaviour, () => !Brain.IsInState(State.DynamicBehaviour));
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
                .Permit(Trigger.ChangeDynamicBehaviour, State.DynamicBehaviour)
                .PermitIf(Trigger.Hungry, eatingBehaviour.StartState, () => eatingBehaviour.IsHungry(IsHurt) && MotivationManager.CalculateMotivation(NView.GetZDO(), ComfortLevel) > 1)
                .OnEntry(t =>
                {
                    m_stuckInIdleTimer = 0;
                    UpdateAiStatus(State.Idle);
                });
        }

        private void ConfigureDynamicBehaviour()
        {
            Brain.Configure(State.DynamicBehaviour)
                .SubstateOf(State.Idle)
                .PermitDynamic(Trigger.StartDynamicBehaviour, () => m_dynamicBehaviour.StartState)
                .PermitReentry(Trigger.ChangeDynamicBehaviour)
                .OnEntry(t =>
                {
                    Jotunn.Logger.LogDebug("DynamicBehaviour.OnEntry()");
                    Brain.Fire(Trigger.StartDynamicBehaviour);
                });
        }

        private void NextDynamicBehaviour()
        {
            if (m_dynamicBehaviour != null)
            {
                var oldBehaviour = m_dynamicBehaviour;
                Jotunn.Logger.LogDebug($"{Character.m_name}: Swithing from {oldBehaviour}");
                m_dynamicBehaviour.Abort();
            }
            
            if (MotivationLevel < 1)
            {
                m_dynamicBehaviour = m_apathyBehaviour;               
            }

            else if (MotivationLevel > 0)
            {
                m_dynamicBehaviour = m_hopelessBehaviour;
            }

            var newBehaviour = m_dynamicBehaviour;
            Jotunn.Logger.LogDebug($"{Character.m_name}: Swithing to {newBehaviour}");
            m_dynamicBehaviourTimer = Time.time + DynamicBehaviourTime;
            Brain.Fire(Trigger.ChangeDynamicBehaviour);
            return;
        }

        private void ConfigureFight()
        {
            Brain.Configure(State.Fight)
                .SubstateOf(State.Root)
                .Permit(Trigger.Fight, fightBehaviour.StartState)
                .OnEntry(t =>
                {
                    fightBehaviour.SuccessState = State.Idle;
                    fightBehaviour.FailState = State.Flee;
                    fightBehaviour.MobilityLevel = base.Mobility;
                    fightBehaviour.AgressionLevel = base.Agressiveness;
                    fightBehaviour.AwarenessLevel = base.Awareness;

                    Brain.Fire(Trigger.Fight);
                })
                .OnExit(t =>
                {
                    ItemDrop.ItemData currentWeapon = (Character as Humanoid).GetCurrentWeapon();
                    if (null != currentWeapon)
                    {
                        (Character as Humanoid).UnequipItem(currentWeapon);
                    }
                    Invoke<MonsterAI>(Instance, "SetAlerted", false);
                });
        }

        private void ConfigureFlee()
        {
            Brain.Configure(State.Flee)
                .SubstateOf(State.Root)
                .PermitIf(UpdateTrigger, State.Idle, (arg) => (m_fleeTimer += arg) > FleeTimeout && !Common.Alarmed(Instance, Mathf.Max(1, base.Awareness - 1)))
                .OnEntry(t =>
                {
                    Debug.Log($"{Instance.name} enter Flee state ");
                    m_fleeTimer = 0f;
                    UpdateAiStatus(State.Flee);
                    Instance.Alert();
                })
                .OnExit(t =>
                {
                    Invoke<MonsterAI>(Instance, "SetAlerted", false);
                    Attacker = null;
                    StopMoving();
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
            EmoteManager.UpdateEmote(NView, ref m_emoteState, ref m_emoteID, ref m_animator);

            m_triggerTimer += dt;
            if (m_triggerTimer < 0.1f) return;

            m_triggerTimer = 0f;

            // Update eating behaviours
            eatingBehaviour.Update(this, dt);

            // Update runtime triggers
            Brain.Fire(Trigger.Hungry);
            Brain.Fire(Trigger.TakeDamage);
            Brain.Fire(Trigger.Follow);

            if (Time.time > m_dynamicBehaviourTimer)
            {
                NextDynamicBehaviour();
            }

            if (Time.time > m_calculateComfortTimer)
            {
                var cl = CalculateComfortLevel();
                if (cl >= 0)
                {
                    ComfortLevel = cl;
                }
                m_calculateComfortTimer = Time.time + CalculateComfortLevelInterval;
                var motivation = MotivationManager.CalculateMotivation(NView.GetZDO(), ComfortLevel);
                Debug.Log($"Motivation = {motivation} ");
                if (MotivationLevel != motivation)
                {
                    MotivationLevel = motivation;
                    NextDynamicBehaviour();
                }
            }

            if (Brain.IsInState(State.DynamicBehaviour))
            {
                m_dynamicBehaviour.Update(this, dt);
            }

            if (Brain.IsInState(State.SearchForItems))
            {
                searchForItemsBehaviour.Update(this, dt);
                return;
            }

            if (Brain.IsInState(State.Fight))
            {
                fightBehaviour.Update(this, dt);
                return;
            }

            if (Brain.IsInState(State.Flee))
            {
                var fleeFrom = Attacker == null ? Character.transform.position : Attacker.transform.position;
                Invoke<MonsterAI>(Instance, "Flee", dt, fleeFrom);
                return;
            }

            if (Brain.State == State.Idle && m_startPosition != null)
            {
                m_stuckInIdleTimer += dt;
                Utils.Invoke<BaseAI>(Instance, "RandomMovement", dt, m_startPosition);
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

            return Helpers.GetComfortFromNearbyPieces(bed.transform.position) + 2;
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
