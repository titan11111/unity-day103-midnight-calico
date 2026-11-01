using System.Collections.Generic;
using UnityEngine;

namespace MidnightCalico
{
    public enum CatPose { Stand, Sit, Groom, Crouch, Hide, Eat, Play, Roll, Hiss, Held }

    /// <summary>
    /// 三毛猫「ミケ」の体をプリミティブで組み立て、関節を手続き的に動かす。
    /// 体長およそ 45cm、体高 25cm の成猫サイズ。
    /// </summary>
    public class CatModel : MonoBehaviour
    {
        Transform body, chest, haunch, neck, head, earL, earR, tailRoot;
        readonly Transform[] legUpper = new Transform[4];
        readonly Transform[] legLower = new Transform[4];
        readonly Transform[] paws = new Transform[4];
        static Mesh torsoMesh, earMesh;
        static Texture2D softCalico;
        readonly List<Transform> tail = new List<Transform>();
        Material eyeMat;
        readonly Dictionary<Renderer, string> originalFurNames = new Dictionary<Renderer, string>();
        readonly List<Transform> pupils = new List<Transform>();
        float earTwitchL, earTwitchR, tipFlick, breathT, headTilt;
        Renderer[] eyeRenderers;
        float gait, tailT, blinkT, pounceT;
        float eyeGlow;
        public float EyeEmission { get; private set; }
        Vector3 bodyBase = new Vector3(0f, 0.2f, 0f);

        public CatPose pose = CatPose.Stand;
        public float speed;          // 移動速度 m/s
        public Vector3? lookTarget;  // 頭を向ける先（ワールド）
        public float glowTarget;     // 懐中電灯で目が光る量
        public bool puffed;          // しっぽが太くなる（驚き）
        public bool earsBack;        // 耳を伏せる（怖い・怒り）
        public float fear;
        public bool megaPurr;        // カリカリを食べて「めっちゃゴロゴロ」中（喉の振動を体の揺れで表現）

