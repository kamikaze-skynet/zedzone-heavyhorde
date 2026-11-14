using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace HeavyHorde
{
    [BepInPlugin(Guid, "Heavy Horde", Version)]
    public class Plugin : BasePlugin
    {
        public const string Guid = "girthy_mods.zedzone.heavyhorde";
        public const string Version = "0.1.0";

        public override void Load()
        {
            Settings.Bind(Config);
            HLog.Verbose = Settings.VerboseLog.Value;
            HLog.Open(Log, Version);
            HLog.Info($"Config: Enabled={Settings.Enabled.Value} Mutated={Settings.Mutated.Chance.Value} Brute={Settings.Brute.Chance.Value} Behemoth={Settings.Behemoth.Chance.Value} " +
                      $"Groups x{Settings.GroupSizeMultiplier.Value} Ambush x{Settings.AmbushSizeMultiplier.Value} Hunts={Settings.Hunts.Value} every {Settings.HuntIntervalMinutes.Value} min");

            try
            {
                ClassInjector.RegisterTypeInIl2Cpp<HordeBehaviour>();
                var go = new GameObject("HeavyHorde");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideAndDontSave;
                go.AddComponent<HordeBehaviour>();
            }
            catch (Exception e) { HLog.Error("Couldn't create the update object (hunts and group sizes are off)", e); }

            Patches.Apply(new Harmony(Guid));
            HLog.Info($"Heavy Horde {Version} loaded.");
        }
    }

    public class HordeBehaviour : MonoBehaviour
    {
        public HordeBehaviour(IntPtr ptr) : base(ptr) { }
        float _next, _nextStats;

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.5f;
            try { Horde.Tick(); }
            catch (Exception e) { HLog.Error("Update failed", e); _next = Time.unscaledTime + 10f; }

            if (Time.unscaledTime > _nextStats)
            {
                _nextStats = Time.unscaledTime + 300f;
                if (Elites.Rolled > 0)
                    HLog.Info($"So far: {Elites.Rolled} zombies rolled, {Elites.MadeMutated} Mutated, {Elites.MadeBrute} Brutes, {Elites.MadeBehemoth} Behemoths.");
            }
        }
    }
}
