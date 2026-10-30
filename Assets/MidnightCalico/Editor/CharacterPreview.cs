using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace MidnightCalico.EditorTools {
 public static class CharacterPreview {
  [MenuItem("MidnightCalico/⑤ 人物プレビューを書き出し",false,30)]
  public static void Render() {
   if(EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Stop Play before preview.");return; }
   var root=new GameObject("CharacterPreview_TEMP");
   var camObject=new GameObject("PreviewCamera_TEMP");
   var key=new GameObject("PreviewLight_TEMP");
   var fill=new GameObject("PreviewFill_TEMP");
   var oldAmbient=RenderSettings.ambientLight;var oldMode=RenderSettings.ambientMode;bool fog=RenderSettings.fog;
   RenderTexture rt=null;Texture2D png=null;
   try {
    root.transform.position=new Vector3(1000,0,1000);
    root.AddComponent<PlayerModel>().Build();
    foreach(var t in root.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
    var light=key.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.color=new Color(1f,0.94f,0.86f);light.cullingMask=1<<31;key.transform.rotation=Quaternion.Euler(30,-30,0);
    var second=fill.AddComponent<Light>();second.type=LightType.Directional;second.intensity=0.55f;second.cullingMask=1<<31;fill.transform.rotation=Quaternion.Euler(20,145,0);
    RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(0.42f,0.45f,0.50f);RenderSettings.fog=false;
    var camera=camObject.AddComponent<Camera>();camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(0.20f,0.23f,0.25f);camera.orthographic=true;camera.orthographicSize=1.03f;camera.nearClipPlane=0.05f;camera.farClipPlane=10;
    rt=new RenderTexture(720,1000,24);camera.targetTexture=rt;png=new Texture2D(720,1000,TextureFormat.RGB24,false);
    Directory.CreateDirectory("Builds/CharacterPreview");
    for(int i=0;i<2;i++) {
     var pos=root.transform.position+new Vector3(i==0?0:1.7f,1.05f,3f);
     camera.transform.position=pos;camera.transform.LookAt(root.transform.position+Vector3.up*0.93f);camera.Render();
     var prev=RenderTexture.active;RenderTexture.active=rt;png.ReadPixels(new Rect(0,0,720,1000),0,0);png.Apply();RenderTexture.active=prev;
     File.WriteAllBytes("Builds/CharacterPreview/"+(i==0?"front":"three-quarter")+".png",png.EncodeToPNG());
    }
    Debug.Log("Character preview saved to Builds/CharacterPreview");
   } finally {
    RenderSettings.ambientLight=oldAmbient;RenderSettings.ambientMode=oldMode;RenderSettings.fog=fog;
    Object.DestroyImmediate(root);Object.DestroyImmediate(camObject);Object.DestroyImmediate(key);Object.DestroyImmediate(fill);
    if(rt!=null)Object.DestroyImmediate(rt);if(png!=null)Object.DestroyImmediate(png);
   }
  }
 }
}
