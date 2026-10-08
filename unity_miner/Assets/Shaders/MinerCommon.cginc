#ifndef MINER_COMMON_INCLUDED
#define MINER_COMMON_INCLUDED

// Utilidades compartidas por los shaders del minero (pipeline Built-in, pase ForwardBase).
// Sombreado juguete: luz envolvente con piso de sombra, luz ambiente minima y borde iluminado.

#include "UnityCG.cginc"
#include "Lighting.cginc"
#include "AutoLight.cginc"

// Convierte constantes sRGB escritas en el shader al espacio de color del proyecto.
#ifdef UNITY_COLORSPACE_GAMMA
    #define MINER_C(x) (x)
#else
    #define MINER_C(x) GammaToLinearSpace(x)
#endif


#include "MinerCut.cginc"

struct MinerAppData
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct MinerV2F
{
    float4 pos : SV_POSITION;
    float3 wnrm : TEXCOORD0;
    float3 wpos : TEXCOORD1;
    float3 opos : TEXCOORD2;
    SHADOW_COORDS(3)
};

MinerV2F MinerVert(MinerAppData v)
{
    MinerV2F o;
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
    return o;
}

// albedo ya en el espacio de color de trabajo. emis se suma sin iluminar.
// spec: intensidad del brillo especular (0 = sin brillo).
half3 MinerShade(MinerV2F i, half3 albedo, half rimAmount, half shadowFloor, half3 emis, half spec, half gloss)
{
    float3 n = normalize(i.wnrm);
    float3 v = normalize(_WorldSpaceCameraPos - i.wpos);
    float3 L = normalize(_WorldSpaceLightPos0.xyz - i.wpos * _WorldSpaceLightPos0.w);
    half atten = SHADOW_ATTENUATION(i);

    float d = dot(n, L);
    half t = smoothstep(-0.3, 0.55, d);
    half3 lit = lerp(shadowFloor, 1.0h, t) * _LightColor0.rgb * atten;
    half3 amb = max(ShadeSH9(half4(n, 1.0)), half3(0.3h, 0.3h, 0.3h));
    half3 col = albedo * (amb * 0.6h + lit);

    half r = 1.0h - saturate(dot(n, v));
    col += albedo * pow(r, 3.0h) * rimAmount;

    if (spec > 0.0h)
    {
        float3 h = normalize(L + v);
        half s = pow(saturate(dot(n, h)), gloss) * spec * smoothstep(0.0, 0.3, d);
        col += _LightColor0.rgb * atten * s;
    }
    return col + emis;
}

#ifdef _ROOFCUT
// fragmento: descarta segun el tramado y devuelve cuanto brillo calido sumar en el borde de la disolucion
half MinerCutClip(float3 wpos, float4 spos)
{
    float f = MinerCutFade(wpos);
    clip(MinerDither(spos.xy) - f);
    return (half)(f * (1.0 - f) * 4.0);
}
#define MINER_CUT_GLOW(col, wpos, spos) col += MinerCutClip(wpos, spos) * half3(0.16, 0.11, 0.05)
#else
#define MINER_CUT_GLOW(col, wpos, spos)
#endif

#endif
