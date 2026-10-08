#!/bin/bash
# Capturas de Rarezas en batch (escenarios de Rarezas.Game.ShotRunner: r0 = todo el regateo, caras = expresiones).
# Uso: SHOT_SCALE=2 bash unity_tools/unity_shot_rarezas.sh <escenario> [carpeta_salida_absoluta]
SCEN="${1:-r0}"; OUT="${2:-/tmp/rarezas_shots}"
cd "$(dirname "$0")/.."
mkdir -p "$OUT"
timeout ${SHOT_TIMEOUT_SH:-2700} xvfb-run -a -s "-screen 0 1280x1024x24" /opt/unity/6000.0.84f1/Editor/Unity -batchmode -force-glcore \
  -projectPath unity_rarezas -executeMethod ShotTool.Capture -shotOut "$OUT" -shotScenario "$SCEN" ${SHOT_SCALE:+-shotScale $SHOT_SCALE} \
  -logFile "$OUT/$SCEN.log" > /dev/null 2>&1
echo "$SCEN rc=$?"
grep -E "^stats |Exception|error CS|^shotcheck" "$OUT/$SCEN.log" | sort | uniq -c | sort -rn | head -60
