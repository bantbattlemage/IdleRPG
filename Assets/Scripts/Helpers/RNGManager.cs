using System;
using UnityEngine;

/// <summary>
/// Centralized seeded RNG manager. Use <see cref="SetSeed(int)"/> to create deterministic sequences.
/// Thread-safe access to System.Random methods used across the project.
///
/// Also exposes a purposefully-non-seeded API surface (`UnseededRange`) for call sites that
/// intentionally require non-deterministic randomness (e.g., cosmetic/debug choices). Routing
/// non-seeded calls through this helper makes the intent explicit and centralizes any
/// engine-specific implementation details.
/// </summary>
public static class RNGManager
{
    private static System.Random rng = new System.Random(Environment.TickCount);
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
            rng = new System.Random(seed);
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
    /// Returns a non-negative random integer (from the seeded generator managed by RNGManager).
    /// </summary>
    /// <returns>A non-negative random integer.</returns>
    public static int Next()
    {
        lock (sync) { return rng.Next(); }
    }

    /// <summary>
    /// Returns a non-negative random integer less than the specified maximum (seeded generator).
    /// </summary>
    public static int Next(int maxValue)
    {
        lock (sync) { return rng.Next(maxValue); }
    }

    /// <summary>
    /// Returns a random integer within a specified range (seeded generator).
    /// </summary>
    public static int Next(int minValue, int maxValue)
    {
        lock (sync) { return rng.Next(minValue, maxValue); }
    }

    /// <summary>
    /// Returns a random double in [0.0, 1.0) from the seeded generator.
    /// </summary>
    public static double NextDouble()
    {
        lock (sync) { return rng.NextDouble(); }
    }

    /// <summary>
    /// Returns a random float in [minInclusive, maxExclusive) using the seeded generator.
    /// </summary>
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
    /// Returns an int in [minInclusive, maxExclusive) using the seeded generator.
    /// </summary>
    public static int Range(int minInclusive, int maxExclusive)
    {
        return Next(minInclusive, maxExclusive);
    }

    /// <summary>
    /// Purposefully non-seeded random integer in [minInclusive, maxExclusive).
    /// Use this when a callsite intentionally requires non-deterministic behavior that
    /// should not be affected by the seeded RNG set via SetSeed(). Examples: cosmetic debug
    /// choices, unique temporary ids, or editor-only randomness.
    ///
    /// Implementation: prefer UnityEngine.Random.Range when running under Unity so we use
    /// the engine's global non-seeded RNG; otherwise fall back to an ephemeral System.Random
    /// seeded from Environment.TickCount so each call is effectively non-deterministic.
    /// </summary>
    public static int UnseededRange(int minInclusive, int maxExclusive)
    {
        // Defensive normalization
        if (maxExclusive <= minInclusive) return minInclusive;

        try
        {
            // Prefer Unity's non-seeded RNG when available
            return UnityEngine.Random.Range(minInclusive, maxExclusive);
        }
        catch
        {
            // Fallback: ephemeral System.Random instance (non-seeded semantics)
            // Use GlobalAccessorIdProvider for a fast unique int source instead of GUID-based GetHashCode
            int uniquePart = GlobalAccessorIdProvider.GetNextId();
            var tmp = new System.Random(Environment.TickCount ^ uniquePart);
            return tmp.Next(minInclusive, maxExclusive);
        }
    }

    /// <summary>
    /// Purposefully non-seeded double in [0.0, 1.0).
    /// Prefers UnityEngine.Random.value when running in Unity to reuse engine RNG; falls back to an ephemeral System.Random.
    /// </summary>
    public static double UnseededDouble()
    {
        try
        {
            return UnityEngine.Random.value; // float promoted to double
        }
        catch
        {
            int uniquePart = GlobalAccessorIdProvider.GetNextId();
            var tmp = new System.Random(Environment.TickCount ^ uniquePart);
            return tmp.NextDouble();
        }
    }

    /// <summary>
    /// Return the current seed used by the RNGManager (seeded generator).
    /// </summary>
    public static int CurrentSeed => currentSeed;
}
