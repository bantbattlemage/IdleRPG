using System;

/// <summary>
/// Centralized seeded RNG manager. Use <see cref="SetSeed(int)"/> to create deterministic sequences.
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

    /// <summary>
    /// Returns a non-negative random integer.
    /// </summary>
    /// <returns>A non-negative random integer.</returns>
    public static int Next()
    {
        lock (sync) { return rng.Next(); }
    }

    /// <summary>
    /// Returns a non-negative random integer less than the specified maximum.
    /// </summary>
    /// <param name="maxValue">The exclusive upper bound of the random number returned.

    /// <returns>A non-negative random integer less than maxValue.</returns>
    public static int Next(int maxValue)
    {
        lock (sync) { return rng.Next(maxValue); }
    }

    /// <summary>
    /// Returns a random integer within a specified range.
    /// </summary>
    /// <param name="minValue">The inclusive lower bound of the random number returned.</param>
    /// <param name="maxValue">The exclusive upper bound of the random number returned.</param>
    /// <returns>A random integer between minValue and maxValue.</returns>
    public static int Next(int minValue, int maxValue)
    {
        lock (sync) { return rng.Next(minValue, maxValue); }
    }

    /// <summary>
    /// Returns a random double-precision floating-point number between 0.0 and 1.0.
    /// </summary>
    /// <returns>A random double-precision floating-point number between 0.0 and 1.0.</returns>
    public static double NextDouble()
    {
        lock (sync) { return rng.NextDouble(); }
    }

    /// <summary>
    /// Returns a random float in [minInclusive, maxExclusive).
    /// </summary>
    /// <param name="minInclusive">The inclusive lower bound of the random number returned.</param>
    /// <param name="maxExclusive">The exclusive upper bound of the random number returned.</param>
    /// <returns>A random float between minInclusive and maxExclusive.</returns>
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
    /// <param name="minInclusive">The inclusive lower bound of the random number returned.</param>
    /// <param name="maxExclusive">The exclusive upper bound of the random number returned.</param>
    /// <returns>An int between minInclusive and maxExclusive.</returns>
    public static int Range(int minInclusive, int maxExclusive)
    {
        return Next(minInclusive, maxExclusive);
    }

    /// <summary>
    /// Return the current seed used by the RNGManager.
    /// </summary>
    public static int CurrentSeed => currentSeed;
}
