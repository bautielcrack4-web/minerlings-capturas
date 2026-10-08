#!/bin/bash
# Capturas de Fortin de Juguete en batch (escenarios de Fortin.Game.ShotRunner; varios separados por coma).
# Uso: SHOT_SCALE=2 bash unity_tools/unity_shot_defensa.sh estilo,niveles [carpeta_salida_absoluta]
SCEN="${1:-estilo}"; OUT="${2:-/tmp/defensa_shots}"
cd "$(dirname "$0")/.."
mkdir -p "$OUT"
LOG="$OUT/$(echo "$SCEN" | tr ',' '_').log"
timeout ${SHOT_TIMEOUT_SH:-3000} xvfb-run -a -s "-screen 0 1280x1024x24" /opt/unity/6000.0.84f1/Editor/Unity -batchmode -force-glcore \
  -projectPath unity_defensa -executeMethod ShotTool.Capture -shotOut "$OUT" -shotScenario "$SCEN" ${SHOT_SCALE:+-shotScale $SHOT_SCALE} \
  -logFile "$LOG" > /dev/null 2>&1
echo "$SCEN rc=$?"
grep -E "^stats |Exception|error CS|^shotcheck" "$LOG" | sort | uniq -c | sort -rn | head -60
