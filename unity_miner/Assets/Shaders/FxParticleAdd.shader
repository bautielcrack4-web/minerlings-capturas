Shader "Mineros/FxParticleAdd"
{
    // Particula aditiva simple (pipeline Built-in): textura * color de vertice. Para destellos, estrellas, anillos, brasas.
    Properties
    {
        _MainTex ("Textura", 2D) = "white" {}
        _Color ("Tinte (por MaterialPropertyBlock)", Color) = (1, 1, 1, 1)
        _Intensity ("Intensidad", Range(0, 4)) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Prueba de profundidad", Float) = 4
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend SrcAlpha One
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [_ZTest]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            half _Intensity;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                c.rgb *= _Intensity;
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
