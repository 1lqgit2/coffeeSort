# -*- coding: utf-8 -*-
"""在场景里定位指定名字的节点，打印其父链与子树"""
import io
import re
import sys

path = sys.argv[1]
targets = sys.argv[2].split(",")

with io.open(path, "r", encoding="utf-8") as f:
    text = f.read()

blocks = re.split(r"^--- !u!(\d+) &(\d+)( stripped)?", text, flags=re.M)
docs = {}
for i in range(1, len(blocks), 4):
    cls, fid, body = blocks[i], blocks[i + 1], blocks[i + 3]
    docs[fid] = (cls, body)

go_name, go_comps, trans = {}, {}, {}
for fid, (cls, body) in docs.items():
    if cls == "1":
        m = re.search(r"^  m_Name: (.*)$", body, flags=re.M)
        go_name[fid] = m.group(1).strip() if m else ""
        go_comps[fid] = re.findall(r"component: \{fileID: (\d+)\}", body)
    elif cls in ("4", "224"):
        go = re.search(r"m_GameObject: \{fileID: (\d+)\}", body)
        fa = re.search(r"m_Father: \{fileID: (\d+)\}", body)
        seg = body.split("m_Children:")[1].split("m_Father:")[0] if "m_Children:" in body else ""
        ch = re.findall(r"- \{fileID: (\d+)\}", seg)
        t = dict(go=go.group(1) if go else None, father=fa.group(1) if fa else None, children=ch, body=body)
        trans[fid] = t

# 反查 go -> transform
go2tr = {}
for tid, t in trans.items():
    if t["go"]:
        go2tr[t["go"]] = tid

def path_of(tid):
    out = []
    cur = tid
    guard = 0
    while cur and cur in trans and guard < 20:
        t = trans[cur]
        out.append(go_name.get(t["go"], "?"))
        cur = t["father"]
        guard += 1
    return "/".join(reversed(out))

def comp_summary(tid):
    t = trans[tid]
    comps = []
    for c in go_comps.get(t["go"], []):
        if c in docs:
            cls, body = docs[c]
            if cls == "114":
                m = re.search(r"m_Script: \{fileID: 11500000, guid: (\w+)", body)
                comps.append("Script(%s)" % (m.group(1)[:8] if m else "?"))
            elif cls == "225":
                comps.append("CanvasGroup")
            elif cls == "223":
                comps.append("Canvas")
    return comps

def dump(tid, depth=0, maxdepth=3):
    t = trans.get(tid)
    if not t or depth > maxdepth:
        return
    pos = re.search(r"m_AnchoredPosition: \{x: ([-\d.e]+), y: ([-\d.e]+)\}", t["body"])
    size = re.search(r"m_SizeDelta: \{x: ([-\d.e]+), y: ([-\d.e]+)\}", t["body"])
    amin = re.search(r"m_AnchorMin: \{x: ([-\d.e]+), y: ([-\d.e]+)\}", t["body"])
    amax = re.search(r"m_AnchorMax: \{x: ([-\d.e]+), y: ([-\d.e]+)\}", t["body"])
    print("%s%s  RT&%s  pos(%s)  size(%s)  anchor(%s-%s) [%s]" % (
        "  " * depth, go_name.get(t["go"], "?"), tid,
        "%s,%s" % pos.groups() if pos else "-",
        "%s,%s" % size.groups() if size else "-",
        "%s,%s" % amin.groups() if amin else "-",
        "%s,%s" % amax.groups() if amax else "-",
        ",".join(comp_summary(tid))))
    for c in t["children"]:
        dump(c, depth + 1, maxdepth)

print("=== 命中节点 ===")
for name, go in [(v, k) for k, v in go_name.items()]:
    if name in targets:
        tid = go2tr.get(go)
        if not tid:
            continue
        print("\n>>> %s   (GO &%s, RT &%s)" % (name, go, tid))
        print("    父链: %s" % path_of(tid))
        dump(tid, 0, 3)
