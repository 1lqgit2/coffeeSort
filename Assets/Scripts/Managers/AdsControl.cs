using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 广告统一入口 —— 抖音小游戏专用。
///
/// 历史版本内部同时封装了 AdMob(GoogleMobileAds) 与 Unity Ads 两套 SDK。
/// 平台清理后两者已全部移除，这里只保留一层薄壳：
///   - 对外继续提供 Instance / ShowInterstitialAd / ShowRewardedAd / RemoveAds / IsRemoveAds
///   - 内部把调用转给 AdSDK 的 AdManager（TTSDK -> TikTokPlatform）
///
/// 场景里没挂 AdManager 时会自动创建一个，避免"点广告没反应"。
/// 广告位 ID 填在下面；不填则使用 AdManager 里的兜底广告位并打印警告。
/// </summary>
public class AdsControl : MonoBehaviour
{
    [Header("抖音小游戏 AppID（仅埋点用，可留空）")]
    public string tikTokAppId = "";

    [Header("激励视频广告位 ID（可填多个，每次随机取）")]
    public List<string> rewardedVideoAdIds = new List<string>();

    [Header("插屏广告位 ID（可填多个，每次随机取）")]
    public List<string> interstitialAdIds = new List<string>();

    private const string RemoveAdsPrefKey = "removeAds";

    private static AdsControl instance;

    public static AdsControl Instance { get { return instance; } }

    private void Awake()
    {
        if (FindObjectsOfType(typeof(AdsControl)).Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 取抖音广告管理器。场景里没有就自动建一个并把配置注入进去，
    /// 这样不用手动往场景拖组件也能跑通广告链路。
    /// </summary>
    private AdManager GetAdManager()
    {
        AdManager manager = AdManager.Instance;
        if (manager != null)
            return manager;

        GameObject go = new GameObject("[AdManager]");
        manager = go.AddComponent<AdManager>();
        manager.adPlatformType = AdPlatformType.TickTok;
        manager.AppId = tikTokAppId;
        manager.videoAdIdList = rewardedVideoAdIds != null
            ? new List<string>(rewardedVideoAdIds)
            : new List<string>();
        manager.interAdIdList = interstitialAdIds != null
            ? new List<string>(interstitialAdIds)
            : new List<string>();

        Debug.Log("[广告] 场景中没有 AdManager，已自动创建并注入广告位配置");
        return manager;
    }

    /// <summary>
    /// 播放激励视频广告。完整看完回调 onSuccess；中途关闭或失败回调 onFail。
    /// 注意：激励视频是用户主动换取奖励，不受"去广告"影响。
    /// </summary>
    public void ShowRewardedAd(Action onSuccess, Action<string> onFail = null)
    {
#if UNITY_EDITOR
        // 编辑器里 TTSDK 是 mock，播不出真广告也没回调，业务侧会以为「看完没发奖」。
        // 这里直接按「看完」处理，方便验证奖励是否真的到账（真机 / 开发者工具仍走下面的真实链路）。
        Debug.Log("[广告] 编辑器环境：模拟播放激励视频并直接发放奖励");
        onSuccess?.Invoke();
        return;
#else
        AdManager manager = GetAdManager();
        if (manager == null)
        {
            Debug.LogWarning("[广告] AdManager 不可用，激励视频已跳过");
            onFail?.Invoke("广告管理器不可用");
            return;
        }

        manager.ShowRewardedVideoAd(
            () => { onSuccess?.Invoke(); },
            err =>
            {
                Debug.LogWarning("[广告] 激励视频失败：" + err);
                onFail?.Invoke(err);
            });
#endif
    }

    /// <summary>
    /// 播放插屏广告。已购买去广告则直接跳过。
    /// </summary>
    public void ShowInterstitialAd()
    {
        if (IsRemoveAds())
        {
            // 之前看过「去广告」礼包就会一直跳过插屏，调试时看不到广告先查这条日志
            Debug.Log("[广告] 已标记去广告（removeAds=1），插屏跳过");
            return;
        }

#if UNITY_EDITOR
        Debug.Log("[广告] 编辑器环境：跳过插屏（真机 / 抖音开发者工具里会展示）");
        return;
#else
        AdManager manager = GetAdManager();
        if (manager != null)
            manager.ShowInterstitialAd();
#endif
    }

    /// <summary>
    /// 兼容历史调用（原方法名拼写就是 ShowInterstital），等价于 ShowInterstitialAd。
    /// </summary>
    public void ShowInterstital()
    {
        ShowInterstitialAd();
    }

    /// <summary>
    /// 标记为已去广告。原来由 IAP 内购成功回调触发，
    /// IAP 移除后改由"看广告领取去广告礼包"的入口调用。
    /// </summary>
    public void RemoveAds()
    {
        PlayerPrefs.SetInt(RemoveAdsPrefKey, 1);
        PlayerPrefs.Save();
    }

    public bool IsRemoveAds()
    {
        return PlayerPrefs.GetInt(RemoveAdsPrefKey, 0) == 1;
    }

    /// <summary>
    /// 唤起抖音侧边栏（跳转 sidebar 场景）。
    /// 走这里而不是直接调 AdManager.Instance：AdManager 是懒创建的，
    /// 场景里没挂时必须先由 GetAdManager() 建出来，否则 Instance 为 null 直接失败。
    /// 编辑器 / 非抖音环境下会走 onFail。
    /// </summary>
    public void NavigateToSideBar(Action onSuccess = null, Action<string> onFail = null)
    {
        AdManager manager = GetAdManager();
        if (manager == null)
        {
            Debug.LogWarning("[广告] AdManager 不可用，无法唤起侧边栏");
            onFail?.Invoke("广告管理器不可用");
            return;
        }

        manager.NavigateToScene(onSuccess, onFail);
    }
}
