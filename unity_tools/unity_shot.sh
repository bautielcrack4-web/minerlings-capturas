#!/bin/bash
# Capturas de Unity en batch (escenarios de Mineros.Game.ShotRunner, iguales a miner_idle/tools/shot.gd).
# Uso: bash unity_tools/unity_shot.sh <escenario> [carpeta_salida]
SCEN="${1:-idle}"; OUT="${2:-/tmp/unity_shots}"
cd "$(dirname "$0")/.."
mkdir -p "$OUT"
timeout ${SHOT_TIMEOUT_SH:-2400} xvfb-run -a -s "-screen 0 1280x1024x24" /opt/unity/6000.0.84f1/Editor/Unity -batchmode -force-glcore \
  -projectPath unity_miner -executeMethod ShotTool.Capture -shotOut "$OUT" -shotScenario "$SCEN" ${SHOT_SCALE:+-shotScale $SHOT_SCALE} \
  -logFile "$OUT/$SCEN.log" > /dev/null 2>&1
echo "$SCEN rc=$?"
grep -E "^stats |Exception|error CS|^shotcheck" "$OUT/$SCEN.log" | sort | uniq -c | sort -rn | head -40
