using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace HeavyHorde
{
    // Testing aid: saves the body-part sprites of the first few zombies to
    // BepInEx\HeavyHorde.sprites\ (raw RGBA + a layout file), so armour art can be fitted
    // to the game's real zombie bodies. Sprite textures are usually not CPU-readable, so each
    // one is copied through a RenderTexture first.
    internal static class SpriteDump
    {
        static int _done, _queued;
        static readonly System.Collections.Generic.List<(ZombieController z, float at)> _pending = new System.Collections.Generic.List<(ZombieController, float)>();

        internal static void Queue(ZombieController z)
        {
            if (!Settings.DumpZombieSprites.Value || _queued >= 3 || z == null) return;
            _queued++;
            _pending.Add((z, Time.time + 2f));
        }

        internal static void Tick()
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (Time.time < _pending[i].at) continue;
                var z = _pending[i].z; _pending.RemoveAt(i);
                try { if (z != null && !z.WasCollected) TryDump(z); }
                catch (Exception e) { HLog.Error("Sprite dump failed", e); }
            }
        }

        internal static void TryDump(ZombieController z)
        {
            if (!Settings.DumpZombieSprites.Value || _done >= 3 || z == null) return;
            int n = ++_done;
            string dir = Path.Combine(BepInEx.Paths.BepInExRootPath, "HeavyHorde.sprites", "zombie" + n);
            Directory.CreateDirectory(dir);
            var sb = new StringBuilder();
            sb.AppendLine($"zombie {n}: {z.name} attr={(z.monsterAttr != null ? z.monsterAttr.monsterName_English : "?")}");
            Part(dir, sb, "head", z.headSR, z);
            Part(dir, sb, "torse", z.torseSR, z);
            Part(dir, sb, "arm1_1", z.arm1_1SR, z);
            Part(dir, sb, "arm1_2", z.arm1_2SR, z);
            Part(dir, sb, "arm2_1", z.arm2_1SR, z);
            Part(dir, sb, "arm2_2", z.arm2_2SR, z);
            Part(dir, sb, "leg1_1", z.leg1_1SR, z);
            Part(dir, sb, "leg1_2", z.leg1_2SR, z);
            Part(dir, sb, "leg2_1", z.leg2_1SR, z);
            Part(dir, sb, "leg2_2", z.leg2_2SR, z);
            Part(dir, sb, "foot1", z.foot1SR, z);
            Part(dir, sb, "foot2", z.foot2SR, z);
            // every other renderer on the body (clothes, hair, hats...)
            try
            {
                var all = z.GetComponentsInChildren<SpriteRenderer>(true);
                int k = 0;
                foreach (var sr in all)
                {
                    if (sr == null || sr.sprite == null || sr.name.StartsWith("HH_")) continue;
                    Part(dir, sb, $"r{k++:00}_{Safe(sr.name)}", sr, z);
                    if (k > 60) break;
                }
            }
            catch (Exception e) { sb.AppendLine("children failed: " + e.Message); }
            File.WriteAllText(Path.Combine(dir, "layout.txt"), sb.ToString());
            HLog.Info($"Saved zombie {n}'s body sprites to {dir}");
        }

        static string Safe(string s)
        {
            var b = new StringBuilder();
            foreach (char c in s ?? "") b.Append(char.IsLetterOrDigit(c) ? c : '_');
            return b.ToString();
        }

        static void Part(string dir, StringBuilder sb, string label, SpriteRenderer sr, ZombieController z)
        {
            try
            {
                if (sr == null || sr.sprite == null) { sb.AppendLine($"{label}: none"); return; }
                var sp = sr.sprite;
                var t = sr.transform;
                Vector3 rel = z.transform.InverseTransformPoint(t.position);
                float rot = t.eulerAngles.z - z.transform.eulerAngles.z;
                Vector3 ls = t.lossyScale;
                Rect r = sp.textureRect;
                sb.AppendLine($"{label}: sprite={sp.name} rect={r.x:0},{r.y:0},{r.width:0},{r.height:0} ppu={sp.pixelsPerUnit:0.##} pivot={sp.pivot.x:0.#},{sp.pivot.y:0.#} " +
                              $"pos={rel.x:0.###},{rel.y:0.###} rot={rot:0.#} scale={ls.x:0.###},{ls.y:0.###} order={sr.sortingOrder} flipX={sr.flipX} color={sr.color} enabled={sr.enabled} path={Path_(t, z.transform)}");
                SaveRgba(Path.Combine(dir, label + ".rgba"), sp);
            }
            catch (Exception e) { sb.AppendLine($"{label}: failed {e.Message}"); }
        }

        static string Path_(Transform t, Transform root)
        {
            string p = t.name;
            for (var c = t.parent; c != null && c.Pointer != root.Pointer; c = c.parent) p = c.name + "/" + p;
            return p;
        }

        static void SaveRgba(string file, Sprite sp)
        {
            var tex = sp.texture;
            Rect r = sp.textureRect;
            int w = Mathf.Max(1, (int)r.width), h = Mathf.Max(1, (int)r.height);
            var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            try
            {
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                var cpu = new Texture2D(w, h, TextureFormat.RGBA32, false);
                cpu.ReadPixels(new Rect(r.x, r.y, w, h), 0, 0, false);
                cpu.Apply(false, false);
                var px = cpu.GetPixels32();
                using var f = new BinaryWriter(File.Create(file));
                f.Write(w); f.Write(h);
                for (int i = 0; i < px.Length; i++) { var c = px[i]; f.Write(c.r); f.Write(c.g); f.Write(c.b); f.Write(c.a); }
                UnityEngine.Object.Destroy(cpu);
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}
