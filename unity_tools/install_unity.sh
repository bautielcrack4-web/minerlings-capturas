#!/bin/bash
# Instala el editor de Unity (Linux) y activa la licencia con UNITY_EMAIL / UNITY_PASSWORD.
# Uso: bash unity_tools/install_unity.sh   (idempotente)
set -e
VER="${UNITY_VERSION:-6000.0.84f1}"
DIR="/opt/unity/$VER"
EDITOR="$DIR/Editor/Unity"
if [ ! -x "$EDITOR" ]; then
  echo "Buscando la URL del editor $VER..."
  URL=$(curl -s "https://services.api.unity.com/unity/editor/release/v1/releases?version=$VER&platform=LINUX&architecture=X86_64" \
    | python3 -c "import sys,json;d=json.load(sys.stdin);print([x['url'] for r in d['results'] for x in r['downloads'] if x['platform']=='LINUX'][0])")
  echo "Descargando $URL (~4.5 GB)..."
  mkdir -p "$DIR"
  curl -sSL --retry 4 "$URL" | tar -xJ -C "$DIR"
fi
echo "Editor: $EDITOR"
if [ -n "$UNITY_LICENSE" ]; then
  # Plan B: contenido de un archivo .ulf generado con Unity Hub en otra computadora
  mkdir -p "$HOME/.local/share/unity3d/Unity"
  printf '%s' "$UNITY_LICENSE" > "$HOME/.local/share/unity3d/Unity/Unity_lic.ulf"
  echo "Licencia escrita desde UNITY_LICENSE."
elif [ -n "$UNITY_EMAIL" ] && [ -n "$UNITY_PASSWORD" ]; then
  echo "Activando licencia Personal..."
  xvfb-run -a "$EDITOR" -batchmode -nographics -quit -logFile - \
    -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" ${UNITY_SERIAL:+-serial "$UNITY_SERIAL"} \
    2>&1 | grep -iE "licen|error|activat" | tail -20 || true
else
  echo "Faltan UNITY_EMAIL / UNITY_PASSWORD en las variables del entorno."
fi
