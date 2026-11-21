# Changelog

## 0.2.2
- Works with ZED ZONE 1.0.21 (November 20). The game added a new setting to its zombie ambush code, so Heavy Horde's timed hunts stopped working with the old build. Rebuilt for the new game code.

## 0.2.1
- Fix: no elites or Chargers were ever made. The roll ran before the game had looked up the zombie's type, so every zombie was skipped. It now looks the type up itself.
- Clearer HeavyHorde.log: one line for every elite or Charger that spawns (type, place, distance from you), how many zombies each hunt and game ambush really spawned, how many game hooks loaded, and a status line every 2 minutes with totals.

## 0.2.0
- New enemy: the Charger. Its own hulking grey body (our art replaces the zombie's skin parts and hides its clothes) under scrap-metal armour with a horned helmet.
- The Charger: Scrap-metal armour plates drawn on its body, front armour that bolts and bullets glance off, a straight-line charge (the game's mutant dash, faster and longer), and it stumbles and falls when a charge misses. 4% of zombies by default (8% in ambushes).

## 0.1.0
- First version. Mutated, Brute and Behemoth zombie variants (game's own mutation and horde-elite systems), a per-hit damage cap on Brutes and Behemoths so Boltgun bolts don't one-shot them, and dismember protection while they're alive.
- More zombies: bigger groups in new areas, bigger and more frequent director ambushes, and timed hunts.
- Host-side; variants travel to guests with each zombie.
