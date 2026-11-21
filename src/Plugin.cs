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
        public const string Version = "0.2.2";

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
        int _lastSeen;

        // Every 2 minutes in game, so the log always shows whether zombies are being seen and rolled.
        void Summary()
        {
            bool host = ZedZone.Net.NetSim.isSimulationAuthority;
            int newSeen = Elites.Seen - _lastSeen;
            _lastSeen = Elites.Seen;
            string role = host ? "host/single player" : "guest (the host's Heavy Horde decides elites)";
            HLog.Info($"Status ({role}): {newSeen} zombies spawned in the last 2 min. Totals: {Elites.Seen} zombies, {Elites.SeenInAmbush} from ambushes/hunts, " +
                      $"{Elites.MadeMutated} Mutated, {Elites.MadeBrute} Brutes, {Elites.MadeBehemoth} Behemoths, {Elites.MadeCharger} Chargers; " +
                      $"{Elites.Reloaded} reloaded, {Elites.SkippedGameSpecial} already special.");
        }


        void LateUpdate()
        {
            try { SpriteDump.Tick(); } catch { }
            try { Charger.Tick(); }
            catch (Exception e) { HLog.Error("Charger tick failed", e); }
        }

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.5f;
            try { Horde.Tick(); }
            catch (Exception e) { HLog.Error("Update failed", e); _next = Time.unscaledTime + 10f; }

            if (Time.unscaledTime > _nextStats)
            {
                bool first = _nextStats == 0f;
                _nextStats = Time.unscaledTime + 120f;
                if (!first && InGameController.instance != null) Summary();
            }
        }
    }
}
