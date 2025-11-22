using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace ReelStripTests
{
    // Minimal fake defs used to test the pool depletion & weighting algorithm
    public class FakeDef
    {
        public string Name;
        public float Weight;
        public int MatchGroupId;
        public FakeDef(string name, float weight, int group = 0)
        {
            Name = name; Weight = weight; MatchGroupId = group;
        }
    }

    public class FakeData
    {
        public int MatchGroupId;
        public FakeData(int g) { MatchGroupId = g; }
    }

    public static class PoolPicker
    {
        // Reimplementation of the selection candidate construction used by ReelStripData.PickFromPool
        public static List<(string name, float effectiveWeight)> BuildCandidates(FakeDef[] pool, int[] counts, List<FakeData> existing)
        {
            var candidates = new List<(string, float)>();
            for (int i = 0; i < pool.Length; i++)
            {
                var sd = pool[i];
                if (sd == null) continue;
                float w = sd.Weight; if (w < 0f) w = 0f;

                if (existing != null && counts != null && i < counts.Length && counts[i] >= 0)
                {
                    int declared = counts[i];
                    int already = 0;
                    for (int j = 0; j < existing.Count; j++) if (existing[j] != null && existing[j].MatchGroupId == sd.MatchGroupId) already++;
                    int remaining = declared - already;
                    if (remaining <= 0) continue;
                    candidates.Add((sd.Name, w * remaining));
                }
                else
                {
                    candidates.Add((sd.Name, w));
                }
            }
            return candidates;
        }

        // Fallback behavior builds weight-only list
        public static List<(string name, float effectiveWeight)> BuildFallback(FakeDef[] pool)
        {
            var list = new List<(string, float)>();
            for (int i = 0; i < pool.Length; i++)
            {
                var sd = pool[i]; if (sd == null) continue;
                float w = sd.Weight; if (w < 0f) w = 0f;
                list.Add((sd.Name, w));
            }
            return list;
        }
    }

    public class Tests
    {
        [Test]
        public void When_NoCounts_Then_WeightsUsed()
        {
            var pool = new FakeDef[] { new FakeDef("A", 1f), new FakeDef("B", 3f) };
            int[] counts = null;
            var candidates = PoolPicker.BuildCandidates(pool, counts, null);
            Assert.AreEqual(2, candidates.Count);
            Assert.AreEqual(("A", 1f), candidates[0]);
            Assert.AreEqual(("B", 3f), candidates[1]);
        }

        [Test]
        public void When_CountsAndExisting_Then_WeightsScaledByRemaining()
        {
            var pool = new FakeDef[] { new FakeDef("A", 1f, group: 1), new FakeDef("B", 2f, group: 2) };
            int[] counts = new int[] { 6, -1 }; // A has 6 copies, B unlimited
            var existing = new List<FakeData> { new FakeData(1) }; // one A already picked
            var candidates = PoolPicker.BuildCandidates(pool, counts, existing);
            // A remaining = 5 -> effective weight = 1 * 5 = 5
            // B unlimited -> weight = 2
            Assert.AreEqual(2, candidates.Count);
            var a = candidates.First(c => c.name == "A");
            var b = candidates.First(c => c.name == "B");
            Assert.AreEqual(5f, a.effectiveWeight);
            Assert.AreEqual(2f, b.effectiveWeight);
        }

        [Test]
        public void When_SymbolExhausted_Then_Excluded()
        {
            var pool = new FakeDef[] { new FakeDef("A", 1f, group: 1), new FakeDef("B", 2f, group: 2) };
            int[] counts = new int[] { 1, -1 }; // A has only 1 copy
            var existing = new List<FakeData> { new FakeData(1) }; // A already picked
            var candidates = PoolPicker.BuildCandidates(pool, counts, existing);
            // A exhausted -> excluded, only B remains
            Assert.AreEqual(1, candidates.Count);
            Assert.AreEqual("B", candidates[0].name);
        }

        [Test]
        public void When_AllExhausted_FallbackToWeightOnly()
        {
            var pool = new FakeDef[] { new FakeDef("A", 1f), new FakeDef("B", 2f) };
            int[] counts = new int[] { 0, 0 }; // both exhausted
            var existing = new List<FakeData>();
            var candidates = PoolPicker.BuildCandidates(pool, counts, existing);
            // BuildCandidates will return empty; simulate fallback
            if (candidates.Count == 0) candidates = PoolPicker.BuildFallback(pool);
            Assert.AreEqual(2, candidates.Count);
            Assert.AreEqual(("A", 1f), candidates[0]);
            Assert.AreEqual(("B", 2f), candidates[1]);
        }

        [Test]
        public void MatchGroupCounting_WorksAcrossExistingSelections()
        {
            var pool = new FakeDef[] { new FakeDef("A1", 1f, group: 10), new FakeDef("A2", 1.5f, group: 10), new FakeDef("C", 2f, group: 20) };
            int[] counts = new int[] { 3, 3, -1 }; // A1 and A2 share group 10 and each entry has its own declared count
            var existing = new List<FakeData> { new FakeData(10), new FakeData(10) }; // two existing that match group 10
            var candidates = PoolPicker.BuildCandidates(pool, counts, existing);
            // For A1 (index 0): declared 3, already 2 -> remaining 1 -> effective weight = 1*1 =1
            // For A2 (index 1): declared 3, already 2 -> remaining 1 -> effective weight =1.5*1=1.5
            // C unlimited -> weight 2
            var a1 = candidates.First(c => c.name == "A1");
            var a2 = candidates.First(c => c.name == "A2");
            var c = candidates.First(c => c.name == "C");
            Assert.AreEqual(1f, a1.effectiveWeight);
            Assert.AreEqual(1.5f, a2.effectiveWeight);
            Assert.AreEqual(2f, c.effectiveWeight);
        }
    }
}
