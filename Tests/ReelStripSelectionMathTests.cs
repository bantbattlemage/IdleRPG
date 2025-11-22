using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SlotsEngine.SelectionMath.Tests
{
    // Simple fake definition describing per-symbol attributes used by selection math
    internal class FakeDef
    {
        public string Name;
        public float Weight;
        public int MaxPerReel; // -1 means unlimited
        public int MatchGroupId; // 0 means no group
        public int FixedCount; // reserved conceptual slots on the strip
        public bool Depletable; // whether FixedCount is consumed as selected

        public FakeDef(string name, float weight, int maxPerReel, int matchGroupId, int fixedCount, bool depletable = true)
        {
            Name = name;
            Weight = weight;
            MaxPerReel = maxPerReel;
            MatchGroupId = matchGroupId;
            FixedCount = fixedCount;
            Depletable = depletable;
        }
    }

    internal class SelectionSimulator
    {
        private readonly int stripSize;
        private readonly FakeDef[] defs;
        private readonly int[] fixedCounts;
        private readonly int[] remainingCounts;
        private readonly bool[] depletable;
        private int picksSoFar;

        public SelectionSimulator(int stripSize, FakeDef[] defs)
        {
            this.stripSize = stripSize;
            this.defs = defs;
            fixedCounts = defs.Select(d => Math.Max(d.FixedCount, 0)).ToArray();
            remainingCounts = fixedCounts.ToArray();
            depletable = defs.Select(d => d.Depletable).ToArray();
            picksSoFar = 0;
        }

        public string Pick(List<(string Name, int MatchGroupId)> existingSelections, bool consumeReserved)
        {
            int remainingSpins = Math.Max(stripSize - picksSoFar, 0);
            // Build usage map for MaxPerReel
            Dictionary<int, int> groupUsage = null;
            if (existingSelections != null && existingSelections.Count > 0)
            {
                groupUsage = new Dictionary<int, int>();
                for (int i = 0; i < existingSelections.Count; i++)
                {
                    var s = existingSelections[i];
                    if (s.MatchGroupId != 0)
                    {
                        groupUsage[s.MatchGroupId] = groupUsage.TryGetValue(s.MatchGroupId, out var c) ? c + 1 : 1;
                    }
                }
            }

            // Compute reserved remaining
            int reservedRemaining = 0;
            for (int i = 0; i < fixedCounts.Length; i++)
            {
                int contribution = depletable[i] ? remainingCounts[i] : fixedCounts[i];
                if (contribution < 0) contribution = 0;
                reservedRemaining += contribution;
            }
            if (reservedRemaining > remainingSpins) reservedRemaining = remainingSpins;
            int randomPoolSize = Math.Max(remainingSpins - reservedRemaining, 0);

            // Total random weight across eligible entries
            float totalRandomWeight = 0f; bool[] randomEligible = new bool[defs.Length];
            for (int i = 0; i < defs.Length; i++)
            {
                bool reservedActive = (!depletable[i] && fixedCounts[i] > 0) || (depletable[i] && remainingCounts[i] > 0);
                if (!reservedActive) { randomEligible[i] = true; totalRandomWeight += defs[i].Weight; }
            }

            var candidates = new List<(int index, float weight)>();
            for (int i = 0; i < defs.Length; i++)
            {
                var d = defs[i];
                // Enforce MaxPerReel when we have existingSelections
                if (existingSelections != null)
                {
                    int max = d.MaxPerReel; if (max >= 0)
                    {
                        int used = 0;
                        if (d.MatchGroupId != 0) { if (groupUsage != null) groupUsage.TryGetValue(d.MatchGroupId, out used); }
                        else { for (int e = 0; e < existingSelections.Count; e++) if (existingSelections[e].Name == d.Name) used++; }
                        if (used >= max) continue;
                    }
                }

                float weightUnits;
                if ((depletable[i] && remainingCounts[i] > 0) || (!depletable[i] && fixedCounts[i] > 0))
                {
                    weightUnits = Math.Min(depletable[i] ? remainingCounts[i] : fixedCounts[i], remainingSpins);
                }
                else
                {
                    if (randomPoolSize <= 0 || !randomEligible[i] || totalRandomWeight <= 0f) continue;
                    weightUnits = (defs[i].Weight / totalRandomWeight) * randomPoolSize;
                    if (weightUnits <= 0f) continue;
                }
                candidates.Add((i, weightUnits));
            }

            if (candidates.Count == 0)
            {
                // fallback to unconstrained by raw weight
                for (int i = 0; i < defs.Length; i++) candidates.Add((i, Math.Max(defs[i].Weight, 1f)));
            }

            // Deterministic pick: choose highest weight, then by name ascending for tie-break.
            candidates.Sort((a,b)=> { int c = b.weight.CompareTo(a.weight); if (c != 0) return c; return string.Compare(defs[a.index].Name, defs[b.index].Name, StringComparison.Ordinal); });
            var chosen = candidates[0].index;

            if (consumeReserved && depletable[chosen] && remainingCounts[chosen] > 0) remainingCounts[chosen]--;
            if (consumeReserved) picksSoFar++;
            return defs[chosen].Name;
        }

        public int RemainingFor(string name) { for (int i = 0; i < defs.Length; i++) if (defs[i].Name == name) return remainingCounts[i]; return 0; }
    }

    public class ReelStripSelectionMathTests
    {
        [Test]
        public void AllReservedSlots_Depletable_FirstStripFill_AllFromThatSymbol()
        {
            int strip = 12;
            var defs = new[]
            {
                new FakeDef("0", weight: 1f, maxPerReel: -1, matchGroupId: 0, fixedCount: 12, depletable: true),
                new FakeDef("A", weight: 10f, maxPerReel: -1, matchGroupId: 1, fixedCount: 0),
                new FakeDef("B", weight: 5f, maxPerReel: -1, matchGroupId: 2, fixedCount: 0)
            };
            var sim = new SelectionSimulator(strip, defs);
            var existing = new List<(string,int)>();

            for (int i = 0; i < strip; i++)
            {
                var pick = sim.Pick(existing, consumeReserved: true);
                Assert.AreEqual("0", pick);
                existing.Add((pick, 0));
            }
            // After 12 consumes, reserved remaining for "0" should be 0
            Assert.AreEqual(0, sim.RemainingFor("0"));
        }

        [Test]
        public void NonDepletable_Count_Is_Always_Chosen()
        {
            int strip = 12;
            var defs = new[]
            {
                new FakeDef("0", weight: 1f, maxPerReel: -1, matchGroupId: 0, fixedCount: 12, depletable: false),
                new FakeDef("A", weight: 100f, maxPerReel: -1, matchGroupId: 1, fixedCount: 0)
            };
            var sim = new SelectionSimulator(strip, defs);
            var existing = new List<(string,int)>();

            for (int i = 0; i < 30; i++)
            {
                var pick = sim.Pick(existing, consumeReserved: true);
                Assert.AreEqual("0", pick, "Non-depletable reserved should monopolize selection");
                existing.Add((pick, 0));
            }
        }

        [Test]
        public void Dummy_Picks_Do_Not_Consume_Reserved_Counts()
        {
            int strip = 10;
            var defs = new[]
            {
                new FakeDef("0", weight: 1f, maxPerReel: -1, matchGroupId: 0, fixedCount: 4, depletable: true),
                new FakeDef("A", weight: 2f, maxPerReel: -1, matchGroupId: 1, fixedCount: 0)
            };
            var sim = new SelectionSimulator(strip, defs);
            var existing = new List<(string,int)>();

            // Generate 6 dummy symbols without consuming; order not asserted, only that reserve remains intact
            for (int i = 0; i < 6; i++)
            {
                var pick = sim.Pick(existing, consumeReserved: false);
                Assert.IsNotNull(pick);
            }

            // Now perform full strip of real picks and tally occurrences
            int zeros = 0; int pulls = strip;
            for (int i = 0; i < pulls; i++)
            {
                var pick = sim.Pick(existing, consumeReserved: true);
                if (pick == "0") zeros++;
            }
            Assert.AreEqual(4, zeros, "Reserved count should appear exactly as many times as fixed count when depletable.");
            Assert.AreEqual(0, sim.RemainingFor("0"));
        }

        [Test]
        public void MaxPerReel_Respected_With_Reserved_Math()
        {
            int strip = 8;
            var defs = new[]
            {
                new FakeDef("X", weight: 1f, maxPerReel: 1, matchGroupId: 42, fixedCount: 3, depletable: true),
                new FakeDef("Y", weight: 10f, maxPerReel: -1, matchGroupId: 99, fixedCount: 0)
            };
            var sim = new SelectionSimulator(strip, defs);
            var existing = new List<(string,int)>{ ("X", 42) }; // already one X used

            // Because X reached max(1), pick must be Y even though X has reserved counts
            var pick = sim.Pick(existing, consumeReserved: true);
            Assert.AreEqual("Y", pick);
        }

        [Test]
        public void RandomPool_Distributes_By_Weights_When_No_Reserve_Remains()
        {
            int strip = 6;
            var defs = new[]
            {
                new FakeDef("A", weight: 1f, maxPerReel: -1, matchGroupId: 0, fixedCount: 0),
                new FakeDef("B", weight: 3f, maxPerReel: -1, matchGroupId: 0, fixedCount: 0),
                new FakeDef("C", weight: 6f, maxPerReel: -1, matchGroupId: 0, fixedCount: 0)
            };
            var sim = new SelectionSimulator(strip, defs);
            var existing = new List<(string,int)>();

            // Our deterministic picker selects the one with greatest share of random pool, here C
            var first = sim.Pick(existing, consumeReserved: true);
            Assert.AreEqual("C", first);
        }
    }
}
