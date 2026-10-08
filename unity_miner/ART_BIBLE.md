# Biblia de arte: un solo estilo para todo Mineros Idle (Unity)

Regla madre: **todo lo que se ve en el juego sale del mismo sistema**: mismo shader, misma paleta, mismas proporciones
y mismo lenguaje de formas que el minero. Si un asset no cumple esto, no entra.

## 1. Estilo
- "Juguete de vinilo": formas redondeadas y gorditas, bordes suaves, facetado leve (nunca polígonos afilados agresivos).
- Sin texturas fotográficas ni pintadas: el color viene del material (y a lo sumo color de vértice para 3 tonos).
- Sin contornos negros en el mundo 3D. El volumen lo dan la luz envolvente y el borde iluminado.
- Proporciones chibi: el minero mide ~1.2 unidades con la cabeza de ~45%. Rocas medianas ~0.8-1.0 de ancho,
  árboles ~2.5-3, utilería a escala del minero (una vagoneta le llega a la cintura).

## 2. Shader único
Todo el 3D usa `Mineros/MinerToon` (`Assets/Shaders/MinerToon.shader` + `MinerCommon.cginc`). La cara del minero y el
chaleco son variantes del mismo modelo de luz (`MinerFace`, `MinerVest`). Nada de Standard salvo como respaldo.
- Luz envolvente: `smoothstep(-0.3, 0.55, N·L)` entre `_Floor` (0.42) y 1.
- Ambiente: armónicos esféricos con mínimo 0.3, al 60%.
- Borde iluminado: `pow(1 - N·V, 3) * _Rim` (0.22).
- Brillo especular (`_Spec` 0.3-0.6) sólo en cristales, gemas, metal, casco y lava.
- Emisión sólo en: lámparas, lava, cristales de la cueva, rocas ricas (destello), efectos.

## 3. Paleta
Una paleta por bioma (portada de `miner_idle/scripts/art.gd` → `Art.BIOMES`): suelo, manchas, decoración, pared, pared
oscura, meseta, labio, suelo bajo, roca (base/oscura/clara), mineral, accesorio principal y secundario, motas.
- Cada material usa **3 tonos** de la paleta (sombra, base, luz) y nada fuera de ella.
- Colores de rango de herramientas: D gris acero, C verde, B azul, A violeta, S dorado, SS rojo.
- Oro siempre `#FFCC33` / `#D9961C`; gemas `#4FC3F7` / `#1F7FC4`.

## 4. Luz y cámara
- Una luz direccional cálida desde arriba-izquierda (mismo ángulo en todos los biomas), sombras suaves.
- Cámara ortográfica isométrica fija; el cañón sube en diagonal.
- Cueva y volcán: ambiente más oscuro y luces puntuales (lámparas, cristales, lava), con tope de luces para móvil.

## 5. Efectos (nivel "Cartoon FX", propios)
- Partículas con texturas generadas por código (círculo suave, estrella de 4 puntas, chispa alargada, nube de polvo,
  anillo). Colores sólo de la paleta del bioma + oro + blanco cálido.
- Formas redondeadas y gordas, vida corta (0.15-0.8 s), escala con rebote, desvanecido suave.
- Un solo foco grande por vez (ver la regla de celebraciones del MEGA_PROMPT).

## 6. Interfaz
- Sprites 9-slice del Kenney UI Pack (CC0) ya incluidos, con la paleta del juego; tipografías Lilita One (títulos y
  números) y Fredoka (textos). Íconos propios en el mismo trazo redondeado.

## 7. Lo que NO entra
- Modelos o efectos de terceros (tiendas, CC0, etc.) salvo que se rehagan con este shader y esta paleta hasta ser
  indistinguibles del resto. Por defecto: no.
- Personajes, logos o arte que imiten juegos existentes.
