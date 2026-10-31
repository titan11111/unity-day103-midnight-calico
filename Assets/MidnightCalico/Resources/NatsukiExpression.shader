Shader "MidnightCalico/NatsukiExpression"
{
    Properties
    {
        _MainTex ("Face", 2D) = "white" {}
        _Fear ("Fear", Range(0,1)) = 0
        _Smile ("Relief", Range(0,1)) = 0
        _Pain ("Pain", Range(0,1)) = 0
        _Blink ("Blink", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex;
        half _Fear, _Smile, _Pain, _Blink;
        struct Input { float2 uv_MainTex; };
        float mask(float2 uv,float2 center,float2 radius)
        {
            float2 q=(uv-center)/radius;
            return 1-smoothstep(0.55,1,dot(q,q));
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv=IN.uv_MainTex;
            float2 sampleUV=uv;
            // 目を見開く／痛みで細める／瞬きを、元の顔の同じ位置で変形。
            float eyeX=uv.x<0.5?0.30:0.70;
            float eyeWidth=1-smoothstep(0.11,0.16,abs(uv.x-eyeX));
            float scale=max(0.12,1+_Fear*0.28-_Smile*0.30-_Pain*0.78-_Blink*0.88);
            float dyEye=uv.y-0.62;
            float dist=abs(dyEye), edge=0.085, inner=0.045*scale;
            if(dist<edge)
            {
                float sourceDist=dist<inner?dist/scale:0.045+(dist-inner)*(edge-0.045)/(edge-inner);
                sampleUV.y+=sign(dyEye)*(sourceDist-dist)*eyeWidth;
            }
            float brows=mask(uv,float2(eyeX,0.747),float2(0.16,0.037));
            float innerBrow=uv.x<0.5?(uv.x-0.30):(0.70-uv.x);
            sampleUV.y-=brows*(_Fear*(0.010+innerBrow*0.04)-_Pain*(0.006-innerBrow*0.04));
            float mouth=mask(uv,float2(0.5,0.24),float2(0.23,0.14));
            float corner=pow(saturate(abs(uv.x-0.5)/0.17),2);
            sampleUV.y-=mouth*(_Smile*0.025*corner-_Fear*0.015*corner);
            float opening=(_Fear*0.024+_Pain*0.008)*(1-smoothstep(0.09,0.15,abs(uv.x-0.5)));
            float dy=uv.y-0.24;
            sampleUV.y-=sign(dy)*opening*mouth;
            fixed3 color=tex2D(_MainTex,sampleUV).rgb;
            float innerMouth=(1-smoothstep(max(0.0005,opening-0.003),max(0.001,opening),abs(dy)))*step(0.003,opening);
            color=lerp(color,fixed3(0.12,0.035,0.04),innerMouth);
            o.Albedo=color;
            o.Metallic=0; o.Smoothness=0.15; o.Alpha=1;
        }
        ENDCG
    }
    FallBack "Standard"
}
