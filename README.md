# jackalope

*note: this mod is a work in progress; please report any bugs or suggestions on the issues tab of this repository, or in the [uch tasing discord](https://discord.gg/5SFJPZ5Bwe)*

---

this is a bepinex mod for ultimate chicken horse, which adds built in functionality for tool assisted speedrunning. previously, [libtas](https://github.com/clementgallet/libTAS) was used for creating uch tas runs, but came with a lot of inconveniences. this project aims to make creating and optimizing tas runs much easier, and to produce the same deterministic results that libtas does.

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
- `/` = import inputs
- `-` = toggle replay
- `=` = fast forward

---

## input format

each line of an input file is formatted as `frames:inputs`. `inputs` is a comma separated list of which keys to hold down, and `frames` is the amount of frames to hold those keys for.

**input list:**
- `w` = up
- `s` = down
- `a` = left
- `d` = right
- `j` = jump
- `k` = sprint
- `l` = give up
- `m` = dance

*example:*
```
10:d,k
20:a,j
```
*sprint right for 10 frames, then hold left and jump for 20 frames*

---

## to do

all releases are in a stable state, but there are still many features or fixes yet to be added:

**features:**
- input file features (breakpoints, comments, etc)
- controller input support
- collision display
- co op support
- input bruteforcer

**fixes:**
- disable the mod if onlinee
- allow buffering a jump at the start

---

## credits

- **[clever endeavor games](https://www.cleverendeavourgames.com/)** // created ultimate chicken horse
- **[bepinex](https://github.com/BepInEx/BepInEx)** // game patcher that the mod is built off of
- **[harmony](https://github.com/pardeike/Harmony)** // library for patching dlls at runtime
- **@mad.man (discord)** // got uch to work in libtas, and encouraged me to start tasing this game
- **[evenmoreplayers mod](https://github.com/batram/UCH-EvenMorePlayers)** // was a useful example as i worked on this