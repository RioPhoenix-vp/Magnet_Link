# Magnet Link

A mobile puzzle game built in Unity. Move a magnet stick around an arena to attract coloured balls, stack them, and release them into matching boxes before the clock runs out.

**[▶ Download and play on itch.io](https://riophoenixvp.itch.io/magnet-link)**

[![Play on itch.io](https://img.shields.io/badge/itch.io-download-fa5c5c?logo=itch.io&logoColor=white)](https://riophoenixvp.itch.io/magnet-link)
![Unity](https://img.shields.io/badge/Unity-6000.0.67f1-black?logo=unity)
![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Android-brightgreen)
![Language](https://img.shields.io/badge/language-C%23-239120?logo=c-sharp)

---

## Gameplay

The magnet attracts balls of whichever colour is currently selected, plus rainbow wildcards, which count as any colour. Balls stack vertically on the stick up to its capacity, then get released into a box.

Boxes come in two kinds. **Colour boxes** accept only their own colour and count toward that level's target. **Dust boxes** accept anything but score nothing — they're for clearing balls you don't want. Destroying a dust box starts a countdown that locks the next one, so dumping is deliberately rate-limited.

Boxes sit in a queue. Only the front box is open; when it fills, it pops and the column shifts up.

### Progression

- Coins and XP awarded on level completion, both persistent
- Profile level rises as XP accumulates; XP requirement doubles each level
- XP reward scales +15% per profile level
- Levels unlock sequentially — beating one opens the next
- Power-up orbs unlock at set level thresholds

---

## Architecture

### Single scene, prefab swapping

There is one scene. Levels are prefabs that `LevelManager` instantiates into a `GameplayRoot` container and destroys on transition. No scene loading anywhere in the project.

This keeps transitions instant and persistent state trivial, but it has one consequence worth understanding before touching the code: **anything inside a level prefab is destroyed on every level change.** Persistent systems live outside the prefabs and are null-guarded at every call site, because there's a window during the swap where they legitimately don't exist.

```
Scene
├── _Managers                  persistent, DontDestroyOnLoad
│   ├── LevelManager           prefab swapping, unlock tracking
│   ├── CoinManager            coin balance
│   ├── ProfileManager         level + XP curve
│   ├── GameManager            shared lookups
│   ├── LevelTimer             countdown clock
│   ├── AudioManager           pooled SFX + music
│   └── MenuNavigator          page switching
│
├── EventSystem                must be outside prefabs — menus need it
├── MainMenuCanvas             active on launch
├── LevelSelectCanvas          inactive; buttons built at runtime
└── GameplayRoot               inactive; levels spawn here
```

Each level prefab carries its own `UIManager`, HUD canvas, box spawner, magnet, arena geometry, and a `LevelTargets` component on the root holding that level's goals.

### Initialisation order

Getting per-level targets to apply correctly turned out to be the trickiest part of the swap architecture.

`LevelTargets.Awake()` finds the `UIManager` inside its own prefab instance via `GetComponentInChildren` and pushes targets directly, rather than going through `UIManager.Instance`. Two reasons: the static reference may still point at the previous level's UIManager mid-swap, and `Start()` ordering between two components in the same prefab is undefined, so a `Start()`-based push would sometimes get overwritten by Inspector defaults.

`LevelManager` uses `DestroyImmediate` rather than `Destroy` when unloading. Deferred destruction leaves the old `UIManager` alive until end of frame, which means it wins the singleton race against the incoming prefab.

### Singleton pattern

Persistent managers follow one shape:

```csharp
private void Awake()
{
    if (Instance != null && Instance != this) { Destroy(gameObject); return; }
    Instance = this;
    DontDestroyOnLoad(gameObject);
}

private void OnDestroy()
{
    if (Instance == this) Instance = null;
}
```

### Self-wiring components

Buttons wire themselves in `Start()` rather than through Inspector references. `MenuButtonAction` and `LevelButtonAction` take an enum for what to do and find their targets at runtime; `ButtonClickSound` attaches to any `Button` and needs no configuration.

This matters because dragged references break every time a prefab respawns, and because level-select buttons are instantiated at runtime and can't be wired in the editor at all.

---

## Scripts

### Persistent

| Script | Role |
|---|---|
| `LevelManager` | Prefab swapping, level index, unlock state |
| `CoinManager` | Coin balance, spend/earn, change event |
| `ProfileManager` | Profile level, XP curve, scaled rewards |
| `LevelTimer` | Level countdown, pause/resume, time bonuses |
| `AudioManager` | Round-robin SFX pool, music loop, combo pitch |
| `MenuNavigator` | Main menu / level select / gameplay switching |
| `GameManager` | Shared box lookups by colour tag |

### Per level

| Script | Role |
|---|---|
| `LevelTargets` | This level's colour targets and duration |
| `UIManager` | Counters, win condition, rewards, box timer, panels |
| `BoxSpawner` | Box grid, queue shifting, dust-box wave scaling |
| `BallBox` | Capacity, fill tint, pump animation, destruction |
| `MagnetStick` | Attraction, stacking, release, squash-and-stretch |
| `PowerUpManager` | Orb unlocks, cooldowns, stock |
| `RainbowBallSpawner` | Wildcard spawning within defined areas |
| `ColorBomb` | Arcing projectile, radius clear |

### UI

| Script | Role |
|---|---|
| `LevelSelectMenu` | Builds the level grid at runtime from unlock state |
| `MenuButtonAction` | Self-wiring menu navigation |
| `LevelButtonAction` | Self-wiring next/retry |
| `ButtonClickSound` | Click SFX on any button |
| `CoinDisplay` | Coin counter, re-pulls on enable |
| `ProfileDisplay` | Level number and XP bar |

---

## Persistence

All state is stored in `PlayerPrefs`:

| Key | Contents |
|---|---|
| `CurrentLevel` | Level index in progress |
| `UnlockedLevel` | Highest unlocked index |
| `PlayerCoins` | Coin balance |
| `ProfileLevel` | Profile level |
| `ProfileXP` | XP within current level |
| `SFXVolume` | SFX volume 0–1 |
| `MusicVolume` | Music volume 0–1 |

To reset during development:

```csharp
PlayerPrefs.DeleteAll();
PlayerPrefs.Save();
```

---

## Building

**Unity 6 (6000.0.67f1)**, portrait orientation.

Canvas Scaler settings on every canvas:

| Setting | Value |
|---|---|
| UI Scale Mode | Scale With Screen Size |
| Reference Resolution | 1080 × 1920 |
| Screen Match Mode | Match Width Or Height |
| Match | 0 |

Player settings: **Portrait** default orientation, other orientations unchecked.

Use **Window → General → Device Simulator** to check layouts across aspect ratios without building.

---

## Releases

Playable builds are published on **[itch.io](https://riophoenixvp.itch.io/magnet-link)**.
Tagged versions are also available under [Releases](../../releases).

Build output is not committed to this repository. `Build/`, `Library/`, and other
generated directories are excluded by `.gitignore`.

This is deliberate. Builds are artifacts — reproducible from source, large, and
binary. Git keeps a full copy of every version of every binary permanently, so
committing builds inflates clone times for everyone and the history never shrinks
even after the files are deleted. Binaries also can't be diffed or merged, so
they carry none of the benefits version control exists to provide.

To tag a version alongside an itch.io upload:

```bash
git tag -a v0.1.0 -m "First playable build"
git push origin v0.1.0
```

Then attach a zipped build to the release on GitHub, or from the CLI:

```bash
gh release create v0.1.0 MagnetLink-Windows-v0.1.0.zip \
  --title "Magnet Link v0.1.0" \
  --notes "Release notes here"
```

Zip the whole build folder rather than the executable alone — a Unity Windows
build needs its `_Data` folder and DLLs alongside the `.exe` to run.

### Building it yourself

1. Clone the repository
2. Open in Unity 6 (6000.0.67f1) — first import takes a few minutes while
   `Library/` is regenerated
3. **File → Build Profiles**, select the target platform, **Build**

---

## Adding a level

1. Duplicate an existing level prefab in `Assets/Prefabs/Levels`
2. Set red / blue / green targets and duration on the root `LevelTargets` component
3. Adjust arena geometry, box spawner grid, and ball layout
4. Add the prefab to `LevelManager.levelPrefabs` in order

The level select grid picks it up automatically — it builds one button per array element.

---

## Working on this

A few things that will bite otherwise:

**Null-guard every `UIManager.Instance` call.** It's genuinely null during the swap window, not defensively so.

**Persistent managers stay out of level prefabs.** If one ends up inside, it gets destroyed and recreated per level, and `DontDestroyOnLoad` will detach it into a separate scene at runtime, which is confusing to debug.

**`EventSystem` lives at scene root.** Inside a prefab, it dies with the level and all menu input stops working.

**`GameObject.Find` doesn't see inactive objects.** `LevelTimer` re-finds its text field lazily, so that text must stay active — clear it to an empty string rather than deactivating it.

**Prefer self-wiring over Inspector references** for anything inside a level prefab.

---

## Roadmap

- [ ] In-level settings/pause panel
- [ ] Music and SFX volume sliders
- [ ] Colourblind support — shape or pattern markers on balls, since red/green/blue
      is the core mechanic and the axis deuteranopia affects most
- [ ] Honeycomb ball spawn layout in `BoxSpawner`
- [ ] Revisit XP curve pacing
- [ ] Level star ratings
- [ ] Coin shop for power-up orbs

---

## Credits

Built by <your name>.

<!--
Optional sections to fill in before publishing:
  - Screenshots or a short GIF near the top — this is the single biggest
    improvement you can make to a game README
  - License (MIT is the usual default; add a LICENSE file to match)
  - Third-party assets and their licences (Dreamteck Splines, CFXR particles,
    any purchased art or audio) — worth listing explicitly if the repo is public
-->
