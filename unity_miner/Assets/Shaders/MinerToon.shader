Shader "Mineros/MinerToon"
{
    // Sombreado suave tipo juguete: luz envolvente con transicion amplia, piso de sombra y borde iluminado.
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
            #pragma vertex MinerVert
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

            fixed4 frag(MinerV2F i) : SV_Target
            {
                half3 col = MinerShade(i, _Color.rgb, _Rim, _Floor, _EmissionColor.rgb, _Spec, _Gloss);
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
