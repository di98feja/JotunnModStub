﻿using RagnarsRokare.MobAI;
using Stateless;
using System;
using UnityEngine;

namespace RagnarsRokare.Factions
{
    public class ApathyBehaviour : IDynamicBehaviour
    {
        private const string Prefix = "RR_Apathy";
        private float _currentStateTimeout;
        private Vector3 m_targetPosition;

        private class State
        {
            public const string Main = Prefix + "Main";
            public const string Sit = Prefix + "Sit";
            public const string Wander = Prefix + "Wander";
        }
        private class Trigger
        {
            public const string Abort = Prefix + "Abort";
            public const string SitDown = Prefix + "SitDown";
            public const string RandomWalk = Prefix + "RandomWalk";

        }

        private String[] Comments = new String[]
        {
            "Curse the gods for putting me here",
            "I give up",
            "What is the meaning of this?",
            "*sobs*",
            "All I do is die, die die...",
            "Everything is so pointless",
            "*stares blankly into the void*",
            "*looks utterly defeated*"
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
            brain.Configure(State.Main)
               .InitialTransition(State.Sit)
               .SubstateOf(parentState)
               .PermitDynamic(Trigger.Abort, () => FailState)
               .OnEntry(t =>
               {
                   aiBase.UpdateAiStatus("Apathy");
                   Common.Dbgl("Entered ApathyBehaviour", true, "NPC");
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
                })
                .OnExit(t =>
                {
                    On.Character.IsEncumbered -= Character_IsEncumbered;
                    aiBase.Character.m_zanim.SetBool(Character.encumbered, value: false);
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

            if (instance.Brain.IsInState(State.Sit))
            {
                if (_currentStateTimeout > Time.time) return;

                instance.Brain.Fire(Trigger.RandomWalk);
                return;
            }

            if (instance.Brain.IsInState(State.Wander))
            {
                if (Time.time > _currentStateTimeout)
                {
                    instance.Brain.Fire(Trigger.SitDown);
                }
                instance.MoveAndAvoidFire(m_targetPosition, dt, 0f);
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