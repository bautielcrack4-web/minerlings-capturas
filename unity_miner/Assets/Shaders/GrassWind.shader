Shader "Mineros/GrassWind"
{
    // Pasto que se mece: igual que MinerToonVC (color por vertice) pero cada vertice se dobla con el viento segun su
    // altura (la base queda quieta). _IslandWind es global (lo pone IslandAmbient con las rafagas). Una sola malla
    // para todos los mechones de los bordes de los caminos.
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
            #include "MinerCommon.cginc"

            fixed4 _Color;
            fixed4 _EmissionColor;
            half _Rim;
            half _Floor;
            half _Spec;
            half _Gloss;
            float _IslandWind;

            struct AppVC
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
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
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float h = saturate(v.vertex.y / 0.45);
                float w = (0.35 + _IslandWind) * h * h;
                float ph = _Time.y * 1.7 + wp.x * 0.7 + wp.z * 0.45;
                v.vertex.x += sin(ph) * 0.09 * w;
                v.vertex.z += cos(ph * 0.83) * 0.06 * w;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wnrm = UnityObjectToWorldNormal(v.normal);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
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
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback "VertexLit"
}
