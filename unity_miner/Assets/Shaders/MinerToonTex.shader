Shader "Mineros/MinerToonTex"
{
    // Igual que MinerToon, con textura (modelos de Tripo: edificios). Color = textura x _Color.
    Properties
    {
        _MainTex ("Textura", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _EmissionColor ("Emision", Color) = (0,0,0,1)
        _Rim ("Borde iluminado", Range(0,1)) = 0.18
        _Floor ("Piso de sombra", Range(0,1)) = 0.5
        _Spec ("Brillo especular", Range(0,2)) = 0
        _Gloss ("Dureza del brillo", Range(2,128)) = 40
        _ClipY ("Corte por altura (obra en construccion)", Float) = 1000
        _EmisRows ("Filas emisivas de la paleta (kit de habitaciones)", Float) = 0
        _KitAtlas ("Atlas del kit: filas emisivas en el uv2", Float) = 0
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
            #pragma multi_compile_local __ _ROOFCUT
            #include "MinerCommon.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _EmisRows;
            float _KitAtlas;   // solo el material del atlas del kit: otras mallas usan el uv2 para otra cosa (el suelo)
            fixed4 _Color;
            fixed4 _EmissionColor;
            half _Rim;
            half _Floor;
            half _Spec;
            half _Gloss;
            float _ClipY;

            struct AppT
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;   // atlas del kit: (pixel local de la paleta, filas emisivas de la pieza)
            };

            struct V2FT
            {
                float4 pos : SV_POSITION;
                float3 wnrm : TEXCOORD0;
                float3 wpos : TEXCOORD1;
                float3 opos : TEXCOORD2;
                SHADOW_COORDS(3)
                float4 uv : TEXCOORD4;   // xy = uv, zw = uv2
            };

            V2FT vert(AppT v)
            {
                V2FT o;
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
                o.uv = float4(v.uv, v.uv2);
                return o;
            }

            fixed4 frag(V2FT i) : SV_Target
            {
                MinerV2F b;
                b.pos = i.pos;
                b.wnrm = i.wnrm;
                b.wpos = i.wpos;
                b.opos = i.opos;
            #if defined(SHADOWS_SCREEN) || defined(SHADOWS_DEPTH) || defined(SHADOWS_CUBE)
                b._ShadowCoord = i._ShadowCoord;
            #endif
                clip(_ClipY - i.wpos.y);
                half3 alb = tex2D(_MainTex, i.uv.xy).rgb * _Color.rgb;
                half3 col = MinerShade(b, alb, _Rim, _Floor, _EmissionColor.rgb, _Spec, _Gloss);
                // borde de la obra: una franja clara justo debajo del corte
                col += saturate(1.0 - (_ClipY - i.wpos.y) * 8.0) * half3(0.35, 0.3, 0.2) * step(_ClipY, 999.0);
                // celdas emisivas del kit (kit_lib.py, glow): las filas de abajo de la paleta (celdas de 8 px) brillan sin
                // luz y laten; la columna dice como: 0 late (0.8-1, 1.4 s), 1 titila (farol, fuego), 2 respira (0.5-1,
                // 2.2 s), 3 parpadeo de LED en secuencia. La fase sale de la posicion en el mundo: cada pieza a su ritmo.
                // con el atlas del kit (IslandArt.KitAtlas) la fila sale del uv2 (pixel local, filas de esa pieza)
                if ((_EmisRows > 0.5 && i.uv.y * _MainTex_TexelSize.w < _EmisRows * 8.0) || (_KitAtlas > 0.5 && i.uv.w > 0.5 && i.uv.z < i.uv.w * 8.0))
                {
                    float k = floor(i.uv.x * 4.0);
                    float t = _Time.y;
                    float ph = frac(dot(floor(i.wpos.xz * 0.6), float2(0.1371, 0.2713))) * 6.2832;
                    float gl = 0.9 + 0.1 * sin(t * 4.488 + ph);
                    float gt = 0.925 + 0.075 * (0.6 * sin(t * 11.0 + ph) + 0.4 * sin(t * 17.3 + ph * 2.1));
                    float gr = 0.75 + 0.25 * sin(t * 2.856 + ph);
                    float gb = 0.25 + 0.75 * step(0.5, frac(t * 0.8 + ph * 0.159 + i.wpos.x * 0.9));
                    float g = k < 0.5 ? gl : (k < 1.5 ? gt : (k < 2.5 ? gr : gb));
                    col = alb * (0.2 + 1.15 * g);
                }
                MINER_CUT_GLOW(col, i.wpos, i.pos);
                return fixed4(col, 1.0);
            }
            ENDCG
        }
        // sombra que respeta el corte (si no, la estatua a medio hacer proyectaria la sombra entera)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            CGPROGRAM
            #pragma vertex vs
            #pragma fragment fs
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_local __ _ROOFCUT
            #include "UnityCG.cginc"
            #include "MinerCut.cginc"
            float _ClipY;
            struct v2s { V2F_SHADOW_CASTER; float3 wpos : TEXCOORD1; };
            v2s vs(appdata_base v)
            {
                v2s o;
                float3 w = mul(unity_ObjectToWorld, v.vertex).xyz;
            #ifdef _ROOFCUT
                w = MinerCutLift(w);
                v.vertex.xyz = mul(unity_WorldToObject, float4(w, 1.0)).xyz;
            #endif
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                o.wpos = w;
                return o;
            }
            float4 fs(v2s i) : SV_Target
            {
                clip(_ClipY - i.wpos.y);
            #ifdef _ROOFCUT
                clip(MinerDither(i.pos.xy) - MinerCutFade(i.wpos));
            #endif
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
    Fallback "VertexLit"
}
