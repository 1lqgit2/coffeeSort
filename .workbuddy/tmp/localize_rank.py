# -*- coding: utf-8 -*-
"""把 RankData.asset 里的英文假名字替换成中文，只改 playerName 行。"""
import io
import os

PATH = r"C:/Unity/project/Unity2022_CoffeeSortPuzzle/Assets/Data/RankData.asset"

MAPPING = {
    "Cayson Floyd": "陈嘉豪",
    "Hazel Rhodes": "林雨桐",
    "Lilliana Holt": "苏雨晴",
    "Grady Cook": "王浩然",
    "Lina Vang": "李思琪",
    "Alonso Melton": "张宇航",
    "Cash Esquivel": "刘子墨",
    "Bennett Hoffman": "赵明轩",
    "Jimmy Tate": "周子豪",
    "Luke": "吴磊",
    "Alina Fox": "郑欣怡",
    "Marry": "孙曼妮",
    "April Berry": "何晓彤",
    "Luciana Williamson": "黄若溪",
    "John": "徐文博",
    "David": "马天宇",
    "Esther Smith": "朱静怡",
    "Keenan Travis": "胡俊凯",
    "Flora Quintero": "高雅静",
    "Giana Mann": "罗诗涵",
    "Avery Johnson": "梁俊熙",
    "Riley Smith": "宋佳琪",
    "Jordan Brown": "唐嘉伟",
    "Taylor Davis": "许文静",
    "Morgan Lee": "韩雨泽",
    "Casey White": "冯诗敏",
    "Jamie Harris": "邓子涵",
    "Alex Martinez": "曹博文",
    "Cameron Clark": "彭雨欣",
    "Dakota Lewis": "董思远",
}

with io.open(PATH, "r", encoding="utf-8") as f:
    lines = f.read().split("\n")

changed, missing = 0, []
for i, line in enumerate(lines):
    if line.lstrip().startswith("- playerName:"):
        old = line.split("playerName:", 1)[1].strip()
        if old in MAPPING:
            prefix = line[: line.index("- playerName:")]
            lines[i] = "%s- playerName: %s" % (prefix, MAPPING[old])
            changed += 1
        else:
            missing.append(old)

with io.open(PATH, "w", encoding="utf-8", newline="\n") as f:
    f.write("\n".join(lines))

print("changed=%d total_entries=%d" % (changed, len(MAPPING)))
if missing:
    print("UNMAPPED:", missing)
