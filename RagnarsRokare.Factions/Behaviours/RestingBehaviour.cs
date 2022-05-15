using RagnarsRokare.MobAI;
using Stateless;
using UnityEngine;

namespace RagnarsRokare.Factions
{
    internal class RestingBehaviour : IDynamicBehaviour
    {
        private const string Prefix = "RR_Resting";

        private SleepBehaviour m_sleepBehaviour;
        private SitBehaviour m_sitBehaviour;
        private DynamicEatingBehaviour m_eatingBehaviour;

        private IDynamicBehaviour m_currentBehaviour;
        private float m_currentStateTimer;

        private StateDef State { get; set; }

        private sealed class StateDef
        {
            private readonly string prefix;

            public string Main { get { return $"{prefix}Main"; } }
            public string Sit { get { return $"{prefix}Sit"; } }
            public string Sleep { get { return $"{prefix}Sleep"; } }
            public string Eating { get { return $"{prefix}Eating"; } }
            public string SubstateExit { get { return $"{prefix}SubstateExit"; } }

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
            public string ReEnter { get { return $"{prefix}ReEnter"; } }
            public TriggerDef(string prefix)
            {
                this.prefix = prefix;
            }
        }

        private string[] Comments = new string[]
        {
            "Ahh, my feet needed this",
            "*humming quietly*"
        };
        private Vector3 lastKnownFoodPosition;

        // Settings
        public string StartState => State.Main;
        public string SuccessState { get; set; }
        public string FailState { get; set; }
        public float RestUpdateTimeout { get; set; } = 20f;
        public Vector3 LastKnownFoodPosition
        {
            get => lastKnownFoodPosition;
            set
            {
                lastKnownFoodPosition = value;
                if (m_eatingBehaviour != null)
                {
                    m_eatingBehaviour.LastKnownFoodPosition = value;
                }
            }
        }
        public float RandomCommentChance { get; set; } = 10f;

        public void Abort()
        {
            m_currentBehaviour?.Abort();
            m_currentBehaviour = null;
        }

        public void Configure(MobAIBase aiBase, StateMachine<string, string> brain, string parentState)
        {
            State = new StateDef(parentState + Prefix);
            Trigger = new TriggerDef(parentState + Prefix);
            var npcAi = aiBase as NpcAI;
            m_sleepBehaviour = new SleepBehaviour();
            m_sleepBehaviour.SuccessState = State.SubstateExit;
            m_sleepBehaviour.FailState = State.SubstateExit;
            m_sleepBehaviour.Configure(npcAi, brain, State.Sleep);
            m_sleepBehaviour.SleepUntilMorning = true;

            m_eatingBehaviour = new DynamicEatingBehaviour();
            m_eatingBehaviour.SuccessState = State.SubstateExit;
            m_eatingBehaviour.FailState = State.SubstateExit;
            m_eatingBehaviour.HealPercentageOnConsume = 0.25f + (StandingsManager.GetStandingTowards(npcAi.NView.GetZDO(), FactionManager.GetNpcFaction(npcAi.NView.GetZDO()))) / 100;
            m_eatingBehaviour.Configure(npcAi, brain, State.Eating);

            m_sitBehaviour = new SitBehaviour();
            m_sitBehaviour.SuccessState = State.SubstateExit;
            m_sitBehaviour.FailState = State.SubstateExit;
            m_sitBehaviour.SitTime = 60 + UnityEngine.Random.Range(-20, 20);
            m_sitBehaviour.Configure(npcAi, brain, State.Sit);

            brain.Configure(State.Main)
               .SubstateOf(parentState)
               .PermitDynamic(Trigger.Abort, () => FailState)
               .Permit(Trigger.FindFood, State.Eating)
               .Permit(Trigger.GotoBed, State.Sleep)
               .Permit(Trigger.SitDown, State.Sit)
               .PermitReentry(Trigger.ReEnter)
               .OnEntry(t =>
               {
                   aiBase.StopMoving();
                   aiBase.UpdateAiStatus("Starting Resting");
                   Common.Dbgl("Entered RestingBehaviour", true, "NPC");

                   var selectedState = SelectState(npcAi);
                   m_currentBehaviour = selectedState.behaviour;
                   m_currentStateTimer = Time.time + RestUpdateTimeout;
                   aiBase.Brain.Fire(selectedState.trigger);
               })
               .OnExit(() => Abort());

            brain.Configure(State.SubstateExit)
                .SubstateOf(State.Main)
                .OnEntry(() =>
                {
                    aiBase.Brain.Fire(Trigger.ReEnter);
                });

            brain.Configure(State.Sleep)
                .SubstateOf(State.Main)
                .InitialTransition(m_sleepBehaviour.StartState);

            brain.Configure(State.Eating)
                .SubstateOf(State.Main)
                .InitialTransition(m_eatingBehaviour.StartState);

            brain.Configure(State.Sit)
                .SubstateOf(State.Main)
                .InitialTransition(m_sitBehaviour.StartState)
                .OnEntry(t =>
                {
                    npcAi.UpdateAiStatus(State.Sit);
                    if (UnityEngine.Random.Range(0f, 100f) <= RandomCommentChance)
                    {
                        SayRandomThing(npcAi.Character);
                    }
                    Debug.Log($"{npcAi.Character.m_name}: Switching to Sit.");
                });
        }

