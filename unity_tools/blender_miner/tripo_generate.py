"""Genera el minero con la API de Tripo (multivista: frente, perfil izq., espalda, perfil der.) y baja el GLB.

Uso:  TRIPO_API_KEY=tsk_... python3 tripo_generate.py [--single] [--out out/tripo]
La clave se lee SOLO de la variable de entorno (no se guarda en el repo). Necesita creditos en la cuenta de Tripo:
sin saldo la API responde code 2010 "You don't have enough credit to create this task".
Despues: blender -b -P tripo_import.py -- out/tripo/minero.glb  (segmenta por piezas y exporta a Unity como el modelo propio).
"""
import argparse, json, os, sys, time, urllib.request
from PIL import Image

API = "https://openapi.tripo3d.ai/v3"   # API v3 (la v2 /v2/openapi queda como legado)
HERE = os.path.dirname(os.path.abspath(__file__))


def req(method, path, key, data=None, files=None):
    if files:
        import uuid
        b = uuid.uuid4().hex
        body = b""
        for name, (fname, content, ctype) in files.items():
            body += ("--%s\r\nContent-Disposition: form-data; name=\"%s\"; filename=\"%s\"\r\nContent-Type: %s\r\n\r\n"
                     % (b, name, fname, ctype)).encode() + content + b"\r\n"
        body += ("--%s--\r\n" % b).encode()
        headers = {"Content-Type": "multipart/form-data; boundary=" + b}
    elif data is not None:
        body = json.dumps(data).encode()
        headers = {"Content-Type": "application/json"}
    else:
        body, headers = None, {}
    headers["Authorization"] = "Bearer " + key
    r = urllib.request.Request(API + path, data=body, headers=headers, method=method)
    try:
        with urllib.request.urlopen(r, timeout=120) as resp:
            return json.loads(resp.read())
    except urllib.error.HTTPError as e:
        return json.loads(e.read() or b"{}")


def upload(key, img_path):
    with open(img_path, "rb") as f:
        res = req("POST", "/files", key, files={"file": (os.path.basename(img_path), f.read(), "image/png")})
    if res.get("code") != 0:
        sys.exit("upload: %s" % res)
    return res["data"]["file_token"]


def views(out):
    """Recorta las vistas de la hoja de referencia (fondo blanco, el minero centrado)."""
    ref = Image.open(os.path.join(HERE, "ref", "miner_ref.webp")).convert("RGB")
    boxes = {
        "front": (230, 0, 980, 650),      # vista grande
        "left": (350, 660, 600, 945),
        "back": (610, 660, 920, 945),
        "right": (950, 660, 1210, 945),
    }
    paths = {}
    for k, b in boxes.items():
        im = ref.crop(b)
        # cuadrado con margen blanco, 1024 px (Tripo prefiere imagenes grandes y centradas)
        s = max(im.size)
        sq = Image.new("RGB", (s, s), (255, 255, 255))
        sq.paste(im, ((s - im.width) // 2, (s - im.height) // 2))
        p = os.path.join(out, k + ".png")
        sq.resize((1024, 1024), Image.LANCZOS).save(p)
        paths[k] = p
    return paths


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=os.path.join(HERE, "out", "tripo"))
    ap.add_argument("--single", action="store_true", help="solo la vista de frente (image_to_model)")
    a = ap.parse_args()
    key = os.environ.get("TRIPO_API_KEY", "")
    if not key:
        sys.exit("falta TRIPO_API_KEY")
    os.makedirs(a.out, exist_ok=True)
    bal = req("GET", "/account/balance", key)
    print("saldo:", bal.get("data"))
    v = views(a.out)
    common = {"model": "v3.1-20260211", "texture": True, "pbr": True, "texture_quality": "detailed",
              "texture_alignment": "original_image", "face_limit": 40000}
    if a.single:
        task = dict(common, input=upload(key, v["front"]))
        path = "/generation/image-to-model"
    else:
        task = dict(common, inputs=[{k: upload(key, v[k])} for k in ("front", "left", "back", "right")])
        path = "/generation/multiview-to-model"
    res = req("POST", path, key, data=task)
    if res.get("code") != 0:
        sys.exit("tarea: %s" % res)
    tid = res["data"]["task_id"]
    print("tarea", tid)
    while True:
        st = req("GET", "/tasks/" + tid, key)
        d = st.get("data", {})
        print("estado", d.get("status"), d.get("progress"))
        if d.get("status") in ("success", "failed", "cancelled", "banned", "expired", "unknown"):
            break
        time.sleep(8)
    if d.get("status") != "success":
        sys.exit("fallo: %s" % st)
    out = d.get("output", {})
    url = out.get("pbr_model_url") or out.get("model_url") or out.get("pbr_model") or out.get("model")
    glb = os.path.join(a.out, "minero.glb")
    urllib.request.urlretrieve(url, glb)
    prev = out.get("rendered_image_url") or out.get("rendered_image")
    if prev:
        urllib.request.urlretrieve(prev, os.path.join(a.out, "preview.webp"))
    print("listo", glb)


if __name__ == "__main__":
    main()
