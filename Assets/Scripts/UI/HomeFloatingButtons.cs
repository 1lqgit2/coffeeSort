using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 首页左侧悬浮按钮：
///   jiliBtn   → 弹出 AdConfirm（激励视频确认框），看完发金币
///   cebianBtn → 弹出 cevianPanel（抖音侧边栏引导面板）
/// 挂在 UIHome 页面 SafeArea/Floating 节点上，事件全部由代码绑定，不需要在 Inspector 里手连
/// </summary>
public class HomeFloatingButtons : MonoBehaviour
{
    [Header("按钮引用")]
    public Button jiliBtn;
    public Button cebianBtn;

    [Header("奖励配置")]
    [Tooltip("看一次激励视频发放的金币数（AdConfirm 面板上展示的数字，建议保持一致）")]
    public int rewardCoins = 100;

    /// <summary>AdConfirm 弹窗打开期间防止重复拉起</summary>
    private bool adConfirmOpened;

    /// <summary>cebianPanel 运行时实例，首次点击创建后复用</summary>
    private GameObject cebianPanel;

    private const string CebianPanelResPath = "cebianPanel";

    private void Start()
    {
        if (jiliBtn != null)
            jiliBtn.onClick.AddListener(OnClickJili);

        if (cebianBtn != null)
            cebianBtn.onClick.AddListener(OnClickCebian);
    }

    // ---------------- 奖励按钮 ----------------

    /// <summary>jiliBtn：弹 AdConfirm 确认框，确认后播放抖音激励视频，看完发金币</summary>
    public void OnClickJili()
    {
        PlayClickSfx();

        if (adConfirmOpened) return;

        adConfirmOpened = true;
        // 点「返回」关闭也会走 onClose，确保标志位复位
        AdConfirm.Show(GrantReward, () => adConfirmOpened = false);
    }

    private void GrantReward()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.AddCoins(rewardCoins);

        Toast("领取成功，金币 +" + rewardCoins);
    }

    // ---------------- 侧边栏按钮 ----------------

    /// <summary>cebianBtn：弹出 cebianPanel 引导面板</summary>
    public void OnClickCebian()
    {
        PlayClickSfx();
        ShowCebianPanel();
    }

    private void ShowCebianPanel()
    {
        if (cebianPanel == null)
        {
            var prefab = Resources.Load<GameObject>(CebianPanelResPath);
            if (prefab == null)
            {
                Debug.LogError("[侧边栏] 未找到 Resources/" + CebianPanelResPath + " 预制体");
                Toast("侧边栏面板缺失");
                return;
            }

            cebianPanel = Instantiate(prefab, GetPanelParent(), false);
            cebianPanel.name = "cebianPanel(Runtime)";

            var rt = cebianPanel.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
            }

            // 面板预制体里 close / go 两个按钮没有序列化事件，这里补上
            var closeBtn = FindButton(cebianPanel.transform, "box/close");
            if (closeBtn != null)
                closeBtn.onClick.AddListener(HideCebianPanel);

            var goBtn = FindButton(cebianPanel.transform, "box/go");
            if (goBtn != null)
                goBtn.onClick.AddListener(OnCebianGo);

            AddMaskCloseButton(cebianPanel);
        }

        cebianPanel.SetActive(true);
        cebianPanel.transform.SetAsLastSibling();
    }

    private void HideCebianPanel()
    {
        if (cebianPanel != null)
            cebianPanel.SetActive(false);
    }

    /// <summary>面板里的「前往侧边栏」按钮</summary>
    private void OnCebianGo()
    {
        PlayClickSfx();

        // 走 AdsControl：它负责在场景没有 AdManager 时创建一个，
        // 直接读 AdManager.Instance 在没播过广告时是 null。
        if (AdsControl.Instance == null)
        {
            Toast("侧边栏暂不可用");
            return;
        }

        AdsControl.Instance.NavigateToSideBar(
            () =>
            {
                Debug.Log("[侧边栏] 唤起成功");
                HideCebianPanel();
            },
            err =>
            {
                Debug.LogWarning("[侧边栏] 唤起失败: " + err);
                // 面板的遮罩会挡住 Toast，先关掉再提示
                HideCebianPanel();
                Toast("侧边栏暂不可用");
            });
    }

    // ---------------- 工具 ----------------

    /// <summary>面板要挂在根 Canvas 下，否则 cebianPanel（无 Canvas）渲染不出来</summary>
    private Transform GetPanelParent()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
            return canvas.rootCanvas.transform;

        var any = FindObjectOfType<Canvas>();
        if (any != null)
            return any.rootCanvas.transform;

        return transform;
    }

    private static Button FindButton(Transform root, string path)
    {
        var t = root.Find(path);
        if (t == null)
        {
            Debug.LogWarning("[侧边栏] 面板里找不到按钮节点: " + path);
            return null;
        }

        var btn = t.GetComponent<Button>();
        if (btn == null)
            Debug.LogWarning("[侧边栏] 节点 " + path + " 上没有 Button 组件");

        return btn;
    }

    /// <summary>
    /// 给遮罩层加一个透明按钮：点面板外的空白区域也能关闭。
    /// 插在根节点下、box 之前（渲染层级最低），点在 box 上时命中的是 box 自己的 Image，
    /// 不会误触这里。比直接给根节点挂 Button 安全 —— 那种写法会被 UGUI 的
    /// GetEventHandler 沿父链向上查找，导致点面板空白处也把面板关掉。
    /// </summary>
    private void AddMaskCloseButton(GameObject panel)
    {
        var go = new GameObject("MaskClose", typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(panel.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.SetSiblingIndex(0);

        // 全透明，但 raycastTarget 保持 true 才能接收点击
        var img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true;

        var btn = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(HideCebianPanel);
    }

    private void PlayClickSfx()
    {
        if (SoundManager.Instance != null && SoundManager.Instance.clickSfx != null)
            SoundManager.Instance.PlaySFX(SoundManager.Instance.clickSfx);
    }

    private void Toast(string msg)
    {
        if (GameManager.Instance != null
            && GameManager.Instance.uiManager != null
            && GameManager.Instance.uiManager.uiToast != null)
        {
            GameManager.Instance.uiManager.uiToast.ShowToast(msg, 1.5f);
        }
        else
        {
            Debug.Log("[首页悬浮按钮] " + msg);
        }
    }
}
