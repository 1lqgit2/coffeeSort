import os, re, io, json

ROOT = r"C:\Unity\project\Unity2022_CoffeeSortPuzzle"
BACKUP = os.path.join(ROOT, "Backup_SDK_CLEANUP_20261124")
ASSETS = os.path.join(ROOT, "Assets")

# 1. 收集被删除资产的 guid
deleted_guids = {}
for base in ["GoogleMobileAds", "ExternalDependencyManager", "Plugins_Android", "Plugins_iOS"]:
    d = os.path.join(BACKUP, base)
    for dirpath, _, filenames in os.walk(d):
        for fn in filenames:
            if fn.endswith(".meta"):
                meta = os.path.join(dirpath, fn)
                try:
                    with io.open(meta, "r", encoding="utf-8", errors="ignore") as f:
                        for line in f:
                            m = re.match(r"guid: ([0-9a-f]{32})", line.strip())
                            if m:
                                deleted_guids[m.group(1)] = os.path.join(base, fn)
                                break
                except Exception:
                    pass

# IAPManager
p = os.path.join(BACKUP, "Scripts", "Managers", "IAPManager.cs.meta")
if os.path.exists(p):
    with io.open(p, "r", encoding="utf-8", errors="ignore") as f:
        for line in f:
            m = re.match(r"guid: ([0-9a-f]{32})", line.strip())
            if m:
                deleted_guids[m.group(1)] = "IAPManager.cs"
                break

print("DELETED_GUID_COUNT=%d" % len(deleted_guids))

# 2. 扫描 Assets 下所有 YAML 型文件
GUID_RE = re.compile(r"guid: ([0-9a-f]{32})")
hits = {}
for dirpath, dirnames, filenames in os.walk(ASSETS):
    for fn in filenames:
        if not fn.endswith((".unity", ".prefab", ".asset", ".mat", ".controller", ".anim")):
            continue
        path = os.path.join(dirpath, fn)
        try:
            with io.open(path, "r", encoding="utf-8", errors="ignore") as f:
                text = f.read()
        except Exception:
            continue
        for m in GUID_RE.finditer(text):
            g = m.group(1)
            if g in deleted_guids:
                rel = os.path.relpath(path, ROOT)
                hits.setdefault(rel, set()).add(deleted_guids[g])

print("FILES_WITH_DANGLING_REF=%d" % len(hits))
for f, assets in sorted(hits.items()):
    print("  %s  <- %s" % (f, ", ".join(sorted(assets)[:4])))
