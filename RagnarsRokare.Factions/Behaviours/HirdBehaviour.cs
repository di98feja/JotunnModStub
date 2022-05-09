using RagnarsRokare.MobAI;
using Stateless;
using System;
using UnityEngine;

namespace RagnarsRokare.Factions
{
    public class HirdBehaviour : IDynamicBehaviour
    {
        private const string Prefix = "RR_Hird";
        private float _currentStateTimeout;
        private Vector3 m_targetPosition;
        private Vector3 m_startPosition;
        private IDynamicBehaviour m_currentBehaviour;
        private SleepBehaviour m_sleepBehaviour;
        private SitBehaviour m_sitBehaviour;
        private WorkdayBehaviour m_workdayBehaviour;
        private DynamicEatingBehaviour m_eatingBehaviour;
        private float m_currentBehaviourTimeout;
        float m_fleeTimer;

        private StateDef State { get; set; }

        private sealed class StateDef
        {
            private readonly string prefix;

            public string Main { get { return $"{prefix}Main"; } }
            public string Sit { get { return $"{prefix}Sit"; } }
            public string Sleep { get { return $"{prefix}Sleep"; } }
            public string Wander { get { return $"{prefix}Wander"; } }
            public string Follow { get { return $"{prefix}Follow"; } }
            public string Flee { get { return $"{prefix}Flee"; } }
            public string Eating { get { return $"{prefix}Eating"; } }
            public string WorkdayBehaviour { get { return $"{prefix}WorkdayBehaviour"; } }
            public string RestingBehaviour { get { return $"{prefix}RestingBehaviour"; } }
            public string StateSelection { get { return $"{prefix}StateSelection"; } }

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
            public string SitDown { get { return $"{prefix}SitDown"; } }
            public string GotoBed { get { return $"{prefix}GotoBed"; } }
            public string FindFood { get { return $"{prefix}FindFood"; } }
            public string GotoWork { get { return $"{prefix}GotoWork"; } }
            public string RandomWalk { get { return $"{prefix}RandomWalk"; } }
            public string Update { get { return $"{prefix}Update"; } }
            public string Allerted { get { return $"{prefix}Allerted"; } }
            public string StartDynamicBehaviour { get { return $"{prefix}StartDynamicBehaviour"; } }
            public string ChangeDynamicBehaviour { get { return $"{prefix}ChangeDynamicBehaviour"; } }
            public string Follow { get { return $"{prefix}Follow"; } }
            public string SelectState { get { return $"{prefix}SelectState"; } }
            public TriggerDef(string prefix)
            {
                this.prefix = prefix;

            }
        }

        private String[] Comments = new String[]
        {
            "*looks happy*",
            "I'm so glad you found me"
        };


        // Settings
        public string StartState => State.Main;
        public string SuccessState { get; set; }
        public string FailState { get; set; }
        public float StateTimeout { get; set; } = 200f + UnityEngine.Random.Range(0f, 50f);
        public float RandomCommentChance { get; set; } = 10f;

        public void Abort()
        {
            m_currentBehaviour.Abort();
        }

