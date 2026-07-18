using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using UnityEngine.UIElements.UIR;

namespace jackalope;

//[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInPlugin("com.beatrixwashere.uch.jackalope", "jackalope", "1.1.3")]
public class jackalope : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;

    private static ConfigEntry<string> importpath;

    public static bool tasPause = false;

    public static int tasFrames = 0;

    public static bool tasReplay = false;

    public static bool tasResetting = false;

    public static bool cancontrol = false;

    public static Text statsDisplay;

    public static StringBuilder timerBuild = new StringBuilder(256);

    public static GameObject mchar;

    public static Character mcharScript;

    public static Rigidbody2D mcharBody;

    public static MethodInfo mcharIR;

    public static List<float[]> inputs; // [up, down, left, right, jump, sprint, suicide, dance]

    public static List<int> inputlengths;

    public static int currentlength;

    public static List<int> inputlines;

    public static List<int> breaks;

    public static int breakstop = -1;

    public static List<Vector3> setpos;

    public static List<Vector3> setvel;

    public static bool legalmode = false;

    public static Vector2[] statepos;

    public static Vector2[] statevel;

    public static ZoomCamera zcam;

    private void Awake()
    {
        // start plugin
        Logger = base.Logger;
        Logger.LogInfo("loaded jackalope!");

        // patch dll
        Logger.LogInfo("attempting to patch...");
        Harmony.CreateAndPatchAll(typeof(jackalope));
        Logger.LogInfo("applied patches!");

        // set up variables
        importpath = Config.Bind(
            "jackalope",
            "import_path",
            @"C:\Program Files (x86)\Steam\steamapps\common\Ultimate Chicken Horse\tas.txt",
            "the input file that jackalope reads from"
        );

        inputs = [];
        inputlengths = [];
        currentlength = 0;
        inputlines = [];
        breaks = [];
        setpos = [];
        setvel = [];
        statepos = new Vector2[10];
        statevel = new Vector2[10];
        Logger.LogInfo("set up environment!");
    }

    [HarmonyPatch(typeof(TreehouseButton), "Awake")]
    [HarmonyPrefix]
    static void DisableControls()
    {
        cancontrol = false;
        Logger.LogInfo("disabled control");
    }

    [HarmonyPatch(typeof(ChallengeControl), "TriggerSinglePlayerStart")]
    [HarmonyPrefix]
    static void EnableControls()
    {
        cancontrol = true;
        Logger.LogInfo("enabled control");
    }

    [HarmonyPatch(typeof(Character), "Awake")]
    [HarmonyPostfix]
    static void FindCharacters()
    {
        if (GameSparksManager.Instance.Connected && !GameState.GetInstance().currentSnapshotInfo.snapshotCode.NullOrEmpty() && cancontrol) return;
        // scan for characters
        foreach (UnityEngine.Object obj in FindObjectsOfType(typeof(Character)))
        {
            // check for challenge mode name
            if (obj.name == "NotAMeatboy(Clone)")
            {
                Logger.LogInfo("character connected!");
                mchar = GameObject.Find("NotAMeatboy(Clone)");
                mcharScript = mchar.GetComponent<Character>();
                mcharBody = mchar.GetComponent<Rigidbody2D>();
                mcharIR = typeof(Character).GetMethod("ReceiveEvent");
                zcam = LobbyManager.instance.CurrentGameController.MainCamera;
                typeof(ZoomCamera).GetField("freeFormCamEnabled", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(zcam, true);
            }
        }
    }

    static void InstantReset()
    {
        tasReplay = false;
        tasPause = true;
        Time.timeScale = 1.0f;
        InputEvent[] e =
        [
            new InputEvent(0, InputEvent.InputKey.Up, 0, true),
            new InputEvent(0, InputEvent.InputKey.Down, 0, true),
            new InputEvent(0, InputEvent.InputKey.Left, 0, true),
            new InputEvent(0, InputEvent.InputKey.Right, 0, true),
            new InputEvent(0, InputEvent.InputKey.Jump, false, true),
            new InputEvent(0, InputEvent.InputKey.Sprint, false, true),
            new InputEvent(0, InputEvent.InputKey.Suicide, false, true),
            new InputEvent(0, InputEvent.InputKey.Inventory, false, true),
        ];
        for (int i = 0; i < e.Length; i++)
        {
            mcharIR.Invoke(mcharScript, [e[i]]);
        }
        typeof(ChallengeControl).GetMethod("ToPlaceMode", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(LobbyManager.instance.CurrentGameController, []);
        typeof(ChallengeControl).GetMethod("ToPlayMode", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(LobbyManager.instance.CurrentGameController, []);
        typeof(ChallengeControl).GetField("singlePlayerDelayTime").SetValue(LobbyManager.instance.CurrentGameController, 0.0f);
        typeof(ChallengeControl).GetMethod("TriggerSinglePlayerStart", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(LobbyManager.instance.CurrentGameController, []);
        zcam.transform.position = new Vector3(mchar.transform.position.x, mchar.transform.position.y, -250);
    }

    static void StartReplay()
    {
        // reset inputs
        tasFrames = 0;
        inputs = [];
        inputlengths = [];
        currentlength = 0;
        inputlines = [];
        breaks = [];
        breakstop = -1;
        setpos = [];
        setvel = [];
        legalmode = false;

        // check and set import path
        string pathtouse = "";
        if (File.Exists(importpath.Value))
        {
            pathtouse = importpath.Value;
        }
        else if(File.Exists(importpath.Value + ".txt"))
        {
            pathtouse = importpath.Value + ".txt";
        }

        // check if path is valid
        if (pathtouse != "")
        {
            using (StreamReader sr = File.OpenText(pathtouse))
            {
                // set up variables; add an extra input to pad the start
                string nextline = "";
                string[] keychecks = { "w", "s", "a", "d", "j", "k", "l", "m" };
                inputs.Add(new float[8]);
                int currentline = 1;
                while ((nextline = sr.ReadLine()) != null)
                {
                    // comments and blank lines
                    if (nextline == "")
                    {
                        // do nothing
                    }
                    else if (nextline[0] == '#')
                    {
                        // do nothing
                    }

                    // commands
                    else if (nextline[0] == '/')
                    {
                        string[] commandargs = nextline.Split(" ", System.StringSplitOptions.RemoveEmptyEntries);
                        switch (commandargs[0])
                        {
                            case "/stop":
                                breakstop = tasFrames;
                                breaks.Add(tasFrames);
                                break;
                            case "/b":
                            case "/break":
                                breaks.Add(tasFrames);
                                break;
                            case "/fjump":
                                typeof(Character).GetMethod("ForceJump").Invoke(mcharScript, []);
                                break;
                            case "/setpos":
                                if (legalmode)
                                {
                                    Logger.LogError("/setpos not allowed in legal mode");
                                    break;
                                }
                                if (commandargs.Length == 3)
                                {
                                    setpos.Add(new Vector3(
                                        (float)Convert.ToDouble(commandargs[1]),
                                        (float)Convert.ToDouble(commandargs[2]),
                                        tasFrames
                                    ));
                                }
                                else
                                {
                                    Logger.LogError("invalid arguments: " + nextline);
                                }
                                break;
                            case "/setvel":
                                if (legalmode)
                                {
                                    Logger.LogError("/setvel not allowed in legal mode");
                                    break;
                                }
                                if (commandargs.Length == 3)
                                {
                                    setvel.Add(new Vector3(
                                        (float)Convert.ToDouble(commandargs[1]),
                                        (float)Convert.ToDouble(commandargs[2]),
                                        tasFrames
                                    ));
                                }
                                else
                                {
                                    Logger.LogError("invalid arguments: " + nextline);
                                }
                                break;
                            case "/legal":
                                legalmode = true;
                                break;
                            case "/state":
                                if (commandargs.Length == 6)
                                {
                                    int slot = Convert.ToInt32(commandargs[1]);
                                    Logger.LogInfo("saving state " + slot);
                                    statepos[slot] = new Vector2(
                                        (float)Convert.ToDouble(commandargs[2]),
                                        (float)Convert.ToDouble(commandargs[3])
                                    );
                                    statevel[slot] = new Vector2(
                                        (float)Convert.ToDouble(commandargs[4]),
                                        (float)Convert.ToDouble(commandargs[5])
                                    );
                                }
                                else
                                {
                                    Logger.LogError("invalid arguments: " + nextline);
                                }
                                break;
                            case "/start":
                                if (legalmode)
                                {
                                    Logger.LogError("/start not allowed in legal mode");
                                    break;
                                }
                                if (commandargs.Length == 5)
                                {
                                    mchar.transform.position = new Vector2(
                                        (float)Convert.ToDouble(commandargs[1]),
                                        (float)Convert.ToDouble(commandargs[2])
                                    );
                                    mcharBody.velocity = new Vector2(
                                        (float)Convert.ToDouble(commandargs[3]),
                                        (float)Convert.ToDouble(commandargs[4])
                                    );
                                }
                                else
                                {
                                    Logger.LogError("invalid arguments: " + nextline);
                                }
                                break;
                            default:
                                Logger.LogError("invalid command in input file: " + nextline);
                                break;
                        }
                    }

                    // input sequences
                    else
                    {
                        // split frames and inputs
                        string[] vals = nextline.Split(":", System.StringSplitOptions.RemoveEmptyEntries);
                        float[] currentinput = new float[8];

                        if (vals.Length == 2)
                        {
                            // check for keys
                            for (int i = 0; i < keychecks.Length; i++)
                            {
                                currentinput[i] = vals[1].Contains(keychecks[i]) ? 1 : 0;
                            }

                            // repeat for the line frame count
                            int inputlen = Convert.ToInt32(vals[0]);
                            inputlengths.Add(inputlen);
                            inputlines.Add(currentline);
                            for (int hold = 0; hold < inputlen; hold++)
                            {
                                inputs.Add(currentinput);
                                tasFrames++;
                            }
                        }
                        else
                        {
                            Logger.LogError("invalid line: " + currentline);
                        }
                    }

                    currentline++;
                }
            }
            Logger.LogInfo("imported!");

            tasReplay = true;
            tasPause = false;
            Time.timeScale = 1.0f;
            tasFrames = 0;

            // fix left/right input acceleration issues
            InputEvent[] e =
            [
                new InputEvent(0, InputEvent.InputKey.Left, 1, true),
                new InputEvent(0, InputEvent.InputKey.Left, 0, true),
                new InputEvent(0, InputEvent.InputKey.Right, 1, true),
                new InputEvent(0, InputEvent.InputKey.Right, 0, true),
            ];
            for (int i = 0; i < e.Length; i++)
            {
                mcharIR.Invoke(mcharScript, [e[i]]);
            }
        }
        else
        {
            Logger.LogError("no file found at " + importpath);
        }
    }

    [HarmonyPatch(typeof(GameControl), "Update")]
    [HarmonyPostfix]
    static void TASControls()
    {
        // frame advance
        if (GameSparksManager.Instance.Connected && !GameState.GetInstance().currentSnapshotInfo.snapshotCode.NullOrEmpty() && cancontrol) return;
        if (Input.GetKeyDown(KeyCode.M) && tasPause)
        {
            Time.timeScale = 1.0f;
        }
        else if (Time.timeScale > 0.0f && tasPause)
        {
            Time.timeScale = 0.0f;
        }

        // pause/unpause
        if (Input.GetKeyDown(KeyCode.Comma))
        {
            tasPause = !tasPause;
            Time.timeScale = tasPause ? 0.0f : 1.0f;
            Logger.LogInfo("pause: " + tasPause);
        }

        // slowdown
        if (Input.GetKeyDown(KeyCode.Period))
        {
            Time.timeScale = Time.timeScale == 1.0f ? 0.5f : (Time.timeScale == 0.5f ? 0.2f : 1.0f);
            Logger.LogInfo("gamespeed: " + Time.timeScale);
        }

        // replay
        if (Input.GetKeyDown(KeyCode.Minus))
        {
            tasReplay = !tasReplay;
            Logger.LogInfo("replay: " + tasReplay);
        }

        // fast forward
        if (Input.GetKeyDown(KeyCode.Equals))
        {
            Time.timeScale = Time.timeScale < 5.0f ? 5.0f : 1.0f;
            Logger.LogInfo("fast forward: " + (Time.timeScale == 5.0f));
        }

        // import
        if (Input.GetKeyDown(KeyCode.Slash))
        {
            StartReplay();
        }

        // starting states
        for(int i = 0; i < 10; i++)
        {
            if(Input.GetKeyDown((KeyCode)(i + 48)))
            {
                if(Input.GetKey(KeyCode.LeftShift))
                {
                    Logger.LogInfo("saving state " + i);
                    statepos[i] = mchar.transform.position;
                    statevel[i] = mcharBody.velocity;
                }
                else
                {
                    Logger.LogInfo("loading state " + i);
                    mchar.transform.position = statepos[i];
                    mcharBody.velocity = statevel[i];
                    zcam.transform.position = new Vector3(statepos[i].x, statepos[i].y, -250);
                }
            }
        }

        // instant reset
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            InstantReset();
        }
    }

    [HarmonyPatch(typeof(Character), "FixedUpdate")]
    [HarmonyPrefix]
    static void TASReplay()
    {
        if (tasReplay && !(GameSparksManager.Instance.Connected && !GameState.GetInstance().currentSnapshotInfo.snapshotCode.NullOrEmpty() && cancontrol))
        {
            // read inputs
            if (inputs.Count > tasFrames && mchar != null)
            {
                InputEvent[] e =
                [
                    new InputEvent(0, InputEvent.InputKey.Up, inputs[tasFrames][0], true),
                    new InputEvent(0, InputEvent.InputKey.Down, inputs[tasFrames][1], true),
                    new InputEvent(0, InputEvent.InputKey.Left, inputs[tasFrames][2], true),
                    new InputEvent(0, InputEvent.InputKey.Right, inputs[tasFrames][3], true),
                    new InputEvent(0, InputEvent.InputKey.Jump, inputs[tasFrames][4] == 1, mcharScript.jump != (inputs[tasFrames][4] == 1)),
                    new InputEvent(0, InputEvent.InputKey.Sprint, inputs[tasFrames][5] == 1, mcharScript.sprint != (inputs[tasFrames][5] == 1)),
                    new InputEvent(0, InputEvent.InputKey.Suicide, inputs[tasFrames][6] == 1, mcharScript.suicide != (inputs[tasFrames][6] == 1)),
                    new InputEvent(0, InputEvent.InputKey.Inventory, inputs[tasFrames][7] == 1, mcharScript.dance != (inputs[tasFrames][7] == 1)),
                ];
                for (int i = 0; i < e.Length; i++)
                {
                    mcharIR.Invoke(mcharScript, [e[i]]);
                }
            }
            else
            {
                tasPause = true;
                Time.timeScale = 0.0f;
                tasReplay = false;
            }
        }
        if (tasResetting && !(GameSparksManager.Instance.Connected && !GameState.GetInstance().currentSnapshotInfo.snapshotCode.NullOrEmpty() && cancontrol))
        {
            // disable reset mode
            if (Time.timeScale == 1.0f)
            {
                tasResetting = false;
                tasPause = true;
                tasFrames = 0;
                inputs = new List<float[]>();
            }
        }
    }

    [HarmonyPatch(typeof(Character), "Disable")]
    [HarmonyPrefix]
    static void TASReset()
    {
        if (!(GameSparksManager.Instance.Connected && !GameState.GetInstance().currentSnapshotInfo.snapshotCode.NullOrEmpty() && cancontrol))
        {
            if (statsDisplay != null)
            {
                Logger.LogInfo("resetting...");
                Time.timeScale = 2.0f;
                tasResetting = true;
                tasPause = false;
            }
        }
        else
        {
            Logger.LogError("levelnet is connected; switch to a locally saved level");
        }
    }

    [HarmonyPatch(typeof(DigitalClock), "ShowSecondsAsTime")]
    [HarmonyPostfix]
    static void TASStats()
    {
        if (GameSparksManager.Instance.Connected && !GameState.GetInstance().currentSnapshotInfo.snapshotCode.NullOrEmpty() && cancontrol) return;
        // set up stats display
        if (statsDisplay == null)
        {
            Logger.LogInfo("finding text component...");
            statsDisplay = GameObject.Find("TimeText").GetComponent<Text>();
            if (statsDisplay != null)
            {
                GameObject timerObj = GameObject.Find("TimeText");
                RectTransform newTransform = timerObj.GetComponent<RectTransform>();
                newTransform.sizeDelta = new Vector2(1500, 100);
                statsDisplay.fontSize = 20;
                statsDisplay.alignment = TextAnchor.UpperLeft;
                statsDisplay.text = "(NOT CONNECTED)";
                Logger.LogInfo("created stats display");
            }
        }

        // update stats display
        else if (mchar != null)
        {
            timerBuild.Length = 0;
            int num = Mathf.FloorToInt(tasFrames / 60f / 60f);
            float num2 = tasFrames / 60f - (float)(num * 60);
            timerBuild.Append(num);
            timerBuild.Append(":");
            timerBuild.Append((num2 < 10f) ? "0" : "");
            timerBuild.Append(num2.ToString("F2"));

            statsDisplay.text = "// jackalope //\n";
            statsDisplay.text += "time: " + timerBuild.ToString() + " (" + tasFrames + "f)\n";
            statsDisplay.text += "position: (" + mchar.transform.position.x + ", " + mchar.transform.position.y + ")\n";
            statsDisplay.text += "velocity: (" + mcharBody.velocity.x + ", " + mcharBody.velocity.y + ")\n";
            statsDisplay.text += "gamespeed: " + Time.timeScale + "x\n";
            statsDisplay.text += "\n";
            if (tasReplay)
            {
                statsDisplay.text += "replay position: line " + (inputlines.Count > 0 ? inputlines[0] : "n/a") + ", frame " + currentlength + "\n";
            }
        }
    }

    [HarmonyPatch(typeof(GameControl), "FixedUpdate")]
    [HarmonyPostfix]
    static void TASUpdate()
    {
        // setpos and setvel commands
        if (GameSparksManager.Instance.Connected && !GameState.GetInstance().currentSnapshotInfo.snapshotCode.NullOrEmpty() && cancontrol) return;
        if (!legalmode && tasReplay)
        {
            if (setpos.Count > 0)
            {
                if (setpos[0].z == tasFrames)
                {
                    mchar.transform.position = new Vector2(setpos[0].x, setpos[0].y);
                    setpos.RemoveAt(0);
                }
            }
            if (setvel.Count > 0)
            {
                if (setvel[0].z == tasFrames)
                {
                    mcharBody.velocity = new Vector2(setvel[0].x, setvel[0].y);
                    setvel.RemoveAt(0);
                }
            }
        }

        // update frame count
        tasFrames += 1;

        // prevent manualzoom
        if(zcam != null)
        {
            zcam.manualZoom = false;
        }

        // check for breakpoints
        if (breaks.Count > 0 && tasReplay)
        {
            if (breaks[0] == tasFrames)
            {
                tasPause = true;
                breaks.RemoveAt(0);
                if (breakstop == tasFrames)
                {
                    tasReplay = false;
                }
            }
        }

        // update input lengths
        if (inputlengths.Count > 0 && tasReplay)
        {
            while (currentlength == inputlengths[0])
            {
                inputlengths.RemoveAt(0);
                inputlines.RemoveAt(0);
                currentlength = 0;
                if (inputlengths.Count == 0)
                {
                    break;
                }
            }
            currentlength++;
        }
    }
}
