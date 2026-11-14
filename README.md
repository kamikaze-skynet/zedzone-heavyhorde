# Heavy Horde

A ZED ZONE mod (BepInEx IL2CPP) that adds more zombies and tougher ones, so fights stay dangerous even with the Boltgun.

## What it does

**Tougher zombies.** When a zombie spawns, it can roll as one of three variants:

| Variant | Default chance | What it is |
|---|---|---|
| Mutated | 15% | Glows, has a name prefix, 2.5x health, armour (takes ~70% damage), +30% damage, a bit faster. Normal guns need more hits; one Boltgun bolt still drops it. |
| Brute | 5% | 6x health, armour, +80% damage, regenerates when left alone, can't have its limbs blown off while alive. **One hit can take at most 40% of its health**, so it needs about 3 Boltgun bolts. |
| Behemoth | 1% | Giant (1.45x size), boss health bar, 20x health, 2.5x damage. One hit takes at most 15%, so about 7 bolts. |

**New enemy: the Charger (4%).** A zombie in welded scrap-metal armour (plates drawn on its body, so it still animates and ragdolls like any zombie). 4x health.
- Shots from the front glance off (15% gets through). Get to its side or back.
- It rushes you in a straight line and slams into everything in its path.
- If the charge misses every player, it stumbles and falls for 2.5 s. That's your opening.
- One hit can take at most 30% of its health.

The art is in `art/` (made by `art/make_charger_art.py`); swap the PNGs' pixels and rerun the script to restyle it.

The variants use the game's own mutation and horde-elite systems, so they look and behave like the game's own special zombies.

**More zombies.**
- Zombie groups in newly generated areas are 1.5x bigger (areas you already visited keep their zombies).
- The game's own ambushes (its AI director) happen more often, bring 2x the zombies, and half the time use the toughest zombie type for the area. Ambush zombies are twice as likely to be elites.
- Hunts: an extra ambush of 4-9 zombies every 6 minutes.

All of it is in `BepInEx\config\girthy_mods.zedzone.heavyhorde.cfg`.

## Why "one hit can take at most X%"?

A Boltgun bolt does about 3,750 damage (impact, internal charge and blast). Health alone can't stop that without turning every elite into a bullet sponge for normal guns. Instead, damage landing within `HitWindow` (0.2 s) counts as one hit, and Brutes and Behemoths cap what one hit can take. A bolt's impact, charge and blast all land inside that window, so a bolt is one hit.

## Co-op

Only the host needs the mod for it to work: the host spawns zombies and owns their health, and a zombie's variant travels to guests with the zombie itself, so guests see the glow, size and boss bar. Guests need the mod to see the Charger's armour plates (without it they see a glowing mutant). Guests should still install it so the dismember protection matches. The host's config is the one that counts.

## Install

Copy `HeavyHorde.dll` and `mod.json` into `ZED ZONE\Mods\HeavyHorde\`, or subscribe on the Steam Workshop. The log is `BepInEx\HeavyHorde.log` (turn on `VerboseLog` to see every elite and ambush).

## Build

```
dotnet build src\HeavyHorde.csproj -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\ZED ZONE"
```

Publish to the Workshop with `.\publish-workshop.ps1 -SteamUser <login>`.
