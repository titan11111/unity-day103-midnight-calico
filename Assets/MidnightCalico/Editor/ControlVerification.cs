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
        static Prop fallingProbe;
        static Vector3 fallingBefore;
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
                    else if (cmd == "play" && !EditorApplication.isPlayingOrWillChangePlaymode) MidnightCalicoSetup.OpenAndPlay();
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
                    VerifyNightEvents(gm);
                    VerifySwitchButtons();
                    PrepareShelterTest(gm);
                    step++; due = EditorApplication.timeSinceStartup + 2.5;
                }
                else if (step == 12)
                {
                    Require(fallingProbe != null && fallingProbe.transform.position.y < fallingBefore.y - 0.4f,"Shelf object actually falls under gravity during shelter entry");
                    Require(gm.player.IsSheltered && gm.player.ProtectedFromDebris,"Player crawls physically under dining table");
                    Require(gm.player.GetComponent<CharacterController>().height < 0.7f,"Crouched collision capsule fits below table top");
                    int hp = gm.player.hp;
                    SessionState.SetInt("mc.shelterHp",hp);
                    gm.player.HitByFallingProp();
                    Require(gm.player.hp == hp,"Table shelter prevents falling prop damage");
                    before = gm.player.transform.position;
                    gm.OpenPause();
                    float remaining = gm.nightEvents.QuakeSecondsLeft;
                    SessionState.SetFloat("mc.quakeRemaining",remaining);
                    step++; due = EditorApplication.timeSinceStartup + 0.4;
                }
                else if (step == 13)
                {
                    Require(Mathf.Abs(gm.nightEvents.QuakeSecondsLeft - SessionState.GetFloat("mc.quakeRemaining",0)) < 0.01f,"Pause freezes earthquake countdown while sheltered");
                    Require(Vector3.Distance(before,gm.player.transform.position) < 0.02f,"Pause preserves shelter position");
                    gm.ClosePause();
                    FixedCameraSystem.I.Shot(new Vector3(9.45f,1.0f,4.65f),new Vector3(11.15f,0.35f,3.2f));
                    step++; due = EditorApplication.timeSinceStartup + 1.5;
                }
                else if (step == 14)
                {
                    ScreenCapture.CaptureScreenshot("Builds/ControlVerification/earthquake-shelter.png");
                    gm.player.StartCoroutine(gm.player.LeaveShelter());
                    step++; due = EditorApplication.timeSinceStartup + 2;
                }
                else if (step == 15)
                {
                    Require(!gm.player.IsSheltered && gm.player.GetComponent<CharacterController>().height > 1.6f,"Player crawls out and restores standing capsule");
                    gm.player.HitByFallingProp();
                    Require(gm.player.hp == Mathf.Max(1,SessionState.GetInt("mc.shelterHp",5)-1),"Exposed player takes falling prop damage");
                    gm.newcomer.enabled=false;
                    PlacePlayer(gm,new Vector3(7.33f,0.02f,5.4f),Quaternion.identity);
                    gm.player.cameraRelative=false; GameInput.stick=Vector2.up;
                    step++; due=EditorApplication.timeSinceStartup+5.5;
                }
                else if (step == 16)
                {
                    GameInput.ClearTouches();
                    Require(gm.player.transform.position.y > 2.85f && gm.player.transform.position.z > 12.4f,"Player walks up all physical stair steps to second floor");
                    Require(gm.house.RoomName(gm.player.transform.position)=="2階 踊り場","Room detection distinguishes upstairs landing");
                    Require(FixedCameraSystem.I.current!=null && FixedCameraSystem.I.current.room=="2階 踊り場","Camera changes to upstairs landing on actual ascent");
                    PlacePlayer(gm,new Vector3(7.33f,3.02f,13.05f),Quaternion.Euler(0,90,0));
                    GameInput.stick=Vector2.up;
                    step++; due=EditorApplication.timeSinceStartup+1.2;
                }
                else if (step == 17)
                {
                    GameInput.ClearTouches();
                    Require(gm.house.RoomName(gm.player.transform.position)=="2階 書斎","Upstairs study is reachable through physical doorway");
                    VerifyNewcomerAndFaces(gm);
                    PlacePlayer(gm,new Vector3(7.33f,3.02f,12.7f),Quaternion.Euler(0,180,0));
                    gm.player.cameraRelative=false; GameInput.stick=Vector2.up;
                    step++; due=EditorApplication.timeSinceStartup+5.7;
                }
                else if (step == 18)
                {
                    GameInput.ClearTouches();
                    Require(gm.player.transform.position.y < 0.2f && gm.player.transform.position.z < 5.8f,"Player walks back down stairs to ground floor without teleport");
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
        static void PlacePlayer(GameManager gm,Vector3 position,Quaternion rotation)
        {
            GameInput.ClearTouches();
            var cc=gm.player.GetComponent<CharacterController>();cc.enabled=false;
            gm.player.transform.SetPositionAndRotation(position,rotation);cc.enabled=true;
            Physics.SyncTransforms(); FixedCameraSystem.I.CancelShot();
        }
        static void VerifyNewcomerAndFaces(GameManager gm)
        {
            Require(gm.newcomer!=null && gm.newcomer.model!=null,"Second timid cat exists with an independent model");
            Require(CatAI.I==gm.cat && gm.newcomer.GetComponent<CatAI>()==null,"New cat does not overwrite original Mike singleton or capture objective");
            Require(gm.newcomer.model.earsBack && gm.newcomer.Fear>0.45f,"Newcomer begins visibly frightened with ears back");
            Require(gm.house.RoomName(gm.newcomer.transform.position)=="2階 書斎","Newcomer lives on actual upstairs floor");
            PlacePlayer(gm,new Vector3(13.1f,3.02f,12.7f),Quaternion.Euler(0,90,0));
            float fear=gm.newcomer.Fear;gm.newcomer.Soothe();
            Require(gm.newcomer.Fear<fear,"Quietly watching newcomer reduces its fear");
            PlacePlayer(gm,new Vector3(13.1f,0.02f,12.7f),Quaternion.identity);
            Require(!gm.newcomer.InReach(gm.player),"New cat cannot be interacted with through floor below");
            PlacePlayer(gm,new Vector3(12f,3.02f,10f),Quaternion.identity);
            var model=gm.player.GetComponent<PlayerModel>();
            Require(model.FaceMaterial!=null && model.FaceMaterial.shader.name=="MidnightCalico/NatsukiExpression" && !ShaderUtil.ShaderHasError(model.FaceMaterial.shader),"Expression shader is loaded and compiled without shader errors");
            gm.player.hp=5;model.danger=false;model.sheltering=false;gm.caught=false;
            Field(gm.nightEvents,"quakeEnd").SetValue(gm.nightEvents,0f);Field(model,"painUntil").SetValue(model,0f);
            model.SendMessage("UpdateFace");
            Require(model.CurrentExpression==PlayerModel.FaceExpression.Neutral,"Face is neutral in calm gameplay");
            gm.nightEvents.TriggerQuake();model.SendMessage("UpdateFace");
            Require(model.CurrentExpression==PlayerModel.FaceExpression.Scared,"Earthquake changes player face to frightened");
            model.Flinch();model.SendMessage("UpdateFace");
            Require(model.CurrentExpression==PlayerModel.FaceExpression.Hurt,"Impact changes player face to pain");
            gm.caught=true;model.SendMessage("UpdateFace");
            Require(model.CurrentExpression==PlayerModel.FaceExpression.Relieved,"Catching Mike changes player face to relief");
            gm.caught=false;Field(model,"painUntil").SetValue(model,0f);
            CaptureFacePreviews(gm,model);
            FixedCameraSystem.I.Shot(new Vector3(15.2f,3.8f,13.5f),gm.newcomer.transform.position+Vector3.up*0.18f);
        }
        static void CaptureFacePreviews(GameManager gm,PlayerModel model)
        {
            var go=new GameObject("ExpressionPreviewCamera_TEMP");var lightGo=new GameObject("ExpressionPreviewLight_TEMP");
            RenderTexture rt=null;Texture2D texture=null;
            var previous=RenderTexture.active;
            try
            {
                var light=lightGo.AddComponent<Light>();light.type=LightType.Point;light.range=3f;light.intensity=1.3f;
                lightGo.transform.position=gm.player.transform.position+new Vector3(0.3f,1.8f,0.7f);
                var cam=go.AddComponent<Camera>();cam.nearClipPlane=0.03f;cam.fieldOfView=36f;
                cam.transform.position=gm.player.transform.position+new Vector3(0,1.68f,0.8f);
                cam.transform.LookAt(gm.player.transform.position+new Vector3(0,1.6f,0));
                rt=new RenderTexture(512,512,24);cam.targetTexture=rt;texture=new Texture2D(512,512,TextureFormat.RGB24,false);
                string[] names={"neutral","scared","hurt","relieved"};
                for(int i=0;i<4;i++)
                {
                    model.FaceMaterial.SetFloat("_Fear",i==1?1:0);model.FaceMaterial.SetFloat("_Pain",i==2?1:0);model.FaceMaterial.SetFloat("_Smile",i==3?1:0);model.FaceMaterial.SetFloat("_Blink",0);
                    cam.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,512,512),0,0);texture.Apply();
                    File.WriteAllBytes("Builds/ControlVerification/face-"+names[i]+".png",texture.EncodeToPNG());
                }
            }
            finally {RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(lightGo);if(rt!=null)UnityEngine.Object.DestroyImmediate(rt);if(texture!=null)UnityEngine.Object.DestroyImmediate(texture);}
        }
        static void PrepareShelterTest(GameManager gm)
        {
            Require(NightEvents.QuakeDuration == 20f && gm.nightEvents.QuakeSecondsLeft > 19f,"Earthquake lasts twenty seconds instead of five");
            Require(gm.house.shelters.Count == 2,"Dining table has entrances at both short ends");
            var cover = gm.house.shelters[0];
            var cc = gm.player.GetComponent<CharacterController>();
            cc.enabled=false; gm.player.transform.position=cover.entrance; cc.enabled=true;
            gm.player.transform.rotation=Quaternion.LookRotation(cover.inside-cover.entrance);
            Physics.SyncTransforms();
            Field(gm.player,"selectionFrame").SetValue(gm.player,-1);
            Require(gm.player.Selection.kind == PlayerController.TargetKind.Shelter,"Earthquake exposes shelter as investigate action");
            int count = gm.damage;
            Require(gm.nightEvents.DropQuakeProps() == 1 && gm.damage > count,"Earthquake pushes elevated kitchen prop off its support and records damage");
            fallingProbe = Prop.all.First(x => x.IsEarthquakeFall && x.transform.position.y > 1.4f);
            fallingBefore = fallingProbe.transform.position;
            gm.player.SendMessage("DoAction");
            Require(gm.player.IsSheltered,"Investigate action begins shelter entry");
        }
        static void VerifyNightEvents(GameManager gm)
        {
            var events = gm.nightEvents;
            Require(events != null, "Night incidents are created with the real house");
            var stove = gm.house.searches.First(x => x.name == "コンロ");
            gm.state = GState.Pause;
            events.TriggerQuake();
            Require(events.QuakeStrength == 0f, "Pause blocks earthquake triggers");
            gm.state = GState.Play;
            gm.cat.state = CatState.Idle;
            Require(gm.cat.VisitStove(), "Calm cat accepts a graph route to stove");
            Require(gm.cat.state == CatState.Stove, "Stove visit is a dedicated cat action");
            gm.cat.transform.position = gm.house.catSpawn;
            events.IgniteFromCat(gm.cat);
            Require(!events.StoveOn, "Timer or distant cat cannot ignite stove");
            gm.cat.transform.position = new Vector3(14.35f,0.92f,0.65f);
            events.IgniteFromCat(gm.cat);
            Require(events.StoveOn && stove.name.Contains("消火"), "Cat touching switch ignites visible stove and labels extinguish action");
            Require(!events.TryExtinguish(new SearchPoint("棚",Vector3.zero,ItemId.None,0,"test")) && events.StoveOn, "Other search targets cannot extinguish stove");
            gm.DoSearch(stove);
            Require(!events.StoveOn && gm.state == GState.Play && stove.name == "コンロ", "Real investigate action extinguishes stove without opening a stale message");
            events.TriggerQuake();
            Require(events.QuakeStrength > 0f && gm.cat.state == CatState.Flee, "Earthquake starts camera shake and cat escape");
            gm.cat.state = CatState.Caught;
            Require(!gm.cat.VisitStove(), "Caught cat cannot start a stove visit");
        }
        static void VerifySwitchButtons()
        {
#if ENABLE_INPUT_SYSTEM
            var flags = BindingFlags.NonPublic | BindingFlags.Static;
            var type = typeof(GameInput);
            var active = type.GetField("activePad",flags);
            var frame = type.GetField("padFrame",flags);
            var previous = active.GetValue(null);
            UnityEngine.InputSystem.Gamepad pad = null;
            try
            {
                UnityEngine.InputSystem.InputSystem.RegisterLayout("{\"name\":\"SwitchVerificationPad\",\"extend\":\"Gamepad\"}");
                pad = (UnityEngine.InputSystem.Gamepad)UnityEngine.InputSystem.InputSystem.AddDevice("SwitchVerificationPad");
                active.SetValue(null,pad); frame.SetValue(null,Time.frameCount);
                Require(GameInput.IsSwitchPad,"Switch layout is recognized");
                Require(ReferenceEquals(type.GetMethod("InvestigateButton",flags).Invoke(null,new object[]{pad}),pad.buttonEast),"Switch printed A/right investigates and confirms");
                Require(ReferenceEquals(type.GetMethod("ToolButton",flags).Invoke(null,new object[]{pad}),pad.buttonSouth),"Switch printed B/bottom uses tool");
                Require(ReferenceEquals(type.GetMethod("LightButton",flags).Invoke(null,new object[]{pad}),pad.buttonWest),"Switch printed Y/left toggles flashlight");
            }
            finally
            {
                active.SetValue(null,null);
                if(pad != null) UnityEngine.InputSystem.InputSystem.RemoveDevice(pad);
                UnityEngine.InputSystem.InputSystem.RemoveLayout("SwitchVerificationPad");
                active.SetValue(null,previous); frame.SetValue(null,-1);
            }
#endif
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
