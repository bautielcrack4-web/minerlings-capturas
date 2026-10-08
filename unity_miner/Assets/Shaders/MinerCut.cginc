#ifndef MINER_CUT_INCLUDED
#define MINER_CUT_INCLUDED

// ---------------------------------------------------------------- techos que se desvanecen (IslandGame.RoofCut)
// Solo con la variante _ROOFCUT (los materiales de los techos del Complejo son copias con esa palabra clave; el resto
// del juego no paga el clip). Al acercar la camara, los techos dentro de un circulo alrededor del centro de la vista
// se levantan un poco y se disuelven con un tramado fino: se ve adentro de las habitaciones.
//   _RoofCut (global): xyz centro en el mundo, w radio.   _RoofCutAmt (global): 0..1 segun el zoom.
//   _CutFull (por renderer, MaterialPropertyBlock): disuelve todo el techo parejo (Modo Cuartel, piso elegido).
//   Paredes (_CutWall = 1 en la copia del material, pedido del dueño: "las paredes tapan lo de atras"): dentro del
//   mismo circulo se disuelve solo la parte de la pared que queda ENTRE la camara y el centro de la vista, por encima
//   de un zocalo de 30 cm sobre _CutBaseY (el piso de esa pared). Las del fondo quedan enteras: corte tipo maqueta.
//   _CutRayO/_CutRayD (globales): rayo del centro de la pantalla; _CutFwd.xz: adelante de la camara (horizontal).
#ifdef _ROOFCUT
float4 _RoofCut;
float _RoofCutAmt;
float _CutFull;
float _CutWall;
float _CutBaseY;
float4 _CutRayO, _CutRayD, _CutFwd;

float MinerCutFade(float3 wpos)
{
    float r = max(_RoofCut.w, 0.01);
    float radial = 1.0 - smoothstep(r * 0.5, r, length(wpos.xz - _RoofCut.xz));
    float f = saturate(max(_CutFull, _RoofCutAmt * radial));
    if (_CutWall > 0.5)
    {
        // centro de la vista a la altura del piso de esta pared
        float t = (_CutBaseY - _CutRayO.y) / (abs(_CutRayD.y) > 1e-4 ? _CutRayD.y : -1e-4);
        float2 c = _CutRayO.xz + _CutRayD.xz * t;
        float d = dot(wpos.xz - c, _CutFwd.xz);
        float nearCam = 1.0 - smoothstep(-0.7, -0.25, d);
        float above = smoothstep(_CutBaseY + 0.28, _CutBaseY + 0.42, wpos.y);
        f = saturate(_RoofCutAmt * radial) * nearCam * above;
    }
    return f;
}

// suba suave del techo mientras se va (curva que arranca rapido y frena); las paredes no suben
float3 MinerCutLift(float3 wpos)
{
    if (_CutWall > 0.5) return wpos;
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
