using BepInEx.Configuration;

namespace HeavyHorde
{
    // One tougher-enemy tier. "Mutated" and "Brute" use the game's own mutation system
    // (glow, name prefix, armour/speed/damage affixes); "Behemoth" uses the game's horde-mode
    // elite system (bigger body, boss health bar).
    internal sealed class TierSettings
    {
        internal ConfigEntry<float> Chance;
        internal ConfigEntry<float> HpMultiplier;
        internal ConfigEntry<float> Armor;
        internal ConfigEntry<float> Damage;
        internal ConfigEntry<float> Speed;
        internal ConfigEntry<float> AttackSpeed;
        internal ConfigEntry<float> Regen;
        internal ConfigEntry<float> Scale;
        internal ConfigEntry<float> MaxDamagePerHit;
        internal ConfigEntry<bool> ResistDismember;
    }

    internal static class Settings
    {
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> VerboseLog;
        internal static ConfigEntry<bool> DumpZombieSprites;

        internal static TierSettings Mutated = new TierSettings();
        internal static TierSettings Brute = new TierSettings();
        internal static TierSettings Behemoth = new TierSettings();
        internal static ConfigEntry<float> AmbushEliteBonus;
        internal static ConfigEntry<float> HitWindow;

        internal static ConfigEntry<float> GroupSizeMultiplier;
        internal static ConfigEntry<float> AmbushSizeMultiplier;
        internal static ConfigEntry<float> AmbushToughChance;
        internal static ConfigEntry<float> DirectorIntervalMultiplier;
        internal static ConfigEntry<float> DirectorAgentsMultiplier;

        internal static TierSettings ChargerTier = new TierSettings();
        internal static ConfigEntry<float> ChargerFrontDamageTaken, ChargerFrontArc, ChargerDashSpeed, ChargerDashDuration, ChargerDashRange, ChargerStumbleSeconds;
        internal static ConfigEntry<bool> ChargerArmorArt, ChargerArmorUseGameMaterial;

        internal static ConfigEntry<bool> Hunts;
        internal static ConfigEntry<float> HuntIntervalMinutes;
        internal static ConfigEntry<int> HuntMinCount;
        internal static ConfigEntry<int> HuntMaxCount;
        internal static ConfigEntry<bool> HuntPreferTough;

        static ConfigDescription R(string d, float min, float max) => new ConfigDescription(d, new AcceptableValueRange<float>(min, max));
        static ConfigDescription RI(string d, int min, int max) => new ConfigDescription(d, new AcceptableValueRange<int>(min, max));

