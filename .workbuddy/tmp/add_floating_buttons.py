# -*- coding: utf-8 -*-
"""
在 Game.unity 的首页 SafeArea 下新增 Floating 容器 + jiliBtn + cebianBtn

结构：
Canvas/UIHome/Root/Content/UIContentPanels/UIHome/Root/SafeArea
  ├─ top
  └─ Floating                (VerticalLayoutGroup, 锚左上, pos(16,-205), size 136x296)
       ├─ jiliBtn            136x136  蓝底 + 金币图标 + 「奖励」
       └─ cebianBtn          136x136  蓝底 + 侧边栏图标 + 「侧边栏」
"""
import io
import re

PATH = r"C:/Unity/project/Unity2022_CoffeeSortPuzzle/Assets/Scenes/Game.unity"

SAFEAREA_RT = "224643011162824096"

# ---- 资源 guid ----
GUID_IMAGE = "fe87c0e1cc204ed48ad3b37840f39efc"   # UnityEngine.UI.Image
GUID_TEXT = "5f7201a12d95ffc409449d95f23cf332"    # UnityEngine.UI.Text
GUID_BUTTON = "4e29b1a8efbd4b44bb3f3716e73f07ff"  # UnityEngine.UI.Button
GUID_VLG = "59f8146938fff824cb5fd77236b75775"     # VerticalLayoutGroup
GUID_SCRIPT = "883c5c6d1cc475646aaacb7cda82b280"   # HomeFloatingButtons.cs
GUID_FONT = "4b55cb2df6c16834786823b918fe5b7f"    # DroidSansFallback.ttf

SPR_BG = ("451aee6bb955cac47ac6c7b2f032a11b", 1)   # ui_button_blue.png  九宫格
SPR_JILI = ("9cd630039ac498b43bec5aa061009136", 0)  # ui_coin.png
SPR_CEBIAN = ("014834e5234427946856378c7063cd45", 0)  # 侧边栏 (2).png

# ---- fileID 分配 ----
ID = {
    "floating_rt": "224999000000000001",
    "floating_go": "224999000000000002",
    "floating_vlg": "224999000000000003",
    "floating_script": "224999000000000004",
}

for prefix, base in (("jili", 10), ("ceb", 30)):
    names = ["go", "rt", "cr", "img", "btn", "icon_go", "icon_rt", "icon_cr", "icon_img",
             "lbl_go", "lbl_rt", "lbl_cr", "lbl_txt"]
    for i, n in enumerate(names):
        ID["%s_%s" % (prefix, n)] = "2249990000000000%02d" % (base + i + 1)


def go_doc(fid, name, comps, tag="Untagged", active=1, layer=5):
    lines = ["GameObject:",
             "  m_ObjectHideFlags: 0",
             "  m_CorrespondingSourceObject: {fileID: 0}",
             "  m_PrefabInstance: {fileID: 0}",
             "  m_PrefabAsset: {fileID: 0}",
             "  serializedVersion: 6",
             "  m_Component:"]
    for c in comps:
        lines.append("  - component: {fileID: %s}" % c)
    lines += ["  m_Layer: %d" % layer,
              "  m_Name: %s" % name,
              "  m_TagString: %s" % tag,
              "  m_Icon: {fileID: 0}",
              "  m_NavMeshLayer: 0",
              "  m_StaticEditorFlags: 0",
              "  m_IsActive: %d" % active]
    return ("1", fid, "\n".join(lines))


def rt_doc(fid, go, father, children, amin, amax, pos, size, pivot):
    lines = ["RectTransform:",
             "  m_ObjectHideFlags: 0",
             "  m_CorrespondingSourceObject: {fileID: 0}",
             "  m_PrefabInstance: {fileID: 0}",
             "  m_PrefabAsset: {fileID: 0}",
             "  m_GameObject: {fileID: %s}" % go,
             "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}",
             "  m_LocalPosition: {x: 0, y: 0, z: 0}",
             "  m_LocalScale: {x: 1, y: 1, z: 1}",
             "  m_ConstrainProportionsScale: 0",
             "  m_Children:"]
    if children:
        for c in children:
            lines.append("  - {fileID: %s}" % c)
    else:
        lines[-1] = "  m_Children: []"
    lines += ["  m_Father: {fileID: %s}" % father,
              "  m_LocalEulerAnglesHint: {x: 0, y: -0, z: -0}",
              "  m_AnchorMin: {x: %s, y: %s}" % amin,
              "  m_AnchorMax: {x: %s, y: %s}" % amax,
              "  m_AnchoredPosition: {x: %s, y: %s}" % pos,
              "  m_SizeDelta: {x: %s, y: %s}" % size,
              "  m_Pivot: {x: %s, y: %s}" % pivot]
    return ("224", fid, "\n".join(lines))


def cr_doc(fid, go):
    return ("222", fid, "\n".join([
        "CanvasRenderer:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: %s}" % go,
        "  m_CullTransparentMesh: 1"]))


