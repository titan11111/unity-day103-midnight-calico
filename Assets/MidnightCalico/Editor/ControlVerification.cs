using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MidnightCalico.EditorTools
{
    // Editor専用。依存追加なしで指ID・中断復帰・実シーンの入力を回帰検査する。
    [InitializeOnLoad]
    public static class ControlVerification
    {
        static readonly List<string> checks = new List<string>();
        static string failure;
        static int step, lightCount, nextFrame;
        static double due;
        static Vector3 before, basisBefore;
        static float darkEmission;
        static bool initialLight;
        static int originalRun;
        static VirtualStick stick;
        static TouchButton run, light;
        static PointerEventData finger;
        static string CommandPath { get { return Path.Combine(Directory.GetCurrentDirectory(), "Temp/controls-command.txt"); } }
        static ControlVerification()
        {
            EditorApplication.update += Update;
            Application.logMessageReceived += OnLog;
            if (SessionState.GetBool("mc.controlsVerifying", false)) { step = 0; due = EditorApplication.timeSinceStartup + 2; }
        }
        static void OnLog(string condition, string stack, LogType type)
        {
            if (SessionState.GetBool("mc.controlsVerifying", false) && (type == LogType.Exception || type == LogType.Error)) failure = condition + "\n" + stack;
        }
        static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            checks.Add(message);
        }
        [MenuItem("MidnightCalico/⑥ 操作UIの回帰検証", false, 31)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before verification.");
            checks.Clear(); failure = null;
            var go = new GameObject("ControlVerification_TEMP", typeof(RectTransform));
            int previousMode = PlayerPrefs.GetInt("mc.runToggle",1);
            try
            {
                GameInput.ClearTouches();
                var button = go.AddComponent<TouchButton>();
                button.SendMessage("OnEnable"); // Edit mode does not run MonoBehaviour lifecycle automatically.
                button.id = GameInput.Run;
                button.visual = new GameObject("visual",typeof(RectTransform)).GetComponent<RectTransform>();
                button.visual.SetParent(go.transform,false);
                var a = new PointerEventData(EventSystem.current) { pointerId = 11 };
                var b = new PointerEventData(EventSystem.current) { pointerId = 12 };
                GameInput.SetRunMode(true);
                button.OnPointerDown(a);
                button.OnPointerDown(b);
                Require(GameInput.runLatched, "Run toggles once even when another finger lands on same button");
                button.OnPointerUp(b);
                Require(button.IsPressed, "Unrelated finger cannot release owned button");
                Require(go.transform.localScale == Vector3.one && button.visual.localScale.x < 1, "Press animation leaves hit rectangle unchanged");
                button.OnPointerExit(a);
                Require(!button.IsPressed && GameInput.runLatched, "Exit releases owned pointer while toggle mode keeps deliberate run state");
                button.OnPointerDown(a); button.OnPointerUp(a);
                Require(!GameInput.runLatched, "Second tap returns to walking");
                GameInput.SetRunMode(false);
                button.OnPointerDown(a); button.OnPointerUp(b);
                Require(GameInput.Held(GameInput.Run), "Hold mode survives another pointer releasing");
                button.OnPointerExit(a);
                Require(!GameInput.Held(GameInput.Run), "Hold mode cancels when owned pointer exits");
                GameInput.stick = Vector2.one; button.OnPointerDown(a);
                GameInput.ClearTouches();
                Require(!button.IsPressed && GameInput.stick == Vector2.zero && !GameInput.runLatched, "Clear resets all held buttons, pointer ownership, stick and run state");
                button.id = GameInput.Torch;
                for (int i=0;i<20;i++) { button.OnPointerDown(a); Require(GameInput.Pressed(GameInput.Torch), "Tap "+(i+1)+" is received"); Require(!GameInput.Pressed(GameInput.Torch), "Tap "+(i+1)+" is consumed once"); button.OnPointerUp(a); }
                button.OnPointerDown(a); GameInput.ClearTouches();
                Require(!GameInput.Pressed(GameInput.Torch), "Menu/reset discards pending action");
                SessionState.SetString("mc.controlsChecks", JsonUtility.ToJson(new Report { checks = checks.ToArray() }));
                SessionState.SetInt("mc.originalRun", previousMode);
                SessionState.SetBool("mc.controlsVerifying", true);
                step = 0; due = EditorApplication.timeSinceStartup + 2;
                EditorApplication.isPlaying = true;
            }
            catch (Exception e) { failure = e.ToString(); WriteReport(); }
            finally { go.SendMessage("OnDisable",SendMessageOptions.DontRequireReceiver); UnityEngine.Object.DestroyImmediate(go); PlayerPrefs.SetInt("mc.runToggle",previousMode); GameInput.ClearTouches(); }
        }
        static void Update()
        {
            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating && File.Exists(CommandPath))
            {
                string cmd = File.ReadAllText(CommandPath).Trim();
                File.Delete(CommandPath);
                try
                {
                    if (cmd == "verify") Run();
                    else if (cmd == "build" && !EditorApplication.isPlayingOrWillChangePlaymode) MidnightCalicoSetup.BuildWebGL();
                    else if (cmd == "preview" && !EditorApplication.isPlayingOrWillChangePlaymode) CharacterPreview.Render();
                }
                catch (Exception e) { Debug.LogException(e); }
            }
            if (!SessionState.GetBool("mc.controlsVerifying", false) || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup < due) return;
            try
            {
                if (failure != null) throw new Exception(failure);
                var gm = GameManager.I;
                if (gm == null) throw new Exception("GameManager did not start");
                if (step == 0)
                {
                    var saved = JsonUtility.FromJson<Report>(SessionState.GetString("mc.controlsChecks","{}"));
                    checks.Clear(); if (saved.checks != null) checks.AddRange(saved.checks);
                    originalRun = SessionState.GetInt("mc.originalRun",1);
                    gm.ui.SetTouchVisible(true); GameInput.SetRunMode(true);
                    gm.ui.SendMessage("StartIntro"); gm.ui.SendMessage("BeginFromIntro");
                    stick = UnityEngine.Object.FindFirstObjectByType<VirtualStick>(FindObjectsInactive.Include);
                    var buttons = UnityEngine.Object.FindObjectsByType<TouchButton>(FindObjectsInactive.Include,FindObjectsSortMode.None);
                    run = buttons.First(x=>x.id==GameInput.Run);
                    light = buttons.First(x=>x.id==GameInput.Torch);
                    finger = new PointerEventData(EventSystem.current) { pointerId=41 };
                    Require(stick != null && run != null && light != null, "Scene builds touch controls");
                    step++; due = EditorApplication.timeSinceStartup + 0.2;
                }
                else if (step == 1)
                {
                    before = gm.player.transform.position;
                    finger.position = RectTransformUtility.WorldToScreenPoint(null,stick.area.TransformPoint(new Vector3(160,160,0)));
                    stick.OnPointerDown(finger);
                    finger.position += new Vector2(0,65*UnityEngine.Object.FindFirstObjectByType<Canvas>().scaleFactor);
                    stick.OnDrag(finger);
                    run.OnPointerDown(new PointerEventData(EventSystem.current){pointerId=42});
                    run.OnPointerUp(new PointerEventData(EventSystem.current){pointerId=42});
                    Require(GameInput.stick.magnitude > 0.4f && GameInput.runLatched, "Move and run use separate fingers concurrently");
                    step++; due = EditorApplication.timeSinceStartup + 0.5;
                }
                else if (step == 2)
                {
                    Require(Vector3.Distance(before,gm.player.transform.position) > 0.05f, "Real player moves from touch stick input");
                    gm.OpenPause();
                    Require(GameInput.stick == Vector2.zero && !GameInput.runLatched, "Pause clears moving stick and latched running");
                    gm.ClosePause(); before = gm.player.transform.position;
                    step++; due = EditorApplication.timeSinceStartup + 0.25;
                }
                else if (step == 3)
                {
                    Require(Vector3.Distance(before,gm.player.transform.position) < 0.035f, "Resume does not replay previous movement");
                    initialLight = gm.player.FlashlightOn; lightCount=0; nextFrame=Time.frameCount+2;
                    step++; due=0;
                }
                else if (step == 4 && Time.frameCount >= nextFrame)
                {
                    if (lightCount > 0) Require(gm.player.FlashlightOn == (lightCount%2==0 ? initialLight : !initialLight), "Runtime light toggle "+lightCount+" applies once");
                    if (lightCount == 20) { step++; due=EditorApplication.timeSinceStartup+0.2; }
                    else
                    {
                        var e = new PointerEventData(EventSystem.current){pointerId=43};
                        light.OnPointerDown(e); light.OnPointerUp(e);
                        lightCount++; nextFrame=Time.frameCount+3;
                    }
                }
                else if (step == 5)
                {
                    gm.OpenInventory();
                    Require(GameInput.stick == Vector2.zero && !GameInput.runLatched, "Inventory clears gameplay controls");
                    gm.CloseInventory();
                    Directory.CreateDirectory("Builds/ControlVerification");
                    VerifyTargets(gm);
                    GameInput.ClearTouches(); GameInput.stick = Vector2.up;
                    ScreenCapture.CaptureScreenshot("Builds/ControlVerification/gameplay.png");
                    step++; due=EditorApplication.timeSinceStartup+0.5;
                }
                else if (step == 6)
                {
                    basisBefore = (Vector3)Field(gm.player,"heldCamFwd").GetValue(gm.player);
                    FixedCameraSystem.I.ForceCut();
                    step++; nextFrame=Time.frameCount+3; due=0;
                }
                else if (step == 7 && Time.frameCount >= nextFrame)
                {
                    Vector3 basis = (Vector3)Field(gm.player,"heldCamFwd").GetValue(gm.player);
                    Require(Vector3.Dot(basisBefore,basis)>0.999f,"Camera cut preserves held movement basis");
                    GameInput.stick = Vector2.right; step++; nextFrame=Time.frameCount+3;
                }
                else if (step == 8 && Time.frameCount >= nextFrame)
                {
                    Require(!(bool)Field(gm.player,"cutBasis").GetValue(gm.player),"Deliberate stick redirection adopts current camera basis");
                    GameInput.ClearTouches();
                    gm.OpenPause(); ScreenCapture.CaptureScreenshot("Builds/ControlVerification/pause.png");
                    step++; due=EditorApplication.timeSinceStartup+0.4;
                }
                else if (step == 9)
                {
                    gm.ClosePause(); gm.cat.enabled=false;
                    gm.cat.model.lookTarget=null; gm.cat.model.pose=CatPose.Stand;
                    gm.cat.transform.position=gm.house.catSpawn;
                    gm.cat.transform.rotation=Quaternion.identity;
                    foreach(var sw in gm.house.switches) { sw.on=false; if(sw.light!=null) sw.light.intensity=0; }
                    if(gm.player.FlashlightOn) gm.player.UseItem(ItemId.Flashlight);
                    var catPos=gm.cat.transform.position;
                    var shot=catPos+new Vector3(0,0.42f,1.15f); var look=catPos+new Vector3(0,0.27f,0);
                    FixedCameraSystem.I.cam.transform.position=shot;
                    FixedCameraSystem.I.cam.transform.LookAt(look);
                    FixedCameraSystem.I.Shot(shot,look);
                    gm.ui.SetTouchVisible(false);
                    Field(gm.cat.model,"blinkT").SetValue(gm.cat.model,5f);
                    step++; due=EditorApplication.timeSinceStartup+2;
                }
                else if (step == 10)
                {
                    darkEmission=gm.cat.model.EyeEmission;
                    Require(darkEmission>0.1f,"Cat eyes reflect visibly in darkness with flashlight off");
                    ScreenCapture.CaptureScreenshot("Builds/ControlVerification/cat-eyes-dark.png");
                    foreach(var sw in gm.house.switches) sw.on=true;
                    step++; due=EditorApplication.timeSinceStartup+0.7;
                }
                else if (step == 11)
                {
                    Require(gm.cat.model.EyeEmission<darkEmission*0.5f,"Cat eye reflection dims in a lit room");
                    WriteReport();
                    SessionState.SetBool("mc.controlsVerifying",false);
                    PlayerPrefs.SetInt("mc.runToggle",originalRun);
                    EditorApplication.isPlaying=false;
                }
            }
            catch (Exception e)
            {
                failure=e.ToString(); WriteReport(); SessionState.SetBool("mc.controlsVerifying",false);
                PlayerPrefs.SetInt("mc.runToggle",SessionState.GetInt("mc.originalRun",1));
                EditorApplication.isPlaying=false;
            }
        }
        static FieldInfo Field(object instance,string name)
        {
            return instance.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);
        }
        static void VerifyTargets(GameManager gm)
        {
            var searches=gm.house.searches.ToArray(); var switches=gm.house.switches.ToArray();
            var catState=gm.cat.state;
            try
            {
                gm.cat.state=CatState.Caught;
                gm.house.searches.Clear(); gm.house.switches.Clear();
                Vector3 origin=gm.player.transform.position;
                Vector3 forward=gm.player.transform.forward;
                var search=new SearchPoint("検証用の棚",origin+forward*0.8f,ItemId.None,1,"test");
                var sw=new LightSwitch("検証用の電灯",origin+forward*0.25f,null,1f);
                gm.house.searches.Add(search); gm.house.switches.Add(sw);
                Field(gm.player,"selectionFrame").SetValue(gm.player,-1);
                Require(gm.player.Selection.lightSwitch==sw,"Nearby switch competes with search objects instead of being masked");
                GameInput.TouchDown(GameInput.Action);
                search.pos=origin+forward*0.01f; sw.pos=origin+forward*0.75f;
                Field(gm.player,"selectionFrame").SetValue(gm.player,-1);
                Require(gm.player.Selection.search==search,"Target selection changes when another target is clearly nearer");
                Require(GameInput.Pressed(GameInput.Action),"Captured action input is available");
                gm.player.SendMessage("DoAction");
                Require(sw.on && gm.state==GState.Play,"Action uses target captured at pointer down, not a newly selected object");
            }
            finally
            {
                gm.house.searches.Clear(); gm.house.searches.AddRange(searches);
                gm.house.switches.Clear(); gm.house.switches.AddRange(switches);
                gm.cat.state=catState;
                Field(gm.player,"selectionFrame").SetValue(gm.player,-1);
            }
        }
        [Serializable] class Report { public string checkedAtUtc; public string result; public string error; public string[] checks; }
        static void WriteReport()
        {
            Directory.CreateDirectory("Builds/ControlVerification");
            File.WriteAllText("Builds/ControlVerification/result.json",JsonUtility.ToJson(new Report { checkedAtUtc=DateTime.UtcNow.ToString("o"), result=failure==null?"passed":"failed",error=failure,checks=checks.ToArray()},true));
            Debug.Log("[ControlVerification] " + (failure==null?"PASSED":"FAILED: "+failure));
        }
    }
}
