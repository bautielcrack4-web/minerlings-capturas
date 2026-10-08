#!/bin/bash
# Instala el soporte de Android para el editor de Unity en Linux (modulo + OpenJDK + SDK + NDK), como Unity Hub.
# Uso: bash unity_tools/install_android.sh   (idempotente; ~1.6 GB de descarga, ~4.5 GB instalado)
# Necesita bsdtar y cpio (apt-get install -y libarchive-tools cpio).
set -e
VER="${UNITY_VERSION:-6000.0.84f1}"
AP="/opt/unity/$VER/Editor/Data/PlaybackEngines/AndroidPlayer"
TMP="${TMPDIR:-/tmp}/unity_android_dl"
mkdir -p "$TMP"
API="https://services.api.unity.com/unity/editor/release/v1/releases?version=$VER&platform=LINUX&architecture=X86_64"
curl -s "$API" > "$TMP/release.json"

url() {  # url <id de modulo>
  python3 - "$1" "$TMP/release.json" <<'EOF'
import json, sys
mid, path = sys.argv[1], sys.argv[2]
d = json.load(open(path))
def walk(ms):
    for m in ms or []:
        if m["id"] == mid: print(m["url"]); sys.exit(0)
        walk(m.get("subModules"))
for r in d["results"]:
    for x in r["downloads"]: walk(x.get("modules"))
EOF
}

get() {  # get <id> -> ruta del archivo descargado
  local u f
  u=$(url "$1"); f="$TMP/$(basename "$u")"
  [ -s "$f" ] || curl -sSL --retry 4 -o "$f" "$u"
  echo "$f"
}

# 1) Modulo: en Linux se usa el mismo .pkg de Mac (xar con un Payload cpio comprimido)
if [ ! -d "$AP/Variations" ]; then
  echo "Modulo Android..."
  PKG=$(get android)
  rm -rf "$TMP/pkg" && mkdir -p "$TMP/pkg" && bsdtar -xf "$PKG" -C "$TMP/pkg"
  PAY=$(find "$TMP/pkg" -name Payload | head -1)
  mkdir -p "$AP" && (cd "$AP" && gzip -dc "$PAY" | cpio -idm --quiet)
  # el Payload trae la carpeta como raiz o con prefijo: aplanar si hace falta
  if [ -d "$AP/AndroidPlayer" ]; then cp -a "$AP/AndroidPlayer/." "$AP/" && rm -rf "$AP/AndroidPlayer"; fi
  rm -rf "$TMP/pkg"
fi

unz() {  # unz <id> <destino> [carpeta interna a renombrar] [nombre final]
  local f; f=$(get "$1")
  mkdir -p "$2"
  if [ -n "$3" ] && [ -d "$2/$4" ]; then return; fi
  unzip -qo "$f" -d "$2"
  if [ -n "$3" ] && [ -d "$2/$3" ] && [ "$3" != "$4" ]; then rm -rf "$2/$4"; mv "$2/$3" "$2/$4"; fi
}

echo "OpenJDK..."
[ -x "$AP/OpenJDK/bin/java" ] || unz "android-open-jdk-17.0.18+8" "$AP/OpenJDK"
echo "SDK..."
[ -d "$AP/SDK/platform-tools" ] || unz android-sdk-platform-tools-36.0.0 "$AP/SDK"
[ -d "$AP/SDK/platforms/android-36" ] || unz android-sdk-platforms-36 "$AP/SDK/platforms"
unz android-sdk-build-tools-36.0.0 "$AP/SDK/build-tools" android-16 36.0.0
unz android-sdk-command-line-tools-16.0 "$AP/SDK/cmdline-tools" cmdline-tools 16.0
[ -d "$AP/SDK/cmake/3.22.1" ] || unz cmake-3.22.1 "$AP/SDK/cmake/3.22.1"
echo "NDK..."
if [ ! -f "$AP/NDK/source.properties" ]; then
  unz android-ndk-r27c "$AP"
  rm -rf "$AP/NDK" && mv "$AP/android-ndk-r27c" "$AP/NDK"
fi
chmod -R u+x "$AP/OpenJDK/bin" "$AP/SDK/platform-tools" "$AP/SDK/build-tools" "$AP/SDK/cmdline-tools" 2>/dev/null || true
yes | "$AP/OpenJDK/bin/java" -version 2>&1 | head -1
echo "Listo: $AP"
du -sh "$AP"
