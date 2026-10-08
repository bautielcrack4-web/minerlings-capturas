#!/bin/bash
# Compila el proyecto Unity contra las DLL de Unity 6. Uso: bash unity_tools/unity_compile/check.sh
cd "$(dirname "$0")"
/opt/dotnet/dotnet build -nologo -v q 2>&1 | sed 's|.*/unity_miner/||' | grep -E "error|Warning\(s\)|Error\(s\)" | sort -u | head -80
