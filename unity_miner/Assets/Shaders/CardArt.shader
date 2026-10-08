Shader "Mineros/CardArt"
{
    // Arte de las cartas de mineros (IslandUi.Cards): esquinas redondeadas, vineta suave, un barrido de luz que cruza la
    // carta (o sigue la inclinacion mientras se arrastra), brillo holografico en las epicas y legendarias y paralaje del
    // dibujo dentro de la ventana. Todo en el fragmento: sin mascara de UI ni texturas extra.
    Properties
    {
        [PerRendererData] _MainTex ("Arte", 2D) = "white" {}
        _Color ("Tinte", Color) = (1,1,1,1)
        _Shine ("Barrido de luz (posicion)", Float) = -1
        _Tilt ("Inclinacion (xy) y paralaje (zw)", Vector) = (0,0,0,0)
        _Foil ("Holografico", Range(0,1)) = 0
        _Round ("Radio de las esquinas (fraccion del ancho)", Float) = 0.06
        _Aspect ("Alto / ancho", Float) = 1.5
        _Glow ("Destello (0..1)", Float) = 0
        _Zoom ("Zoom del dibujo (respira)", Float) = 0.92
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            float _Shine, _Foil, _Round, _Aspect, _Glow, _Zoom;
            float4 _Tilt;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                // ventana redondeada (en unidades del ancho)
                float2 p = (uv - 0.5) * float2(1.0, _Aspect);
                float2 hb = float2(0.5, 0.5 * _Aspect) - _Round;
                float2 q = abs(p) - hb;
                float d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - _Round;
                float mask = saturate(0.5 - d * 300.0);
                // paralaje: el dibujo se corre un poco al reves de la inclinacion (profundidad detras del vidrio)
                float2 suv = (uv - 0.5) * _Zoom + 0.5 + _Tilt.zw;
                fixed4 col = tex2D(_MainTex, suv) * i.color;
                // vineta suave
                float2 e = uv - 0.5;
                col.rgb *= 1.0 - dot(e, e) * 0.55;
                // barrido de luz en diagonal (angosto y brillante + ancho y suave)
                float s = uv.x * 0.65 + uv.y * 0.35 - _Shine + _Tilt.x * 0.35;
                float band = exp(-s * s / 0.0025) * 0.45 + exp(-s * s / 0.06) * 0.1;
                col.rgb += band;
                // holografico: arcoiris que se mueve con la inclinacion, solo sobre las luces del dibujo
                float h = frac(uv.x * 0.8 + uv.y * 0.6 + _Tilt.x * 0.9 + _Tilt.y * 0.6);
                float3 rb = saturate(abs(frac(h + float3(0.0, 0.333, 0.667)) * 6.0 - 3.0) - 1.0);
                float lum = dot(col.rgb, float3(0.3, 0.59, 0.11));
                col.rgb += rb * _Foil * 0.22 * smoothstep(0.35, 0.9, lum);
                // destello completo (al caer, al elegir)
                col.rgb = lerp(col.rgb, float3(1, 1, 1), saturate(_Glow) * 0.85);
                col.a *= mask;
                return col;
            }
            ENDCG
        }
    }
}
