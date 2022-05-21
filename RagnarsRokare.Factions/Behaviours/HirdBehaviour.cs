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
        private WorkdayBehaviour m_workdayBehaviour;
        private RestingBehaviour m_restingBehaviour;
        private DynamicFightBehaviour m_dynamicFightBehaviour;
        private float m_currentBehaviourTimeout;
        float m_fleeTimer;

        private StateDef State { get; set; }

        private sealed class StateDef
        {
            private readonly string prefix;

            public string Main { get { return $"{prefix}Main"; } }
            public string Wander { get { return $"{prefix}Wander"; } }
            public string Follow { get { return $"{prefix}Follow"; } }
            public string Flee { get { return $"{prefix}Flee"; } }
            public string WorkdayBehaviour { get { return $"{prefix}WorkdayBehaviour"; } }
            public string RestingBehaviour { get { return $"{prefix}RestingBehaviour"; } }

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
            public string GotoWork { get { return $"{prefix}GotoWork"; } }
            public string GotoRest { get { return $"{prefix}GotoRest"; } }
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

        private String[] WanderComments = new String[]
        {
            "*looks happy*",
            "I'm so glad you found me"
        };

        private String[] StartFleeComments = new String[]
        {
            "Did you hear that?",
            "Something is amiss..",
            "AAAAHHHHH!"
        };

        private String[] EndFleeComments = new String[]
        {
            "Hm, I think it is safe now",
            "Phuu, close call"
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
            m_currentBehaviour = null;
        }

        public void Configure(MobAIBase mobAi, StateMachine<string, string> brain, string parentState)
        {
            State = new StateDef(parentState + Prefix);
            Trigger = new TriggerDef(parentState + Prefix);
            var npcAi = mobAi as NpcAI;

            m_workdayBehaviour = new WorkdayBehaviour();
            m_workdayBehaviour.SuccessState = State.Main;
            m_workdayBehaviour.FailState = State.Main;
            m_workdayBehaviour.Configure(npcAi, brain, State.WorkdayBehaviour);

            m_restingBehaviour = new RestingBehaviour();
            m_restingBehaviour.SuccessState = State.Main;
            m_restingBehaviour.FailState = State.Main;
            m_restingBehaviour.LastKnownFoodPosition = mobAi.StartPosition;
            m_restingBehaviour.RestUpdateTimeout = StateTimeout / 4;
            m_restingBehaviour.Configure(npcAi, brain, State.RestingBehaviour);

            m_dynamicFightBehaviour = new DynamicFightBehaviour();
            m_dynamicFightBehaviour.SuccessState = State.Main;
            m_dynamicFightBehaviour.FailState = State.Flee;
            m_dynamicFightBehaviour.Configure(npcAi, brain, State.Main);

            brain.Configure(State.Main)
               .SubstateOf(parentState)
               .PermitDynamic(Trigger.Abort, () => FailState)
               .PermitIf(Trigger.Allerted, m_dynamicFightBehaviour.StartState, () => (!brain.IsInState(State.Flee) || !brain.IsInState(m_dynamicFightBehaviour.StartState))  && (mobAi.TimeSinceHurt < 20.0f || Common.Alarmed(mobAi.Instance, mobAi.Awareness)))
               .Permit(Trigger.Follow, State.Follow)
               .Permit(Trigger.GotoWork, State.WorkdayBehaviour)
               .Permit(Trigger.GotoRest, State.RestingBehaviour)
               .PermitReentry(Trigger.SelectState)
               .OnEntry(t =>
               {
                   npcAi.UpdateAiStatus("Hird");
                   Common.Dbgl("Entered HirdBehaviour", true, "NPC");
                   mobAi.Character.m_speed = 2f;
                   mobAi.Character.m_walkSpeed = 1.5f;
                   mobAi.Character.m_runSpeed = 5f;
                   mobAi.Character.m_swimSpeed = 2f;
                   m_currentBehaviourTimeout = 0f;
               });

            brain.Configure(State.WorkdayBehaviour)
                .SubstateOf(State.Main)
                .InitialTransition(m_workdayBehaviour.StartState)
                .OnEntry(() => { m_currentBehaviour = m_workdayBehaviour; });

            brain.Configure(State.RestingBehaviour)
                .SubstateOf(State.Main)
                .InitialTransition(m_restingBehaviour.StartState)
                .OnEntry(() => { m_currentBehaviour = m_restingBehaviour; });

            brain.Configure(State.Follow)
                .SubstateOf(State.Main)
                .OnEntry(t =>
                {
                    npcAi.UpdateAiStatus(State.Follow);
                    npcAi.Instance.SetAlerted(false);
                })
                .OnExit(t =>
                {
                    m_startPosition = m_restingBehaviour.LastKnownFoodPosition = npcAi.Instance.transform.position;
                });

            brain.Configure(State.Wander)
                .SubstateOf(State.Main)
                .OnEntry(t =>
                {
                    if (UnityEngine.Random.Range(0f, 100f) <= RandomCommentChance)
                    {
                        SayRandomThing(npcAi.Character, WanderComments);
                    }
                    npcAi.UpdateAiStatus(State.Wander);
                    _currentStateTimeout = Time.time + StateTimeout/4;
                    m_targetPosition = GetRandomPointInRadius(npcAi.HomePosition, 20f);
                    Debug.Log($"{npcAi.Character.m_name}: Switching to Wander.");
                });

            brain.Configure(State.Flee)
                .SubstateOf(State.Main)
                .OnEntry(t =>
                {
                    m_fleeTimer = 0f;
                    npcAi.UpdateAiStatus("Flee");
                    npcAi.Instance.Alert();
                    SayRandomThing(npcAi.Character, StartFleeComments);
                })
                .OnExit(t =>
                {
                    (npcAi.Instance as MonsterAI).SetAlerted(false);
                    npcAi.Attacker = null;
                    npcAi.StopMoving();
                    SayRandomThing(npcAi.Character, EndFleeComments);
                });
        }

        private void SayRandomThing(Character npc, string[] comments)
        {
            int index = UnityEngine.Random.Range(0, WanderComments.Length);
            npc.GetComponent<Talker>().Say(Talker.Type.Normal, comments[index]);
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
                m_currentBehaviour?.Abort();
                m_currentBehaviour = null;
                instance.Brain.Fire(Trigger.Follow);
                return;
            }

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
                bool outOfDanger = !Common.Alarmed(instance.Instance, Mathf.Max(1, instance.Awareness + 2)) && m_fleeTimer > 10f;
                if (outOfDanger)
                {
                    instance.Brain.Fire(Trigger.SelectState);
                }
                return;
            }
            else if (instance.TimeSinceHurt < 20.0f || Common.Alarmed(instance.Instance, instance.Awareness))
            {
                instance.Brain.Fire(Trigger.Allerted);
            }

            if (instance.Brain.IsInState(State.WorkdayBehaviour) && EnvMan.instance.GetDayFraction() >= 0.70f)
            {
                m_workdayBehaviour.Abort();
                instance.Brain.Fire(Trigger.GotoRest);
            }

            if (Time.time > m_currentBehaviourTimeout)
            {
                m_currentBehaviourTimeout = Time.time + StateTimeout;
                if (m_restingBehaviour.RainCheck(instance) || EnvMan.instance.GetDayFraction() >= 0.70f)
                {
                    if (m_currentBehaviour != m_restingBehaviour)
                    {
                        instance.Brain.Fire(Trigger.GotoRest);
                    }
                }
                else
                {
                    instance.Brain.Fire(Trigger.GotoWork);
                }
                return;
            }

            if (instance.Brain.IsInState(State.WorkdayBehaviour))
            {
                m_currentBehaviour.Update(instance, dt);
                return;
            }

            if (instance.Brain.IsInState(State.RestingBehaviour))
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
