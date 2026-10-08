Shader "Mineros/MinerVest"
{
    // Torso: camisa, chaleco reflectivo abierto adelante con dos franjas y cinturon (por espacio de objeto).
    // La malla es una capsula de radio 0.27 y alto 0.72 centrada; el frente es +Z.
    Properties
    {
        _Shirt ("Camisa", Color) = (0.3,0.33,0.39,1)
        _Vest ("Chaleco", Color) = (1,0.54,0.16,1)
        _Stripe ("Franja", Color) = (1,0.96,0.75,1)
        _Belt ("Cinturon", Color) = (0.29,0.2,0.15,1)
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

            fixed4 _Shirt;
            fixed4 _Vest;
            fixed4 _Stripe;
            fixed4 _Belt;
            half _Rim;
            half _Floor;

            fixed4 frag(MinerV2F i) : SV_Target
            {
                float3 c = _Shirt.rgb;
                float ang = atan2(i.opos.x, i.opos.z);
                float openFront = step(abs(ang), 0.32);
                float y = i.opos.y;
                float isVest = (1.0 - openFront) * step(-0.2, y) * step(y, 0.24);
                c = lerp(c, _Vest.rgb, isVest);
                float s = (step(abs(y - 0.02), 0.025) + step(abs(y + 0.09), 0.025)) * isVest;
                c = lerp(c, _Stripe.rgb, saturate(s));
                float b = step(y, -0.2) * step(-0.27, y);
                c = lerp(c, _Belt.rgb, b);
                float buckle = b * step(abs(ang), 0.22);
                c = lerp(c, MINER_C(float3(0.86, 0.7, 0.3)), buckle);
                half3 col = MinerShade(i, c, _Rim, _Floor, half3(0, 0, 0), 0, 1);
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback "VertexLit"
}
