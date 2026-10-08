# Minero en Blender: plan y checklist

Objetivo: el minero de la hoja `ref/miner_ref.webp` (pose T, 4 vistas, detalles) en 3D, igual a la referencia, y usarlo
en el juego Unity con la animacion procedural que ya existe (`MinerModel`).

## Como se trabaja
- `blender -b -P build_miner.py -- --out out --views front,left,back,face` arma el modelo por codigo y renderiza
  (Cycles, camara 85 mm a la altura del pecho como la referencia, fondo transparente).
- `python3 compare.py ref/front.png out/front.png out/cmp_front.png` -> silueta IoU, diferencia de color y mapa
  (verde = falta, rojo = sobra). `python3 measure.py` mide anchos fila por fila contra la referencia.
- `... --export ../../unity_miner/Assets/Resources/Miner/miner.json` exporta las piezas a Unity (ver export_miner.py).

## Historial de iteraciones (vista de frente)
| Iteracion | IoU silueta | Dif. color | Cambio principal |
|---|---|---|---|
| 1 | 0.26 | 73 | primer armado por piezas (fondo gris rompia la medida) |
| 2 | 0.77 | 78 | fondo transparente sobre blanco, ala inclinada, guantes |
| 3 | 0.85 | 44 | exposicion, torso/piernas/botas a medida |
| 5 | 0.86 | 40 | casco mas bajo, piernas en A, solapas |
| 8-9 | 0.86 | 37 | camara en perspectiva (arcos del casco), medidas fila por fila |
| 12-16 | 0.87 | 33 | casco unido al ala, cara (cejas, orejas, patillas), cuello en V |

## Checklist
- [x] Proporciones y silueta de frente (IoU 0.87).
- [x] Paleta muestreada de la referencia (amarillo, marron, grises, piel).
- [x] Casco: domo, banda que sigue la curva, ala caida a los costados, lampara con lente emisiva.
- [x] Cara: ojos grandes con iris/pupila/brillo, parpado, cejas gruesas, nariz, sonrisa, orejas, patillas.
- [x] Camisa con cuello en V y remera oscura, mangas arremangadas, tiradores, cinturon con hebilla, dos bolsas.
- [x] Pantalon holgado con rodilleras, botas con cuello acolchado y suela.
- [x] Mochila (espalda) y pico aparte.
- [x] Export a Unity por piezas (cadera, torso, cabeza, brazos, piernas) con pivotes, colores por vertice y canales
      (piel, pelo, casco, lente); ~11k triangulos por minero; animacion procedural existente funciona.
- [ ] Guantes: en la referencia los dedos van juntos y curvados; aca se ven mas abiertos.
- [ ] Pliegues de tela (camisa, pantalon), bolsillo y costuras: no estan modelados.
- [ ] Rostro: falta el sombreado de mejillas/labios y la forma exacta de la mandibula.
- [ ] Indistinguible de la referencia: NO. La referencia es un render esculpido; con primitivas por codigo el limite
      practico esta en "mismo personaje, misma paleta y proporciones". Para acercarse mas: generar con Tripo
      (`tripo_generate.py`, multivista, necesita creditos) y segmentar ese modelo por piezas con el mismo export.

## Modelo de Tripo (en uso en el juego)
- Generado con `tripo_generate.py` (API v3, multivista frente/izq/espalda/der, v3.1, textura detallada): 40 creditos.
  El GLB original y la vista previa quedan en `tripo/` (minero.glb, preview.png).
- Silueta de frente IoU 0.870, diferencia de color 32 (`tripo/cmp_front.png`); el modelo propio llegaba a 0.865/33,
  pero el de Tripo tiene cara, pliegues, guantes y botas como la referencia.
- `blender -b -P tripo_import.py -- tripo/minero.glb --yaw 180 --tris 14000 --export ../../unity_miner/Assets/Resources/Miner/miner.json`
  normaliza (alto 1, mirando a la camara), segmenta por zonas para el esqueleto del juego, diezma a 14k triangulos y
  pasa la textura a colores por vertice (en lineal).
- [ ] Grietas finas en las uniones de cadera/piernas y hombros cuando se mueven (las piezas estan cortadas, no pesadas):
      solucion completa = rig de Tripo (Auto Rig) o pesos suaves en Unity (SkinnedMeshRenderer).

## Edificios de la Isla Minera (Tripo P1, texto a modelo, 40 creditos cada uno)
- `tripo_text.py <nombre> "<prompt>"` genera (estilo fijo: low-poly, isla, colores alegres). Fuentes en `tripo/edificios/`.
- `blender -b -P tripo_building.py -- tripo/edificios/casa.glb --name house --size 3.0` normaliza y exporta a
  `unity_miner/Assets/Resources/Island/<nombre>.json + .png` (malla con UV + textura 1024; shader `MinerToonTex`).
- Casa, Deposito, Cantina, Duchas y Herreria hechos (~2.6k triangulos cada uno). Saldo restante: 60 creditos.