        internal static void Bind(ConfigFile c)
        {
            Enabled = c.Bind("General", "Enabled", true, "Turn the whole mod on or off. Only the host's (or single player's) settings matter: the host spawns the zombies and owns their health.");
            DumpZombieSprites = c.Bind("Debug", "DumpZombieSprites", true, "Save the body sprites of the first 3 zombies each session to BepInEx\\HeavyHorde.sprites (used to fit the Charger's armour art). Turn off when not needed.");
            VerboseLog = c.Bind("General", "VerboseLog", false, "Log every elite roll and ambush in BepInEx\\HeavyHorde.log.");

            // ---- Mutated: common, tougher than normal, still one bolt
            BindTier(c, "Mutated", Mutated,
                chance: 0.15f, hp: 2.5f, armor: 0.4f, dmg: 0.3f, spd: 0.15f, atk: 0.25f, regen: 0f, scale: 0f, cap: 0f, resist: false,
                what: "Common mutated zombie: glows, has a name prefix, more health and armour, hits harder, moves a bit faster. Normal guns need several more hits; a Boltgun bolt still drops it.");
            // ---- Brute: rare, built to eat Boltgun bolts
            BindTier(c, "Brute", Brute,
                chance: 0.05f, hp: 6f, armor: 0.6f, dmg: 0.8f, spd: 0.1f, atk: 0.35f, regen: 0.3f, scale: 0f, cap: 0.4f, resist: true,
                what: "Rare heavy mutant built to take Boltgun hits: a single hit can take at most MaxDamagePerHit of its health, so it needs about 3 bolts. Regenerates when left alone.");
            // ---- Behemoth: very rare, boss-like
            BindTier(c, "Behemoth", Behemoth,
                chance: 0.01f, hp: 20f, armor: 0f, dmg: 1.5f, spd: 0f, atk: 0f, regen: 0f, scale: 1.45f, cap: 0.15f, resist: true,
                what: "Very rare giant with a boss health bar (the game's own horde elite). Needs about 7 bolts.");

            // ---- Charger: new enemy type
            BindTier(c, "Charger", ChargerTier,
                chance: 0.04f, hp: 4f, armor: 0f, dmg: 0.6f, spd: 0f, atk: 0f, regen: 0f, scale: 0f, cap: 0.3f, resist: true,
                what: "Armoured Charger: scrap-metal plates, rushes in a straight line and knocks into everything in its path. Shots from the front glance off; flank it or hit its back. If its charge misses you, it stumbles and falls.");
            ChargerFrontDamageTaken = c.Bind("Charger", "FrontDamageTaken", 0.15f, R("Share of damage that gets through its front armour (0.15 = 15%).", 0, 1));
            ChargerFrontArc = c.Bind("Charger", "FrontArc", 120f, R("Width (degrees) of the armoured front.", 10, 360));
            ChargerDashSpeed = c.Bind("Charger", "DashSpeedMultiplier", 1.6f, R("Charge speed compared with the game's mutant dash.", 0.2f, 5));
            ChargerDashDuration = c.Bind("Charger", "DashDurationMultiplier", 1.4f, R("Charge length compared with the game's mutant dash.", 0.2f, 5));
            ChargerDashRange = c.Bind("Charger", "DashRangeMultiplier", 1.5f, R("How far away it starts a charge, compared with the game's mutant dash.", 0.2f, 5));
            ChargerStumbleSeconds = c.Bind("Charger", "StumbleSeconds", 2.5f, R("Seconds it lies on the ground after a charge that hit no player. 0 = never stumbles.", 0, 10));
            ChargerArmorArt = c.Bind("Charger", "ArmorArt", true, "Draw the armour plates on its body.");
            ChargerArmorUseGameMaterial = c.Bind("Charger", "ArmorUsesGameLighting", true, "Draw the plates with the body part's own material (follows the game's lighting). Turn off if the plates look wrong.");

            AmbushEliteBonus = c.Bind("Elites", "AmbushEliteBonus", 2f, R("Elite chances are multiplied by this for zombies that arrive in ambushes and hunts.", 0, 20));
            HitWindow = c.Bind("Elites", "HitWindow", 0.2f, R("Seconds that count as one hit for MaxDamagePerHit. A Boltgun bolt's impact, internal charge and blast land within about 0.05 s, so they count as one hit.", 0.02f, 2));

            GroupSizeMultiplier = c.Bind("Horde", "GroupSizeMultiplier", 1.5f, R("Zombie group sizes in newly generated areas are multiplied by this. Areas you already visited keep their zombies.", 0.1f, 5));
            AmbushSizeMultiplier = c.Bind("Horde", "AmbushSizeMultiplier", 2f, R("The game's own zombie ambushes (its AI director) bring this many times more zombies.", 0.1f, 6));
            AmbushToughChance = c.Bind("Horde", "AmbushToughChance", 0.5f, R("Chance (0-1) an ambush picks the toughest zombie type for the area.", 0, 1));
            DirectorIntervalMultiplier = c.Bind("Horde", "DirectorIntervalMultiplier", 0.6f, R("Multiplies the game's minimum time between ambushes and other director events. Below 1 = more often.", 0.1f, 5));
            DirectorAgentsMultiplier = c.Bind("Horde", "DirectorAgentsMultiplier", 2f, R("Multiplies how many ambush zombies the director may keep alive at once.", 0.5f, 6));

            Hunts = c.Bind("Hunts", "Enabled", true, "Extra ambushes on a timer on top of the game's own, so there's always something coming.");
            HuntIntervalMinutes = c.Bind("Hunts", "IntervalMinutes", 6f, R("Real minutes between hunts.", 0.5f, 120));
            HuntMinCount = c.Bind("Hunts", "MinCount", 4, RI("Fewest zombies in a hunt.", 1, 60));
            HuntMaxCount = c.Bind("Hunts", "MaxCount", 9, RI("Most zombies in a hunt.", 1, 60));
            HuntPreferTough = c.Bind("Hunts", "PreferTough", true, "Hunts use the toughest zombie type for the area.");
        }

        static void BindTier(ConfigFile c, string s, TierSettings t, float chance, float hp, float armor, float dmg, float spd, float atk, float regen, float scale, float cap, bool resist, string what)
        {
            t.Chance = c.Bind(s, "Chance", chance, R(what + " Chance (0-1) per zombie spawn.", 0, 1));
            t.HpMultiplier = c.Bind(s, "HpMultiplier", hp, R("Health multiplier.", 0.1f, 200));
            if (s != "Behemoth")
            {
                t.Armor = c.Bind(s, "Armor", armor, R("Armour: damage taken is divided by (1 + Armor). 1 = half damage.", 0, 10));
                t.Speed = c.Bind(s, "Speed", spd, R("Extra move speed (0.15 = +15%).", 0, 3));
                t.AttackSpeed = c.Bind(s, "AttackSpeed", atk, R("Extra attack speed (0.25 = +25%).", 0, 3));
                t.Regen = c.Bind(s, "Regen", regen, R("Health regeneration strength after a few seconds without being hit (game's mutation regen affix). 0 = none.", 0, 5));
                t.Damage = c.Bind(s, "Damage", dmg, R("Extra melee damage (0.3 = +30%).", 0, 10));
            }
            else
            {
                t.Damage = c.Bind(s, "DamageMultiplier", 1f + dmg, R("Melee damage multiplier.", 0.1f, 20));
                t.Scale = c.Bind(s, "Scale", scale, R("Body size multiplier.", 1f, 3f));
            }
            t.MaxDamagePerHit = c.Bind(s, "MaxDamagePerHit", cap, R("Most of its full health one hit can take (0.4 = 40%, so at least 3 hits). 0 = no limit.", 0, 1));
            t.ResistDismember = c.Bind(s, "ResistDismember", resist, "Limbs can't be blown off while it is alive (so explosions can't cripple it in one go).");
        }
    }
}
