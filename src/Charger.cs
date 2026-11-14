using System;
using System.Collections.Generic;
using System.IO;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using ZedZone.Net;

namespace HeavyHorde
{
    // The Charger: a zombie in welded scrap armour.
    //  - Armour plates (our own sprites) are drawn on top of its body parts and follow its
    //    skeleton, so it animates, turns and ragdolls like any zombie.
    //  - Hits from the front do FrontDamageTaken of their damage (bolts and bullets glance off);
    //    flank it or shoot its back. Plus a per-hit cap like the Brute.
    //  - It uses the game's own mutant dash (ZombieController.MutationDashAttack): it rushes
    //    in a straight line and hits everything in its path. Vomit is switched off.
    //  - If a charge misses every player, it stumbles and falls (knockdown) - your opening.
    // Stats and AI run on the host; guests with the mod draw the armour from the zombie's
    // "HeavyHorde" property, which comes with the zombie's data.
    internal static class Charger
    {
        sealed class State
        {
            public ZombieController Z;
            public IntPtr Ptr;
            public float ApplyAt;
            public bool Applied;
            public bool Dashing;
            public float DashStart;
            public bool HitPlayer;
            public readonly List<(SpriteRenderer src, SpriteRenderer ov, Vector3 center)> Plates = new List<(SpriteRenderer, SpriteRenderer, Vector3)>();
        }

        static readonly Dictionary<IntPtr, State> _live = new Dictionary<IntPtr, State>();
        static readonly List<IntPtr> _dead = new List<IntPtr>();
        static Sprite _helmet, _chest, _pauldron, _bracer, _greave;
        static bool _artTried;

        internal static bool Is(BasicCharacterController c) => c != null && _live.ContainsKey(c.Pointer);

        // Called from Elites.BeforeInit for tier Charger (host and guest).
        internal static void Register(BasicCharacterController c)
        {
            var z = c.TryCast<ZombieController>();
            if (z == null) return;
            _live[c.Pointer] = new State { Z = z, Ptr = c.Pointer, ApplyAt = Time.time + 0.6f };
        }

        internal static void Forget(IntPtr p) => _live.Remove(p);

        // ---- front armour: prefix path from Toughness.CapDamage
        internal static void FrontArmor(BasicCharacterController c, ref float damage, Vector2 force)
        {
            float taken = Settings.ChargerFrontDamageTaken.Value;
            if (taken >= 1f || force.sqrMagnitude < 1e-6f) return;
            Vector2 look = c.lookDirection;
            if (look.sqrMagnitude < 1e-6f) return;
            Vector2 from = -force.normalized;  // direction the hit came from
            float cos = Mathf.Cos(Settings.ChargerFrontArc.Value * 0.5f * Mathf.Deg2Rad);
            if (Vector2.Dot(from, look.normalized) >= cos)
            {
                HLog.Debug($"Charger front armour: {damage:0} -> {damage * taken:0}");
                damage *= taken;
            }
        }

        // ---- dash tracking (host)
        internal static void OnDashStart(ZombieController z)
        {
            if (z != null && _live.TryGetValue(z.Pointer, out var s)) { s.Dashing = true; s.HitPlayer = false; s.DashStart = Time.time; }
        }

        internal static void OnDashContact(ZombieController z, Il2CppSystem.Collections.Generic.HashSet<BasicCharacterController> hitted)
        {
            if (z == null || hitted == null || !_live.TryGetValue(z.Pointer, out var s)) return;
            foreach (var h in hitted)
                if (h != null && h.isPlayer) { s.HitPlayer = true; break; }
        }

        // ---- per frame (from HordeBehaviour.LateUpdate)
        internal static void Tick()
        {
            if (_live.Count == 0) return;
            float now = Time.time;
            bool host = NetSim.isSimulationAuthority;
            _dead.Clear();
            foreach (var kv in _live)
            {
                var s = kv.Value;
                bool alive;
                try { alive = s.Z != null && s.Z.WasCollected == false && s.Z.gameObject != null; } catch { alive = false; }
                if (!alive) { _dead.Add(kv.Key); continue; }
                try
                {
                    if (!s.Applied)
                    {
                        if (now < s.ApplyAt) continue;
                        s.Applied = true;
                        if (host) SetupAi(s.Z);
                        BuildPlates(s);
                    }
                    SyncPlates(s);
                    if (host) TickAi(s, now);
                }
                catch (Exception e) { HLog.Error("Charger update failed", e); _dead.Add(kv.Key); }
            }
            foreach (var p in _dead) _live.Remove(p);
        }

        static void SetupAi(ZombieController z)
        {
            z._mutationSkillsEnabled = true;
            z.mutationVomitCooldown = 1e6f;                  // dash only, no vomit
            z.mutationDashSpeed *= Settings.ChargerDashSpeed.Value;
            z.mutationDashDuration *= Settings.ChargerDashDuration.Value;
            z.mutationDashTriggerRange *= Settings.ChargerDashRange.Value;
            HLog.Debug($"Charger ready: dash speed {z.mutationDashSpeed:0.#}, duration {z.mutationDashDuration:0.##}, trigger range {z.mutationDashTriggerRange:0.#}");
        }

