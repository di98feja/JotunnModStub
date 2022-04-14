using RagnarsRokare.MobAI;
using Stateless;
using System;
using UnityEngine;

namespace RagnarsRokare.Factions
{
    public class HopelessBehaviour : IDynamicBehaviour
    {
        private const string Prefix = "RR_Hopeless";
        private float _currentStateTimeout;
        private Vector3 m_targetPosition;
        private IDynamicBehaviour m_dynamicBehaviour;
        private SleepBehaviour m_sleepBehaviour;
        private DynamicEatingBehaviour m_eatingBehaviour;
        private MaxStack<Container> m_containers;

        private class State
        {
            public const string Main = Prefix + "Main";
            public const string Sit = Prefix + "Sit";
            public const string Wander = Prefix + "Wander";
            public const string DynamicBehaviour = Prefix + "DynamicBehaviour";
        }
        private class Trigger
        {
            public const string Abort = Prefix + "Abort";
            public const string SitDown = Prefix + "SitDown";
            public const string RandomWalk = Prefix + "RandomWalk";
            public const string StartDynamicBehaviour = Prefix + "StartDynamicBehaviour";
            public const string ChangeDynamicBehaviour = Prefix + "ChangeDynamicBehaviour";

        }

        private String[] Comments = new String[]
        {
            "Why did the gods place me here?",
            "I wish I had a purpose",
            "What am I suppose to do here?",
            "How long must this go on?",
            "Is there a reason for this?",
            "All I do is die, die, die...",
            "*looks bored*",
            "*sigh*"
        };

        // Settings
        public string StartState => State.Main;
        public string SuccessState { get; set; }
        public string FailState { get; set; }
        public float StateTimeout { get; set; } = 10f + UnityEngine.Random.Range(0f, 10f);
        public float RandomCommentChance { get; set; } = 25f;

        public void Abort()
        {
            On.Character.IsEncumbered -= Character_IsEncumbered;
            m_dynamicBehaviour.Abort();
        }

        public void Configure(MobAIBase aiBase, StateMachine<string, string> brain, string parentState)
        {
            m_sleepBehaviour = new SleepBehaviour();
            m_sleepBehaviour.SuccessState = State.Main;
            m_sleepBehaviour.FailState = State.Main;
            m_sleepBehaviour.Configure(aiBase, brain, State.DynamicBehaviour);
            m_sleepBehaviour.SleepUntilMorning = true;

            m_eatingBehaviour = new DynamicEatingBehaviour();
            m_eatingBehaviour.SuccessState = State.Main;
            m_eatingBehaviour.FailState = State.Main;
            m_eatingBehaviour.HungryTimeout = 60f;
            m_eatingBehaviour.Configure(aiBase, brain, State.DynamicBehaviour);


            brain.Configure(State.Main)
               .InitialTransition(State.Wander)
               .SubstateOf(parentState)
               .PermitDynamic(Trigger.Abort, () => FailState)
               .OnEntry(t =>
               {
                   aiBase.UpdateAiStatus("Hopeless");
                   Common.Dbgl("Entered HopelessBehaviour", true, "NPC");
               });

            brain.Configure(State.Sit)
                .SubstateOf(State.Main)
                .Permit(Trigger.RandomWalk, State.Wander)
                .Permit(Trigger.ChangeDynamicBehaviour, State.DynamicBehaviour)
                .OnEntry(t =>
                {
                    if (UnityEngine.Random.Range(0f, 100f) <= RandomCommentChance)
                    {
                        SayRandomThing(aiBase.Character);
                    }
                    _currentStateTimeout = Time.time + StateTimeout;
                    EmoteManager.StartEmote(aiBase.NView, EmoteManager.Emotes.Sit, false);
                    Debug.Log($"{aiBase.Character.m_name}: Switching to Sit.");
                })
                .OnExit(t =>
                {
                    EmoteManager.StopEmote(aiBase.NView);
                });

            brain.Configure(State.Wander)
                .SubstateOf(State.Main)
                .Permit(Trigger.ChangeDynamicBehaviour, State.DynamicBehaviour)
                .Permit(Trigger.SitDown, State.Sit)
                .OnEntry(t =>
                {
                    if (UnityEngine.Random.Range(0f, 100f) <= RandomCommentChance)
                    {
                        SayRandomThing(aiBase.Character);
                    }
                    On.Character.IsEncumbered += Character_IsEncumbered;

                    _currentStateTimeout = Time.time + StateTimeout;
                    m_targetPosition = GetRandomPointInRadius(aiBase.HomePosition, 2f);
                    Debug.Log($"{aiBase.Character.m_name}: Switching to Wander.");
                })
                .OnExit(t =>
                {
                    On.Character.IsEncumbered -= Character_IsEncumbered;
                    aiBase.Character.m_zanim.SetBool(Character.encumbered, value: false);
                });

            brain.Configure(State.DynamicBehaviour)
                .SubstateOf(State.Wander)
                .PermitDynamic(Trigger.StartDynamicBehaviour, () => m_dynamicBehaviour.StartState)
                .PermitReentry(Trigger.ChangeDynamicBehaviour)
                .OnEntry(t =>
                {
                    Jotunn.Logger.LogDebug("DynamicBehaviour.OnEntry()");
                    Debug.Log($"{aiBase.Character.m_name}: Switching to {m_dynamicBehaviour}");
                    brain.Fire(Trigger.StartDynamicBehaviour);
                });
        }

        private void SayRandomThing(Character npc)
        {
            int index = UnityEngine.Random.Range(0, Comments.Length);
            npc.GetComponent<Talker>().Say(Talker.Type.Normal, Comments[index]);
        }

        private bool Character_IsEncumbered(On.Character.orig_IsEncumbered orig, Character self)
        {
            return true;
        }

        public void Update(MobAIBase instance, float dt)
        {
            // Update eating
            m_eatingBehaviour.Update(instance, dt);
            if (m_dynamicBehaviour == m_eatingBehaviour)
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
