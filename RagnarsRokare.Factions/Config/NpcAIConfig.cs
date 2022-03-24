using RagnarsRokare.MobAI;

namespace RagnarsRokare.Factions
{
    public class NpcAIConfig : MobAIBaseConfig
    {
        public float EatInterval { get; set; } = 1000;
        public string[] AcceptedContainers { get; set; } = new string[] { "piece_chest_wood" };
    }
}
