# jackalope

*note: this mod is a work in progress; please report any bugs or suggestions on the issues tab of this repository, or in the [uch tasing discord](https://discord.gg/5SFJPZ5Bwe)*

---

this is a bepinex mod for ultimate chicken horse, which adds functionality for tool assisted speedrunning.

read the sections below to get started!

---

## setup

*currently, jackalope is only supported on windows, and is exclusive to the steam version. further platform support is planned!*

1. download [bepinex](https://docs.bepinex.dev/articles/user_guide/installation/index.html) and extract it into your ultimate chicken horse folder
2. download `jackalope.dll` from the releases tab on the right, and put it into your `BepInEx/plugins` folder
3. run `UltimateChickenHorse.exe`

---

## controls

- `m` = frame advance
- `,` = pause/unpause
- `.` = slowdown game
- `/` = reset timer and inputs
- `0-9` = load savestates
- `shift + 0-9` = save savestates
- `-` = replay mode
- `=` = fast forward
- `[` = export inputs
- `]` = import inputs

---

## to do

all releases are in a stable state, but there are still many features or fixes yet to be added:

**features:**
- compare against ghosts
- collision display
- freecam or other camera modes
- co op support
- input bruteforcer

**fixes:**
- resets should be instant
- loading savestates should get to the save point automatically instead of simply queueing inputs
- exporting/importing inputs should let you choose a file to save to or load from
- the tas timer should sync to the level start
- it is still unknown whether or not the game is deterministic

---

## credits

- **[clever endeavor games](https://www.cleverendeavourgames.com/)** // created ultimate chicken horse
- **[bepinex](https://github.com/BepInEx/BepInEx)** // game patcher that the mod is built off of
- **[harmony](https://github.com/pardeike/Harmony)** // library for patching dlls at runtime
- **@mad.man (discord)** // got uch to work in libtas, and encouraged me to start tasing this game
- **[evenmoreplayers mod](https://github.com/batram/UCH-EvenMorePlayers)** // was a useful example as i worked on this