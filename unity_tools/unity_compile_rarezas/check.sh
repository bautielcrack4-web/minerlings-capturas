#!/bin/bash
# Compila unity_rarezas contra las DLL de Unity 6 (sin licencia ni ejecucion). Uso: bash unity_tools/unity_compile_rarezas/check.sh
cd "$(dirname "$0")"
if [ ! -d /opt/gma/GoogleMobileAds ]; then
  mkdir -p /opt/gma /tmp/gma && curl -sSL https://package.openupm.com/com.google.ads.mobile/-/com.google.ads.mobile-11.5.0.tgz | tar xz -C /tmp/gma && cp -r /tmp/gma/package/GoogleMobileAds /opt/gma/
fi
/opt/dotnet/dotnet build -nologo -v q 2>&1 | sed 's|.*/unity_rarezas/||' | grep -E "error|Warning\(s\)|Error\(s\)" | sort -u | head -80
