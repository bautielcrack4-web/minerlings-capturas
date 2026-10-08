"""Genera un modelo con Tripo desde texto (P Series, low-poly para juego) y baja el GLB.

Uso: TRIPO_API_KEY=tsk_... python3 tripo_text.py <nombre> "<prompt>" [--faces 3000] [--out out/tripo_b]
Muestra el costo en creditos (diferencia de saldo).
"""
import argparse, os, sys, time, urllib.request, json

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from tripo_generate import req  # noqa: E402

STYLE = ("cute stylized low-poly mobile game building, bright cheerful colors, soft clean shapes, chunky proportions, "
         "isometric casual game asset like a cozy island city builder, single building on a small flat grass base, "
         "no text, no characters")
PROP_STYLE = ("cute stylized low-poly mobile game prop, bright cheerful colors, soft clean shapes, chunky proportions, "
              "casual cozy island game asset, single object, no text, no characters")
NEG = "text, letters, people, characters, realistic, dirty, broken mesh, floating parts, multiple buildings"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("name")
    ap.add_argument("prompt")
    ap.add_argument("--faces", type=int, default=3000)
    ap.add_argument("--out", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "out", "tripo_b"))
    ap.add_argument("--model", default="P1-20260311")
    ap.add_argument("--prop", action="store_true", help="objeto suelto (sin base de pasto)")
    a = ap.parse_args()
    key = os.environ["TRIPO_API_KEY"]
    os.makedirs(a.out, exist_ok=True)
    b0 = req("GET", "/account/balance", key)["data"]["balance"]
    task = {"prompt": a.prompt + ". " + (PROP_STYLE if a.prop else STYLE), "negative_prompt": NEG, "model": a.model, "face_limit": a.faces,
            "texture": True, "pbr": False}
    res = req("POST", "/generation/text-to-model", key, data=task)
    if res.get("code") != 0:
        sys.exit("tarea: %s" % res)
    tid = res["data"]["task_id"]
    while True:
        d = req("GET", "/tasks/" + tid, key).get("data", {})
        if d.get("status") in ("success", "failed", "cancelled", "banned", "expired", "unknown"):
            break
        time.sleep(6)
    if d.get("status") != "success":
        sys.exit("fallo: %s" % d)
    out = d.get("output", {})
    url = out.get("model_url") or out.get("pbr_model_url")
    glb = os.path.join(a.out, a.name + ".glb")
    urllib.request.urlretrieve(url, glb)
    prev = out.get("rendered_image_url")
    if prev:
        urllib.request.urlretrieve(prev, os.path.join(a.out, a.name + "_preview.webp"))
    b1 = req("GET", "/account/balance", key)["data"]["balance"]
    print("listo", glb, "costo", b0 - b1, "saldo", b1)


if __name__ == "__main__":
    main()
