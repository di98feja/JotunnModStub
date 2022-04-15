using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RagnarsRokare.Factions
{
    public static class ErrandsManager
    {
        public class Errand
        {
            public uint Id { get; set; }
            public string RequestString { get; set; }
            public ItemDrop.ItemData RequestItem { get; set; }
            public int RequestItemAmount { get; set; }
            public string CompletedString { get; set; }
            public string CanceledString { get; set; }
        }

        private static Errand[] Errands;
        private const string RX_PlayerErrands = @"(?<npcId>\d+);(?<errandId>\d+)\|*";

        public static void Init()
        {
            Errands = new Errand[]
            {
                new Errand
                {
                    Id = 0,
                    RequestString = "No errands available",
                    CompletedString = "Nothing to do",
                    CanceledString = "Nothing to cancel"
                },
                new Errand
                {
                    Id = 1,
                    RequestString = $"I long for some tender meat, could you bring me some $item_necktailgrilled ?",
                    RequestItem = ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Consumable, "NeckTailGrilled").FirstOrDefault()?.m_itemData,
                    RequestItemAmount = 2,
                    CompletedString = $"Ahh, thank you!    That smells so good!",
                    CanceledString = "Oh...  to bad, I was really looking forward to a good meal.."
                },
                new Errand
                {
                    Id = 2,
                    RequestString = $"I was chased by some boars the other day and then they ate all my berries. \nCould you teach them a lesson?",
                    RequestItem = ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Trophie, "TrophyBoar").FirstOrDefault()?.m_itemData,
                    RequestItemAmount = 1,
                    CompletedString = "Ha! These won't eat any more of my berries! You have my gratitude.",
                    CanceledString = "They too much for you?  I really thought you could handle some pigs."
                },
                new Errand
                {
                    Id = 3,
                    RequestString = $"It is something I remember from the days before...this place.  the sweet taste of honey. \nCould you bring me some?",
                    RequestItem = ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Consumable, "Honey").FirstOrDefault()?.m_itemData,
                    RequestItemAmount = 2,
                    CompletedString = "*gets a dreamy look* Yes!  It is every bit as good as I remembered it. \nThank you, thank you!",
                    CanceledString = "No? You could not find any?"
                },
                new Errand
                {
                    Id = 4,
                    RequestString = $"I need to patch my shirt. \nIf you could bring me some leather scraps I would be most grateful!",
                    RequestItem = ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Material, "LeatherScraps").FirstOrDefault()?.m_itemData,
                    RequestItemAmount = 5,
                    CompletedString = "Ahh!  Yes, these will do nicely. \nThank you!",
                    CanceledString = "No? You could not find any?"
                }
            };
        }

        public static Errand GetRandomErrand()
        {
            return Errands[UnityEngine.Random.Range(1, Errands.Length)];
        }

        public static bool CanStartNewErrand(ZDO npcZdo)
        {
            return npcZdo.GetInt(Misc.Constants.Z_NpcActiveErrand) == 0;
        }

        public static void StartErrand(uint errandId, ZDO npcZdo, Player player)
        {
            if (!CanStartNewErrand(npcZdo)) return;
            var playerZdo = player.m_nview.GetZDO();
            var activeErrandsString = RemoveThisNpcErrandFromPlayer(npcZdo, playerZdo);
            activeErrandsString = $"{activeErrandsString}|{npcZdo.m_uid.id};{errandId}";
            playerZdo.Set(Misc.Constants.Z_PlayerActiveErrands, activeErrandsString);
            npcZdo.Set(Misc.Constants.Z_NpcActiveErrand, $"{playerZdo.m_uid.id};{errandId}");
        }

        private static string RemoveThisNpcErrandFromPlayer(ZDO npcZdo, ZDO playerZdo)
        {
            var activeErrandsString = playerZdo.GetString(Misc.Constants.Z_PlayerActiveErrands);
            if (activeErrandsString.Contains(npcZdo.m_uid.id.ToString()))
            {
                var errandToRemove = ParseErrandsString(activeErrandsString).Where(e => e.Item1 == npcZdo.m_uid.id).Select(e => $"|{e.Item1};{e.Item2}").First();
                activeErrandsString = activeErrandsString.Replace(errandToRemove, "");
            }

            return activeErrandsString;
        }

        public static bool HasErrand(ZDO npcZdo, Player player)
        {
            var activeErrandsString = player.m_nview.GetZDO().GetString(Misc.Constants.Z_PlayerActiveErrands);
            bool playerHasErrand = activeErrandsString.Contains(npcZdo.m_uid.id.ToString());
            bool npcHasErrand = npcZdo.GetString(Misc.Constants.Z_NpcActiveErrand).Contains(player.m_nview.GetZDO().m_uid.id.ToString());
            if (playerHasErrand && npcHasErrand)
            {
                return true;
            }
            else
            {
                CancelErrand(npcZdo);
                return false;
            }
        }

        public static bool CanCompleteErrand(ZDO npcZdo, Player player)
        {
            if (!HasErrand(npcZdo, player)) return false;

            var activeErrand = ParseErrandsString(npcZdo.GetString(Misc.Constants.Z_NpcActiveErrand)).First();
            return player.m_inventory.CountItems(Errands[activeErrand.Item2].RequestItem.m_shared.m_name) >= Errands[activeErrand.Item2].RequestItemAmount;
        }

        public static string CompleteErrand(ZDO npcZdo, Player player)
        {
            var activeErrand = ParseErrandsString(npcZdo.GetString(Misc.Constants.Z_NpcActiveErrand)).First();
            var errand = Errands[activeErrand.Item2];

            var inventoryItem = player.m_inventory.GetItem(errand.RequestItem.m_shared.m_name);
            bool success = player.GetInventory().RemoveItem(inventoryItem, errand.RequestItemAmount);
            npcZdo.Set(Misc.Constants.Z_NpcActiveErrand, $"{0};{0}");
            RemoveThisNpcErrandFromPlayer(npcZdo, Player.m_localPlayer.m_nview.GetZDO());
            StandingsManager.IncreaseStandingTowards(npcZdo, FactionManager.GetPlayerFaction(player), Misc.Constants.ErrandStandingIncrease);
            return errand.CompletedString;
        }

        public static string CancelErrand(ZDO npcZdo)
        {
            var currentErrand = ParseErrandsString(npcZdo.GetString(Misc.Constants.Z_NpcActiveErrand));
            if (!currentErrand.Any())
            {
                return Errands[0].CanceledString;
            }
            else
            {
                npcZdo.Set(Misc.Constants.Z_NpcActiveErrand, $"{0};{0}");
                RemoveThisNpcErrandFromPlayer(npcZdo, Player.m_localPlayer.m_nview.GetZDO());
                return Errands[currentErrand.First().Item2].CanceledString;
            }
        }
        private static IEnumerable<(uint, uint)> ParseErrandsString(string errandsString)
        {
            foreach (System.Text.RegularExpressions.Match faction in System.Text.RegularExpressions.Regex.Matches(errandsString, RX_PlayerErrands))
            {
                yield return (uint.Parse(faction.Groups["npcId"].Value), uint.Parse(faction.Groups["errandId"].Value, CultureInfo.InvariantCulture));
            }

        }

    }
}
