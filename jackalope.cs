using System;
using System.Collections.Generic;
using System.IO;
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

    public static Text statsDisplay;

    public static StringBuilder timerBuild = new StringBuilder(256);

    public static GameObject mainCharacter;

    public static Character mainCharacterScript;

    public static Rigidbody2D mainCharacterBody;

    public static List<float[]> inputs; // [up, down, left, right, jump, sprint, suicide, dance]

    public static Dictionary<int, List<float[]>> savestates;

    private void Awake()
    {
        // start plugin
        Logger = base.Logger;
        Logger.LogInfo($"loaded jackalope!");

        // patch dll
        Logger.LogInfo($"attempting to patch...");
        Harmony.CreateAndPatchAll(typeof(jackalope));
        Logger.LogInfo($"applied patches!");

        inputs = new List<float[]>();
        savestates = new Dictionary<int, List<float[]>>();
        for (int i = 0; i < 10; i++)
        {
            savestates.Add(i, []);
        }
        Logger.LogInfo($"set up savestates!");
    }

    [HarmonyPatch(typeof(Character), "Awake")]
    [HarmonyPostfix]
    static void FindCharacters()
    {
        // scan for character
        foreach (UnityEngine.Object obj in FindObjectsOfType(typeof(Character)))
        {
            Debug.Log("character found: " + obj.name);
            // check for challenge mode name
            if (obj.name == "NotAMeatboy(Clone)")
            {
                Debug.Log("character connected!");
                mainCharacter = GameObject.Find("NotAMeatboy(Clone)");
                mainCharacterScript = mainCharacter.GetComponent<Character>();
                mainCharacterBody = mainCharacter.GetComponent<Rigidbody2D>();
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

        // reset time
        if (Input.GetKeyDown(KeyCode.Slash))
        {
            Debug.Log("reset");
            tasFrames = 0;
            inputs = new List<float[]>();
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
            Time.timeScale = Time.timeScale != 5.0f ? 5.0f : 1.0f;
            Debug.Log("fast forward: " + (Time.timeScale == 5.0f));
        }

        // savestates
        for (int i = 48; i < 58; i++)
        {
            if (Input.GetKeyDown((KeyCode)i))
            {
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    savestates[i - 48] = [.. inputs];
                    Debug.Log("saved state " + (i - 48) + "! length = " + savestates[i - 48].Count);
                }
                else
                {
                    inputs = [.. savestates[i - 48]];
                    tasFrames = 0;
                    Debug.Log("loaded state " + (i - 48) + "! length = " + inputs.Count);
                }
            }
        }

        // export
        if (Input.GetKeyDown(KeyCode.LeftBracket))
        {
            using (StreamWriter sw = File.CreateText(@"C:\Program Files (x86)\Steam\steamapps\common\Ultimate Chicken Horse\tas.txt"))
            {
                for (int i = 0; i < inputs.Count; i++)
                {
                    string nextline = "";
                    for (int j = 0; j < 8; j++)
                    {
                        nextline += inputs[i][j] + ",";
                    }
                    sw.WriteLine(nextline);
                    Debug.Log(nextline);
                }
            }
            Debug.Log("exported!");
        }

        // import
        if (Input.GetKeyDown(KeyCode.RightBracket))
        {
            tasFrames = 0;
            inputs = new List<float[]>();
            if (File.Exists(@"C:\Program Files (x86)\Steam\steamapps\common\Ultimate Chicken Horse\tas.txt"))
            {
                using (StreamReader sr = File.OpenText(@"C:\Program Files (x86)\Steam\steamapps\common\Ultimate Chicken Horse\tas.txt"))
                {
                    string nextline = "";
                    while ((nextline = sr.ReadLine()) != null)
                    {
                        inputs.Add(new float[8]);
                        string[] vals = nextline.Split(",", System.StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 0; i < vals.Length; i++)
                        {
                            inputs[tasFrames][i] = (float)Convert.ToDouble(vals[i]);
                        }
                        tasFrames++;
                    }
                }
                tasFrames = 0;
                Debug.Log("imported!");
            }
            else
            {
                Debug.Log("no file found");
            }
        }
    }

    [HarmonyPatch(typeof(Character), "fullUpdate")]
    [HarmonyPrefix]
    static void TASReplay()
    {
        if (tasReplay)
        {
            // read inputs
            if (inputs.Count > tasFrames && mainCharacter != null)
            {
                Debug.Log("replaying...");
                mainCharacterScript.up = inputs[tasFrames][0];
                mainCharacterScript.down = inputs[tasFrames][1];
                mainCharacterScript.left = inputs[tasFrames][2];
                mainCharacterScript.right = inputs[tasFrames][3];
                mainCharacterScript.jump = inputs[tasFrames][4] == 1;
                mainCharacterScript.sprint = inputs[tasFrames][5] == 1;
                mainCharacterScript.suicide = inputs[tasFrames][6] == 1;
                mainCharacterScript.dance = inputs[tasFrames][7] == 1;
            }
            else
            {
                tasPause = true;
                Time.timeScale = 0.0f;
            }
        }
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
        else if (mainCharacter != null)
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
            statsDisplay.text += "position: (" + mainCharacter.transform.position.x + ", " + mainCharacter.transform.position.y + ")\n";
            statsDisplay.text += "velocity: (" + mainCharacterBody.velocity.x + ", " + mainCharacterBody.velocity.y + ")\n";
            statsDisplay.text += "gamespeed: " + Time.timeScale + "x\n";
        }
    }

    [HarmonyPatch(typeof(GameControl), "FixedUpdate")]
    [HarmonyPostfix]
    static void TASUpdate()
    {
        // write inputs
        inputs.Add(new float[8]);
        inputs[tasFrames][0] = mainCharacterScript.up;
        inputs[tasFrames][1] = mainCharacterScript.down;
        inputs[tasFrames][2] = mainCharacterScript.left;
        inputs[tasFrames][3] = mainCharacterScript.right;
        inputs[tasFrames][4] = mainCharacterScript.jump ? 1 : 0;
        inputs[tasFrames][5] = mainCharacterScript.sprint ? 1 : 0;
        inputs[tasFrames][6] = mainCharacterScript.suicide ? 1 : 0;
        inputs[tasFrames][7] = mainCharacterScript.dance ? 1 : 0;

        // update frame count
        tasFrames += 1;
    }
}
