# Mineros Idle en Unity: traspaso y plan

Este documento es para la sesión que continúe el port a Unity. Leelo completo antes de empezar.

## Objetivo
Rehacer "Mineros Idle" en **Unity 6 (6000.0 LTS)** con mejor calidad visual que la versión Godot (`miner_idle/`),
conservando TODA la mecánica, economía, metas, logros, Esencia, eventos, interfaz y primera experiencia ya diseñadas.
Fuente de verdad del diseño: `miner_idle/docs/MEGA_PROMPT.md`, `miner_idle/docs/ECONOMY.md` y el código de `miner_idle/scripts/`.

## Reglas
- Arte propio: el minero es un diseño original (ver `miner_idle/scripts/miner_view.gd`); no usar personajes ni arte de otros juegos.
- Interfaz: texturas Kenney CC0 ya incluidas en `miner_idle/assets/ui/` (copiarlas a `unity_miner/Assets/UI/` con sus licencias).
- Nunca pedir credenciales por el chat. La licencia llega por variables del entorno.

## Puesta en marcha (cada sesión nueva arranca con el servidor limpio)
1. `bash unity_tools/install_unity.sh` — descarga el editor (~4.5 GB) e intenta activar la licencia. Si la activacion
   del editor falla, usar el cliente de licencias directo (asi funciono el 2026-10-05):
   `/opt/unity/6000.0.84f1/Editor/Data/Resources/Licensing/Client/Unity.Licensing.Client --activate-ulf --username "$UNITY_EMAIL" --password "$UNITY_PASSWORD"`
   (`Invalid Credential 143.002` = contraseña incorrecta o cuenta sin contraseña propia).
2. .NET 8 para las pruebas y el chequeo rapido: `curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0 --install-dir /opt/dotnet`
   - Pruebas del Core: `cd unity_tools/core_tests && /opt/dotnet/dotnet run` (293 pruebas).
   - Compilacion rapida sin abrir Unity: `bash unity_tools/unity_compile/check.sh`. OJO: referencia TODOS los modulos
     de Unity, asi que no detecta un modulo faltante en `Packages/manifest.json` (paso con Android JNI).
3. Abrir el proyecto (importa todo, ~3 min la primera vez):
   `xvfb-run -a /opt/unity/6000.0.84f1/Editor/Unity -batchmode -projectPath unity_miner -quit -logFile -`
   Siempre pasar `-projectPath`: sin el, Unity crea Library/ProjectSettings/Temp en la carpeta actual.
4. Capturas: `bash unity_tools/unity_shot.sh <escenario> <carpeta>` (~30 s cada una, 720x1544, sin excepciones).
   Escenarios (Game/ShotRunner.cs, mismos que miner_idle/tools/shot.gd): idle rich b0..b3 ev_gold_rush ev_frenzy
   ev_meteor ev_chest milestone goal unlocked panels boss clear play fx_boss fx_chest fx_meteor fx_ms fx_hard fx_gold
   fx_frenzy, y propios de Unity: drag (arrastre real y fusion en Equipo), sparks (chispas de cerca), cost (capas).
   Cada captura imprime `stats <nombre> draws= batches= setpass= tris=` y idle/cost imprimen `cost <grupo> ...`.
   Referencias de Godot: instalar Godot 4.7.1 en /opt/godot y `bash miner_idle/tools/shot.sh <carpeta> <escenario>`
   (antes `godot --headless --import --path miner_idle`).
5. Android: `bash unity_tools/install_android.sh` (modulo + OpenJDK + SDK 36 + NDK r27c, ~4.8 GB; necesita
   `apt-get install -y libarchive-tools cpio`) y luego
   `xvfb-run -a /opt/unity/6000.0.84f1/Editor/Unity -batchmode -projectPath unity_miner -buildTarget Android -executeMethod BuildAndroid.Apk -buildOut Builds/MinerosIdle.apk -logFile -`
   (APK IL2CPP arm64, OpenGL ES 3, firmado con la clave de depuracion; `Builds/` esta en .gitignore).

