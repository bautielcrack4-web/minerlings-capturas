Shader "Hidden/Mineros/IslandPost"
{
    // Posprocesado de la isla (Built-in, OnRenderImage): bloom suave, desenfoque de maqueta arriba y abajo, color
    // "caramelo" (saturacion, calidez, contraste) y viñeta. La interfaz va encima (Overlay) y no se toca.
    Properties { _MainTex ("", 2D) = "white" {} }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex, _Bloom, _Blur;
    float4 _MainTex_TexelSize;
    half _Threshold, _BloomInt, _Sat, _Warm, _Contrast, _Vignette, _Tilt;
    half3 _Tint;   // tinte de la noche (las luces fuertes lo esquivan: faroles y ventanas siguen calidos)

    half3 Box4(float2 uv, float s)
    {
        float4 o = _MainTex_TexelSize.xyxy * float4(-1, -1, 1, 1) * s;
        return (tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb + tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb) * 0.25;
    }

    half4 FragPrefilter(v2f_img i) : SV_Target
    {
        half3 c = Box4(i.uv, 1);
        half br = max(c.r, max(c.g, c.b));
        half soft = saturate((br - _Threshold + 0.1) / 0.2);
        half contrib = max(soft * soft * 0.2, br - _Threshold) / max(br, 1e-4);
        return half4(c * saturate(contrib), 1);
    }
    half4 FragDown(v2f_img i) : SV_Target { return half4(Box4(i.uv, 1), 1); }
    half4 FragUp(v2f_img i) : SV_Target { return half4(Box4(i.uv, 0.5), 1); }

    half4 FragFinal(v2f_img i) : SV_Target
    {
        half3 c = tex2D(_MainTex, i.uv).rgb;
        // desenfoque de maqueta: arriba y abajo de la pantalla
        float ty = abs(i.uv.y - 0.47) * 2.0;
        half tilt = smoothstep(0.55, 1.0, ty) * _Tilt;
        c = lerp(c, tex2D(_Blur, i.uv).rgb, tilt);
        c += tex2D(_Bloom, i.uv).rgb * _BloomInt;
        // color en espacio gamma (aprox) para que los ajustes se sientan como en un editor de fotos
        half3 g = sqrt(saturate(c));
        half l = dot(g, half3(0.299, 0.587, 0.114));
        g = lerp(l.xxx, g, _Sat);
        g *= half3(1.0 + _Warm, 1.0 + _Warm * 0.35, 1.0 - _Warm);
        g = (g - 0.5) * _Contrast + 0.5;
        g *= lerp(_Tint, half3(1, 1, 1), smoothstep(0.62, 0.92, l));
        // viñeta tibia
        float2 v = (i.uv - 0.5) * float2(1.0, 0.75);
        half vig = 1.0 - saturate(dot(v, v) * 2.2) * _Vignette;
        g *= lerp(half3(0.85, 0.75, 0.8), half3(1, 1, 1), vig);
        g = saturate(g);
        return half4(g * g, 1);
    }
    ENDCG
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragPrefilter
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragDown
            ENDCG }
        Pass { Blend One One
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragUp
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragFinal
            ENDCG }
    }
}
