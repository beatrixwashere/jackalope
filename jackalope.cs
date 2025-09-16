using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace jackalope;

//[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInPlugin("com.beatrixwashere.uch.jackalope", "jackalope", "1.0.0")]
public class jackalope : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;

    public static bool tasPause = true;

    public static int tasFrames = 0;

    public static bool tasReplay = false;

    public static bool tasResetting = false;

    public static Text statsDisplay;

    public static StringBuilder timerBuild = new StringBuilder(256);

    public static GameObject mchar;

    public static Character mcharScript;

    public static Rigidbody2D mcharBody;

    public static MethodInfo mcharIR;

    public static List<float[]> inputs; // [up, down, left, right, jump, sprint, suicide, dance]

    private void Awake()
    {
        // start plugin
        Logger = base.Logger;
        Logger.LogInfo($"loaded jackalope!");

        // patch dll
        Logger.LogInfo($"attempting to patch...");
        Harmony.CreateAndPatchAll(typeof(jackalope));
        Logger.LogInfo($"applied patches!");

        // set up variables
        inputs = [];
        Logger.LogInfo($"set up environment!");
    }

    [HarmonyPatch(typeof(Character), "Awake")]
    [HarmonyPostfix]
    static void FindCharacters()
    {
        // scan for characters
        foreach (UnityEngine.Object obj in FindObjectsOfType(typeof(Character)))
        {
            Debug.Log("character found: " + obj.name);
            // check for challenge mode name
            if (obj.name == "NotAMeatboy(Clone)")
            {
                Debug.Log("character connected!");
                mchar = GameObject.Find("NotAMeatboy(Clone)");
                mcharScript = mchar.GetComponent<Character>();
                mcharBody = mchar.GetComponent<Rigidbody2D>();
                mcharIR = typeof(Character).GetMethod("ReceiveEvent");
            }
        }
    }

    [HarmonyPatch(typeof(GameControl), "Update")]
    [HarmonyPostfix]
    static void TASControls()
    {
        // frame advance
        if (Input.GetKeyDown(KeyCode.M) && tasPause)
        {
            Debug.Log("advance");
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
            Debug.Log("pause: " + tasPause);
        }

        // slowdown
        if (Input.GetKeyDown(KeyCode.Period))
        {
            Time.timeScale = Time.timeScale == 1.0f ? 0.5f : (Time.timeScale == 0.5f ? 0.2f : 1.0f);
            Debug.Log("gamespeed: " + Time.timeScale);
        }

        // replay
        if (Input.GetKeyDown(KeyCode.Minus))
        {
            tasReplay = !tasReplay;
            Debug.Log("replay: " + tasReplay);
        }

        // fast forward
        if (Input.GetKeyDown(KeyCode.Equals))
        {
            Time.timeScale = Time.timeScale < 5.0f ? 5.0f : 1.0f;
            Debug.Log("fast forward: " + (Time.timeScale == 5.0f));
        }

        // import
        if (Input.GetKeyDown(KeyCode.Slash))
        {
            // reset inputs
            tasFrames = 0;
            inputs = new List<float[]>();

            // check for tas file
            if (File.Exists(@"C:\Program Files (x86)\Steam\steamapps\common\Ultimate Chicken Horse\tas.txt"))
            {
                using (StreamReader sr = File.OpenText(@"C:\Program Files (x86)\Steam\steamapps\common\Ultimate Chicken Horse\tas.txt"))
                {
                    // set up variables; add an extra input to pad the start
                    string nextline = "";
                    string[] keychecks = { "w", "s", "a", "d", "j", "k", "l", "m" };
                    inputs.Add(new float[8]);
                    while ((nextline = sr.ReadLine()) != null)
                    {
                        string[] vals = nextline.Split(":", System.StringSplitOptions.RemoveEmptyEntries);
                        float[] currentinput = new float[8];

                        // check for keys
                        for (int i = 0; i < keychecks.Length; i++)
                        {
                            currentinput[i] = vals[1].Contains(keychecks[i]) ? 1 : 0;
                        }

                        // repeat for the line frame count
                        for (int hold = 0; hold < Convert.ToInt32(vals[0]); hold++)
                        {
                            inputs.Add(currentinput);
                            tasFrames++;
                        }
                    }
                }
                tasFrames = 0;
                Debug.Log("imported!");

                tasReplay = true;
                //tasPause = false;
                //Time.timeScale = 1.0f;

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
                Debug.Log("no file found");
            }
        }
    }

    [HarmonyPatch(typeof(Character), "FixedUpdate")]
    [HarmonyPrefix]
    static void TASReplay()
    {
        if (tasReplay)
        {
            // read inputs
            if (inputs.Count > tasFrames && mchar != null)
            {
                Debug.Log("replaying...");
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
        if (tasResetting)
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
        Debug.Log("resetting...");
        Time.timeScale = 2.0f;
        tasResetting = true;
        tasPause = false;
    }

    [HarmonyPatch(typeof(DigitalClock), "ShowSecondsAsTime")]
    [HarmonyPostfix]
    static void TASStats()
    {
        // set up stats display
        if (statsDisplay == null)
        {
            Debug.Log("finding text component...");
            statsDisplay = GameObject.Find("TimeText").GetComponent<Text>();
            if (statsDisplay != null)
            {
                GameObject timerObj = GameObject.Find("TimeText");
                RectTransform newTransform = timerObj.GetComponent<RectTransform>();
                newTransform.sizeDelta = new Vector2(1500, 100);
                statsDisplay.fontSize = 20;
                statsDisplay.alignment = TextAnchor.UpperLeft;
                statsDisplay.text = "(NOT CONNECTED)";
                Debug.Log("created stats display");
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
        }
    }

    [HarmonyPatch(typeof(GameControl), "FixedUpdate")]
    [HarmonyPostfix]
    static void TASUpdate()
    {
        // update frame count
        tasFrames += 1;
    }
}
