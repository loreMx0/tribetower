# tribetower
Do you like high halls? Yup.Do you like Coral Tower? YES!. Why not experience them both.


# TribeTower Waves

A BepInEx plugin for **Hollow Knight: Silksong (mobile port)** that lets you
rewrite the enemy waves in the pre-Karmelita gauntlet (Tribe Tower / "waver"
arena) using a plain-text config file — no code, no asset editing, no Unity
Editor required.

Waves **1–3 are hard-protected** so the intro stays vanilla. Waves **4 and up**
are rebuilt from scratch, enemy by enemy, using the positions you specify.

---

## Table of contents

- [Install](#install)
- [Where the config lives](#where-the-config-lives)
- [Wave syntax](#wave-syntax)
- [Enemy names](#enemy-names)
- [Position names](#position-names)
- [Examples](#examples)
- [How replacement works](#how-replacement-works)
- [Troubleshooting](#troubleshooting)
- [Building from source](#building-from-source)
- [Credits / license](#credits--license)

---

## Install

1. Make sure **BepInEx 5** is installed for your Silksong build. You should see
   a `BepInEx/` folder next to the game's managed DLLs.
2. Drop `TribeTowerWaves.dll` into:
   ```
   BepInEx/plugins/
   ```
3. Launch the game once. A new config file appears:
   ```
   BepInEx/config/com.btw.tribetower.waves.cfg
   ```
4. Edit that file (see below), then **restart the game** or enter the arena
   again. Config is read when the battle scene loads.

---

## Where the config lives

| Platform | Path |
|----------|------|
| Android  | `/storage/emulated/0/Android/data/<pkg>/files/BepInEx/config/com.btw.tribetower.waves.cfg` |
| Desktop  | `not available` |

The file is regenerated with defaults if you delete it. Save it as UTF-8.

---

## Wave syntax

Each wave is a single line of **comma-separated** entries:

```
EnemyName@Position, EnemyName@Position, ...
```

- **EnemyName** — one of the aliases from the table below (case-insensitive).
- **Position** — one of the position codes from the table below (case-insensitive).
- **Whitespace is ignored** — `Fly@L,Fly@R` and `Fly @ L , Fly @ R` are the same.
- **Order matters only for spawn order**, not for visuals.

```
Wave4 = Chief@C, Fly@L, Fly@R
```

**Empty string = leave that wave vanilla.** If you write nothing for Wave 9,
Wave 9 keeps its original enemies.

### Defaults shipped with the mod

| Wave | Default spec |
|------|--------------|
| 1    | *(protected, always vanilla)* |
| 2    | *(protected, always vanilla)* |
| 3    | *(protected, always vanilla)* |
| 4    | `Chief@C, Fly@L, Fly@R` |
| 5    | `Hunter@L, Child@R, Fly@FL, Fly@FR` |
| 6    | `Chief@FL, Chief@FR, Fly@L, Fly@R` |
| 7    | `Chief@UL, Chief@UR, Fly@UC, Child@L, Child@R` |
| 8    | `Hunter@L, Hunter@R, Child@FL, Child@FR, Fly@UL, Fly@UR, Chief@C, Chief@UC` |
| 9    | *(empty — vanilla)* |
| 10+  | *(empty — vanilla, appended if you fill them)* |
so on...
---

## Enemy names

These are the **aliases** you type in the config. They map to the real
GameObject names in the game.

| Alias  | Actual enemy              | Notes |
|--------|---------------------------|-------|
| `Hunter` | Bone Hunter             | Ground melee |
| `Child`  | Bone Hunter Child       | Small, fast |
| `Fly`    | Bone Hunter Fly         | Flying |
| `Chief`  | Bone Hunter Fly Chief   | Big flying boss-type |

You can also use the **full name** (`Bone Hunter`, `Bone Hunter Fly`, …) —
the parser matches case-insensitively against both.

> Adding a brand-new enemy type is **not** supported by this mod. It only
> reuses enemies already present in Waves 1–4 of the arena.

---

## Position names

Positions are fixed world coordinates inside the arena. Use these codes:

```
              UL        UC        UR        (air — higher up)
              (141,25.5) (148,25.5) (155,25.5)

    FL        L         C         R         FR   (ground)
 (135,20.5)(141,20.5)(148,20.5)(155,20.5)(161,20.5)
```

| Code | Meaning       | X    | Y    |
|------|---------------|------|------|
| `FL` | Far left      | 135  | 20.5 |
| `L`  | Left          | 141  | 20.5 |
| `C`  | Center        | 148  | 20.5 |
| `R`  | Right         | 155  | 20.5 |
| `FR` | Far right     | 161  | 20.5 |
| `UL` | Upper left    | 141  | 25.5 |
| `UC` | Upper center  | 148  | 25.5 |
| `UR` | Upper right   | 155  | 25.5 |

**You can place multiple enemies on the same spot** — they'll stack and
push each other apart. `Fly@C, Fly@C, Fly@C` is legal.

You can also reuse the same spot for the same enemy repeatedly:
```
Chief@UL, Chief@UC, Chief@UR
```

---

## Examples

### Easy mode — fewer enemies in Wave 4

```ini
[Waves]
Wave4 = Fly@L
```

### Hard mode — mix ground and air across several waves

```ini
[Waves]
Wave4 = Chief@C, Fly@FL, Fly@FR
Wave5 = Hunter@L, Hunter@R, Chief@UC
Wave6 = Chief@FL, Chief@FR, Child@C, Child@L, Child@R
Wave7 = Chief@UL, Chief@UR, Fly@UC, Fly@C, Fly@C
Wave8 = Hunter@L, Hunter@R, Hunter@C, Chief@UL, Chief@UR, Fly@FL, Fly@FR
```

### Boss-rush style — one big enemy per wave

```ini
[Waves]
Wave4 = Chief@C
Wave5 = Chief@L, Chief@R
Wave6 = Chief@FL, Chief@FR
Wave7 = Chief@UL, Chief@UR
Wave8 = Chief@C, Chief@UC
```

### Add brand-new waves beyond the vanilla count

Just fill in Wave 9, 10, or add `Wave11`, `Wave12`, … to the config. They are
appended to the end of the battle's wave list.

```ini
[Waves]
Wave9  = Chief@UL, Chief@UR, Fly@C
Wave10 = Chief@C, Chief@UC, Chief@FL, Chief@FR
Wave11 = Hunter@L, Hunter@R, Hunter@C
```

### Restore vanilla

Delete the config file (or blank every `WaveN` value). The mod will do nothing
and the arena plays exactly like the base game.

---

## How replacement works

For each wave number **N ≥ 4**:

1. A fresh, inactive `GameObject` named `Wave N` is created as a child of the
   arena's wave parent.
2. Each enemy listed in the spec is **instantiated from the source enemy**
   already present in vanilla Waves 1–4 (see the alias table), placed at the
   requested position, and parented to the new wave object — still inactive.
3. A fresh `BattleWave` component is added. Because the GameObject is
   inactive, `Awake` is deferred and runs *after* we set its fields.
4. `startDelay`, `clearDeathDrops`, and `startWaveEventRegister` are copied
   from the last vanilla wave so timing and event behaviour match.
5. `activateEnemiesOnStart` is forced to `true` so the freshly-parented
   children actually wake up when the wave starts.
6. The GameObject is activated → `BattleWave.Awake` runs with all children
   already in place.

If `N` already exists in the vanilla list, the entry is **replaced** and the
old GameObject is deactivated (kept in the scene so any lingering references
don't null-ref). If `N` is beyond the vanilla list, it is **appended**.

Waves 1–3 are never touched, even if you put a value in their config fields —
the plugin logs a warning and skips them.

---

## Troubleshooting

**The game crashes when entering the arena.**
Check `BepInEx/LogOutput.log` for lines starting with `[Waves]`. Common causes:

- A typo in an enemy alias → look for `Unknown enemy alias: X`.
- A typo in a position → look for `Unknown position: X`.
- Missing `@` in an entry → look for `Bad entry (no @): X`.

Fix the config and relaunch.

**My wave changes do nothing.**
- Make sure `Enabled = true` under `[General]`.
- Confirm you're editing the right file — path is printed in the log on
  plugin load.
- Waves 1–3 cannot be changed by design. Only 4+.
- You must **restart the game** (or at least reload the battle scene); the
  config is not hot-reloaded.

**Some enemies don't spawn.**
- Each alias can only be sourced if the vanilla wave that contains it is
  still in the scene. If you've modded Waves 1–4 with another plugin, the
  source enemies might be gone.
- The plugin logs `Source Hunter = ...` / `Source Chief = ...` on load. If
  any of these say `NULL`, the arena doesn't contain the expected vanilla
  enemies and the whole replacement is aborted (so you at least get vanilla).

**Logs are noisy.**
Set `LogDiagnostics = false` under `[General]`. You'll still get errors and
warnings.

---

## Building from source

Requires the .NET SDK and the following DLLs placed in a `libs/` folder next
to the `.csproj`:

```
libs/
├── BepInEx.dll                    # from BepInEx/core/
├── 0Harmony.dll                   # from BepInEx/core/
├── PlayMaker.dll                  # from <game>_Data/Managed/
├── UnityEngine.CoreModule.dll     # from <game>_Data/Managed/
├── UnityEngine.PhysicsModule.dll  # from <game>_Data/Managed/
└── Assembly-CSharp.dll            # from <game>_Data/Managed/
```

Then:

```bash
dotnet build -c Release
```

Output: `bin/Release/TribeTowerWaves.dll`. Copy it to `BepInEx/plugins/`.

The project targets `net472` so the DLL loads correctly under the Mono
runtime used by the mobile port. On Linux you may need:

```xml
<PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3" PrivateAssets="all" />
```

---
