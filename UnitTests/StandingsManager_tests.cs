using Microsoft.VisualStudio.TestTools.UnitTesting;
using RagnarsRokare.Factions;
using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace UnitTests
{
    [TestClass]
    public class StandingsManager_tests
    {
        [TestMethod]
        public void StandingsManager_GetStandings_none()
        {
            var standingString = string.Empty;

            var result = RagnarsRokare.Factions.StandingsManager.GetStandings(standingString);
            Assert.AreEqual(0, result.Count());
        }

        [TestMethod]
        public void StandingsManager_GetStandings_single()
        {
            var faction = new Faction { FactionId = Guid.NewGuid().ToString() };
            var standingString = $"{faction.FactionId};{(5.5f).ToString(CultureInfo.InvariantCulture)}";

            var result = RagnarsRokare.Factions.StandingsManager.GetStandings(standingString);
            Assert.AreEqual(1, result.Count());
            Assert.AreEqual(result.First().Item1, faction.FactionId.ToString());
            Assert.AreEqual(result.First().Item2, 5.5f);
        }

        [TestMethod]
        public void StandingsManager_GetStandings_multi()
        {
            var faction1 = new Faction { FactionId = Guid.NewGuid().ToString() };
            var faction2 = new Faction { FactionId = Guid.NewGuid().ToString() };
            var faction3 = new Faction { FactionId = Guid.NewGuid().ToString() };
            var faction4 = new Faction { FactionId = Guid.NewGuid().ToString() };
            var faction5 = new Faction { FactionId = Guid.NewGuid().ToString() };
            var faction6 = new Faction { FactionId = Guid.NewGuid().ToString() };
            var standingString = new StringBuilder();
            standingString.Append($"{faction1.FactionId};{(0.5f).ToString(CultureInfo.InvariantCulture)}|");
            standingString.Append($"{faction2.FactionId};{(15.5f).ToString(CultureInfo.InvariantCulture)}|");
            standingString.Append($"{faction3.FactionId};{(25.5f).ToString(CultureInfo.InvariantCulture)}|");
            standingString.Append($"{faction4.FactionId};{(18.5f).ToString(CultureInfo.InvariantCulture)}|");
            standingString.Append($"{faction5.FactionId};{(30.0f).ToString(CultureInfo.InvariantCulture)}|");
            standingString.Append($"{faction6.FactionId};{(5.0f).ToString(CultureInfo.InvariantCulture)}");

            var result = RagnarsRokare.Factions.StandingsManager.GetStandings(standingString.ToString());
            Assert.AreEqual(6, result.Count());
            Assert.AreEqual(result.ElementAt(0).Item1, faction1.FactionId.ToString());
            Assert.AreEqual(result.ElementAt(0).Item2, 0.5f);
            Assert.AreEqual(result.ElementAt(1).Item1, faction2.FactionId.ToString());
            Assert.AreEqual(result.ElementAt(1).Item2, 15.5f);
            Assert.AreEqual(result.ElementAt(2).Item1, faction3.FactionId.ToString());
            Assert.AreEqual(result.ElementAt(2).Item2, 25.5f);
            Assert.AreEqual(result.ElementAt(3).Item1, faction4.FactionId.ToString());
            Assert.AreEqual(result.ElementAt(3).Item2, 18.5f);
            Assert.AreEqual(result.ElementAt(4).Item1, faction5.FactionId.ToString());
            Assert.AreEqual(result.ElementAt(4).Item2, 30.0f);
            Assert.AreEqual(result.ElementAt(5).Item1, faction6.FactionId.ToString());
            Assert.AreEqual(result.ElementAt(5).Item2, 5.0f);
        }
    }
}
