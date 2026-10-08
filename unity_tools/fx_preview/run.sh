#!/bin/bash
# Hojas de contacto de los efectos sin Unity. Uso: bash unity_tools/fx_preview/run.sh [carpeta_salida] [args de preview.py]
# Exporta texturas y parametros con el MISMO codigo C# del juego (FxTexGen/FxSpecs) y los simula en Python.
OUT="${1:-/tmp/fx_preview}"
shift
cd "$(dirname "$0")"
/opt/dotnet/dotnet run --project FxExport -- "$OUT/data" >/dev/null && python3 preview.py --data "$OUT/data" --out "$OUT" "$@"
