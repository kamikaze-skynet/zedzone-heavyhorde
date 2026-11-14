using System;
using System.Reflection;
using HarmonyLib;

namespace HeavyHorde
{
    internal static class Patches
    {
        internal static void Apply(Harmony h)
        {
            Patch(h, typeof(BasicCharacterController), nameof(BasicCharacterController.Init), nameof(Init_Pre));
            Patch(h, typeof(BasicCharacterController), nameof(BasicCharacterController.OnHit), nameof(OnHit_Pre));
            Patch(h, typeof(ZombieController), nameof(ZombieController.OnExplosionDismember), nameof(ExplosionDismember_Pre));
            Patch(h, typeof(ZombieController), nameof(ZombieController.TryDismemberOnHit), nameof(TryDismember_Pre));
            Patch(h, typeof(ZombieController), nameof(ZombieController.MutationDashAttack), nameof(Dash_Pre));
            Patch(h, typeof(ZombieController), nameof(ZombieController.ExecuteDashContactDamage), null, postfix: nameof(DashContact_Post));
            Patch(h, typeof(DirectorController), nameof(DirectorController.ExecuteZombieAmbush), nameof(Ambush_Pre), finalizer: nameof(Ambush_Fin));
        }

        static void Patch(Harmony h, Type t, string method, string prefix, string finalizer = null, string postfix = null)
        {
            try
            {
                var target = AccessTools.Method(t, method);
                if (target == null) { HLog.Warn($"{t.Name}.{method} not found - that part of the mod is off."); return; }
                var self = typeof(Patches);
                h.Patch(target,
                    prefix: prefix == null ? null : new HarmonyMethod(self.GetMethod(prefix, BindingFlags.Static | BindingFlags.NonPublic)),
                    postfix: postfix == null ? null : new HarmonyMethod(self.GetMethod(postfix, BindingFlags.Static | BindingFlags.NonPublic)),
                    finalizer: finalizer == null ? null : new HarmonyMethod(self.GetMethod(finalizer, BindingFlags.Static | BindingFlags.NonPublic)));
                HLog.Debug($"Patched {t.Name}.{method}");
            }
            catch (Exception e) { HLog.Error($"Couldn't patch {t.Name}.{method}", e); }
        }

        static void Init_Pre(BasicCharacterController __instance)
        {
            try { Elites.BeforeInit(__instance); }
            catch (Exception e) { HLog.Error("Elite roll failed", e); }
        }

        static void OnHit_Pre(BasicCharacterController __instance, ref float m_damage, UnityEngine.Vector2 m_force)
        {
            try { Toughness.CapDamage(__instance, ref m_damage, m_force); }
            catch (Exception e) { HLog.Error("Damage cap failed", e); }
        }

        static bool ExplosionDismember_Pre(ZombieController __instance)
        {
            try { return Toughness.AllowDismember(__instance); }
            catch { return true; }
        }

        static bool TryDismember_Pre(ZombieController __instance, bool died)
        {
            if (died) return true;
            try { return Toughness.AllowDismember(__instance); }
            catch { return true; }
        }

        static void Dash_Pre(ZombieController __instance)
        {
            try { Charger.OnDashStart(__instance); } catch { }
        }

        static void DashContact_Post(ZombieController __instance, Il2CppSystem.Collections.Generic.HashSet<BasicCharacterController> hitted)
        {
            try { Charger.OnDashContact(__instance, hitted); } catch { }
        }

        static void Ambush_Pre(ref int minCount, ref int maxCount, ref bool preferTough)
        {
            try { Horde.BeforeAmbush(ref minCount, ref maxCount, ref preferTough); }
            catch (Exception e) { HLog.Error("Ambush tweak failed", e); }
        }

        static Exception Ambush_Fin(Exception __exception)
        {
            Horde.AfterAmbush();
            return __exception;
        }
    }
}
