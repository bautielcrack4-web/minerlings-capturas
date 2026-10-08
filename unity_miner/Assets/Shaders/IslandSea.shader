Shader "Mineros/IslandSea"
{
    // Mar de la isla (Built-in, ForwardBase). Olas por vertice, color por distancia a la costa (turquesa en la orilla,
    // azul profundo afuera), arena que se ve bajo el agua, causticas, espuma que sigue la forma real de la costa
    // (misma formula que IslandArt.EdgeR) con anillos que salen hacia afuera, destellos del sol y sombras de nubes.
    Properties
    {
        _Shallow ("Orilla", Color) = (0.30, 0.82, 0.90, 1)
        _Deep ("Profundo", Color) = (0.06, 0.40, 0.72, 1)
        _Sand ("Arena bajo el agua", Color) = (0.95, 0.88, 0.62, 1)
        _FoamCol ("Espuma", Color) = (1, 1, 1, 1)
        _ShoreR ("Radio de la isla", Float) = 11
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            fixed4 _Shallow, _Deep, _Sand, _FoamCol;
            float _ShoreR;

            struct appdata { float4 vertex : POSITION; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wpos : TEXCOORD0;
                float3 wnrm : TEXCOORD1;
                SHADOW_COORDS(2)
            };

            float Waves(float2 p, float t)
            {
                return sin(p.x * 0.35 + t * 1.1) * 0.07 + sin(p.y * 0.42 - t * 0.9) * 0.06 + sin((p.x + p.y) * 0.9 + t * 2.1) * 0.025;
            }

            v2f vert(appdata v)
            {
                v2f o;
                float t = _Time.y;
                float3 w = mul(unity_ObjectToWorld, v.vertex).xyz;
                float h = Waves(w.xz, t);
                float hx = Waves(w.xz + float2(0.3, 0), t) - h;
                float hz = Waves(w.xz + float2(0, 0.3), t) - h;
                v.vertex.y += h;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.wnrm = normalize(float3(-hx / 0.3, 1, -hz / 0.3));
                TRANSFER_SHADOW(o)
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            // borde de la arena: EdgeR(a + 0.4, radio + 2) de IslandArt
            float Edge(float a)
            {
                float r = _ShoreR + 2.0;
                float b = a + 0.4;
                return r * (1 + 0.045 * sin(b * 3 + 0.7) + 0.03 * sin(b * 5 + 2.1) + 0.02 * sin(b * 9)) + 0.15;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y;
                float2 p = i.wpos.xz;
                float a = atan2(p.y, p.x);
                float d = length(p) - Edge(a);                     // metros desde la costa (afuera > 0)
                float k = saturate(d / 7.0);
                half3 col = lerp(_Shallow.rgb, _Deep.rgb, smoothstep(0, 1, k));
                // arena que se trasluce en la orilla
                col = lerp(_Sand.rgb, col, saturate(d / 1.6 + 0.35));
                // causticas suaves en lo bajo
                float c1 = Noise(p * 0.9 + float2(t * 0.35, t * 0.2));
                float c2 = Noise(p * 1.3 - float2(t * 0.25, -t * 0.3));
                float caust = pow(saturate(1 - abs(c1 - c2) * 3.2), 6);
                col += caust * 0.12 * (1 - smoothstep(0, 5, d));
                // espuma: linea en la costa que respira + anillos que salen hacia afuera, cortados por ruido
                float n = Noise(p * 1.7 + t * 0.4);
                float shoreLine = 1 - smoothstep(0.0, 0.45 + 0.2 * sin(t * 1.4 + a * 4), d);
                float ring = frac(d * 0.45 - t * 0.28);
                float rings = smoothstep(0.1, 0.0, abs(ring - 0.5) - 0.05) * (1 - smoothstep(0.5, 4.5, d)) * step(0.45, n);
                float foam = saturate(shoreLine * step(0.25, n + shoreLine * 0.6) + rings * 0.8);
                col = lerp(col, _FoamCol.rgb, foam * 0.92);
                // luz: sombras de nubes y destellos del sol
                half atten = SHADOW_ATTENUATION(i);
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float3 V = normalize(_WorldSpaceCameraPos - i.wpos);
                float3 nrm = normalize(i.wnrm + float3(Noise(p * 2.0 + t) - 0.5, 0, Noise(p * 2.0 - t) - 0.5) * 0.35);
                float3 H = normalize(L + V);
                float spec = pow(saturate(dot(nrm, H)), 400) * 1.2;
                float twinkle = step(0.992, Noise(p * 6.0 + float2(t * 2.3, -t * 1.7))) * (1 - foam) * 0.9;
                col *= lerp(0.7, 1.0, atten);
                col += (spec + twinkle) * _LightColor0.rgb * atten;
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    Fallback "VertexLit"
}
