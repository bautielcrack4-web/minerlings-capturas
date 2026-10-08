Shader "Mineros/MinerToonVC"
{
    // Igual que MinerToon, pero el color sale del vertice (ya en espacio de trabajo) multiplicado por _Color.
    // Lo usa VertexColorMerge para dibujar muchas submallas de distinto color en una sola llamada.
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _EmissionColor ("Emision", Color) = (0,0,0,1)
        _Rim ("Borde iluminado", Range(0,1)) = 0.22
        _Floor ("Piso de sombra", Range(0,1)) = 0.42
        _Spec ("Brillo especular", Range(0,2)) = 0
        _Gloss ("Dureza del brillo", Range(2,128)) = 40
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="ForwardBase" }
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_instancing
            #pragma multi_compile_local __ _ROOFCUT
            #include "MinerCommon.cginc"

            fixed4 _Color;
            fixed4 _EmissionColor;
            half _Rim;
            half _Floor;
            half _Spec;
            half _Gloss;

            struct AppVC
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // misma disposicion que MinerV2F (campos planos: los varyings anidados fallan en algunos traductores a GLSL)
            struct V2FVC
            {
                float4 pos : SV_POSITION;
                float3 wnrm : TEXCOORD0;
                float3 wpos : TEXCOORD1;
                float3 opos : TEXCOORD2;
                SHADOW_COORDS(3)
                half3 col : TEXCOORD4;
            };

            V2FVC vert(AppVC v)
            {
                V2FVC o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.wnrm = UnityObjectToWorldNormal(v.normal);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
            #ifdef _ROOFCUT
                o.wpos = MinerCutLift(o.wpos);
                v.vertex.xyz = mul(unity_WorldToObject, float4(o.wpos, 1.0)).xyz;   // la sombra recibida tambien
                o.pos = UnityWorldToClipPos(o.wpos);
            #else
                o.pos = UnityObjectToClipPos(v.vertex);
            #endif
                o.opos = v.vertex.xyz;
                TRANSFER_SHADOW(o)
                o.col = v.color.rgb;
                return o;
            }

            fixed4 frag(V2FVC i) : SV_Target
            {
                MinerV2F b;
                b.pos = i.pos;
                b.wnrm = i.wnrm;
                b.wpos = i.wpos;
                b.opos = i.opos;
            #if defined(SHADOWS_SCREEN) || defined(SHADOWS_DEPTH) || defined(SHADOWS_CUBE)
                b._ShadowCoord = i._ShadowCoord;
            #endif
                half3 col = MinerShade(b, _Color.rgb * i.col, _Rim, _Floor, _EmissionColor.rgb, _Spec, _Gloss);
                MINER_CUT_GLOW(col, i.wpos, i.pos);
                return fixed4(col, 1.0);
            }
            ENDCG
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            CGPROGRAM
            #pragma vertex vs
            #pragma fragment fs
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_local __ _ROOFCUT
            #include "MinerCutShadow.cginc"
            ENDCG
        }
    }
    Fallback "VertexLit"
}
