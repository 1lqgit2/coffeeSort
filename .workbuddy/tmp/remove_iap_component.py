import io, re, os

ROOT = r"C:\Unity\project\Unity2022_CoffeeSortPuzzle"
path = os.path.join(ROOT, "Assets", "Scenes", "Game.unity")
backup = os.path.join(ROOT, "Backup_SDK_CLEANUP_20261124", "Game.unity.beforeIAPRemoval")

with io.open(path, "r", encoding="utf-8", newline="") as f:
    text = f.read()

with io.open(backup, "w", encoding="utf-8", newline="") as f:
    f.write(text)

before_blocks = len(re.findall(r"^--- !u!", text, re.M))

# 1. 删除 IAPManager 的 GameObject / Transform / MonoBehaviour 三个块
for fid in (573916326, 573916327, 573916328):
    pattern = re.compile(r"^--- !u!\d+ &%d\n.*?(?=^--- !u!|\Z)" % fid, re.M | re.S)
    text, n = pattern.subn("", text)
    print("block %d removed: %d" % (fid, n))

# 2. 从父 Transform 的 m_Children 中移除
needle = "  - {fileID: 573916327}\n"
print("children entry found: %d" % text.count(needle))
text = text.replace(needle, "", 1)

after_blocks = len(re.findall(r"^--- !u!", text, re.M))
print("blocks: %d -> %d" % (before_blocks, after_blocks))

with io.open(path, "w", encoding="utf-8", newline="") as f:
    f.write(text)
print("WROTE_OK")