        public void Configure(MobAIBase mobAi, StateMachine<string, string> brain, string parentState)
        {
            State = new StateDef(parentState + Prefix);
            Trigger = new TriggerDef(parentState + Prefix);
            var npcAi = mobAi as NpcAI;
            m_sleepBehaviour = new SleepBehaviour();
            m_sleepBehaviour.SuccessState = State.Main;
            m_sleepBehaviour.FailState = State.Main;
            m_sleepBehaviour.Configure(npcAi, brain, State.Sleep);
            m_sleepBehaviour.SleepUntilMorning = true;

            m_eatingBehaviour = new DynamicEatingBehaviour();
            m_eatingBehaviour.SuccessState = State.Main;
            m_eatingBehaviour.FailState = State.Main;
            m_eatingBehaviour.HealPercentageOnConsume = 0.25f + (StandingsManager.GetStandingTowards(npcAi.NView.GetZDO(), FactionManager.GetNpcFaction(npcAi.NView.GetZDO())))/100;
            m_eatingBehaviour.Configure(npcAi, brain, State.Eating);

            m_sitBehaviour = new SitBehaviour();
            m_sitBehaviour.SuccessState = State.Main;
            m_sitBehaviour.FailState= State.Main;
            m_sitBehaviour.SitTime = 20;
            m_sitBehaviour.Configure(npcAi, brain, State.Sit);

            m_workdayBehaviour = new WorkdayBehaviour();
            m_workdayBehaviour.SuccessState = State.Main;
            m_workdayBehaviour.FailState = State.Main;
            m_workdayBehaviour.Configure(npcAi, brain, State.WorkdayBehaviour);

            brain.Configure(State.Main)
               .InitialTransition(State.StateSelection)
               .SubstateOf(parentState)
               .PermitDynamic(Trigger.Abort, () => FailState)
               .PermitIf(Trigger.Allerted, State.Flee, () => !brain.IsInState(State.Flee) && (mobAi.TimeSinceHurt < 20.0f || Common.Alarmed(mobAi.Instance, mobAi.Awareness)))
               .Permit(Trigger.Follow, State.Follow)
               .Permit(Trigger.SelectState, State.StateSelection)
               .OnEntry(t =>
               {
                   npcAi.UpdateAiStatus("Hird");
                   Common.Dbgl("Entered HirdBehaviour", true, "NPC");
                   mobAi.Character.m_speed = 2f;
                   mobAi.Character.m_walkSpeed = 1.5f;
                   mobAi.Character.m_runSpeed = 5f;
                   mobAi.Character.m_swimSpeed = 2f;
               });

            brain.Configure(State.StateSelection)
                .SubstateOf(State.Main)
                .Permit(Trigger.SitDown, State.Sit)
                .Permit(Trigger.RandomWalk, State.Wander)
                .Permit(Trigger.GotoBed, State.Sleep)
                .Permit(Trigger.GotoWork, State.WorkdayBehaviour)
                .Permit(Trigger.FindFood, State.Eating)
                .OnEntry(() =>
                {
                    m_currentBehaviour = null;
                    if (m_eatingBehaviour.IsHungry(mobAi.IsHurt))
                    {
                        m_currentBehaviour = m_eatingBehaviour;
                        mobAi.Brain.Fire(Trigger.FindFood);
                        return;
                    }
                    
                    if (EnvMan.instance.IsNight())
                    {
                        m_currentBehaviour = m_sleepBehaviour;
                        mobAi.Brain.Fire(Trigger.GotoBed);
                        return;
                    }
                    else if (EnvMan.instance.GetDayFraction() >= 0.70f)
                    {
                        mobAi.Brain.Fire(Trigger.SitDown);
                        return;
                    }
                    m_currentBehaviourTimeout = Time.time + StateTimeout;
                    if (EnvMan.instance.IsWet() || EnvMan.instance.IsCold() || EnvMan.instance.IsFreezing())
                    {
                        var motivation = MotivationManager.GetMotivation(mobAi.NView.GetZDO());
                        if (motivation < Misc.Constants.Motivation_Hopefull)
                        {
                            mobAi.Brain.Fire(Trigger.SitDown);
                            return;
                        }
                        else if (motivation < Misc.Constants.Motivation_Inspired && UnityEngine.Random.value < 0.5f)
                        {
                            mobAi.Brain.Fire(Trigger.SitDown);
                            return;
                        }
                    }
                    m_currentBehaviour = m_workdayBehaviour;
                    mobAi.Brain.Fire(Trigger.GotoWork);
                });

            brain.Configure(State.WorkdayBehaviour)
                .SubstateOf(State.Main)
                .InitialTransition(m_workdayBehaviour.StartState);

            brain.Configure(State.Sleep)
                .SubstateOf(State.Main)
                .InitialTransition(m_sleepBehaviour.StartState);

            brain.Configure(State.Eating)
                .SubstateOf(State.Main)
                .InitialTransition(m_eatingBehaviour.StartState);

            brain.Configure(State.Follow)
                .SubstateOf(State.Main)
                .OnEntry(t =>
                {
                    npcAi.UpdateAiStatus(State.Follow);
                    npcAi.Instance.SetAlerted(false);
                })
                .OnExit(t =>
                {
                    m_startPosition = m_eatingBehaviour.LastKnownFoodPosition = npcAi.Instance.transform.position;
                });

            brain.Configure(State.Sit)
                .SubstateOf(State.Main)
                .Permit(Trigger.StartDynamicBehaviour, m_sitBehaviour.StartState)
                .OnEntry(t =>
                {
                    npcAi.UpdateAiStatus(State.Sit);
                    if (UnityEngine.Random.Range(0f, 100f) <= RandomCommentChance)
                    {
                        SayRandomThing(npcAi.Character);
                    }
                    Debug.Log($"{npcAi.Character.m_name}: Switching to Sit.");
                    m_currentBehaviour = m_sitBehaviour;
                    brain.Fire(Trigger.StartDynamicBehaviour);
                })
                .OnExit(t =>
                {
                });

            brain.Configure(State.Wander)
                .SubstateOf(State.Main)
                .OnEntry(t =>
                {
                    if (UnityEngine.Random.Range(0f, 100f) <= RandomCommentChance)
                    {
                        SayRandomThing(npcAi.Character);
                    }
                    npcAi.UpdateAiStatus(State.Wander);
                    _currentStateTimeout = Time.time + StateTimeout;
                    m_targetPosition = GetRandomPointInRadius(npcAi.HomePosition, 20f);
                    Debug.Log($"{npcAi.Character.m_name}: Switching to Wander.");
                })
                .OnExit(t =>
                {
                    npcAi.Character.m_zanim.SetBool(Character.encumbered, value: false);
                });

            brain.Configure(State.Flee)
                .SubstateOf(State.Main)
                .PermitIf(Trigger.Update, State.StateSelection, () => !Common.Alarmed(npcAi.Instance, Mathf.Max(1, npcAi.Awareness + 2)) && m_fleeTimer > 10f)
                .OnEntry(t =>
                {
                    m_fleeTimer = 0f;
                    npcAi.UpdateAiStatus("Flee");
                    npcAi.Instance.Alert();
                })
                .OnExit(t =>
                {
                    (npcAi.Instance as MonsterAI).SetAlerted(false);
                    npcAi.Attacker = null;
                    npcAi.StopMoving();
                });

        }

