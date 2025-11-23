using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Centralized registry for active DOTween Tweeners so they can be grouped and cancelled by owner.
/// Lightweight: stores tuples (owner, tweener) and removes completed tweens automatically.
/// Implements a simple MonoBehaviour singleton so callers can access TweenPool.Instance.
/// </summary>
public class TweenPool : MonoBehaviour
{
    private static TweenPool instance;
    public static TweenPool Instance
    {
        get
        {
            if (instance == null) instance = FindObjectOfType<TweenPool>();
            return instance;
        }
    }

    private readonly List<(Object owner, Tweener tween)> entries = new List<(Object, Tweener)>();

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Debug.LogWarning("Multiple TweenPool instances detected; keeping the first.", this);
        }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>
    /// Register a tweener with an owning object (usually a component or gameObject) so it can be killed as a group later.
    /// Also assigns the owner as the DOTween target so native DOTween kill-by-target features work too.
    /// </summary>
    public void Register(Object owner, Tweener tween)
    {
        if (owner == null || tween == null) return;
        // ensure DOTween target is set for this tweener so DOTween.Kill(owner) will also apply
        try
        {
            tween.SetTarget(owner);
        }
        catch
        {
            // ignore any issues setting target (some tweens may not accept target changes)
        }

        entries.Add((owner, tween));
        // cleanup on complete to avoid growing list indefinitely
        tween.OnKill(() => { entries.RemoveAll(e => e.tween == tween); });
    }

    /// <summary>
    /// Kill all tweens registered for the given owner.
    /// </summary>
    public void KillOwner(Object owner)
    {
        if (owner == null) return;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var e = entries[i];
            if (e.owner == owner)
            {
                if (e.tween != null && e.tween.IsActive()) e.tween.Kill();
                entries.RemoveAt(i);
            }
        }
        // also use DOTween's native kill-by-target to catch any tweens that were SetTarget(owner)
        try { DOTween.Kill(owner); } catch { }
    }

    /// <summary>
    /// Kill all tweens in the pool.
    /// </summary>
    public void KillAll()
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var e = entries[i];
            if (e.tween != null && e.tween.IsActive()) e.tween.Kill();
        }
        entries.Clear();
        try { DOTween.KillAll(); } catch { }
    }
}
