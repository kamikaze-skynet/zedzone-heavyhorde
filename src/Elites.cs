using System;
using System.Collections.Generic;
using System.Globalization;
using Il2CppInterop.Runtime;
using UnityEngine;
using ZedZone.HordeMode;
using ZedZone.Net;

namespace HeavyHorde
{
    internal enum Tier { None = 0, Mutated = 1, Brute = 2, Behemoth = 3, Charger = 4 }

    // Rolls tougher variants when a zombie is created, on the host only.
    //
    // The roll is written into the zombie's CharacterData as properties the game already
    // understands, before BasicCharacterController.Init runs:
    //   - NuclearMutation.PropertyKey (Mutated, Brute): the game's own mutation affixes
    //     (hp multiplier, armour, damage, speed, attack speed, regen) plus its glow and name prefix.
    //   - HordeElite.PropertyKey (Behemoth): "scale|hpMul|dmgMul|wave", the horde-mode elite
    //     (bigger body, elite glow, boss health bar).
    // Init applies them itself. CharacterData is what the host sends guests when a zombie
    // spawns, so guests see the same elite (glow, size, health bar) even without this mod,
    // and the host owns the zombie's health, so the toughness holds in co-op.
    //
    // Our own property ("HeavyHorde") records the roll, so a zombie keeps its tier when its
    // area unloads and reloads, and normal zombies don't get rerolled every reload.
    internal static class Elites
    {
        internal const string Key = "HeavyHorde";

        // Elite tier of each live controller, by native pointer. Filled in on every Init
        // (host and guest), so a reused pointer is always overwritten.
        static readonly Dictionary<IntPtr, Tier> _tiers = new Dictionary<IntPtr, Tier>();

        // Set while the director runs an ambush, so ambush zombies roll with AmbushEliteBonus.
        [ThreadStatic] internal static int AmbushDepth;

        static readonly System.Random _rng = new System.Random();
        internal static int Rolled, MadeMutated, MadeBrute, MadeBehemoth, MadeCharger;
        // Diagnostics for the periodic summary in HeavyHorde.log.
        internal static int Seen, SeenInAmbush, SkippedGameSpecial, SkippedNoData, Reloaded;
        // Rolls made inside the current ambush/hunt call.
        internal static int AmbushSpawned, AmbushElites;
        static IntPtr _lastPtr; static int _lastFrame = -1;

        internal static Tier TierOf(BasicCharacterController c)
        {
            if (c == null) return Tier.None;
            return _tiers.TryGetValue(c.Pointer, out var t) ? t : Tier.None;
        }

        internal static TierSettings SettingsOf(Tier t) => t switch
        {
            Tier.Mutated => Settings.Mutated,
            Tier.Brute => Settings.Brute,
            Tier.Behemoth => Settings.Behemoth,
            Tier.Charger => Settings.ChargerTier,
            _ => null,
        };

        // Prefix on ZombieController.Init and BasicCharacterController.Init (the zombie one
        // calls the base one, so the second call in the same frame is skipped).
        //
        // Note: the base Init looks up monsterAttr itself (MonsterManager.GetMonsterAttrById),
        // after it applies the mutation/elite properties. So on a fresh zombie monsterAttr is
        // still empty here and we look it up ourselves from CharacterData.characterAttrId.
        internal static void BeforeInit(BasicCharacterController c)
        {
            if (c == null) return;
            int frame = Time.frameCount;
            if (c.Pointer == _lastPtr && frame == _lastFrame) return;
            _lastPtr = c.Pointer; _lastFrame = frame;

            _tiers.Remove(c.Pointer);
            Charger.Forget(c.Pointer);
            if (!Settings.Enabled.Value) return;
            if (c.isPlayer) return;
            var z = c.TryCast<ZombieController>();
            if (z == null) return;  // only infected; no animals, people or pets
            var cd = c.characterData;
            if (cd == null) { SkippedNoData++; return; }
            var attr = AttrOf(c, cd);
            if (attr != null && (attr.isAnimal || attr.monsterSpeciesType != MonsterSpeciesType.Zombie)) return;
            Seen++;
            if (AmbushDepth > 0) SeenInAmbush++;
            SpriteDump.Queue(z);

            string mine = cd.GetProperty(Key);
            if (!string.IsNullOrEmpty(mine)) Reloaded++;
            else if (NetSim.isSimulationAuthority) mine = Roll(c, cd, attr);

            if (!string.IsNullOrEmpty(mine) && int.TryParse(mine, out int t) && t > 0 && t <= 4)
            {
                _tiers[c.Pointer] = (Tier)t;
                if ((Tier)t == Tier.Charger) Charger.Register(c);
            }
        }

