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
        private IDynamicBehaviour m_dynamicBehaviour;
        private SleepBehaviour m_sleepBehaviour;
        private DynamicEatingBehaviour m_eatingBehaviour;


        private class State
        {
            public const string Main = Prefix + "Main";
            public const string Sit = Prefix + "Sit";
            public const string Wander = Prefix + "Wander";
            public const string Follow = Prefix + "Follow";
            public const string DynamicBehaviour = Prefix + "DynamicBehaviour";
        }
        private class Trigger
        {
            public const string Abort = Prefix + "Abort";
            public const string SitDown = Prefix + "SitDown";
            public const string RandomWalk = Prefix + "RandomWalk";
            public const string StartDynamicBehaviour = Prefix + "StartDynamicBehaviour";
            public const string ChangeDynamicBehaviour = Prefix + "ChangeDynamicBehaviour";
            public const string Follow = Prefix + "Follow";
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
        public float StateTimeout { get; set; } = 10f + UnityEngine.Random.Range(0f, 10f);
        public float RandomCommentChance { get; set; } = 10f;

        public void Abort()
        {
            m_dynamicBehaviour.Abort();
        }

        public void Configure(MobAIBase mobAi, StateMachine<string, string> brain, string parentState)
        {
            var npcAi = mobAi as NpcAI;
            m_sleepBehaviour = new SleepBehaviour();
            m_sleepBehaviour.SuccessState = State.Main;
            m_sleepBehaviour.FailState = State.Main;
            m_sleepBehaviour.Configure(npcAi, brain, State.DynamicBehaviour);
            m_sleepBehaviour.SleepUntilMorning = true;

            m_eatingBehaviour = new DynamicEatingBehaviour();
            m_eatingBehaviour.SuccessState = State.Main;
            m_eatingBehaviour.FailState = State.Main;
            m_eatingBehaviour.HealPercentageOnConsume = 0.25f + (StandingsManager.GetStandingTowards(npcAi.NView.GetZDO(), FactionManager.GetNpcFaction(npcAi.NView.GetZDO())))/100;
            m_eatingBehaviour.Configure(npcAi, brain, State.DynamicBehaviour);


            brain.Configure(State.Main)
               .InitialTransition(State.Wander)
               .SubstateOf(parentState)
               .PermitDynamic(Trigger.Abort, () => FailState)
               .Permit(Trigger.Follow, State.Follow)
               .OnEntry(t =>
               {
                   npcAi.UpdateAiStatus("Hird");
                   Common.Dbgl("Entered HirdBehaviour", true, "NPC");
               });

            brain.Configure(State.Follow)
                .Permit(Trigger.RandomWalk, State.Wander)
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
                .Permit(Trigger.RandomWalk, State.Wander)
                .Permit(Trigger.ChangeDynamicBehaviour, State.DynamicBehaviour)
                .OnEntry(t =>
                {
                    npcAi.UpdateAiStatus(State.Sit);
                    if (UnityEngine.Random.Range(0f, 100f) <= RandomCommentChance)
                    {
                        SayRandomThing(npcAi.Character);
                    }
                    _currentStateTimeout = Time.time + StateTimeout;
                    EmoteManager.StartEmote(npcAi.NView, EmoteManager.Emotes.Sit, false);
                    Debug.Log($"{npcAi.Character.m_name}: Switching to Sit.");
                })
                .OnExit(t =>
                {
                    EmoteManager.StopEmote(npcAi.NView);
                });

            brain.Configure(State.Wander)
                .SubstateOf(State.Main)
                .Permit(Trigger.ChangeDynamicBehaviour, State.DynamicBehaviour)
                .Permit(Trigger.SitDown, State.Sit)
                .OnEntry(t =>
                {
                    if (UnityEngine.Random.Range(0f, 100f) <= RandomCommentChance)
                    {
                        SayRandomThing(npcAi.Character);
                    }
                    npcAi.UpdateAiStatus(State.Wander);
                    _currentStateTimeout = Time.time + StateTimeout;
                    m_targetPosition = GetRandomPointInRadius(npcAi.HomePosition, 2f);
                    Debug.Log($"{npcAi.Character.m_name}: Switching to Wander.");
                })
                .OnExit(t =>
                {
                    npcAi.Character.m_zanim.SetBool(Character.encumbered, value: false);
                });

            brain.Configure(State.DynamicBehaviour)
                .SubstateOf(State.Wander)
                .PermitDynamic(Trigger.StartDynamicBehaviour, () => m_dynamicBehaviour.StartState)
                .PermitReentry(Trigger.ChangeDynamicBehaviour)
                .OnEntry(t =>
                {
                    Jotunn.Logger.LogDebug("DynamicBehaviour.OnEntry()");
                    Debug.Log($"{npcAi.Character.m_name}: Switching to {m_dynamicBehaviour}");
                    brain.Fire(Trigger.StartDynamicBehaviour);
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
                    instance.Brain.Fire(Trigger.RandomWalk);
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
            if (instance.Brain.IsInState(m_eatingBehaviour.StartState))    
            {
                return;
            }
            if (instance.Brain.IsInState(State.DynamicBehaviour))
            {
                m_dynamicBehaviour.Update(instance, dt);
                return;
            }

            if (m_eatingBehaviour.IsHungry(instance.IsHurt))
            {
                m_dynamicBehaviour = m_eatingBehaviour;
                instance.Brain.Fire(Trigger.ChangeDynamicBehaviour);
                return;
            }

            if (EnvMan.instance.IsNight())
            {
                m_dynamicBehaviour = m_sleepBehaviour;
                instance.Brain.Fire(Trigger.ChangeDynamicBehaviour);
                return;
            }

            if (instance.Brain.IsInState(State.Wander))
            {
                if (Time.time > _currentStateTimeout)
                {
                    instance.Brain.Fire(Trigger.SitDown);
                }
                instance.MoveAndAvoidFire(m_targetPosition, dt, 0f, false, true);
                return;
            }

            if (instance.Brain.IsInState(State.Sit))
            {
                if (Time.time > _currentStateTimeout)
                {
                    instance.Brain.Fire(Trigger.RandomWalk);
                }
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
