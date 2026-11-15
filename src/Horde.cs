using System;
using UnityEngine;
using ZedZone.Net;

namespace HeavyHorde
{
    // "More enemies": all of it runs on the host (or in single player).
    //  - Bigger zombie groups in newly generated areas (MonsterAttr min/maxGroupNumber).
    //  - The game's AI director (DirectorController) runs ambushes more often, with more
    //    zombies, more of them alive at once, and more often the toughest type for the area.
    //  - Hunts: extra director ambushes on our own timer.
    // Director zombies are tracked and cleaned up by the game itself, and the director picks
    // its spawn anchor among all players, so guests get ambushed too.
    internal static class Horde
    {
        static IntPtr _tunedMonsters, _tunedDirector;
        static float _nextHunt = -1f;
        static readonly System.Random _rng = new System.Random();

        internal static void Tick()
        {
            if (!Settings.Enabled.Value) return;
            if (InGameController.instance == null) { _nextHunt = -1f; return; }
            if (!NetSim.isSimulationAuthority) return;

            TuneMonsters();
            var dir = DirectorController.instance;
            if (dir == null) return;
            TuneDirector(dir);
            TickHunts(dir);
        }

        static void TuneMonsters()
        {
            var mm = MonsterManager.instance;
            if (mm == null || mm.Pointer == _tunedMonsters) return;
            var list = mm.monsterAttrs;
            if (list == null || list.Count == 0) return;
            _tunedMonsters = mm.Pointer;

            float m = Settings.GroupSizeMultiplier.Value;
            if (Mathf.Approximately(m, 1f)) return;
            int n = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var a = list[i];
                if (a == null || a.isAnimal || a.monsterSpeciesType != MonsterSpeciesType.Zombie) continue;
                if (a.maxGroupNumber <= 0) continue;
                int min = Math.Max(1, Mathf.RoundToInt(a.minGroupNumber * m));
                int max = Math.Max(min, Mathf.RoundToInt(a.maxGroupNumber * m));
                HLog.Debug($"Group size {a.monsterName_English}: {a.minGroupNumber}-{a.maxGroupNumber} -> {min}-{max}");
                a.minGroupNumber = min;
                a.maxGroupNumber = max;
                n++;
            }
            HLog.Info($"Zombie group sizes x{m} for {n} zombie types (new areas only).");
        }

        static void TuneDirector(DirectorController dir)
        {
            if (dir.Pointer == _tunedDirector) return;
            _tunedDirector = dir.Pointer;
            float oldInterval = dir.minEventInterval;
            int oldAgents = dir.maxDirectorAgents;
            dir.minEventInterval = oldInterval * Settings.DirectorIntervalMultiplier.Value;
            dir.maxDirectorAgents = Math.Max(1, Mathf.CeilToInt(oldAgents * Settings.DirectorAgentsMultiplier.Value));
            HLog.Info($"Director: min time between events {oldInterval:0.#} -> {dir.minEventInterval:0.#}, max ambush zombies {oldAgents} -> {dir.maxDirectorAgents}");
        }

        static void TickHunts(DirectorController dir)
        {
            if (!Settings.Hunts.Value) return;
            float now = Time.time;
            float interval = Settings.HuntIntervalMinutes.Value * 60f;
            if (_nextHunt < 0f) { _nextHunt = now + interval; return; }
            if (now < _nextHunt) return;

            if (Time.timeScale <= 0f) return;  // paused
            bool eligible;
            try { eligible = dir.IsDirectorEligible(); } catch { eligible = true; }
            if (!eligible) { _nextHunt = now + 30f; return; }  // e.g. at home or in a menu: try again soon

            int min = Settings.HuntMinCount.Value;
            int max = Math.Max(min, Settings.HuntMaxCount.Value);
            bool ok = false;
            try
            {
                InHunt = true;
                Elites.AmbushSpawned = Elites.AmbushElites = 0;
                ok = dir.ExecuteZombieAmbush(dir.GetPlayerChunkType(), min, max, Settings.HuntPreferTough.Value);
            }
            catch (Exception e) { HLog.Error("Hunt failed", e); }
            finally { InHunt = false; }

            HLog.Info(ok ? $"Hunt: {Elites.AmbushSpawned} zombies spawned ({Elites.AmbushElites} elite), asked for {min}-{max}."
                         : "Hunt: no spot to spawn from, trying again in 30 s.");
            _nextHunt = now + (ok ? interval : 30f);
        }

        // Our own hunt calls ExecuteZombieAmbush with counts we already chose: don't multiply them again.
        internal static bool InHunt;

        // Prefix on DirectorController.ExecuteZombieAmbush.
        internal static void BeforeAmbush(ref int minCount, ref int maxCount, ref bool preferTough)
        {
            Elites.AmbushDepth++;
            if (InHunt) return;
            Elites.AmbushSpawned = Elites.AmbushElites = 0;
            _directorAmbush = true;
            if (!Settings.Enabled.Value) return;
            float m = Settings.AmbushSizeMultiplier.Value;
            int oldMin = minCount, oldMax = maxCount;
            minCount = Math.Max(1, Mathf.RoundToInt(minCount * m));
            maxCount = Math.Max(minCount, Mathf.RoundToInt(maxCount * m));
            if (!preferTough && _rng.NextDouble() < Settings.AmbushToughChance.Value) preferTough = true;
            HLog.Debug($"Ambush {oldMin}-{oldMax} -> {minCount}-{maxCount}{(preferTough ? ", toughest type" : "")}");
        }

        static bool _directorAmbush;

        internal static void AfterAmbush()
        {
            if (Elites.AmbushDepth > 0) Elites.AmbushDepth--;
            if (_directorAmbush && Elites.AmbushDepth == 0)
            {
                _directorAmbush = false;
                if (Elites.AmbushSpawned > 0)
                    HLog.Info($"Game ambush: {Elites.AmbushSpawned} zombies spawned ({Elites.AmbushElites} elite).");
            }
        }
    }
}