        public void Build(bool newcomer = false, bool kitten = false)
        {
            var fur = newcomer ? Mat.Make("newCatFurSoft",new Color(0.42f,0.48f,0.55f),null,0.08f) : Mat.Make("calicoFurSoft",Color.white,SoftCalico(),0.08f);
            if (kitten) fur = Mat.Make("kittenGinger", new Color(0.92f, 0.56f, 0.24f), null, 0.12f);
            var white = Mat.Make("catWhite", new Color(0.95f, 0.94f, 0.9f), null, 0.08f);
            var black = Mat.Make("catBlack", new Color(0.09f, 0.08f, 0.07f), null, 0.1f);
            var orange = Mat.Make("catOrange", new Color(0.85f, 0.5f, 0.2f), null, 0.08f);
            if (kitten) black = orange;
            if(newcomer) orange = Mat.Make("newCatGrey",new Color(0.42f,0.48f,0.55f),null,0.08f);
            var pink = Mat.Make("catPink", new Color(0.92f, 0.6f, 0.62f), null, 0.4f);
            eyeMat = new Material(Mat.Make("catEyeBase", new Color(0.75f, 0.78f, 0.25f), null, 0.95f));
            eyeMat.name = "catEye";
            Mat.SetEmission(eyeMat, Color.black);
            var pupil = Mat.Make("catPupil", new Color(0.02f, 0.02f, 0.02f), null, 0.95f);

            body = Geo.Pivot(transform, "body", bodyBase);
            // 一枚の連続した輪郭で胸から腰までつなぎ、球体の継ぎ目をなくす。
            if (torsoMesh == null) torsoMesh = BuildTorsoMesh();
            Geo.MeshObj(body,"torso",torsoMesh,Vector3.zero,Vector3.zero,Vector3.one,fur);
            chest = Geo.Pivot(body,"chestP",new Vector3(0,0.01f,0.115f));
            haunch = Geo.Pivot(body,"haunchP",new Vector3(0,0,-0.13f));
            // 首・頭
            neck = Geo.Pivot(body, "neck", new Vector3(0, 0.045f, 0.155f));
            head = Geo.Pivot(neck, "head", new Vector3(0, 0.034f, 0.034f));
            if (kitten) head.localScale = Vector3.one * 1.18f;
            Geo.Sph(head, "skull", Vector3.zero, new Vector3(0.135f, 0.116f, 0.125f), fur);
            Geo.Sph(head,"muzzleL",new Vector3(-0.014f,-0.025f,0.047f),new Vector3(0.034f,0.028f,0.035f),white);
            Geo.Sph(head,"muzzleR",new Vector3(0.014f,-0.025f,0.047f),new Vector3(0.034f,0.028f,0.035f),white);
            Geo.Sph(head, "nose", new Vector3(0, -0.017f, 0.066f), new Vector3(0.013f, 0.008f, 0.008f), pink);
            // 目（懐中電灯を当てると光る）
            var eyes = new List<Renderer>();
            for (int s = -1; s <= 1; s += 2)
            {
                var e = Geo.Sph(head, "eye", new Vector3(s * 0.032f, 0.009f, 0.054f), new Vector3(0.024f, 0.015f, 0.009f), eyeMat);
                e.GetComponent<Renderer>().sharedMaterial = eyeMat;
                eyes.Add(e.GetComponent<Renderer>());
                var pp = Geo.Sph(head, "pupil", new Vector3(s * 0.032f, 0.009f, 0.0587f), new Vector3(0.005f, 0.013f, 0.002f), pupil);
                pupils.Add(pp.transform);
                // 目のハイライト
                var hl = Geo.Sph(head, "catchlight", new Vector3(s * 0.032f + 0.004f, 0.013f, 0.060f), Vector3.one * 0.002f, Mat.Emissive("catchlight", Color.white, Color.white * 0.8f));
                hl.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            eyeRenderers = eyes.ToArray();
            // 頬の毛・眉のひげ
            // 耳
            earL = Geo.Pivot(head, "earL", new Vector3(-0.043f, 0.037f, -0.005f));
            earR = Geo.Pivot(head, "earR", new Vector3(0.043f, 0.037f, -0.005f));
            if (earMesh == null) earMesh = BuildEarMesh();
            foreach (var ear in new[] { earL, earR })
            {
                Geo.MeshObj(ear,"ear",earMesh,Vector3.zero,Vector3.zero,new Vector3(1f,0.8f,1f),fur);
                Geo.MeshObj(ear,"earIn",earMesh,new Vector3(0,0.007f,0.006f),Vector3.zero,new Vector3(0.62f,0.56f,0.3f),pink);
            }
            // ひげ
            var whisker = Mat.Make("whisker", new Color(0.95f, 0.95f, 0.95f), null, 0.6f);
            for (int s = -1; s <= 1; s += 2)
                for (int k = 0; k < 3; k++)
                {
                    var w = Geo.Box(head, "whisker", new Vector3(s * 0.065f, -0.025f + k * 0.006f, 0.062f), new Vector3(0.065f, 0.0006f, 0.0006f), whisker, false);
                    w.transform.localRotation = Quaternion.Euler(0, s * (-15f + k * 6f), s * (8f - k * 8f));
                    w.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }

            // 脚: 0=左前 1=右前 2=左後 3=右後
            Vector3[] hips = { new Vector3(-0.05f, -0.02f, 0.14f), new Vector3(0.05f, -0.02f, 0.14f), new Vector3(-0.055f, -0.02f, -0.15f), new Vector3(0.055f, -0.02f, -0.15f) };
            for (int i = 0; i < 4; i++)
            {
                bool back = i >= 2;
                var legMat = (i == 2) ? orange : (i == 3 ? fur : white);
                legUpper[i] = Geo.Pivot(body, "leg" + i, hips[i]);
                Geo.MeshObj(legUpper[i],"thigh",BuildLimbMesh(0.09f,back?0.031f:0.021f,0.017f),Vector3.zero,Vector3.zero,Vector3.one,legMat);
                // 肘の継ぎ目は脚の太さに揃えた小さな丸みだけにし、玉が付いたように見えないようにする。
                Geo.Sph(legUpper[i],"softElbow",new Vector3(0,-0.09f,0),new Vector3(0.019f,0.02f,0.019f),legMat);
                legLower[i] = Geo.Pivot(legUpper[i],"knee",new Vector3(0,-0.09f,0));
                Geo.MeshObj(legLower[i],"shin",BuildLimbMesh(0.085f,0.017f,0.012f),Vector3.zero,Vector3.zero,Vector3.one,white);
                paws[i] = Geo.Pivot(legLower[i],"foot",new Vector3(0,-0.085f,0));
                Geo.Sph(paws[i], "paw", new Vector3(0,0,0.008f),new Vector3(0.034f,0.024f,0.047f),white);
            }

            // 一本の連続した先細りの尻尾を8関節で曲げる。
            tailRoot=Geo.Pivot(body,"tailRoot",new Vector3(0,0.025f,-0.204f));
            Transform parent=tailRoot;
            for(int i=0;i<8;i++)
            {
                var seg=Geo.Pivot(parent,"tail"+i,i==0?Vector3.zero:new Vector3(0,0,-0.034f));
                tail.Add(seg);parent=seg;
            }
            BuildTailSurface(fur);

            foreach (var r in GetComponentsInChildren<Renderer>()) r.receiveShadows = true;
        }

        static Texture2D SoftCalico()
        {
            if(softCalico!=null)return softCalico;
            const int width=256,height=128;softCalico=new Texture2D(width,height,TextureFormat.RGB24,true);softCalico.name="Soft broad calico patches";
            var pixels=new Color[width*height];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                float u=x/(float)width,v=y/(float)height;
                float orange=Mathf.Exp(-((u-0.23f)*(u-0.23f)/0.012f+(v-0.7f)*(v-0.7f)/0.09f));
                float black=Mathf.Exp(-((u-0.72f)*(u-0.72f)/0.016f+(v-0.58f)*(v-0.58f)/0.07f));
                Color c=new Color(0.92f,0.9f,0.84f);
                c=Color.Lerp(c,new Color(0.77f,0.43f,0.18f),Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.35f,0.48f,orange)));
                // 黒斑はチェスト正面に寄らない位置へ移し、境界をなだらかにして穴のように見えるのを防ぐ。
                c=Color.Lerp(c,new Color(0.17f,0.15f,0.13f),Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.3f,0.62f,black)));
                c*=0.97f+0.03f*Mathf.PerlinNoise(u*110f,v*30f);pixels[y*width+x]=c;
            }
            softCalico.SetPixels(pixels);softCalico.Apply();return softCalico;
        }

        static Mesh BuildLimbMesh(float length,float top,float bottom)
        {
            const int sides=12;var v=new Vector3[26];var uv=new Vector2[26];var tr=new List<int>();
            for(int r=0;r<2;r++) for(int k=0;k<=sides;k++)
            {
                int n=r*13+k;float a=k*Mathf.PI*2/sides;float radius=r==0?top:bottom;
                v[n]=new Vector3(Mathf.Cos(a)*radius,-r*length,Mathf.Sin(a)*radius);
                uv[n]=new Vector2(k/(float)sides,r);
                if(r==0 && k<sides){tr.Add(n);tr.Add(n+1);tr.Add(n+13);tr.Add(n+1);tr.Add(n+14);tr.Add(n+13);}
            }
            var m=new Mesh{name="Feline tapered limb"};m.vertices=v;m.uv=uv;m.triangles=tr.ToArray();m.RecalculateNormals();return m;
        }
        void BuildTailSurface(Material fur)
        {
            const int sides=12,rings=9;var v=new Vector3[rings*13];var uv=new Vector2[v.Length];var weights=new BoneWeight[v.Length];var tr=new List<int>();
            for(int r=0;r<rings;r++) for(int k=0;k<=sides;k++)
            {
                int n=r*13+k;float a=k*Mathf.PI*2/sides;float radius=Mathf.Lerp(0.017f,0.003f,r/8f);
                v[n]=new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,-r*0.034f);uv[n]=new Vector2(k/(float)sides,r/8f);
                weights[n]=new BoneWeight{boneIndex0=Mathf.Clamp(r,0,7),weight0=1f};
                if(r<8 && k<sides){tr.Add(n);tr.Add(n+13);tr.Add(n+1);tr.Add(n+1);tr.Add(n+13);tr.Add(n+14);}
            }
            var binds=new Matrix4x4[8];for(int i=0;i<8;i++)binds[i]=tail[i].worldToLocalMatrix*tailRoot.localToWorldMatrix;
            var m=new Mesh{name="Feline continuous tail"};m.vertices=v;m.uv=uv;m.triangles=tr.ToArray();m.boneWeights=weights;m.bindposes=binds;m.RecalculateNormals();m.RecalculateBounds();
            var go=new GameObject("tailSurface");go.transform.SetParent(tailRoot,false);var skin=go.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh=m;skin.bones=tail.ToArray();skin.rootBone=tailRoot;skin.sharedMaterial=fur;skin.updateWhenOffscreen=true;
        }

        static Mesh BuildTorsoMesh()
        {
            float[] z={-0.235f,-0.21f,-0.16f,-0.08f,0.03f,0.115f,0.18f,0.215f};
            float[] w={0.005f,0.045f,0.079f,0.068f,0.067f,0.076f,0.054f,0.012f};
            float[] h={0.012f,0.06f,0.082f,0.067f,0.07f,0.086f,0.067f,0.02f};
            const int sides=24, subdivisions=4;
            int rings=(z.Length-1)*subdivisions+1;
            // 両端の小さな開口をふさぐ中心頂点を2つ追加し、胸先に穴が見えないようにする。
            // 平らな円盤だと光を正面反射してテカった丸に見えるため、わずかに外へ膨らませてなだらかな丸みにする。
            var vertices=new Vector3[rings*(sides+1)+2];var uv=new Vector2[vertices.Length];
            int rearCap=vertices.Length-2, frontCap=vertices.Length-1;
            vertices[rearCap]=new Vector3(0,0.005f,z[0]-0.01f);uv[rearCap]=new Vector2(0.5f,0f);
            vertices[frontCap]=new Vector3(0,0.005f,z[z.Length-1]+0.012f);uv[frontCap]=new Vector2(0.5f,1f);
            var triangles=new List<int>();
            for(int r=0;r<rings;r++)
            {
                float f=r/(float)subdivisions;int a0=Mathf.Min(Mathf.FloorToInt(f),z.Length-2);float t=f-a0;
                float smooth=t*t*(3f-2f*t);
                float rw=Mathf.Lerp(w[a0],w[a0+1],smooth),rh=Mathf.Lerp(h[a0],h[a0+1],smooth);
                float rz=Mathf.Lerp(z[a0],z[a0+1],t);
                for(int k=0;k<=sides;k++)
                {
                    float a=k*Mathf.PI*2f/sides;int v=r*(sides+1)+k;
                    vertices[v]=new Vector3(Mathf.Cos(a)*rw,0.005f+Mathf.Sin(a)*rh,rz);
                    uv[v]=new Vector2(k/(float)sides,r/(float)(rings-1));
                    if(r<rings-1 && k<sides)
                    { int n=v+sides+1;triangles.Add(v);triangles.Add(v+1);triangles.Add(n);triangles.Add(v+1);triangles.Add(n+1);triangles.Add(n); }
                    // 巻き順を断定せず両面分追加し、どちらから見ても穴に見えないようにする。
                    if(r==0 && k<sides)
                    { triangles.Add(rearCap);triangles.Add(v+1);triangles.Add(v); triangles.Add(rearCap);triangles.Add(v);triangles.Add(v+1); }
                    if(r==rings-1 && k<sides)
                    { triangles.Add(frontCap);triangles.Add(v);triangles.Add(v+1); triangles.Add(frontCap);triangles.Add(v+1);triangles.Add(v); }
                }
            }
            var mesh=new Mesh{name="Feline continuous torso"};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static Mesh BuildEarMesh()
        {
            var m=new Mesh{name="Feline tapered ear"};
            m.vertices=new[]{new Vector3(-0.024f,0,0.006f),new Vector3(0.024f,0,0.006f),new Vector3(0,0.061f,-0.009f),
                new Vector3(-0.022f,0,-0.012f),new Vector3(0.022f,0,-0.012f),new Vector3(0,0.061f,-0.015f)};
            m.triangles=new[]{0,1,2,5,4,3,0,2,3,3,2,5,1,4,2,2,4,5,0,3,1,1,3,4};m.RecalculateNormals();m.RecalculateBounds();return m;
        }

        // Only fur materials change; eyes, nose and whiskers retain their own materials.
        public void ApplyAppearance(int coat, int age)
        {
            coat = Mathf.Clamp(coat, 0, GameManager.CoatNames.Length - 1);
            var baseColor = coat == 1 ? new Color(0.92f, 0.56f, 0.24f) :
                coat == 2 ? new Color(0.07f, 0.075f, 0.085f) :
                coat == 4 ? new Color(0.42f, 0.48f, 0.55f) : new Color(0.95f, 0.94f, 0.9f);
            var fur = coat == 0 ? Mat.Make("chosenCalicoSoft", Color.white, SoftCalico(), 0.08f) : Mat.Make("chosenFur" + coat, baseColor, null, 0.12f);
            var pale = Mat.Make("chosenPale" + coat, coat == 2 ? baseColor : new Color(0.95f, 0.94f, 0.9f), null, 0.08f);
            var patch = Mat.Make("chosenPatch" + coat, coat == 0 || coat == 1 ? new Color(0.85f, 0.5f, 0.2f) : baseColor, null, 0.08f);
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (!originalFurNames.ContainsKey(r)) originalFurNames[r] = r.sharedMaterial != null ? r.sharedMaterial.name : "";
                string n = originalFurNames[r];
                if (n.Contains("Fur") || n.Contains("kittenGinger") || n.StartsWith("chosen")) r.sharedMaterial = fur;
                else if (n.StartsWith("catWhite")) r.sharedMaterial = coat == 3 ? fur : pale;
                else if (n.StartsWith("catOrange") || n.StartsWith("newCatGrey")) r.sharedMaterial = patch;
                else if (n.StartsWith("catBlack")) r.sharedMaterial = coat == 0 ? Mat.Make("chosenBlack", new Color(0.09f, 0.08f, 0.07f), null, 0.1f) : patch;
            }
            if (head != null) head.localScale = Vector3.one * (age == 0 ? 1.18f : 1f);
        }

        void LateUpdate() { Animate(Time.deltaTime); }

        public void Animate(float dt)
        {
            if (body == null || legUpper[0] == null || eyeRenderers == null) return;
            gait += dt * Mathf.Max(0f,speed) / (speed > 2.5f ? 0.64f : 0.32f) * Mathf.PI * 2f;
            tailT += dt;
            blinkT -= dt;

            bool moving = speed > 0.05f;
            bool running = speed > 2.5f;
            float amp = running ? 55f : 28f;

            // ── 脚 ──
            for (int i = 0; i < 4; i++)
            {
                float phase;
                if (running) phase = gait + (i >= 2 ? Mathf.PI : 0f) + (i % 2 == 1 ? 0.35f : 0f);   // 跳ねるように走る
                else phase = gait + (i == 0 ? Mathf.PI * 0.5f : i == 1 ? Mathf.PI * 1.5f : i == 2 ? 0f : Mathf.PI);                         // 対角の脚が同時
                float upper = i>=2 ? -24f : 6f, lower = i>=2 ? 46f : -12f;
                if (moving)
                {
                    float cycle = Mathf.Repeat(phase / (2f*Mathf.PI),1f);
                    float stance = running ? 0.42f : 0.64f;
                    float stride = running ? 0.13f : 0.075f;
                    float swing = Mathf.Clamp01((cycle-stance)/(1f-stance));
                    float z = cycle < stance ? Mathf.Lerp(stride,-stride,cycle/stance) : Mathf.Lerp(-stride,stride,swing);
                    float lift = cycle < stance ? 0f : Mathf.Sin(swing*Mathf.PI)*(running?0.055f:0.028f);
                    float y = -0.174f + lift;
                    float distance = Mathf.Clamp(Mathf.Sqrt(y*y+z*z),0.025f,0.1748f);
                    float bend = Mathf.Acos(Mathf.Clamp((distance*distance-0.09f*0.09f-0.085f*0.085f)/(2f*0.09f*0.085f),-1f,1f));
                    float sign = i>=2 ? 1f : -1f;
                    lower = sign*bend*Mathf.Rad2Deg;
                    upper = (Mathf.Atan2(-z,-y)-Mathf.Atan2(0.085f*Mathf.Sin(sign*bend),0.09f+0.085f*Mathf.Cos(bend)))*Mathf.Rad2Deg;
                }
                switch (pose)
                {
                    case CatPose.Sit:
                    case CatPose.Groom:
                        if (i >= 2) { upper = -70f; lower = 120f; } else { upper = -12f; lower = 0f; }
                        break;
                    case CatPose.Crouch:
                    case CatPose.Hide:
                    case CatPose.Eat:
                        if (!moving) { upper = i >= 2 ? -55f : 50f; lower = i >= 2 ? 100f : -95f; }
                        break;
                    case CatPose.Play:
                        if (!moving)
                        {
                            float swipe = Mathf.Max(0f, Mathf.Sin(tailT * 9f));
                            if (i == 1) { upper = -60f - 60f * swipe; lower = 30f; }
                            else if (i >= 2) { upper = -55f; lower = 100f; }
                            else { upper = 30f; lower = -60f; }
                        }
                        break;
                    case CatPose.Roll:
                        upper = Mathf.Sin(tailT * 5f + i) * 40f - 20f;
                        lower = 40f;
                        break;
                    case CatPose.Held:
                        upper = 15f; lower = 10f;
                        break;
                }
                legUpper[i].localRotation = Quaternion.Slerp(legUpper[i].localRotation, Quaternion.Euler(upper, 0, 0), 1f - Mathf.Exp(-dt * 18f));
                legLower[i].localRotation = Quaternion.Slerp(legLower[i].localRotation, Quaternion.Euler(lower, 0, 0), 1f - Mathf.Exp(-dt * 18f));
                if (paws[i] != null) paws[i].localRotation = Quaternion.Euler(-(Mathf.DeltaAngle(0,legUpper[i].localEulerAngles.x)+Mathf.DeltaAngle(0,legLower[i].localEulerAngles.x)),0,0);
            }

            // ── 胴体の姿勢 ──
            Vector3 bPos = bodyBase;
            Vector3 bRot = Vector3.zero;
            if (moving)
            {
                bPos.y += Mathf.Abs(Mathf.Sin(gait)) * (running ? 0.018f : 0.003f);
                bRot.x = running ? Mathf.Sin(gait) * 4f : 0f;
            }
            switch (pose)
            {
                case CatPose.Sit:
                case CatPose.Groom:
                    bPos.y = 0.16f; bPos.z = -0.04f; bRot.x = -32f; break;
                case CatPose.Crouch:
                case CatPose.Hide:
                    if (!moving) { bPos.y = 0.11f; } else { bPos.y -= 0.04f; }
                    break;
                case CatPose.Eat:
                    bPos.y = 0.12f; bRot.x = 8f; break;
                case CatPose.Play:
                    pounceT += dt;
                    if (!moving) { bPos.y = 0.13f + Mathf.Max(0f, Mathf.Sin(pounceT * 4f)) * 0.06f; bRot.x = -10f + Mathf.Sin(pounceT * 4f) * 12f; }
                    break;
                case CatPose.Roll:
                    bPos.y = 0.1f; bRot.z = 90f + Mathf.Sin(tailT * 2.2f) * 60f; break;
                case CatPose.Hiss:
                    bPos.y = 0.24f; bRot.x = 6f; break;
                case CatPose.Held:
                    bPos.y = 0.0f; bRot.x = -60f; break;
            }
            if(fear > 0.1f) bPos.x += Mathf.Sin(tailT*37f) * 0.0025f * fear;
            body.localPosition = Vector3.Lerp(body.localPosition, bPos, 1f - Mathf.Exp(-dt * 10f));
            body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.Euler(bRot), 1f - Mathf.Exp(-dt * 8f));

            // ── 頭 ──
            Quaternion headRot = Quaternion.identity;
            if (pose == CatPose.Groom)
            {
                headRot = Quaternion.Euler(35f + Mathf.Sin(tailT * 6f) * 10f, -50f, -20f);
            }
            else if (pose == CatPose.Eat)
            {
                headRot = Quaternion.Euler(55f + Mathf.Sin(tailT * 8f) * 6f, 0, 0);
            }
            else if (lookTarget.HasValue)
            {
                Vector3 dir = lookTarget.Value - head.position;
                Quaternion world = Quaternion.LookRotation(dir.sqrMagnitude > 0.0001f ? dir : head.forward);
                Quaternion local = Quaternion.Inverse(neck.rotation) * world;
                Vector3 e = local.eulerAngles;
                float yaw = Mathf.Clamp(Mathf.DeltaAngle(0, e.y), -75f, 75f);
                float pitch = Mathf.Clamp(Mathf.DeltaAngle(0, e.x), -40f, 40f);
                headRot = Quaternion.Euler(pitch, yaw, 0);
            }
            else if (!moving)
            {
                headRot = Quaternion.Euler(Mathf.Sin(tailT * 0.7f) * 5f, Mathf.Sin(tailT * 0.4f) * 25f, 0);
            }
            neck.localRotation = Quaternion.Slerp(neck.localRotation, headRot, 1f - Mathf.Exp(-dt * 7f));

            // ── 耳 ──
            // 耳：ときどき片耳だけピクッと動く。怖いと伏せる（イカ耳）
            if (Random.value < dt * 0.4f) earTwitchL = 1f;
            if (Random.value < dt * 0.4f) earTwitchR = 1f;
            earTwitchL = Mathf.MoveTowards(earTwitchL, 0f, dt * 5f);
            earTwitchR = Mathf.MoveTowards(earTwitchR, 0f, dt * 5f);
            float back = earsBack ? 1f : 0f;
            earL.localRotation = Quaternion.Slerp(earL.localRotation, Quaternion.Euler(back * 25f, -earTwitchL * 35f + back * -40f, -12f - back * 55f + earTwitchL * 10f), 1f - Mathf.Exp(-dt * 14f));
            earR.localRotation = Quaternion.Slerp(earR.localRotation, Quaternion.Euler(back * 25f, earTwitchR * 35f + back * 40f, 12f + back * 55f - earTwitchR * 10f), 1f - Mathf.Exp(-dt * 14f));

            // 呼吸（止まっている時ほど見える）
            breathT += dt * (moving ? 3.2f : 1.7f) * (pose == CatPose.Hide ? 1.6f : 1f) * (megaPurr ? 2.2f : 1f);
            float br = Mathf.Sin(breathT) * (moving ? 0.01f : 0.025f);
            // 「めっちゃゴロゴロ」中は喉の振動を小刻みな揺れで重ねる
            if (megaPurr) br += Mathf.Sin(Time.time * 42f) * 0.012f;
            chest.localScale = new Vector3(1f + br*0.35f, 1f + br*0.35f, 1f);
            // 首をかしげる（警戒・興味）
            float tiltTarget = (pose == CatPose.Crouch || pose == CatPose.Play) && !moving ? Mathf.Sin(tailT * 0.6f) * 14f : 0f;
            headTilt = Mathf.Lerp(headTilt, tiltTarget, 1f - Mathf.Exp(-dt * 3f));
            head.localRotation = Quaternion.Euler(0, 0, headTilt);

            // ── しっぽ ──
            float baseUp, sway, curl;
            if (running) { baseUp = 5f; sway = 10f; curl = 4f; }
            else if (pose == CatPose.Hiss) { baseUp = 65f; sway = 3f; curl = -10f; }
            else if (pose == CatPose.Sit || pose == CatPose.Groom) { baseUp = -60f; sway = 25f; curl = 10f; }
            else if (pose == CatPose.Hide || pose == CatPose.Crouch) { baseUp = -15f; sway = 35f; curl = 6f; }
            else if (pose == CatPose.Play) { baseUp = 10f; sway = 45f; curl = 6f; }
            else { baseUp = 55f; sway = 12f; curl = -14f; }
            if(fear > 0.1f) { baseUp = -65f; curl = 22f; sway = 3f; }
            float swaySpeed = pose == CatPose.Play || pose == CatPose.Hide ? 6f : 1.8f;
            for (int i = 0; i < tail.Count; i++)
            {
                float k = i / (float)(tail.Count - 1);
                float x = (i == 0 ? baseUp : curl) + 0f;
                float y = Mathf.Sin(tailT * swaySpeed - i * 0.6f) * sway * (0.3f + k);
                // しっぽの先だけピクピク（狩りの集中・いらだち）
                if (i >= tail.Count - 3) y += tipFlick * Mathf.Sin(tailT * 22f) * 30f;
                tail[i].localRotation = Quaternion.Slerp(tail[i].localRotation, Quaternion.Euler(x, y, 0), 1f - Mathf.Exp(-dt * 10f));
                float fat = puffed ? 1.9f : 1f;
                tail[i].localScale = Vector3.Lerp(tail[i].localScale, new Vector3(i == 0 ? fat : 1f, i == 0 ? fat : 1f, 1f), 1f - Mathf.Exp(-dt * 8f));
            }

            if ((pose == CatPose.Hide || pose == CatPose.Crouch || pose == CatPose.Play) && Random.value < dt * 0.7f) tipFlick = 1f;
            tipFlick = Mathf.MoveTowards(tipFlick, 0f, dt * 1.5f);

            // 瞳孔：暗い所・興奮で丸く、ライトを当てると細く
            float dil = (pose == CatPose.Play || pose == CatPose.Hide || pose == CatPose.Hiss) ? 1f : 0.55f;
            dil = Mathf.Lerp(dil, 0.15f, Mathf.Clamp01(eyeGlow));
            foreach (var p in pupils)
            {
                var ps = p.localScale;
                ps.x = Mathf.Lerp(ps.x, Mathf.Lerp(0.0025f, 0.011f, dil), 1f - Mathf.Exp(-dt * 6f));
                p.localScale = ps;
            }

            // ── 目の反射（タペタム） ──
            // 暗い部屋では弱い黄緑の反射。カメラ側へ顔を向けると見え、点灯した部屋では控えめに。
            bool roomLit = false;
            var gm = GameManager.I;
            if (gm != null && gm.house != null)
            {
                string room = gm.house.RoomName(transform.position);
                foreach (var sw in gm.house.switches) if (sw.on && sw.room == room) { roomLit = true; break; }
            }
            float facing = 0f;
            var cs = FixedCameraSystem.I;
            if (cs != null && cs.cam != null)
            {
                Vector3 toCamera = cs.cam.transform.position - head.position;
                facing = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.05f, 0.7f, Vector3.Dot(head.forward, toCamera.normalized)));
                facing *= 1f - Mathf.InverseLerp(8f, 18f, toCamera.magnitude);
            }
            float darkReflection = (roomLit ? 0.06f : 0.42f) * facing;
            eyeGlow = Mathf.MoveTowards(eyeGlow, Mathf.Max(darkReflection, glowTarget * facing), dt * 4f);
            if (blinkT < 0f) blinkT = Random.Range(2.5f, 6f);
            bool blink = blinkT < 0.12f;
            EyeEmission = blink ? 0f : eyeGlow * 2.2f;
            Mat.SetEmission(eyeMat, new Color(0.65f, 0.85f, 0.3f) * EyeEmission);
            foreach (var r in eyeRenderers)
            {
                var s = r.transform.localScale;
                s.y = Mathf.Lerp(s.y, blink ? 0.001f : 0.015f, 1f - Mathf.Exp(-dt * 30f));
                r.transform.localScale = s;
            }
            foreach (var p in pupils)
            {
                var q = p.localScale;
                q.y = Mathf.Lerp(q.y, blink ? 0.002f : 0.02f, 1f - Mathf.Exp(-dt * 30f));
                p.localScale = q;
            }
        }
    }
}
