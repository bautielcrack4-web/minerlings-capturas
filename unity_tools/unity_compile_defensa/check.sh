#!/bin/bash
# Compila unity_defensa contra las DLL de Unity 6 (sin licencia ni ejecucion). Uso: bash unity_tools/unity_compile_rarezas/check.sh
cd "$(dirname "$0")"
if [ ! -f "$HOME/.cache/fortin_gma/GoogleMobileAds.dll" ]; then
  t=$(mktemp -d) && curl -sSL -o "$t/gma.tgz" https://package.openupm.com/com.google.ads.mobile/-/com.google.ads.mobile-11.5.0.tgz \
    && tar xzf "$t/gma.tgz" -C "$t" && mkdir -p "$HOME/.cache/fortin_gma" && cp "$t"/package/GoogleMobileAds/*.dll "$HOME/.cache/fortin_gma/"
fi
/opt/dotnet/dotnet build -nologo -v q 2>&1 | sed 's|.*/unity_defensa/||' | grep -E "error|Warning\(s\)|Error\(s\)" | sort -u | head -80