        static void TickAi(State s, float now)
        {
            var z = s.Z;
            if (!z._mutationSkillsEnabled) z._mutationSkillsEnabled = true;  // the game may reset it
            if (!s.Dashing) return;
            bool active;
            try { active = z._mutationDashActive; } catch { active = false; }
            if (active || now - s.DashStart < 0.25f) return;
            s.Dashing = false;
            if (s.HitPlayer || z.deadFlag) return;
            float t = Settings.ChargerStumbleSeconds.Value;
            if (t <= 0f) return;
            try
            {
                if (z.CanEnterKnockdown() && z.TryEnterKnockdown(BasicCharacterController.KnockdownCause.Impact, t))
                    HLog.Debug("Charger missed and stumbled");
            }
            catch (Exception e) { HLog.Debug("Charger stumble failed: " + e.Message); }
        }

        // ---- armour plates
        static void LoadArt()
        {
            if (_artTried) return;
            _artTried = true;
            _helmet = Load("charger_helmet");
            _chest = Load("charger_chest");
            _pauldron = Load("charger_pauldron");
            _bracer = Load("charger_bracer");
            _greave = Load("charger_greave");
        }

        static Sprite Load(string name)
        {
            try
            {
                using var st = typeof(Charger).Assembly.GetManifestResourceStream("HeavyHorde.Art." + name + ".rgba");
                if (st == null) { HLog.Warn("Missing art " + name); return null; }
                using var br = new BinaryReader(st);
                int w = br.ReadInt32(), h = br.ReadInt32();
                byte[] b = br.ReadBytes(w * h * 4);
                var px = new Color32[w * h];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(b[i * 4], b[i * 4 + 1], b[i * 4 + 2], b[i * 4 + 3]);
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "HH_" + name };
                tex.SetPixels32(new Il2CppStructArray<Color32>(px));
                tex.Apply(false, false);
                tex.hideFlags = HideFlags.HideAndDontSave;
                var s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, Vector4.zero);
                s.hideFlags = HideFlags.HideAndDontSave;
                s.name = "HH_" + name;
                return s;
            }
            catch (Exception e) { HLog.Error("Loading art " + name + " failed", e); return null; }
        }

        static void BuildPlates(State s)
        {
            if (!Settings.ChargerArmorArt.Value) return;
            LoadArt();
            var z = s.Z;
            Add(s, z.headSR, _helmet, 1.08f, true);
            Add(s, z.torseSR, _chest, 0.92f, false);
            Add(s, z.arm1_1SR, _pauldron, 1.0f, false);
            Add(s, z.arm2_1SR, _pauldron, 1.0f, false);
            Add(s, z.arm1_2SR, _bracer, 0.85f, false);
            Add(s, z.arm2_2SR, _bracer, 0.85f, false);
            Add(s, z.leg1_1SR, _pauldron, 0.85f, false);
            Add(s, z.leg2_1SR, _pauldron, 0.85f, false);
            Add(s, z.leg1_2SR, _greave, 0.9f, false);
            Add(s, z.leg2_2SR, _greave, 0.9f, false);
            HLog.Debug($"Charger armour: {s.Plates.Count} plates");
        }

        static void Add(State s, SpriteRenderer src, Sprite art, float cover, bool keepAspect)
        {
            if (src == null || art == null) return;
            var part = src.sprite;
            if (part == null) return;
            Bounds b = part.bounds;   // in the part's local space
            Vector2 size = b.size, artSize = art.bounds.size;
            if (size.x <= 0f || size.y <= 0f) return;

            var go = new GameObject("HH_ChargerPlate");
            go.transform.SetParent(src.transform, false);
            // long side of the art along the long side of the body part
            bool rotate = (size.x > size.y) != (artSize.x > artSize.y) && !keepAspect;
            if (rotate) { go.transform.localRotation = Quaternion.Euler(0, 0, 90f); artSize = new Vector2(artSize.y, artSize.x); }
            float sx = size.x * cover / artSize.x, sy = size.y * cover / artSize.y;
            if (keepAspect) sx = sy = Mathf.Min(sx, sy);
            go.transform.localScale = rotate ? new Vector3(sy, sx, 1f) : new Vector3(sx, sy, 1f);
            go.transform.localPosition = new Vector3(b.center.x, b.center.y, 0f);

            var ov = go.AddComponent<SpriteRenderer>();
            ov.sprite = art;
            if (Settings.ChargerArmorUseGameMaterial.Value)
            {
                try { ov.sharedMaterial = src.sharedMaterial; } catch { }
            }
            ov.sortingLayerID = src.sortingLayerID;
            ov.sortingOrder = src.sortingOrder + 1;
            s.Plates.Add((src, ov, new Vector3(b.center.x, b.center.y, 0f)));
        }

        static void SyncPlates(State s)
        {
            foreach (var (src, ov, center) in s.Plates)
            {
                if (ov == null || ov.WasCollected) continue;
                if (src == null || src.WasCollected) { ov.enabled = false; continue; }
                bool show = src.enabled && src.gameObject.activeInHierarchy && src.sprite != null;
                if (ov.enabled != show) ov.enabled = show;
                if (!show) continue;
                ov.sortingLayerID = src.sortingLayerID;
                ov.sortingOrder = src.sortingOrder + 1;
                if (ov.flipX != src.flipX || ov.flipY != src.flipY)
                {
                    ov.flipX = src.flipX; ov.flipY = src.flipY;
                    ov.transform.localPosition = new Vector3(src.flipX ? -center.x : center.x, src.flipY ? -center.y : center.y, 0f);
                }
                var c = src.color;
                ov.color = new Color(1f, 1f, 1f, c.a);
            }
        }

        internal static int LiveCount => _live.Count;
    }
}