def img_doc(fid, go, sprite_guid, sprite_type, color="1, 1, 1, 1", raycast=0, preserve=0):
    return ("114", fid, "\n".join([
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: %s}" % go,
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        "  m_Script: {fileID: 11500000, guid: %s, type: 3}" % GUID_IMAGE,
        "  m_Name: ",
        "  m_EditorClassIdentifier: ",
        "  m_Material: {fileID: 0}",
        "  m_Color: {r: %s}" % color,
        "  m_RaycastTarget: %d" % raycast,
        "  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}",
        "  m_Maskable: 1",
        "  m_OnCullStateChanged:",
        "    m_PersistentCalls:",
        "      m_Calls: []",
        "  m_Sprite: {fileID: 21300000, guid: %s, type: 3}" % sprite_guid,
        "  m_Type: %d" % sprite_type,
        "  m_PreserveAspect: %d" % preserve,
        "  m_FillCenter: 1",
        "  m_FillMethod: 4",
        "  m_FillAmount: 1",
        "  m_FillClockwise: 1",
        "  m_FillOrigin: 0",
        "  m_UseSpriteMesh: 0",
        "  m_PixelsPerUnitMultiplier: 1"]))


def btn_doc(fid, go, target_graphic):
    return ("114", fid, "\n".join([
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: %s}" % go,
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        "  m_Script: {fileID: 11500000, guid: %s, type: 3}" % GUID_BUTTON,
        "  m_Name: ",
        "  m_EditorClassIdentifier: ",
        "  m_Navigation:",
        "    m_Mode: 3",
        "    m_WrapAround: 0",
        "    m_SelectOnUp: {fileID: 0}",
        "    m_SelectOnDown: {fileID: 0}",
        "    m_SelectOnLeft: {fileID: 0}",
        "    m_SelectOnRight: {fileID: 0}",
        "  m_Transition: 1",
        "  m_Colors:",
        "    m_NormalColor: {r: 1, g: 1, b: 1, a: 1}",
        "    m_HighlightedColor: {r: 0.9607843, g: 0.9607843, b: 0.9607843, a: 1}",
        "    m_PressedColor: {r: 0.78431374, g: 0.78431374, b: 0.78431374, a: 1}",
        "    m_SelectedColor: {r: 0.9607843, g: 0.9607843, b: 0.9607843, a: 1}",
        "    m_DisabledColor: {r: 0.78431374, g: 0.78431374, b: 0.78431374, a: 0.5019608}",
        "    m_ColorMultiplier: 1",
        "    m_FadeDuration: 0.1",
        "  m_SpriteState:",
        "    m_HighlightedSprite: {fileID: 0}",
        "    m_PressedSprite: {fileID: 0}",
        "    m_SelectedSprite: {fileID: 0}",
        "    m_DisabledSprite: {fileID: 0}",
        "  m_AnimationTriggers:",
        "    m_NormalTrigger: Normal",
        "    m_HighlightedTrigger: Highlighted",
        "    m_PressedTrigger: Pressed",
        "    m_SelectedTrigger: Selected",
        "    m_DisabledTrigger: Disabled",
        "  m_Interactable: 1",
        "  m_TargetGraphic: {fileID: %s}" % target_graphic,
        "  m_OnClick:",
        "    m_PersistentCalls:",
        "      m_Calls: []"]))


def text_doc(fid, go, content, size, color="1, 1, 1, 1", align=4):
    return ("114", fid, "\n".join([
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: %s}" % go,
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        "  m_Script: {fileID: 11500000, guid: %s, type: 3}" % GUID_TEXT,
        "  m_Name: ",
        "  m_EditorClassIdentifier: ",
        "  m_Material: {fileID: 0}",
        "  m_Color: {r: %s}" % color,
        "  m_RaycastTarget: 0",
        "  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}",
        "  m_Maskable: 1",
        "  m_OnCullStateChanged:",
        "    m_PersistentCalls:",
        "      m_Calls: []",
        "  m_FontData:",
        "    m_Font: {fileID: 12800000, guid: %s, type: 3}" % GUID_FONT,
        "    m_FontSize: %d" % size,
        "    m_FontStyle: 0",
        "    m_BestFit: 1",
        "    m_MinSize: 10",
        "    m_MaxSize: %d" % size,
        "    m_Alignment: %d" % align,
        "    m_AlignByGeometry: 0",
        "    m_RichText: 1",
        "    m_HorizontalOverflow: 0",
        "    m_VerticalOverflow: 0",
        "    m_LineSpacing: 1",
        "  m_Text: %s" % content]))


def vlg_doc(fid, go, spacing, top=0, bottom=0):
    return ("114", fid, "\n".join([
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: %s}" % go,
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        "  m_Script: {fileID: 11500000, guid: %s, type: 3}" % GUID_VLG,
        "  m_Name: ",
        "  m_EditorClassIdentifier: ",
        "  m_Padding:",
        "    m_Left: 0",
        "    m_Right: 0",
        "    m_Top: %d" % top,
        "    m_Bottom: %d" % bottom,
        "  m_ChildAlignment: 1",
        "  m_Spacing: %d" % spacing,
        "  m_ChildForceExpandWidth: 0",
        "  m_ChildForceExpandHeight: 0",
        "  m_ChildControlWidth: 0",
        "  m_ChildControlHeight: 0",
        "  m_ChildScaleWidth: 0",
        "  m_ChildScaleHeight: 0",
        "  m_ReverseArrangement: 0"]))


