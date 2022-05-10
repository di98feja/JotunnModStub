using RagnarsRokare.MobAI;
using Stateless;
using UnityEngine;

namespace RagnarsRokare.Factions
{
    internal class WorkdayBehaviour : IDynamicBehaviour
    {
        private const string Prefix = "RR_Workday";

        private DynamicSortingBehaviour m_dynamicSortingBehaviour;
        private DynamicWorkerBehaviour m_dynamicWorkerBehaviour;
        private DynamicRepairingBehaviour m_dynamicRepairingBehaviour;
        private IDynamicBehaviour m_currentBehaviour;
        private float m_currentTaskTimer;

        private StateDef State { get; set; }

        private sealed class StateDef
        {
            private readonly string prefix;

            public string Main { get { return $"{prefix}Main"; } }
            public string Sorting { get { return $"{prefix}Sorting"; } }
            public string Assignments { get { return $"{prefix}Assignments"; } }
            public string Repairing { get { return $"{prefix}Repairing"; } }

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
            public string Sort { get { return $"{prefix}Sort"; } }
            public string DoAssignments { get { return $"{prefix}DoAssignments"; } }
            public string Repair { get { return $"{prefix}Repair"; } }
            public string StartWork { get { return $"{prefix}StartWork"; } }
            public string ChangeWork { get { return $"{prefix}ChangeWork"; } }
            public TriggerDef(string prefix)
            {
                this.prefix = prefix;
            }
        }

        // Settings
        public string StartState => State.Main;
        public string SuccessState { get; set; }
        public string FailState { get; set; }
        public float TaskTimeout { get; set; } = 20f;

        public void Abort()
        {
            m_currentBehaviour.Abort();
            m_currentBehaviour = null;
        }

        public void Configure(MobAIBase aiBase, StateMachine<string, string> brain, string parentState)
        {
            State = new StateDef(parentState + Prefix);
            Trigger = new TriggerDef(parentState + Prefix);
            var npcAi = aiBase as NpcAI;
            m_dynamicSortingBehaviour = new DynamicSortingBehaviour();
            m_dynamicSortingBehaviour.SuccessState = State.Main;
            m_dynamicSortingBehaviour.FailState = State.Main;
            m_dynamicSortingBehaviour.Configure(npcAi, brain, State.Main);

            m_dynamicWorkerBehaviour = new DynamicWorkerBehaviour();
            m_dynamicWorkerBehaviour.SuccessState = State.Main;
            m_dynamicWorkerBehaviour.FailState = State.Main;
            m_dynamicWorkerBehaviour.AcceptedContainerNames = npcAi.AcceptedContainerNames;
            m_dynamicWorkerBehaviour.Configure(npcAi, brain, State.Main);

            m_dynamicRepairingBehaviour = new DynamicRepairingBehaviour();
            m_dynamicRepairingBehaviour.SuccessState = State.Main;
            m_dynamicRepairingBehaviour.FailState= State.Main;
            m_dynamicRepairingBehaviour.Configure(npcAi, brain, State.Main);

            brain.Configure(State.Main)
               .SubstateOf(parentState)
               .PermitReentry(Trigger.ChangeWork)
               .PermitDynamic(Trigger.Abort, () => FailState)
               .PermitDynamic(Trigger.StartWork, () => m_currentBehaviour.StartState)
               .OnEntry(t =>
               {
                   aiBase.StopMoving();
                   aiBase.UpdateAiStatus("Starting workday");
                   Common.Dbgl("Entered WorkdayBehaviour", true, "NPC");

                   switch (UnityEngine.Random.Range(0, 3))
                   {
                       case 0:
                           m_currentBehaviour = m_dynamicSortingBehaviour;
                           break;
                       case 1:
                            m_currentBehaviour = m_dynamicWorkerBehaviour;
                           break;
                       case 2:
                           m_currentBehaviour = m_dynamicRepairingBehaviour;
                           break;
                   }

                   m_currentTaskTimer = Time.time + TaskTimeout;
                   brain.Fire(Trigger.StartWork);
               });
        }

        public void Update(MobAIBase aiBase, float dt)
        {
            if (Time.time > m_currentTaskTimer)
            {
                m_currentBehaviour.Abort();
                aiBase.Brain.Fire(Trigger.ChangeWork);
                return;
            }
            else
            {
                m_currentBehaviour.Update(aiBase, dt);
            }
        }
    }
}
