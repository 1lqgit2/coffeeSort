using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using TTSDK;
#endif

// 广告平台枚举
public enum AdPlatformType
{
    [InspectorName("抖音小游戏")]
    TickTok,
    [InspectorName("TopOn聚合")]
    TopOn,
    [InspectorName("微信小游戏")]
    WebChat,
}


/// <summary>
/// 广告管理
/// </summary>
public class AdManager : MonoBehaviour
{
    private static AdManager instance;

    /// <summary>
    /// 全局唯一实例。
    /// 这里用属性而不是裸静态字段：初始场景可能是「additive 加载 → 再卸载自己」的流程，
    /// 一旦静态字段指向的物体被场景卸载销毁，属性会自动重新找一个，避免 Instance 悄悄变成 null。
    /// </summary>
    public static AdManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<AdManager>();
            }

            return instance;
        }
    }

    [Header("平台")]
    public AdPlatformType adPlatformType = AdPlatformType.TickTok;
    [Header("小游戏ID")]
    public string AppId;
    [Header("激励视频广告位")]
    public List<string> videoAdIdList = new();
    [Header("插屏广告位")]
    public List<string> interAdIdList = new();

    private IAdPlatform platFormIns = null;
    private float lastAdClickTime = -10f;

    // 本次运行实际使用的广告位：EnsurePlatform 时随机挑一次就固定下来。
    // 每次展示都重新随机的话，TikTokPlatform 会因为「广告位变了」把预加载好的实例销毁重建，
    // 插屏等于每次都从零加载，越改越慢。
    private string currentVideoAdId = "";
    private string currentInterAdId = "";

    // 抖音 SDK 初始化状态：InitSDK 是异步的，没回调完就 CreateXxxAd 必失败
    private bool sdkInitDone;
    private bool sdkReadyNotified;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            // 已经有一个实例了（通常是初始场景里的那个），本场景内的重复实例直接销毁，
            // 避免广告平台被初始化两次。
            // 先 enabled=false：Destroy 是延迟到帧末执行的，不禁用的话本帧 Start() 仍会跑，
            // 会把 TT.InitSDK 和广告平台再初始化一遍。
            enabled = false;
            Destroy(gameObject);
            return;
        }

        instance = this;

        // 关键：跨场景常驻，避免场景卸载后 Instance 变成「已销毁对象」。
        if (transform.parent != null)
        {
            transform.SetParent(null, true);
        }

        if (transform.root == transform)
        {
            DontDestroyOnLoad(gameObject);
            Debug.Log("[广告] AdManager 已设为跨场景常驻");
        }
        else
        {
            Debug.LogWarning("[广告] AdManager 不在根节点，无法跨场景常驻，切换场景后广告可能失效");
        }
    }

    private void Start()
    {
        // 抖音小游戏 SDK 初始化（WebGL 真机环境；编辑器下不初始化，广告由 AdsControl 走模拟分支）
        this.InitTTSdk();

        if (this.AppId == "")
        {
            Debug.LogWarning("请填写APPID");
        }

        if (this.videoAdIdList == null || this.videoAdIdList.Count == 0)
        {
            Debug.LogWarning("视频广告位ID列表为空, 将使用默认广告位");
        }

        if (this.interAdIdList == null || this.interAdIdList.Count == 0)
        {
            Debug.LogWarning("插屏广告位ID列表为空, 将使用默认广告位");
        }

        // 立即把平台实例建出来：AdsControl 懒创建 AdManager 时，Start() 要下一帧才跑，
        // 之前 platFormIns 一直是 null，导致「第一次点广告必失败」。
        this.EnsurePlatform();

        // InitSDK 回调可能早于 platFormIns 赋值返回，那次通知会因实例为 null 被跳过，这里补一次
        this.TryNotifySdkReady();

        this.lastAdClickTime = -10f;
    }

    private void Update()
    {
        // 加载超时 / 挂起回调兜底
        this.platFormIns?.Tick();
    }

    /// <summary>
    /// 确保广告平台实例存在。所有对外广告入口都会先调它，
    /// 这样即使 Start() 还没跑（组件刚被 AddComponent 出来），也能直接播广告。
    /// </summary>
    public void EnsurePlatform()
    {
        if (this.platFormIns != null)
            return;

        switch (this.adPlatformType)
        {
            case AdPlatformType.TickTok:
                {
                    string videoId = this.GetRandVideoAdId();
                    string interId = this.GetRandScreenAdId();
                    this.currentVideoAdId = videoId;
                    this.currentInterAdId = interId;
                    this.platFormIns = new TikTokPlatform(this.AppId, videoId, interId);
                    Debug.Log($"[广告] 平台已就绪：抖音小游戏，激励视频={videoId}，插屏={interId}");
                    break;
                }
            case AdPlatformType.TopOn:
                Debug.LogWarning("TopOn广告平台尚未实现");
                break;
        }

        this.TryNotifySdkReady();
    }

    private void InitTTSdk()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            // OnTTContainerInitCallback 的签名是 void Invoke(int code, ContainerEnv env)
            TT.InitSDK((code, env) =>
            {
                this.sdkInitDone = true;

                if (code == 0)
                {
                    Debug.Log("[抖音SDK] 初始化完成");
                }
                else
                {
                    // 1 = SDK 版本不支持，2 = 引擎版本不支持
                    Debug.LogWarning($"[抖音SDK] 初始化失败，错误码 {code}");
                }

                // 初始化（无论成功失败）之后才允许创建广告实例
                this.TryNotifySdkReady();
            });
        }
        catch (Exception e)
        {
            this.sdkInitDone = true;
            Debug.LogWarning($"[抖音SDK] 初始化异常（不影响广告接口调用）：{e.Message}");
        }

        // 兜底：InitSDK 回调没来也不能让广告永久不可用
        this.StartCoroutine(this.SdkReadyFallback());
