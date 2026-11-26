# 项目长期记忆 - Unity2022_CoffeeSortPuzzle

## 环境
- Unity 版本：2022.3.62f3c1（中国版），安装路径 `C:\Unity\engine\2022.3.62f3c1\Editor\Unity.exe`
- 已安装平台模块：仅 WebGLSupport、windowsstandalonesupport（**无 Android / iOS Build Support**）
- 目标平台：Android / 抖音小游戏（WebGL）

## 第三方 SDK 现状（2026-11-24 清理后）
- 仅保留：抖音 TTSDK（Assets/Plugins/ByteGame）+ Assets/AdSDK 封装、DOTween（Plugins/Demigiant）、OPS 混淆、NiceVibrations（震动）
- 已删除：GoogleMobileAds(AdMob)、ExternalDependencyManager(EDM4U)、com.unity.ads、com.unity.purchasing + IAPManager
- 原生插件目录 Assets/Plugins/Android、Assets/Plugins/iOS 已删（内容原本全是 AdMob 库）

## 广告链路现状
- 唯一入口：`Assets/Scripts/Managers/AdsControl.cs`（抖音壳）→ `Assets/AdSDK/Scripts/AdManager.cs` → `TikTokPlatform.cs`（TTSDK）
- AdsControl 对外保留：Instance / ShowRewardedAd(onSuccess,onFail) / ShowInterstitialAd / ShowInterstital / RemoveAds / IsRemoveAds
- 场景里没有 AdManager 实例，由 `AdsControl.GetAdManager()` 缺失时自动创建并注入 tikTokAppId + 广告位列表（AdManager.Awake 自带 DontDestroyOnLoad）
- 广告位 ID 需在 Game.unity 的 AdsControl 组件上填写；编辑器内 TTSDK 是 mock，广告只在 WebGL 真机 / 抖音开发者工具生效
- 内购已移除：商店礼包统一改为看激励视频领取，去广告也由看广告触发

## UI 层级要点
- **运行时 UI 全在 `Assets/Scenes/Game.unity` 内部（解包对象，不是 prefab 实例）**；`Assets/Prefabs/UIs/uiwindow/UIHome.prefab` 未被任何场景引用，里面的 Left/Right 悬浮列在场景中不存在 —— 改界面前先确认改的是不是运行时那份
- 首页路径：`Canvas/UIHome/Root/Content/UIContentPanels/UIHome/Root/{Content(PlayBtn/Level), SafeArea(top/Floating)}`
- CanvasScaler：ScaleWithScreenSize 1080×1920，**match=0（按宽适配）**，画布宽恒 1080、高度随比例变 → HUD 一律用角锚点
- 首页悬浮按钮：`SafeArea/Floating`（VLG）下 `jiliBtn`（弹 Resources/AdConfirm 激励确认框 → 看完发 100 金币）+ `cebianBtn`（弹 Resources/cebianPanel 侧边栏引导面板，面板内 go 按钮跳 TT 侧边栏）。脚本 `Assets/Scripts/UI/HomeFloatingButtons.cs` 代码绑定事件
- AdSDK 面板要点：`AdConfirm` 自带 **Overlay Canvas（sortingOrder 999）**，可直接 Instantiate 无需父级，会稳定盖在主 UI 之上；`cebianPanel` 无 Canvas 必须挂主 Canvas，close/go 按钮 onClick 为空需代码补绑，遮罩点击关闭靠运行时插的透明 MaskClose 按钮（不要给根节点挂 Button，UGUI 的 GetEventHandler 会沿父链冒泡导致点面板空白也关闭）
- **画布模式**：主 Canvas 是 **ScreenSpaceCamera（renderMode=1，绑相机 675685220）**，不是 Overlay；`uiToast` 的 Canvas 是 **World Space（renderMode=2）**。往主 Canvas 挂新 UI 时坐标/拉伸照常，但独立弹窗若自带 Overlay Canvas 会渲染在最上层
- **广告实例化时机坑**：`AdManager` 场景里从未挂载，靠 `AdsControl.GetAdManager()` 懒创建，且 `platFormIns` 在 AdManager.Start() 才赋值 → UI 一律调 `AdsControl`，不要直接摸 `AdManager.Instance`

## UI 与字体
- UI 全部是 UGUI `Text`（没有 TMP 组件），主字体 `Assets/Font/Peace Sans Webfont.ttf`（纯英文，无中文字形）
- 中文字体统一用 `Assets/AdSDK/Font/DroidSansFallback.ttf`（guid 4b55cb2df6c16834786823b918fe5b7f）；同目录 `DroidSansFallback SDF.asset` 是 TMP_FontAsset，UGUI 用不了
- 中文文案已全量替换（2026-11-24），后续新增 UI 文案时记得同步换字体，否则中文显示方块
- 排行榜玩家名不在 prefab 里，而是数据资产 `Assets/Data/RankData.asset`（`RankDataSO.playerRanks`，30 条），由 `UILobbyRankList` 实例化 `uiRankItem` 显示；「我」那一行由代码写死 `meData.playerName`（已改为「我」）。改假名/名次文案请改这个 asset 而不是 prefab

## 约定
- 修改前先备份到项目根 `Backup_*` 目录；改 meta/dll 前确认 Unity 编辑器是否正在运行（运行中改 meta 需 Reimport 生效）
- 用户同时开着 DroppyTower 与当前项目两个 Unity 实例，batchmode 验证前需确认项目锁未被占用
