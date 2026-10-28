// 深夜のフィルム調：色収差・色かぶり（影は青緑、明かりは暖色）・コントラスト・粒子ノイズ
Shader "Hidden/MidnightCalico/FilmLook"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Grain;
            float _Seed;
            float _CA;
            float _Sat;
            float _Contrast;
            float _Exposure;
            float4 _ShadowTint;
            float4 _LightTint;

            float rand(float2 co)
            {
                return frac(sin(dot(co, float2(12.9898, 78.233))) * 43758.5453);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float2 d = uv - 0.5;
                float ca = _CA * dot(d, d);
                float r = tex2D(_MainTex, uv + d * ca).r;
                float g = tex2D(_MainTex, uv).g;
                float b = tex2D(_MainTex, uv - d * ca).b;
                float3 c = float3(r, g, b) * _Exposure;
                float l = dot(c, float3(0.299, 0.587, 0.114));
                c = lerp(float3(l, l, l), c, _Sat);
                c = (c - 0.45) * _Contrast + 0.45;
                c = saturate(c);
                float3 tint = lerp(_ShadowTint.rgb, _LightTint.rgb, smoothstep(0.02, 0.55, l));
                c *= tint;
                float n = rand(uv * _ScreenParams.xy * 0.5 + _Seed) - 0.5;
                c += n * _Grain * (1.25 - l);
                return fixed4(saturate(c), 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
