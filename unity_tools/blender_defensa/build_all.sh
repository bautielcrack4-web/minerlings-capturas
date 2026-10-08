#!/bin/bash
# Regenera todo el arte del juego desde los scripts (Blender 4.2 en /opt/blender o $BLENDER).
set -e
cd "$(dirname "$0")"
B=${BLENDER:-/opt/blender/blender-4.2.3-linux-x64/blender}
$B -b -P heroes.py   2>&1 | grep -E "^TRIS|Error|Traceback" || true
$B -b -P enemigos.py 2>&1 | grep -E "^TRIS|Error|Traceback" || true
$B -b -P utileria.py 2>&1 | grep -E "^TRIS|Error|Traceback" || true
$B -b -P tableros.py -- --only ${CAPS:-escritorio} 2>&1 | grep -E "^TRIS|Error|Traceback" || true