        private (string trigger, IDynamicBehaviour behaviour) SelectState(NpcAI npcAi)
        {
            if (m_eatingBehaviour.IsHungry(npcAi.IsHurt))
            {
                return (Trigger.FindFood, m_eatingBehaviour);
            }
            else if (EnvMan.instance.IsNight())
            {
                return (Trigger.GotoBed, m_sleepBehaviour);
            }
            else if (EnvMan.instance.GetDayFraction() >= 0.70f)
            {
                return (Trigger.SitDown, m_sitBehaviour);
            }
            else
            {
                return (Trigger.SitDown, m_sitBehaviour);
            }
        }

        public bool RainCheck(MobAIBase mobAi)
        {
            if (EnvMan.instance.IsWet() || EnvMan.instance.IsCold() || EnvMan.instance.IsFreezing())
            {
                var motivation = MotivationManager.GetMotivation(mobAi.NView.GetZDO());
                if (motivation < Misc.Constants.Motivation_Hopefull)
                {
                    if (m_currentBehaviour != m_sitBehaviour)
                    {
                        return true;
                    }
                }
                else if (motivation < Misc.Constants.Motivation_Inspired && UnityEngine.Random.value < 0.5f)
                {
                    if (m_currentBehaviour != m_sitBehaviour)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public void Update(MobAIBase aiBase, float dt)
        {
            if (aiBase.Brain.IsInState(State.Sleep) && m_sleepBehaviour.SleepUntilMorning)
            {
                m_currentBehaviour.Update(aiBase, dt);
                return;
            }
            else if (Time.time > m_currentStateTimer)
            {
                var selectedState = SelectState(aiBase as NpcAI);
                if (selectedState.behaviour != m_currentBehaviour)
                {
                    m_currentBehaviour.Abort();
                    m_currentBehaviour = selectedState.behaviour;
                    m_currentStateTimer = Time.time + RestUpdateTimeout;
                    aiBase.Brain.Fire(selectedState.trigger);
                    return;
                }
                else
                {
                    m_currentStateTimer = Time.time + RestUpdateTimeout;
                    return;
                }
            }
            else
            {
                m_currentBehaviour.Update(aiBase, dt);
            }
        }

        private void SayRandomThing(Character npc)
        {
            int index = UnityEngine.Random.Range(0, Comments.Length);
            npc.GetComponent<Talker>().Say(Talker.Type.Normal, Comments[index]);
        }
    }
}
