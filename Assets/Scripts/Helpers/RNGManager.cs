using System;

/// <summary>
/// Centralized seeded RNG manager. Use SetSeed to create deterministic sequences.
/// Thread-safe access to System.Random methods used across the project.
/// </summary>
public static class RNGManager
{
    private static Random rng = new Random(Environment.TickCount);
    private static readonly object sync = new object();
    private static int currentSeed = Environment.TickCount;

    /// <summary>
    /// Set a fixed seed for deterministic behavior. Call this at startup when you need reproducible runs.
    /// </summary>
    public static void SetSeed(int seed)
    {
        lock (sync)
        {
            currentSeed = seed;
            rng = new Random(seed);
        }
    }

    /// <summary>
    /// Re-seed using an environment/time-derived value (non-deterministic).
    /// </summary>
    public static void Reseed()
    {
        SetSeed(Environment.TickCount);
    }

    public static int Next()
    {
        lock (sync) { return rng.Next(); }
    }

    public static int Next(int maxValue)
    {
        lock (sync) { return rng.Next(maxValue); }
    }

    public static int Next(int minValue, int maxValue)
    {
        lock (sync) { return rng.Next(minValue, maxValue); }
    }

    public static double NextDouble()
    {
        lock (sync) { return rng.NextDouble(); }
    }

    public static float RangeFloat(float minInclusive, float maxExclusive)
    {
        if (maxExclusive <= minInclusive) return minInclusive;
        lock (sync)
        {
            double d = rng.NextDouble();
            return (float)(minInclusive + d * (maxExclusive - minInclusive));
        }
    }

    /// <summary>
    /// Returns an int in [minInclusive, maxExclusive).
    /// </summary>
    public static int Range(int minInclusive, int maxExclusive)
    {
        return Next(minInclusive, maxExclusive);
    }

    /// <summary>
    /// Return the current seed used by the RNGManager.
    /// </summary>
    public static int CurrentSeed => currentSeed;
}
