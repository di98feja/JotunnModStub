using RagnarsRokare.MobAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RagnarsRokare.Factions
{
    internal class NpcAI : MobAI.MobAIBase
    {

        public override void Follow(Player player)
        {
            throw new NotImplementedException();
        }

        public override void GotShoutedAtBy(MobAIBase mob)
        {
            throw new NotImplementedException();
        }

        protected override void RPC_MobCommand(long sender, ZDOID playerId, string command)
        {
            throw new NotImplementedException();
        }
    }
}
