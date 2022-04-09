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
        private float m_sleepTimer;

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
            "Why did the gods place me here",
            "I need something to do",
            "What am I suppose to do here?",
            "*sobs*",
            "How long must this go on?",
            "Is there a reason for this?",
            "*Looks at a tree*",
            "*looks utterly bored*"
        };

        // Settings
        public string StartState => State.Main;
        public string SuccessState { get; set; }
        public string FailState { get; set; }
        public float StateTimeout { get; set; } = 10f;
        public float RandomCommentChance { get; set; } = 25f;

        public void Abort()
        {
            On.Character.IsEncumbered -= Character_IsEncumbered;
        }

        public void Configure(MobAIBase aiBase, StateMachine<string, string> brain, string parentState)
        {
            m_sleepBehaviour = new SleepBehaviour();
            m_sleepBehaviour.SuccessState = State.Main;
            m_sleepBehaviour.FailState = State.Main;
            m_sleepBehaviour.Configure(aiBase, brain, State.DynamicBehaviour);
            m_sleepBehaviour.SleepTime = 60f;

            m_dynamicBehaviour = m_sleepBehaviour;

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
                .OnEntry(t =>
                {
                    if (UnityEngine.Random.Range(0f, 100f) <= RandomCommentChance)
                    {
                        SayRandomThing(aiBase.Character);
                    }
                    _currentStateTimeout = Time.time + StateTimeout;
                    aiBase.Character.GetComponentInChildren<Animator>().SetTrigger("Laydown");
                    //EmoteManager.StartEmote(aiBase.NView, EmoteManager.Emotes.Sit, false);
                })
                .OnExit(t =>
                {
                    EmoteManager.StopEmote(aiBase.NView);
                });

            brain.Configure(State.Wander)
                .SubstateOf(State.Main)
                .Permit(Trigger.StartDynamicBehaviour, State.DynamicBehaviour)
                .OnEntry(t =>
                {
                    if (UnityEngine.Random.Range(0f, 100f) <= RandomCommentChance)
                    {
                        SayRandomThing(aiBase.Character);
                    }
                    On.Character.IsEncumbered += Character_IsEncumbered;

                    _currentStateTimeout = Time.time + StateTimeout;
                    m_targetPosition = GetRandomPointInRadius(aiBase.HomePosition, 2f);
                })
                .OnExit(t =>
                {
                    On.Character.IsEncumbered -= Character_IsEncumbered;
                    aiBase.Character.m_zanim.SetBool(Character.encumbered, value: false);
                });

            brain.Configure(State.DynamicBehaviour)
                .SubstateOf(State.Main)
                .PermitDynamic(Trigger.StartDynamicBehaviour, () => m_dynamicBehaviour.StartState)
                .PermitReentry(Trigger.ChangeDynamicBehaviour)
                .OnEntry(t =>
                {
                    Jotunn.Logger.LogDebug("DynamicBehaviour.OnEntry()");
                    NextDynamicBehaviour(aiBase);
                    brain.Fire(Trigger.StartDynamicBehaviour);
                });
        }

        private void NextDynamicBehaviour(MobAIBase aiBase)
        {
            if (m_dynamicBehaviour != null)
            {
                var oldBehaviour = m_dynamicBehaviour;
                Jotunn.Logger.LogDebug($"{aiBase.Character.m_name}: Swithing from {oldBehaviour}");
                m_dynamicBehaviour.Abort();
            }
            if (m_sleepTimer > 60f)
            {
                m_dynamicBehaviour = m_sleepBehaviour;
                m_sleepTimer = 0f;
            }
            else return;
            var newBehaviour = m_dynamicBehaviour;
            Jotunn.Logger.LogDebug($"{aiBase.Character.m_name}: Swithing to {newBehaviour}");
            aiBase.Brain.Fire(Trigger.ChangeDynamicBehaviour);
            return;
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
            m_sleepTimer += dt;
            
            if (instance.Brain.IsInState(State.Wander))
            {
                if (Time.time > _currentStateTimeout)
                {
                    instance.Brain.Fire(Trigger.StartDynamicBehaviour);
                }
                instance.MoveAndAvoidFire(m_targetPosition, dt, 0f);
                return;
            }
            
            if (instance.Brain.IsInState(State.DynamicBehaviour))
            {
                m_dynamicBehaviour.Update(instance, dt);
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