        static MonsterAttr AttrOf(BasicCharacterController c, CharacterData cd)
        {
            var a = c.monsterAttr;
            if (a != null) return a;
            try { return MonsterManager.instance?.GetMonsterAttrById(cd.characterAttrId); }
            catch { return null; }
        }

        static string NameOf(MonsterAttr a) => a != null ? a.monsterName_English : "zombie";

        static string Roll(BasicCharacterController c, CharacterData cd, MonsterAttr attr)
        {
            Rolled++;
            if (AmbushDepth > 0) AmbushSpawned++;
            // Leave zombies the game already made special (radiation mutants, horde-mode elites) alone.
            if (!string.IsNullOrEmpty(cd.GetProperty(NuclearMutation.PropertyKey)) ||
                !string.IsNullOrEmpty(cd.GetProperty(HordeElite.PropertyKey)))
            {
                SkippedGameSpecial++;
                cd.SetProperty(Key, "0");
                return "0";
            }

            float bonus = AmbushDepth > 0 ? Settings.AmbushEliteBonus.Value : 1f;
            double r = _rng.NextDouble();
            Tier tier = Tier.None;
            float pB = Settings.Behemoth.Chance.Value * bonus;
            float pR = Settings.Brute.Chance.Value * bonus;
            float pM = Settings.Mutated.Chance.Value * bonus;
            float pC = Settings.ChargerTier.Chance.Value * bonus;
            if ((r -= pC) < 0) tier = Tier.Charger;
            else if ((r -= pB) < 0) tier = Tier.Behemoth;
            else if ((r -= pR) < 0) tier = Tier.Brute;
            else if ((r -= pM) < 0) tier = Tier.Mutated;

            try
            {
                switch (tier)
                {
                    case Tier.Mutated: WriteMutation(cd, Settings.Mutated); MadeMutated++; break;
                    case Tier.Brute: WriteMutation(cd, Settings.Brute); MadeBrute++; break;
                    case Tier.Behemoth: WriteHordeElite(cd, Settings.Behemoth); MadeBehemoth++; break;
                    case Tier.Charger: WriteMutation(cd, Settings.ChargerTier); MadeCharger++; break;
                }
            }
            catch (Exception e)
            {
                HLog.Error($"Couldn't make a {tier} out of {NameOf(attr)}", e);
                tier = Tier.None;
            }

            string v = ((int)tier).ToString(CultureInfo.InvariantCulture);
            cd.SetProperty(Key, v);
            if (tier != Tier.None)
            {
                if (AmbushDepth > 0) AmbushElites++;
                HLog.Info($"Spawned a {tier} ({NameOf(attr)}){(AmbushDepth > 0 ? " in an ambush" : "")} at {(Vector2)c.transform.position}, {DistanceToPlayer(c)}");
            }
            return v;
        }

        static void WriteMutation(CharacterData cd, TierSettings s)
        {
            var roll = new NuclearMutation.Roll
            {
                hpMul = s.HpMultiplier.Value,
                dmg = s.Damage.Value,
                spd = s.Speed.Value,
                atk = s.AttackSpeed.Value,
                arm = s.Armor.Value,
                reg = s.Regen.Value,
            };
            int mask = 0;
            if (roll.dmg > 0) mask |= NuclearMutation.AffixDamage;
            if (roll.spd > 0) mask |= NuclearMutation.AffixSpeed;
            if (roll.atk > 0) mask |= NuclearMutation.AffixAttackSpeed;
            if (roll.arm > 0) mask |= NuclearMutation.AffixArmor;
            if (roll.reg > 0) mask |= NuclearMutation.AffixRegen;
            roll.mask = mask;
            cd.SetProperty(NuclearMutation.PropertyKey, NuclearMutation.Encode(roll));
        }

        static void WriteHordeElite(CharacterData cd, TierSettings s)
        {
            var ci = CultureInfo.InvariantCulture;
            string v = string.Join("|",
                s.Scale.Value.ToString("0.###", ci),
                s.HpMultiplier.Value.ToString("0.###", ci),
                s.Damage.Value.ToString("0.###", ci),
                "0");
            cd.SetProperty(HordeElite.PropertyKey, v);
        }

        static string DistanceToPlayer(BasicCharacterController c)
        {
            try
            {
                var p = GameController.instance?.playerCharacter;
                if (p == null) return "player position unknown";
                float d = Vector2.Distance(c.transform.position, p.transform.position);
                return $"{d:0} m from you";
            }
            catch { return "player position unknown"; }
        }

        internal static void Forget(IntPtr p) => _tiers.Remove(p);
        internal static int LiveCount => _tiers.Count;
    }
}
