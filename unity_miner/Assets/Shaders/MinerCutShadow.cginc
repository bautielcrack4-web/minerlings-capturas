#ifndef MINER_CUT_SHADOW_INCLUDED
#define MINER_CUT_SHADOW_INCLUDED

// Pase de sombra de los toon: igual al de siempre, pero en la variante _ROOFCUT el techo que se disuelve tambien deja
// de dar sombra adentro de la habitacion (con el mismo tramado y la misma suba).
#include "UnityCG.cginc"
#include "MinerCut.cginc"

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
#ifdef _ROOFCUT
    clip(MinerDither(i.pos.xy) - MinerCutFade(i.wpos));
#endif
    SHADOW_CASTER_FRAGMENT(i)
}

#endif
