import io, re, os

ROOT = r"C:\Unity\project\Unity2022_CoffeeSortPuzzle"
FILES = [
    r"Assets\Scripts\Managers\AdsControl.cs",
    r"Assets\Scripts\UI\UIBuyItem.cs",
    r"Assets\Scripts\UI\UIHeartInfo.cs",
    r"Assets\Scripts\UI\UIRevive.cs",
    r"Assets\Scripts\UI\UIShop.cs",
    r"Assets\Scripts\UI\UISetting.cs",
]


def strip_noise(s):
    out = []
    i = 0
    n = len(s)
    while i < n:
        c = s[i]
        # 块注释
        if c == "/" and i + 1 < n and s[i + 1] == "*":
            j = s.find("*/", i + 2)
            i = n if j < 0 else j + 2
            continue
        # 行注释
        if c == "/" and i + 1 < n and s[i + 1] == "/":
            j = s.find("\n", i)
            i = n if j < 0 else j
            continue
        # 逐字字符串
        if c == "@" and i + 1 < n and s[i + 1] == '"':
            i += 2
            while i < n:
                if s[i] == '"':
                    if i + 1 < n and s[i + 1] == '"':
                        i += 2
                        continue
                    i += 1
                    break
                i += 1
            out.append('""')
            continue
        # 普通字符串
        if c == '"':
            i += 1
            while i < n:
                if s[i] == "\\":
                    i += 2
                    continue
                if s[i] == '"':
                    i += 1
                    break
                i += 1
            out.append('""')
            continue
        # 字符字面量
        if c == "'":
            i += 1
            while i < n:
                if s[i] == "\\":
                    i += 2
                    continue
                if s[i] == "'":
                    i += 1
                    break
                i += 1
            out.append("''")
            continue
        out.append(c)
        i += 1
    return "".join(out)


ok_all = True
for rel in FILES:
    path = os.path.join(ROOT, rel)
    with io.open(path, "r", encoding="utf-8") as f:
        raw = f.read()
    code = strip_noise(raw)
    counts = {k: code.count(k) for k in "{}()[]"}
    balance_ok = (
        counts["{"] == counts["}"]
        and counts["("] == counts[")"]
        and counts["["] == counts["]"]
    )
    # namespace/class 基本检查
    has_class = re.search(r"\bclass\s+\w+", code) is not None
    status = "OK" if (balance_ok and has_class) else "FAIL"
    if not balance_ok or not has_class:
        ok_all = False
    print("%-42s %s  brace=%d/%d paren=%d/%d bracket=%d/%d class=%s" % (
        os.path.basename(rel), status,
        counts["{"], counts["}"], counts["("], counts[")"],
        counts["["], counts["]"], has_class))

print("ALL_OK=%s" % ok_all)