## Estado (2026-10-05: el juego corre en Unity 6 sin errores en consola en todos los escenarios)
- [x] Core: economia completa en C# puro (`Scripts/Core`), 293 pruebas con .NET (`unity_tools/core_tests`).
- [x] Game: GameManager, Bootstrap (escena por codigo), guardado, ShotRunner/ShotTool para capturas en batch.
- [x] Miners, Audio, Art (kit propio), Fx + Juice, World (cañon, rocas, IA, eventos, jefe, biomas, Excavar), UI completa.
- [x] Primera ejecucion real y QA visual contra capturas de Godot (todos los escenarios). Arreglos de esa pasada:
  - manifest: sin `com.unity.modules.textrendering` (no existe en Unity 6), con `androidjni` (vibracion).
  - UI: texto de botones invisible (Content sin estirar -> rect negativo); CanvasRenderer en Confetti/Stripes;
    fantasma del arrastre de Equipo con la camara del canvas.
  - Fuente: Fredoka es variable y Unity usaba el peso 300; ahora `Fredoka-SemiBold.ttf` (instancia wght 600 como Godot).
  - Mundo: barras de vida sobre rocas golpeadas (faltaban), numeros flotantes al tamaño de Godot con contorno mas grueso,
    destello de golpe con tope 65% (antes la roca quedaba blanca entera).
  - Rendimiento: el cañon dibujaba una submalla por color (54 materiales). `VertexColorMerge` + shader `MinerToonVC`
    las juntan por color de vertice: idle paso de ~680 a ~250 draw calls y de ~330 a ~130 setpass (en el editor,
    que ademas hace una pasada de profundidad para sombras en pantalla; en movil con 1 cascada no hay esa pasada).
  - Shaders del estilo registrados en "Always Included Shaders" (GraphicsSettings).
- [x] Riesgos verificados: shaders MinerToon/Face/Vest/VC y FxParticleAdd/Alpha compilan y se ven; anclas de UI y
      paneles bien a 720x1544; arrastre y fusion de Equipo (escenario drag); sacudida/zoom/camara lenta del jefe;
      draw calls medidos. Chispas: escenario `sparks` ampliado, salen alargadas hacia afuera (SparkAlongX queda en false).
- [x] Build de Android: `Builds/MinerosIdle.apk` (20 MB, com.mineros.idle, IL2CPP arm64, minSdk 24, targetSdk 36).
      Con el editor apuntando a Android, idle mide ~180 draw calls, ~65 setpass y ~215k triangulos.
      Gradle descarga el plugin de Android por el proxy de la sesion: si falla con "Plugin com.android.application was
      not found", escribir `~/.gradle/gradle.properties` con systemProp.https.proxyHost/Port tomados de JAVA_TOOL_OPTIONS.
- [ ] Pendientes / ideas:
  - Icono de la app (hoy usa el de Unity por defecto).
  - Mineros: ~17 renderers por minero (50 en idle). Se podrian fundir las piezas rigidas por hueso para bajar draws.
  - 350k triangulos en idle (90k el cañon): revisar presupuesto de los arboles/accesorios para gama baja.
  - Probar el APK en un telefono real (rendimiento, area segura con muesca, vibracion, audio).
  - En Xvfb (render por software) los cuadros son lentos y el reloj de las transiciones (tope dt 0.05) avanza mas lento
    que en tiempo real: es del entorno, no del juego.

## Arquitectura propuesta
- `Core/` lógica pura (sin UnityEngine): GameState + contenido + guardado. Unity la envuelve en `GameManager : MonoBehaviour`
  que llama `Tick(dt)`, guarda en `Application.persistentDataPath` y traduce eventos C# a la UI.
- Escena construida por código desde un `Bootstrap` (evita editar YAML de escenas a mano): cámara ortográfica isométrica,
  luz direccional con sombras suaves, entorno.
- `Game/`: WorldController (cañón diagonal en mallas 3D, spawn de rocas, cámara que sigue), MinerController
  (modelo 3D propio generado por código + animación procedural, como `miner_view.gd`), RockController
  (rocas low-poly procedurales por bioma, tamaños, duras, ricas, cofre, jefe por fases), EventDirector (Fiebre de Oro,
  Frenesí, Meteoritos, Roca Cofre), Fx (partículas, números flotantes, hit-stop, sacudida).
- `UI/`: uGUI con sprites 9-slice de Kenney; HUD, tarjetas con hitos, rastreador de metas, desbloqueos con "¡NUEVO!" y mano
  tutorial, cola de celebraciones, paneles (Equipo con arrastre y fusión, Tienda, Pase, Logros, Renacer con Esencia, etc.).
- `Audio/`: clips sintetizados con `AudioClip.Create` (portar `miner_idle/scripts/sfx.gd`) y música por bioma.

## Fases y criterio de terminado
1. Proyecto + Core + GameManager + ShotTool: captura de una escena vacía con luz y cámara correctas.
2. Mundo 3D: cañón diagonal con meseta, desnivel y accesorios por bioma; rocas; cámara. Captura revisada.
3. Mineros 3D + IA + golpe + rotura + monedas. Comparar con capturas de Godot (`miner_idle/tools/shot.sh`).
4. Interfaz completa con desbloqueos y tutorial.
5. Eventos, jefe, hitos, celebraciones, audio.
6. Excavar (minijuego) y todos los paneles.
7. QA: sin errores en consola, capturas de cada pantalla, rendimiento móvil (build Android opcional).