#else
        // 编辑器 / 非 WebGL：TTSDK 是 mock，广告由 AdsControl 的编辑器模拟分支接管
        this.sdkInitDone = false;
#endif
    }

    private IEnumerator SdkReadyFallback()
    {
        yield return new WaitForSecondsRealtime(3f);

        if (!this.sdkInitDone)
        {
            Debug.LogWarning("[抖音SDK] 初始化回调未在 3 秒内返回，先放行广告调用");
            this.sdkInitDone = true;
        }

        this.TryNotifySdkReady();
    }

    private void TryNotifySdkReady()
    {
        if (!this.sdkInitDone || this.sdkReadyNotified)
            return;

        if (this.platFormIns is TikTokPlatform tikTok)
        {
            this.sdkReadyNotified = true;
            tikTok.OnSdkReady();
        }
    }

    /// <summary>
    /// 播放插屏广告
    /// </summary>
    public void ShowInterstitialAd()
    {
        this.EnsurePlatform();

        if (this.platFormIns == null)
        {
            Debug.LogWarning("[广告] 广告平台未就绪，插屏跳过");
            return;
        }

        this.platFormIns.ShowInterstitialAd(string.IsNullOrEmpty(this.currentInterAdId)
            ? this.GetRandScreenAdId()
            : this.currentInterAdId);
    }

    /// <summary>
    /// 跳转场景
    /// </summary>
    /// <param name="onSuccess">成功回调</param>
    /// <param name="onFail">失败回调</param>
    public void NavigateToScene(Action onSuccess = null, Action<string> onFail = null)
    {
        this.EnsurePlatform();

        if (this.platFormIns == null)
        {
            Debug.LogError("广告平台实例未初始化，无法跳转场景");
            onFail?.Invoke("广告平台实例未初始化");
            return;
        }

        try
        {
            this.platFormIns.NavigateToScene(onSuccess, onFail);
        }
        catch (Exception e)
        {
            Debug.LogError($"跳转场景时发生异常：{e.Message}");
            onFail?.Invoke($"跳转场景时发生异常：{e.Message}");
        }
    }

    public void ShareGame(Action<bool> callBack = null)
    {
        this.EnsurePlatform();
        this.platFormIns?.ShareGame(callBack);
    }

    /// <summary>
    /// 播放激励视频广告
    /// </summary>
    /// <param name="onSuccess">成功回调</param>
    /// <param name="onFail">失败回调</param>
    public void ShowRewardedVideoAd(Action onSuccess, Action<string> onFail)
    {
        // 0.5s 内连点只放行一次；但被拦下时必须回调失败，否则业务侧会「点了没反应、也没提示」
        if (!this.CheckTimeLimit())
        {
            onFail?.Invoke("操作过于频繁，请稍后再试");
            return;
        }

        this.EnsurePlatform();

        if (this.platFormIns == null)
        {
            Debug.LogError("广告平台实例未初始化，无法显示广告");
            onFail?.Invoke("广告平台实例未初始化");
            return;
        }

        try
        {
            string videoId = string.IsNullOrEmpty(this.currentVideoAdId)
                ? this.GetRandVideoAdId()
                : this.currentVideoAdId;
            this.platFormIns.ShowRewardedVideoAd(videoId, onSuccess, onFail);
        }
        catch (Exception e)
        {
            Debug.LogError($"显示广告时发生异常：{e.Message}");
            onFail?.Invoke($"显示广告时发生异常：{e.Message}");
        }
    }


    // 随机取一个激励视频广告位ID
    private string GetRandVideoAdId()
    {
        if (this.videoAdIdList == null || this.videoAdIdList.Count == 0)
        {
            Debug.LogError("[广告] 激励视频广告位未配置，正在使用模板兜底ID（大概率不属于当前小游戏 appid，会报 1002）。"
                           + "请在场景 AdsController 的 AdsControl 组件 -> Rewarded Video Ad Ids 里填入「激励视频」类型广告位");
            return "f32bf9970ah83mq1kg"; // 默认广告ID，防止空字符串
        }
        int index = UnityEngine.Random.Range(0, this.videoAdIdList.Count);
        return this.videoAdIdList[index];
    }

    // 随机取一个插屏广告位ID
    private string GetRandScreenAdId()
    {
        if (this.interAdIdList == null || this.interAdIdList.Count == 0)
        {
            Debug.LogError("[广告] 插屏广告位未配置，正在使用模板兜底ID（大概率不属于当前小游戏 appid，会报 158902/创建失败）。"
                           + "请在场景 AdsController 的 AdsControl 组件 -> Interstitial Ad Ids 里填入「插屏」类型广告位");
            return "dk217ga5e5b48uo4gh"; // 默认广告ID，防止空字符串
        }
        int index = UnityEngine.Random.Range(0, this.interAdIdList.Count);
        return this.interAdIdList[index];
    }

    // 限制 0.5 秒内广告只能触发一次
    private bool CheckTimeLimit()
    {
        if (Time.time - lastAdClickTime < 0.5f)
        {
            Debug.LogWarning("广告点击过于频繁，请稍后再试。");
            return false;
        }
        lastAdClickTime = Time.time;
        return true;
    }
}
