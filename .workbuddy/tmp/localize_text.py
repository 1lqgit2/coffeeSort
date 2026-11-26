import os, re, io, json

ROOT = r"C:\Unity\project\Unity2022_CoffeeSortPuzzle"
ASSETS = os.path.join(ROOT, "Assets")
EXCLUDE_DIRS = {"GoogleMobileAds"}

FONT_GUID = "4b55cb2df6c16834786823b918fe5b7f"  # DroidSansFallback.ttf
FONT_LINE = "    m_Font: {fileID: 12800000, guid: %s, type: 3}" % FONT_GUID

TRANS = {
    "Play": "开始",
    "Store": "商店",
    "Shop": "商店",
    "Home": "首页",
    "Settings": "设置",
    "Level": "关卡",
    "Level 5": "关卡 5",
    "Level 10": "关卡 10",
    "Level 15": "关卡 15",
    "Level 26": "关卡 26",
    "Level 56": "关卡 56",
    "Hard": "困难",
    "Hard level": "困难关卡",
    "Special": "特殊",
    "Free": "免费",
    "Full": "已满",
    "HOT": "热门",
    "Remove ADs": "去广告",
    "Remove ADs Pack": "去广告礼包",
    "Restore Purchase": "恢复购买",
    "PRIVACY POLICY": "隐私政策",
    "TERMS OF USE": "使用条款",
    "Refill": "补充",
    "Refill Lives": "补充生命",
    "Your Lives are full now.": "生命已满",
    "Continue": "继续",
    "Play On": "继续游戏",
    "Out Of Space": "空间不足",
    "Get a new slot to \nkeep playing!": "新增一个空位\n继续游戏！",
    "Hold to see board": "按住查看棋盘",
    "You will lose 1 life!": "将失去 1 条生命！",
    "You will lose a heart": "将失去 1 颗心",
    "Leave": "离开",
    "Warning": "警告",
    "WARNING!": "警告！",
    "Success": "成功",
    "Failure": "失败",
    "COMPLETED": "已完成",
    "Tap to Continue": "点击继续",
    "Try Again": "再试一次",
    "Super Pack": "超级礼包",
    "Slot+": "空位+",
    "Add more slots": "增加空位",
    "Game Fail": "游戏失败",
    "Clear All": "全部清除",
    "Quit Level": "退出关卡",
    "Enter Level...": "输入关卡...",
    "Enter Gift Pack ID": "输入礼包码",
    "Enter text...": "请输入文字",
    "Selection": "选择",
    "Heavy": "重",
    "Medium": "中",
    "Light": "轻",
    "Ballon": "气球",
    "Rank": "排行",
    "'Rank": "排行",
    "Weekly Rank": "周排行",
    "Reward": "奖励",
    "Player_Name": "玩家名",
    "LOADING...": "加载中...",
    "test info": "测试信息",
    "Cup Left": "剩余杯子",
}

# 已经是中文，只需要把字体换成支持中文的 DroidSansFallback
FONT_ONLY = {"修改ab", "显示   GM工具", "隐藏  GM工具", "显示  GM工具", "隐藏   GM工具"}

TEXT_RE = re.compile(r"^  m_Text: ?(.*)$", re.M)
FONT_RE = re.compile(r"^(    m_Font: \{fileID: )(-?\d+)(, guid: )([0-9a-f]{32})(, type: \d+\})$", re.M)


def unescape(v):
    v = v.strip()
    if len(v) >= 2 and v[0] == '"' and v[-1] == '"':
        v = v[1:-1]
        v = re.sub(r"\\u([0-9a-fA-F]{4})", lambda m: chr(int(m.group(1), 16)), v)
        v = v.replace("\\n", "\n")
    return v


def yaml_text(s):
    if "\n" in s:
        return '"' + s.replace("\\", "\\\\").replace('"', '\\"').replace("\n", "\\n") + '"'
    if s != s.strip() or ": " in s or s.startswith(("#", "-", "'", '"', "@", "`")):
        return '"' + s.replace('"', '\\"') + '"'
    return s


def blocks(text):
    idxs = [m.start() for m in re.finditer(r"^--- !u!", text, re.M)]
    if not idxs:
        return []
    return [(s, idxs[i + 1] if i + 1 < len(idxs) else len(text)) for i, s in enumerate(idxs)]


changed_text = 0
changed_font = 0
report = []

for dirpath, dirnames, filenames in os.walk(ASSETS):
    dirnames[:] = [d for d in dirnames if d not in EXCLUDE_DIRS]
    for fn in filenames:
        if not fn.endswith((".prefab", ".unity")):
            continue
        path = os.path.join(dirpath, fn)
        with io.open(path, "r", encoding="utf-8", errors="strict") as f:
            original = f.read()
        text = original
        pieces = []
        last = 0
        hit_file = 0
        for s, e in blocks(text):
            blk = text[s:e]
            m = TEXT_RE.search(blk)
            if not m:
                continue
            val = unescape(m.group(1))
            if val in TRANS:
                new = TRANS[val]
                blk = blk[: m.start(1)] + yaml_text(new) + blk[m.end(1) :]
                changed_text += 1
                hit_file += 1
                if "TextMesh:" not in blk:
                    blk, n = FONT_RE.subn(lambda mm: mm.group(1) + "12800000" + mm.group(3) + FONT_GUID + mm.group(5), blk, count=1)
                    changed_font += n
            elif val in FONT_ONLY:
                if "TextMesh:" not in blk:
                    blk, n = FONT_RE.subn(lambda mm: mm.group(1) + "12800000" + mm.group(3) + FONT_GUID + mm.group(5), blk, count=1)
                    changed_font += n
                    hit_file += 1
            else:
                continue
            pieces.append(text[last:s])
            pieces.append(blk)
            last = e
        if pieces:
            pieces.append(text[last:])
            new_text = "".join(pieces)
            with io.open(path, "w", encoding="utf-8", newline="") as f:
                f.write(new_text)
            report.append((os.path.relpath(path, ROOT), hit_file))

print("TEXT_REPLACED=%d FONT_REPLACED=%d FILES=%d" % (changed_text, changed_font, len(report)))
for p, n in report:
    print("  %-70s %d" % (p, n))
