Shader "Bluff/Barrier"
{
    Properties { _Color ("Color", Color) = (0.5,0.9,0.8,1) }
    SubShader
    {
        Tags { "Queue"="Transparent+9" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct v2f { float4 position:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float2 uv:TEXCOORD2; };
            v2f vert(appdata_base v)
            {
                v2f o;
                o.position=UnityObjectToClipPos(v.vertex);
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                o.normal=UnityObjectToWorldNormal(v.normal);
                o.uv=v.texcoord.xy;
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float rim=pow(1-saturate(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world))),2.5);
                float wave=pow(saturate(sin(i.uv.y*25-_Time.y*3)),16);
                return fixed4(_Color.rgb,_Color.a*(.055+rim*.42+wave*.05));
            }
            ENDCG
        }
    }
}
