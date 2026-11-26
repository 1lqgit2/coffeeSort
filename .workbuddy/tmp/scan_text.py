import os, re, sys, json, io

ROOT = r"C:\Unity\project\Unity2022_CoffeeSortPuzzle\Assets"
EXCLUDE_DIRS = ["GoogleMobileAds"]

def iter_files():
    for dirpath, dirnames, filenames in os.walk(ROOT):
        rel = os.path.relpath(dirpath, ROOT)
        if any(rel.startswith(e) or e in rel.split(os.sep) for e in EXCLUDE_DIRS):
            continue
        for fn in filenames:
            if fn.endswith((".prefab", ".unity")):
                yield os.path.join(dirpath, fn)

# split yaml doc into blocks by '--- !u!'
def blocks(text):
    idxs = [m.start() for m in re.finditer(r"^--- !u!", text, re.M)]
    if not idxs:
        return []
    out = []
    for i, s in enumerate(idxs):
        e = idxs[i+1] if i+1 < len(idxs) else len(text)
        out.append((s, e))
    return out

TEXT_RE = re.compile(r"^  m_Text: ?(.*)$", re.M)
FONT_RE = re.compile(r"^(    m_Font: \{fileID: )(-?\d+)(, guid: )([0-9a-f]{32})(, type: \d+\})$", re.M)

def unescape(v):
    v = v.strip()
    if len(v) >= 2 and v[0] == '"' and v[-1] == '"':
        v = v[1:-1]
        v = v.encode("utf-8").decode("unicode_escape")
    return v

rows = {}
for path in iter_files():
    with io.open(path, "r", encoding="utf-8", errors="replace") as f:
        text = f.read()
    rel = os.path.relpath(path, r"C:\Unity\project\Unity2022_CoffeeSortPuzzle")
    for s, e in blocks(text):
        blk = text[s:e]
        m = TEXT_RE.search(blk)
        if not m:
            continue
        raw = m.group(1)
        val = unescape(raw)
        fm = FONT_RE.search(blk)
        guid = fm.group(4) if fm else "NO_FONT"
        # only ascii-letter-containing
        if re.search(r"[A-Za-z]", val):
            rows.setdefault((val, guid), []).append(rel)

print("UNIQUE_TEXT_COUNT=%d" % len(rows))
print("TOTAL_INSTANCES=%d" % sum(len(v) for v in rows.values()))
out = []
for (val, guid), files in sorted(rows.items()):
    out.append({"text": val, "font_guid": guid, "files": sorted(set(files)), "n": len(files)})
with io.open(r"C:\Unity\project\Unity2022_CoffeeSortPuzzle\.workbuddy\tmp\texts.json", "w", encoding="utf-8") as f:
    json.dump(out, f, ensure_ascii=False, indent=1)

for item in out:
    print("%-45s | font=%s | x%d | %s" % (item["text"][:45], item["font_guid"][:8], item["n"], ", ".join(os.path.basename(x) for x in item["files"])))
