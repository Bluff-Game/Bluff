// Shader utama untuk objek 3D (meja, karakter, kartu). Standard lighting + rim light + alpha cutout
// (untuk sudut kartu yang membulat). Disimpan di Resources supaya Shader.Find bekerja juga di build.
Shader "Bluff/Lit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.3
        _Metallic ("Metallic", Range(0,1)) = 0.0
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,1)
        _RimColor ("Rim Color (alpha = strength)", Color) = (1,1,1,0)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        _SurfaceTex ("Surface Grain (RGB), Polish (A)", 2D) = "white" {}
        _SurfaceNormal ("Surface Normal (Linear RGB)", 2D) = "bump" {}
        _SurfaceScale ("Surface Repeats Per Metre", Float) = 1
        _SurfaceRelief ("Surface Relief", Range(0,1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        LOD 200
        Cull Back

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows alphatest:_Cutoff addshadow
        #pragma target 3.0
        #pragma multi_compile_local __ _SURFACE_DETAIL

        sampler2D _MainTex;
        #if defined(_SURFACE_DETAIL)
        sampler2D _SurfaceTex;
        sampler2D _SurfaceNormal;
        half _SurfaceScale;
        half _SurfaceRelief;
        #endif
        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        fixed4 _EmissionColor;
        fixed4 _RimColor;
        half _RimPower;

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            #if defined(_SURFACE_DETAIL)
            float2 detailUV = IN.uv_MainTex * _SurfaceScale;
            half4 detail = tex2D(_SurfaceTex, detailUV);
            half3 detailNormal = tex2D(_SurfaceNormal, detailUV).xyz * 2.0h - 1.0h;
            o.Albedo *= detail.rgb;
            o.Normal = normalize(half3(detailNormal.xy * _SurfaceRelief, max(detailNormal.z, 0.25h)));
            o.Smoothness *= detail.a;
            o.Occlusion = lerp(0.93h, 1.0h, detail.r);
            #endif
            half rim = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
            o.Emission = _EmissionColor.rgb + _RimColor.rgb * _RimColor.a * pow(rim, _RimPower);
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Legacy Shaders/Transparent/Cutout/VertexLit"
}
