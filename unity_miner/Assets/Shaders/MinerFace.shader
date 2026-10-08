Shader "Mineros/MinerFace"
{
    // Cabeza con cara procedural (ojos grandes con brillos, cejas, sonrisa con dientes y lengua, rubor).
    // Se dibuja en espacio de objeto sobre una esfera; la cara mira hacia +Z.
    Properties
    {
        _Skin ("Piel", Color) = (0.97,0.8,0.66,1)
        _Brow ("Ceja", Color) = (0.3,0.19,0.12,1)
        _Blink ("Parpadeo", Range(0,1)) = 0
        _Rim ("Borde iluminado", Range(0,1)) = 0.2
        _Floor ("Piso de sombra", Range(0,1)) = 0.42
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
            #pragma vertex MinerVert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "MinerCommon.cginc"

            fixed4 _Skin;
            fixed4 _Brow;
            half _Blink;
            half _Rim;
            half _Floor;

            float Ell(float2 p, float2 c, float2 r)
            {
                float2 q = (p - c) / r;
                return length(q) - 1.0;
            }

            float Cover(float sd, float aa)
            {
                return 1.0 - smoothstep(-aa, aa, sd);
            }

            fixed4 frag(MinerV2F i) : SV_Target
            {
                float3 n = normalize(i.opos);
                float2 p = float2(atan2(n.x, n.z), asin(clamp(n.y, -1.0, 1.0)));
                // el ancho de pixel se limita para que la costura de atan2 (en la nuca) no emborrone detalles
                float aa = min(max(fwidth(p.x), fwidth(p.y)) * 1.2, 0.05);
                aa = max(aa, 0.0015);

                float3 col = _Skin.rgb;
                // rubor
                float bl = max(1.0 - length((p - float2(-0.47, -0.16)) / float2(0.13, 0.08)), 0.0)
                         + max(1.0 - length((p - float2(0.47, -0.16)) / float2(0.13, 0.08)), 0.0);
                col = lerp(col, MINER_C(float3(1.0, 0.5, 0.48)), saturate(bl) * 0.45);

                // ojos y cejas
                float3 eyeCol = MINER_C(float3(0.1, 0.07, 0.06));
                [unroll]
                for (int k = 0; k < 2; k++)
                {
                    float sx = (k == 0) ? -1.0 : 1.0;
                    float2 c = float2(0.27 * sx, 0.06);
                    if (_Blink > 0.5)
                    {
                        float d = abs(Ell(p, c + float2(0.0, -0.04), float2(0.12, 0.07))) - 0.016;
                        float m = Cover(d, aa) * step(c.y - 0.03, p.y);
                        col = lerp(col, eyeCol, m);
                    }
                    else
                    {
                        float e = Cover(Ell(p, c, float2(0.12, 0.175)), aa);
                        col = lerp(col, eyeCol, e);
                        float h1 = Cover(Ell(p, c + float2(-0.035, 0.065), float2(0.048, 0.055)), aa);
                        float h2 = Cover(Ell(p, c + float2(0.04, -0.07), float2(0.022, 0.025)), aa);
                        col = lerp(col, float3(1.0, 1.0, 1.0), max(h1, h2 * 0.9));
                    }
                    float2 bc = c + float2(0.0, 0.25);
                    float bd = abs(Ell(p, bc + float2(0.0, -0.06), float2(0.11, 0.07))) - 0.016;
                    float bm = Cover(bd, aa) * step(bc.y - 0.02, p.y);
                    col = lerp(col, _Brow.rgb, bm);
                }

                // sonrisa amplia con dientes y lengua
                float2 mc = float2(0.0, -0.2);
                float mouth = Cover(Ell(p, mc, float2(0.3, 0.2)), aa) * step(p.y, mc.y);
                float3 mcol = MINER_C(float3(0.42, 0.1, 0.1));
                float teeth = step(mc.y - 0.06, p.y);
                mcol = lerp(mcol, MINER_C(float3(0.98, 0.97, 0.94)), teeth);
                float tongue = Cover(Ell(p, mc + float2(0.04, -0.15), float2(0.12, 0.06)), aa);
                mcol = lerp(mcol, MINER_C(float3(0.93, 0.45, 0.45)), tongue * (1.0 - teeth));
                col = lerp(col, mcol, mouth);
                float lip = abs(Ell(p, mc, float2(0.3, 0.2))) - 0.009;
                col = lerp(col, MINER_C(float3(0.35, 0.12, 0.1)), Cover(lip, aa) * step(p.y, mc.y) * 0.8);

                half3 outc = MinerShade(i, col, _Rim, _Floor, half3(0, 0, 0), 0, 1);
                return fixed4(outc, 1.0);
            }
            ENDCG
        }
    }
    Fallback "VertexLit"
}
