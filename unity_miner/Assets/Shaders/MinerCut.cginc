#ifndef MINER_CUT_INCLUDED
#define MINER_CUT_INCLUDED

// ---------------------------------------------------------------- techos que se desvanecen (IslandGame.RoofCut)
// Solo con la variante _ROOFCUT (los materiales de los techos del Complejo son copias con esa palabra clave; el resto
// del juego no paga el clip). Al acercar la camara, los techos dentro de un circulo alrededor del centro de la vista
// se levantan un poco y se disuelven con un tramado fino: se ve adentro de las habitaciones.
//   _RoofCut (global): xyz centro en el mundo, w radio.   _RoofCutAmt (global): 0..1 segun el zoom.
//   _CutFull (por renderer, MaterialPropertyBlock): disuelve todo el techo parejo (Modo Cuartel, piso elegido).
#ifdef _ROOFCUT
float4 _RoofCut;
float _RoofCutAmt;
float _CutFull;

float MinerCutFade(float3 wpos)
{
    float r = max(_RoofCut.w, 0.01);
    float radial = 1.0 - smoothstep(r * 0.5, r, length(wpos.xz - _RoofCut.xz));
    return saturate(max(_CutFull, _RoofCutAmt * radial));
}

// suba suave del techo mientras se va (curva que arranca rapido y frena)
float3 MinerCutLift(float3 wpos)
{
    float f = MinerCutFade(wpos);
    wpos.y += (1.0 - (1.0 - f) * (1.0 - f)) * 0.32;
    return wpos;
}

// tramado de ruido gradiente entrelazado (Jimenez): sin el patron cuadriculado del Bayer
float MinerDither(float2 pix)
{
    return frac(52.9829189 * frac(dot(pix, float2(0.06711056, 0.00583715))));
}
#endif

#endif