        private void SayRandomThing(Character npc)
        {
            int index = UnityEngine.Random.Range(0, Comments.Length);
            npc.GetComponent<Talker>().Say(Talker.Type.Normal, Comments[index]);
        }

        public void Update(MobAIBase instance, float dt)
        {
            // Update Follow
            var monsterAi = instance.Instance as MonsterAI;
            if (instance.Brain.IsInState(State.Follow))
            {
                if (!monsterAi.GetFollowTarget())
                {
                    instance.Brain.Fire(Trigger.SelectState);
                }
                else
                {
                    monsterAi.Follow(monsterAi.GetFollowTarget(), dt);
                }
                return;
            }
            else if (monsterAi.GetFollowTarget())
            {
                instance.Brain.Fire(Trigger.Follow);
                return;
            }

            // Update eating
            m_eatingBehaviour.Update(instance, dt);
            instance.Brain.Fire(Trigger.Allerted);
            instance.Brain.Fire(Trigger.Update);

            if (instance.Brain.IsInState(State.Flee))
            {
                if (instance.Attacker != null)
                {
                    var fleeFrom = instance.Attacker.transform.position;
                    instance.Instance.Flee(dt, fleeFrom);
                }
                else
                {
                    instance.MoveAndAvoidFire(instance.HomePosition, dt, 0f, true, false);
                }
                m_fleeTimer += dt;
                return;
            }

            if (instance.Brain.IsInState(m_eatingBehaviour.StartState))    
            {
                return;
            }

            if (instance.Brain.IsInState(State.WorkdayBehaviour) && EnvMan.instance.GetDayFraction() >= 0.70f)
            {
                m_workdayBehaviour.Abort();
                instance.Brain.Fire(Trigger.SelectState);
            }

            if (instance.Brain.IsInState(State.Sleep) && m_sleepBehaviour.SleepUntilMorning)
            {
                m_currentBehaviour.Update(instance, dt);
                return;
            }

            if (Time.time > m_currentBehaviourTimeout)
            {
                instance.Brain.Fire(Trigger.SelectState);
                return;
            }

            if (instance.Brain.IsInState(State.WorkdayBehaviour))
            {
                m_currentBehaviour.Update(instance, dt);
                return;
            }

            if (instance.Brain.IsInState(State.Sit))
            {
                m_currentBehaviour.Update(instance, dt);
                return;
            }

            if (instance.Brain.IsInState(State.Wander))
            {
                if (Time.time > _currentStateTimeout)
                {
                    instance.Brain.Fire(Trigger.SelectState);
                }
                instance.MoveAndAvoidFire(m_targetPosition, dt, 0f, false, true);
                return;
            }
        }

        private Vector3 GetRandomPointInRadius(Vector3 center, float radius)
        {
            float f = UnityEngine.Random.value * (float)Math.PI * 2f;
            float num = UnityEngine.Random.Range(0f, radius);
            return center + new Vector3(Mathf.Sin(f) * num, 0f, Mathf.Cos(f) * num);
        }
    }
}
