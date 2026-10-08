"""Redibujo de referencias con el modelo de imagenes de OpenAI, SIEMPRE por la Batch API (mitad de precio) y en
calidad baja, antes de mandarlas a Tripo.

- Mineros (`--kind miner`): cada recorte de la lamina -> mismo personaje en pose T, de frente, sin herramienta, fondo
  blanco (/v1/images/edits).
- Habitaciones (`--kind room`): conceptos por texto (/v1/images/generations), un archivo `<nombre>.txt` por pieza.

Uso:
  OPENAI_API_KEY=... python3 gpt_tpose.py send <carpeta_entrada> --kind miner|room [--model gpt-image-2] [--quality low]
  OPENAI_API_KEY=... python3 gpt_tpose.py fetch <batch_id> <carpeta_salida>
La clave se lee SOLO del entorno (no se guarda en el repo). `send` imprime el id del lote; `fetch` baja lo que termino.
"""
import argparse, base64, json, os, sys, time, uuid, urllib.request, urllib.error

API = "https://api.openai.com/v1"
MINER = ("Redraw exactly this same character: same face, beard, helmet, outfit, colors, armor, backpack, crystals and "
         "proportions. Full-body stylized 3D mobile game character, T-pose: standing straight, both arms stretched out "
         "horizontally to the sides, palms down, legs slightly apart, facing the camera, front view. Hands empty: NO "
         "pickaxe, NO hammer, NO tool. Only this one character, centered, whole body visible with margin. Pure white "
         "background, no shadow, no floor, no text.")
ROOM_BASE = ("Stylized 3D mobile game asset in the same style as a cute chunky miner character sheet: bright saturated "
             "colors, soft rounded shapes, clean hand-painted look. Single isolated piece, front view, centered, whole "
             "object visible with margin, pure white background, no shadow, no floor, no text. ")


def call(method, path, key, data=None, ctype=None):
    headers = {"Authorization": "Bearer " + key}
    if ctype: headers["Content-Type"] = ctype
    req = urllib.request.Request(API + path, data=data, method=method, headers=headers)
    for attempt in range(5):
        try:
            with urllib.request.urlopen(req, timeout=300) as r:
                return r.read()
        except urllib.error.HTTPError as e:
            if e.code in (429, 500, 502, 503) and attempt < 4:
                time.sleep(3 * (attempt + 1)); continue
            raise RuntimeError("HTTP %d %s" % (e.code, e.read().decode(errors="replace")[:400]))


def upload(key, path, purpose):
    b = uuid.uuid4().hex
    with open(path, "rb") as f:
        content = f.read()
    body = (("--%s\r\nContent-Disposition: form-data; name=\"purpose\"\r\n\r\n%s\r\n" % (b, purpose)).encode()
            + ("--%s\r\nContent-Disposition: form-data; name=\"file\"; filename=\"%s\"\r\n\r\n" % (b, os.path.basename(path))).encode()
            + content + ("\r\n--%s--\r\n" % b).encode())
    return json.loads(call("POST", "/files", key, body, "multipart/form-data; boundary=" + b))["id"]


def send(a, key):
    lines = []
    files = sorted(os.listdir(a.inp))
    if a.kind == "miner":
        for f in files:
            if not f.lower().endswith(".png"): continue
            fid = upload(key, os.path.join(a.inp, f), "vision")
            lines.append({"custom_id": f[:-4], "method": "POST", "url": "/v1/images/edits",
                          "body": {"model": a.model, "prompt": MINER, "images": [{"file_id": fid}], "quality": a.quality,
                                   "size": "1024x1024", "background": "opaque", "n": 1}})
        endpoint = "/v1/images/edits"
    else:
        for f in files:
            if not f.endswith(".txt"): continue
            with open(os.path.join(a.inp, f)) as t:
                prompt = ROOM_BASE + t.read().strip()
            lines.append({"custom_id": f[:-4], "method": "POST", "url": "/v1/images/generations",
                          "body": {"model": a.model, "prompt": prompt, "quality": a.quality, "size": "1024x1024", "n": 1}})
        endpoint = "/v1/images/generations"
    jl = os.path.join(a.inp, "_lote.jsonl")
    with open(jl, "w") as o:
        for l in lines: o.write(json.dumps(l) + "\n")
    fid = upload(key, jl, "batch")
    b = json.loads(call("POST", "/batches", key, json.dumps({"input_file_id": fid, "endpoint": endpoint, "completion_window": "24h"}).encode(), "application/json"))
    print(b["id"], len(lines), "pedidos")


def fetch(a, key):
    b = json.loads(call("GET", "/batches/" + a.batch, key))
    print(b["status"], b["request_counts"])
    os.makedirs(a.out, exist_ok=True)
    for fld in ("output_file_id", "error_file_id"):
        if not b.get(fld): continue
        text = call("GET", "/files/%s/content" % b[fld], key).decode()
        for line in text.splitlines():
            r = json.loads(line)
            name = r["custom_id"]
            body = (r.get("response") or {}).get("body") or {}
            data = body.get("data")
            if data and data[0].get("b64_json"):
                with open(os.path.join(a.out, name + ".png"), "wb") as o:
                    o.write(base64.b64decode(data[0]["b64_json"]))
                print(name, "ok")
            else:
                print(name, "ERROR", json.dumps(r.get("error") or body.get("error"))[:300])


def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd")
    s = sub.add_parser("send"); s.add_argument("inp"); s.add_argument("--kind", default="miner")
    s.add_argument("--model", default="gpt-image-2"); s.add_argument("--quality", default="low")
    f = sub.add_parser("fetch"); f.add_argument("batch"); f.add_argument("out")
    a = ap.parse_args()
    key = os.environ.get("OPENAI_API_KEY")
    if not key: sys.exit("falta OPENAI_API_KEY en el entorno")
    if a.cmd == "send": send(a, key)
    elif a.cmd == "fetch": fetch(a, key)
    else: ap.print_help()


if __name__ == "__main__":
    main()
