# -*- coding: utf-8 -*-
"""打印 prefab 的 Transform 层级树（GameObject 名 + RectTransform 尺寸/位置 + 组件类型）"""
import io
import re
import sys

path = sys.argv[1]

with io.open(path, "r", encoding="utf-8") as f:
    text = f.read()

blocks = re.split(r"^--- !u!(\d+) &(\d+)", text, flags=re.M)
# blocks: [pre, cls, id, body, cls, id, body...]
docs = {}
order = []
for i in range(1, len(blocks), 3):
    cls, fid, body = blocks[i], blocks[i + 1], blocks[i + 2]
    docs[fid] = (cls, body)
    order.append(fid)

# gameobject names
go_name = {}
trans = {}   # fileID -> dict(go, father, children, pos, size, comps)
for fid, (cls, body) in docs.items():
    if cls == "1":  # GameObject
        m = re.search(r"^  m_Name: (.*)$", body, flags=re.M)
        name = m.group(1).strip() if m else ""
        comps = re.findall(r"component: \{fileID: (\d+)\}", body)
        go_name[fid] = (name, comps)
    elif cls in ("4", "224"):  # Transform / RectTransform
        go = re.search(r"m_GameObject: \{fileID: (\d+)\}", body)
        fa = re.search(r"m_Father: \{fileID: (\d+)\}", body)
        ch = re.findall(r"- \{fileID: (\d+)\}", body.split("m_Children:")[1].split("m_Father:")[0]) if "m_Children:" in body else []
        pos = re.search(r"m_AnchoredPosition: \{x: ([-\d.e]+), y: ([-\d.e]+)\}", body)
        size = re.search(r"m_SizeDelta: \{x: ([-\d.e]+), y: ([-\d.e]+)\}", body)
        anchor_min = re.search(r"m_AnchorMin: \{x: ([-\d.e]+), y: ([-\d.e]+)\}", body)
        anchor_max = re.search(r"m_AnchorMax: \{x: ([-\d.e]+), y: ([-\d.e]+)\}", body)
        trans[fid] = dict(go=go.group(1) if go else None,
                          father=fa.group(1) if fa else None,
                          children=ch, pos=pos.groups() if pos else None,
                          size=size.groups() if size else None,
                          amin=anchor_min.groups() if anchor_min else None,
                          amax=anchor_max.groups() if anchor_max else None)

SCRIPT_NAMES = {}
for fid, (cls, body) in docs.items():
    if cls == "114":
        m = re.search(r"m_Script: \{fileID: 11500000, guid: (\w+)", body)
        if m:
            SCRIPT_NAMES[fid] = m.group(1)

def comp_label(cid):
    if cid not in docs:
        return "?"
    cls, body = docs[cid]
    mapping = {"1": "GameObject", "4": "Transform", "224": "RectTransform",
               "222": "CanvasRenderer", "223": "Canvas", "114": "MonoBehaviour"}
    if cls == "114":
        g = SCRIPT_NAMES.get(cid, "")
        return "Script(%s)" % g[:8]
    return mapping.get(cls, cls)

# roots
roots = [tid for tid, t in trans.items() if not t["father"] or t["father"] == "0"]

def dump(tid, depth):
    t = trans.get(tid)
    if not t:
        return
    go = t["go"]
    name, comps = go_name.get(go, ("?", []))
    extra = []
    for c in comps:
        lbl = comp_label(c)
        if lbl in ("Transform", "RectTransform", "GameObject"):
            continue
        extra.append(lbl)
    pos = "pos(%s,%s)" % t["pos"] if t["pos"] else ""
    size = "size(%s,%s)" % t["size"] if t["size"] else ""
    print("%s%s  %s  %s  [%s]" % ("  " * depth, name, pos, size, ",".join(extra)))
    for c in t["children"]:
        dump(c, depth + 1)

for r in roots:
    dump(r, 0)
