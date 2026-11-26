import io, re

root = r"C:/Unity/project/Unity2022_CoffeeSortPuzzle/"
files = [
    r"Assets/Scripts/UI/HomeFloatingButtons.cs",
    r"Assets/AdSDK/Scripts/AdConfirm.cs",
    r"Assets/Scripts/Managers/AdsControl.cs",
]

allok = True
for rel in files:
    s = io.open(root + rel, encoding="utf-8").read()
    t = re.sub(r"//[^\n]*", "", s)
    t = re.sub(r"/\*.*?\*/", "", t, flags=re.S)
    t = re.sub(r'"(?:\\.|[^"\\])*"', '""', t)
    t = re.sub(r"'(?:\\.|[^'\\])*'", "''", t)
    ok = (t.count("{") == t.count("}") and t.count("(") == t.count(")")
          and t.count("[") == t.count("]") and re.search(r"\bclass\s+\w+", s))
    allok = allok and bool(ok)
    print("%-26s {}=%d/%d ()=%d/%d []=%d/%d  %s" % (
        rel.split("/")[-1],
        t.count("{"), t.count("}"), t.count("("), t.count(")"),
        t.count("["), t.count("]"),
        "OK" if ok else "MISMATCH"))

print("ALL_OK" if allok else "HAS_ISSUE")
