# Música, ambientes y pasos de Curio Barn (sintetizados)

Todo se genera por síntesis procedural en Python (numpy + scipy) y se codifica a OGG Vorbis con ffmpeg.
No se usa ningún archivo de terceros, así que no hay licencias que registrar (no hace falta `SOURCES.md`).
La salida es determinística: los mismos scripts dan los mismos archivos.

## Regenerar (un comando)

```bash
python3 unity_tools/music/build_all.py
```

Escribe en `unity_rarezas/Assets/Resources/Audio/`, crea el `.meta` de Unity de cada archivo nuevo si falta y al final
imprime la verificación. Tarda alrededor de 1 minuto con 4 núcleos.

Variantes:

```bash
python3 unity_tools/music/build_all.py music_pico amb_sea step_wood   # solo esos (step_wood = sus 3 variantes)
python3 unity_tools/music/build_all.py --out /tmp/prueba --no-meta     # a otra carpeta, sin .meta
python3 unity_tools/music/verify.py                                    # solo verificar lo que ya está en Audio/
```

Requisitos: `python3` con numpy 2.x y scipy, y `ffmpeg`/`ffprobe` con libvorbis (por defecto `/usr/bin/ffmpeg`;
se puede cambiar con la variable de entorno `FFMPEG`).

## Qué genera

| Archivo | Zona | Contenido | Formato |
|---|---|---|---|
| `music_rancho` | rancho y galpón | guitarra country fingerpicking (Travis), guitarra solista y violín, sol mayor, 96 bpm | estéreo, q3 |
| `music_tienda` | tienda | vals musette 3/4 con acordeón, bajo pizzicato y celesta, re menor / fa mayor, 152 bpm | estéreo, q3 |
| `music_galeria` | galería | trío de jazz suave: piano, contrabajo caminando y escobillas, con swing y ii–V–I en fa mayor | estéreo, q3 |
| `music_museo` | museo | cuarteto de cuerdas, re mayor, 64 bpm | estéreo, q3 |
| `music_lujo` | museo de lujo | orquesta chica: arpa en arpegios, cuerdas, flauta y clarinete, cornos y timbal suave, mi bemol mayor | estéreo, q3 |
| `music_bosque` | bosque | flauta sobre punteo de nylon y pad | estéreo, q3 |
| `music_playa` | playa | rasgueo de ukelele, ukelele solista, silbido, shaker y conga | estéreo, q3 |
| `music_mazmorra` | mazmorra | órgano de castillo embrujado en plan gracioso, tuba staccato, xilófono y tic-tac de woodblock | estéreo, q3 |
| `music_jungla` | jungla | congas en tumbao, shaker, clave y un gancho de marimba | estéreo, q3 |
| `music_pico` | pico | glockenspiel, arpegio de celesta, pad aireado y sub-bajo, en mi mayor lidio | estéreo, q3 |
| `amb_dungeon` | mazmorra | gotas con eco y retumbo lejano | mono, q2 |
| `amb_jungle` | jungla | pájaros, insectos, hojas y alguna rana | mono, q2 |
| `amb_peak` | pico | viento helado con silbidos y crepitar de lava lejana | mono, q2 |
| `amb_sea` | playa | olas suaves y espuma | mono, q2 |
| `step_{dirt,wood,marble,sand,stone,snow}_{0,1,2}` | pasos | talón + punta con la textura de cada suelo | mono, q4 |

Niveles: la música queda en RMS −20 dBFS y los ambientes en −26 dBFS. En ambos casos los picos quedan por debajo de
−1.5 dBFS antes de codificar, y el limitador es circular. Los pasos tienen un RMS activo de −31 dBFS, igual que los
`step_N` existentes.

## Archivos

- `engine.py`: DSP vectorizado (filtros, reverb por convolución con IR sintética, ruido y envolventes periódicas,
  limitador look-ahead), notación (`'E5:1 D5:.5 r:1'`), acordes y el mezclador `Song` con loop sin costura, más la
  E/S con ffmpeg.
- `instruments.py`: los modelos de instrumento.
  - Karplus-Strong para guitarra, nylon, ukelele, arpa y contrabajo.
  - Piano aditivo con inarmonicidad, dos cuerdas y martillo.
  - Cuerdas frotadas con vibrato y ensamble.
  - Acordeón musette.
  - Órgano de drawbars con leslie.
  - Flauta con soplido, clarinete, corno y tuba.
  - Marimba, xilófono, celesta, glockenspiel y timbal modales.
  - Pads.
  - Bombo, escobillas, shaker, congas y woodblock.
- `songs.py`: los 10 temas, con progresión, melodía (motivo y variación) y arreglo.
- `sfx.py`: los ambientes y los pasos.
- `build_all.py`: genera todo en paralelo, escribe los `.meta` y verifica.
- `verify.py`: decodifica cada OGG y reporta duración, canales, tamaño, RMS, pico, centroide espectral (un proxy de
  brillo) y la costura de los loops.

## Cómo se logra el loop sin costura

Cada tema se renderiza con varios segundos de cola: la reverb y las notas que siguen sonando. Esa cola se pliega
sobre el principio con una suma módulo L, así que lo que suena al final del loop sigue sonando al volver a empezar.

Todo lo que es lineal (EQ, reverb) se aplica antes del pliegue. Lo que no es lineal, el limitador, trabaja en modo
circular. Además, las progresiones terminan en dominante (V7) para que el loop también cierre en lo armónico.

En los ambientes, las camas de ruido son ruido periódico, generado por FFT con un período exactamente igual al largo
del loop. Se modulan con envolventes también periódicas, y los eventos (gotas, pájaros, crujidos) se pliegan igual
que en la música.

`verify.py` revisa la costura así:

- Compara el nivel de los últimos 20 ms contra los primeros 20 ms. Ese salto no puede ser mayor que el salto más
  grande entre ventanas de 20 ms que ya hay dentro del tema, porque en la música el compás 1 arranca con un ataque.
- Mide el salto de muestra en el empalme y el error de predicción lineal, relativos al percentil 99 de la señal.
  Un valor < 1 indica que no hay click.

## Uso en el juego

`Game/Ambience.cs` ya pide estos nombres: `StageMusic`, la música de cada zona, `amb_*` y `step_*` como banco con
variantes `_0.._2`.

Los `.meta` copian los ajustes de importación de `music_day` (streaming), `amb_birds` y `step_0`. Esto importa porque
`Sfx.LoadBanks()` hace `Resources.LoadAll` de toda la carpeta Audio.
