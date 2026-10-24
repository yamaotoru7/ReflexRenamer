"""オリジナル版の実測位置（UI ツリー）とダイアログテンプレートを突き合わせ、コントロールごとのアンカーを求める。

使い方: python -I tools/anchors.py <resources.json> <tree.txt> <dialogId> <originX> <originY> <designW> <actualW> <designH> <actualH>
出力: 各コントロールの ID、クラス、アンカー（L/R/T/B の組み合わせ）、実測との差
"""
import json
import re
import sys


def dlu_x(v):
    return int(round(v * 7 / 4.0 + 1e-9))


def dlu_y(v):
    return int(round(v * 14 / 8.0 + 1e-9))


res, tree, dialog_id, ox, oy, dw, aw, dh, ah = sys.argv[1:10]
ox, oy, dw, aw, dh, ah = map(int, (ox, oy, dw, aw, dh, ah))
grow_w = aw - dw
grow_h = ah - dh

dialogs = {d["Id"]: d["Value"] for d in json.load(open(res, encoding="utf-8"))["Dialogs"]}
controls = dialogs[dialog_id]["Controls"]

# 実測：id= と座標を持つ行を集める（同じ ID が複数あるときは順番に対応させる）
actual = {}
for line in open(tree, encoding="utf-8"):
    m = re.search(r" id=(-?\d+) .*?\[(-?\d+),(-?\d+),(\d+),(\d+)\]", line)
    if not m:
        continue
    cid = int(m.group(1))
    actual.setdefault(cid, []).append(tuple(map(int, m.groups()[1:])))

rules = {}
for c in controls:
    cid = c["Id"]
    if cid not in actual or not actual[cid]:
        continue
    ax, ay, aw_, ah_ = actual[cid].pop(0)
    ex, ey = ox + dlu_x(c["X"]), oy + dlu_y(c["Y"])
    ew, eh = dlu_x(c["Width"]), dlu_y(c["Height"])
    dx, dy, dwid, dhgt = ax - ex, ay - ey, aw_ - ew, ah_ - eh
    h = "LR" if abs(dwid - grow_w) <= 2 else "R" if abs(dx - grow_w) <= 2 else "L"
    v = "TB" if abs(dhgt - grow_h) <= 2 else "B" if abs(dy - grow_h) <= 2 else "T"
    note = "" if (h, v) != ("L", "T") or (dx, dy, dwid, dhgt) == (0, 0, 0, 0) else f"  ※差 dx={dx} dy={dy} dw={dwid} dh={dhgt}"
    print(f'{cid:6} {c["Class"]:18} {h}{v}{note}')
    rules[str(cid)] = h + v
print(json.dumps(rules))