def script_doc(fid, go, jili_btn, ceb_btn, coins=50):
    return ("114", fid, "\n".join([
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: %s}" % go,
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        "  m_Script: {fileID: 11500000, guid: %s, type: 3}" % GUID_SCRIPT,
        "  m_Name: ",
        "  m_EditorClassIdentifier: ",
        "  jiliBtn: {fileID: %s}" % jili_btn,
        "  cebianBtn: {fileID: %s}" % ceb_btn,
        "  rewardCoins: %d" % coins]))


def make_button(prefix, label, icon_guid, icon_type, icon_w, icon_h, icon_y):
    g = lambda k: ID["%s_%s" % (prefix, k)]
    return [
        go_doc(g("go"), prefix + "Btn",
               [g("rt"), g("cr"), g("img"), g("btn")], tag="E_Button"),
        rt_doc(g("rt"), g("go"), ID["floating_rt"], [g("icon_rt"), g("lbl_rt")],
               ("0.5", "0.5"), ("0.5", "0.5"), ("0", "0"), ("136", "136"), ("0.5", "0.5")),
        cr_doc(g("cr"), g("go")),
        img_doc(g("img"), g("go"), SPR_BG[0], SPR_BG[1], raycast=1),
        btn_doc(g("btn"), g("go"), g("img")),

        go_doc(g("icon_go"), "Icon", [g("icon_rt"), g("icon_cr"), g("icon_img")]),
        rt_doc(g("icon_rt"), g("icon_go"), g("rt"), [],
               ("0.5", "0.5"), ("0.5", "0.5"), ("0", str(icon_y)),
               (str(icon_w), str(icon_h)), ("0.5", "0.5")),
        cr_doc(g("icon_cr"), g("icon_go")),
        img_doc(g("icon_img"), g("icon_go"), icon_guid, icon_type),

        go_doc(g("lbl_go"), "Label", [g("lbl_rt"), g("lbl_cr"), g("lbl_txt")]),
        rt_doc(g("lbl_rt"), g("lbl_go"), g("rt"), [],
               ("0.5", "0.5"), ("0.5", "0.5"), ("0", "-46"), ("136", "34"), ("0.5", "0.5")),
        cr_doc(g("lbl_cr"), g("lbl_go")),
        text_doc(g("lbl_txt"), g("lbl_go"), label, 26),
    ]


docs = []
# Floating 容器
docs.append(go_doc(ID["floating_go"], "Floating",
                   [ID["floating_rt"], ID["floating_vlg"], ID["floating_script"]]))
docs.append(rt_doc(ID["floating_rt"], ID["floating_go"], SAFEAREA_RT,
                   [ID["jili_rt"], ID["ceb_rt"]],
                   ("0", "1"), ("0", "1"), ("16", "-205"), ("136", "296"), ("0", "1")))
docs.append(vlg_doc(ID["floating_vlg"], ID["floating_go"], 24))
docs.append(script_doc(ID["floating_script"], ID["floating_go"], ID["jili_btn"], ID["ceb_btn"], 50))

# jiliBtn：奖励 + 金币图标
docs += make_button("jili", "奖励", SPR_JILI[0], SPR_JILI[1], 84, 84, 14)
# cebianBtn：侧边栏 + 侧边栏图标
docs += make_button("ceb", "侧边栏", SPR_CEBIAN[0], SPR_CEBIAN[1], 64, 69, 16)

# ---------- 写入 ----------
with io.open(PATH, "r", encoding="utf-8") as f:
    src = f.read()


def patch_doc(text, fid, old, new):
    head = "--- !u!224 &%s\n" % fid
    i = text.index(head)
    k = text.find("\n--- ", i + len(head))
    j = k if k != -1 else len(text)
    block = text[i:j]
    assert old in block, "未找到目标片段: %r" % old
    block = block.replace(old, new, 1)
    return text[:i] + block + text[j:]


# SafeArea 的 m_Children 追加 Floating
old_children = """  m_Children:
  - {fileID: 224408371274512179}
  m_Father: {fileID: 224395776597522808}"""
new_children = """  m_Children:
  - {fileID: 224408371274512179}
  - {fileID: %s}
  m_Father: {fileID: 224395776597522808}""" % ID["floating_rt"]

src = patch_doc(src, SAFEAREA_RT, old_children, new_children)

# 追加新文档
chunks = []
for cls, fid, body in docs:
    chunks.append("\n--- !u!%s &%s\n%s\n" % (cls, fid, body))
if not src.endswith("\n"):
    src += "\n"
src += "".join(chunks)

with io.open(PATH, "w", encoding="utf-8", newline="\n") as f:
    f.write(src)

print("写入完成：新增 %d 个文档" % len(docs))
