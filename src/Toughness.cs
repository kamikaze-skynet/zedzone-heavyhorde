using System;
using System.Collections.Generic;
using UnityEngine;
using ZedZone.Net;

namespace HeavyHorde
{
    // Per-hit damage cap for Brutes and Behemoths, on the host (which owns zombie health).
    //
    // A Boltgun bolt does about 3,750 damage (impact + internal charge + blast), which kills
    // anything by health alone. Instead of making elites bullet sponges for every gun, damage
    // that lands within HitWindow seconds counts as one hit, and one hit can take at most
    // MaxDamagePerHit of the elite's full health. So a Brute needs about 3 bolts whatever
    // the bolt's damage, while normal guns are only slowed by its health and armour.
    internal static class Toughness
    {
        struct Window { public float Start; public float Taken; }
        static readonly Dictionary<IntPtr, Window> _windows = new Dictionary<IntPtr, Window>();

        // Prefix on BasicCharacterController.OnHit (ZombieController.OnHit calls it first).
        internal static void CapDamage(BasicCharacterController c, ref float damage, Vector2 force)
        {
            if (damage <= 0f || c == null || !Settings.Enabled.Value) return;
            Tier tier = Elites.TierOf(c);
            if (tier < Tier.Brute) return;
            if (!NetSim.isSimulationAuthority) return;
            if (tier == Tier.Charger) Charger.FrontArmor(c, ref damage, force);
            var s = Elites.SettingsOf(tier);
            float cap = s.MaxDamagePerHit.Value;
            if (cap <= 0f) return;

            float full = c.FullHP;
            if (full <= 0f) return;
            float limit = cap * full;
            float now = Time.time;

            _windows.TryGetValue(c.Pointer, out var w);
            if (now - w.Start > Settings.HitWindow.Value) { w.Start = now; w.Taken = 0f; }
            float allowed = Mathf.Max(0f, limit - w.Taken);
            if (damage > allowed)
            {
                HLog.Debug($"{tier} hit for {damage:0} capped to {allowed:0} (full HP {full:0})");
                damage = Mathf.Max(allowed, 0.01f);
            }
            w.Taken += damage;
            _windows[c.Pointer] = w;

            if (_windows.Count > 512) _windows.Clear();
        }

        // Prefix on ZombieController.OnExplosionDismember / TryDismemberOnHit: false skips it.
        internal static bool AllowDismember(BasicCharacterController c)
        {
            if (!Settings.Enabled.Value) return true;
            Tier tier = Elites.TierOf(c);
            if (tier < Tier.Brute) return true;
            if (!Elites.SettingsOf(tier).ResistDismember.Value) return true;
            try { if (c.deadFlag) return true; } catch { }
            return false;
        }
    }
}
