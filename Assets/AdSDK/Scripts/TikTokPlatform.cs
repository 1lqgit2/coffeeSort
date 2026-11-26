using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

//#if TickTok
using TTSDK;
using TTSDK.UNBridgeLib.LitJson;

/*
 * 抖音平台广告
 *
 * 两条最重要的平台约束（踩过坑，别改回去）：
 *   1. TT.InitSDK 是异步的。SDK 没就绪就 TT.CreateXxxAd 稳定拿到 createInterstitialAd:fail（errMsg 还是空的）。
 *      所以本类的构造函数里【绝对不创建广告实例】，创建全部推迟到 OnSdkReady() 或首次展示时按需创建。
 *   2. 插屏实例只能展示一次，展示/失败后平台会自动销毁它。所以 OnClose / OnError 里只清引用，
 *      不要立刻重建（旧实例销毁是异步的，紧接着 create 会稳定失败），下次要展示时再按需创建。
 */
public class TikTokPlatform : IAdPlatform
{
    /// <summary>
    /// 是否向第三方（ecpm.xxhygames.com）上报 openid/机型等数据做 ECPM 与转化回传。
    /// 默认关闭：上面报会把用户信息发到外部服务器，抖音审核和隐私政策里必须申报，
    /// 确认是自己的后台、且已在上架材料里写明后再改成 true。
    /// </summary>
    public static bool EnableRemoteTracking = false;

    // 宿主频控（开放平台规则；TTSDK 注释里的 15s 是旧值）
    private const float INTER_LAUNCH_GUARD = 30f;   // 启动后 30s 内不能展示插屏
    private const float INTER_MIN_INTERVAL = 60f;   // 两次插屏间隔 60s
    private const float INTER_AFTER_REWARD = 60f;   // 激励视频之后 60s 内不能展示插屏
    private const float AD_LOAD_TIMEOUT = 10f;      // 加载超时兜底，避免一直挂着 pending
    private const float SDK_LAZY_PASS = 8f;         // InitSDK 回调不回来时，8s 后惰性放行

    // 小游戏ID
    private readonly string miniAppid;
    private readonly float platformCreateTime;

    // SDK 状态
    private bool sdkReady;
    private bool? devToolCache;

    // 激励视频
    private TTRewardedVideoAd rewardedVideoAd = null;
    private string videoAdId = "";
    private bool rewardLoaded;
    private bool rewardLoading;
    private bool rewardShowPending;
    private float rewardLoadingStartTime;
    private Action onRewardSuccess;
    private Action<string> onRewardFail;
    private float prevTimeScale = 1f;
    private float lastRewardCloseTime = -999f;

    // 插屏广告
    private TTInterstitialAd interAdIns;
    private string interAdId = "";
    private bool interLoading;
    private bool interShowPending;
    private float interLoadingStartTime;
    private float moduleStartTime;
    private float lastShowTime = -999f;
    private float interFailCooldownUntil = -999f;

    // 埋点
    private readonly string ecpmURL = "https://ecpm.xxhygames.com/openid/openlog/insertlog";  // ECPM后台地址
    private readonly string conversionURL = "https://analytics.oceanengine.com/api/v2/conversion";   // 巨量引擎API接口

    private TTSystemInfo systemInfo;

    public TikTokPlatform(string appId, string videoAdId, string interAdId)
    {
        this.miniAppid = appId ?? "";
        this.videoAdId = videoAdId;
        this.interAdId = interAdId;

        this.platformCreateTime = Time.realtimeSinceStartup;
        this.moduleStartTime = this.platformCreateTime;

        if (string.IsNullOrEmpty(this.miniAppid))
        {
            // AppID 只用于 ECPM/转化回传，广告本身按广告位 ID 播放，不影响收益流程。
            Debug.Log("[抖音广告] 未填写小游戏 AppID，广告可正常播放，仅埋点上报不可用");
        }
        else if (EnableRemoteTracking && !this.miniAppid.EndsWith("07"))
        {
            // 只有开启回传时才需要 AppID 格式校验，否则这条警告纯属噪音
            Debug.LogWarning($"[抖音广告] AppID「{this.miniAppid}」格式可能不匹配回传接口，广告播放不受影响");
        }

        if (EnableRemoteTracking)
        {
            try
            {
                this.Login();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[抖音广告] 埋点初始化失败（不影响广告）：{e.Message}");
            }
        }

        // 注意：这里不创建任何广告实例。等 AdManager 收到 InitSDK 回调后调用 OnSdkReady()。
    }

    /// <summary>
    /// TT.InitSDK 完成后由 AdManager 调用：此时才允许创建广告实例，并顺手预加载一次插屏，
    /// 这样玩家第一次打开设置就能直接看到插屏（否则首次只能是「开始加载，下次再展示」）。
    /// </summary>
    public void OnSdkReady()
    {
        if (this.sdkReady)
            return;

        this.sdkReady = true;
        this.moduleStartTime = Time.realtimeSinceStartup;   // 保护期从广告模块真正启动算
        Debug.Log("[抖音广告] SDK 已就绪，开始预加载插屏");

        if (this.IsDevTool())
            Debug.Log("[抖音广告] 当前是开发者工具，展示的是模拟广告页，不校验宿主频控");

        this.EnsureInterstitialAd();
    }

    /// <summary>
    /// 所有创建 / 展示入口都先过这一层：SDK 实在回调不回来时，超时后惰性放行，
    /// 避免「初始化回调丢了 → 广告永久不可用」。
    /// </summary>
    private bool IsSdkUsable()
    {
        if (this.sdkReady)
            return true;

#if UNITY_WEBGL && !UNITY_EDITOR
        if (Time.realtimeSinceStartup - this.platformCreateTime < SDK_LAZY_PASS)
            return false;

        Debug.LogWarning("[抖音广告] InitSDK 回调未返回，惰性放行广告调用");
        this.sdkReady = true;
        this.moduleStartTime = Time.realtimeSinceStartup;
        return true;
#else
        return false;
#endif
    }

    private bool IsDevTool()
    {
        if (this.devToolCache.HasValue)
            return this.devToolCache.Value;

        bool dev = false;
        try
        {
            var info = TT.GetSystemInfo();
            // platform: ios / android / devtools；真机只会是 ios / android
            dev = info != null && string.Equals(info.platform, "devtools", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[抖音广告] 获取运行环境失败：" + e.Message);
        }

        this.devToolCache = dev;
        return dev;
    }

    /// <summary>AdManager.Update 驱动，处理加载超时（用 realtime，timeScale=0 时也能跑）</summary>
    public override void Tick()
    {
        float now = Time.realtimeSinceStartup;

        if (this.interLoading && now - this.interLoadingStartTime > AD_LOAD_TIMEOUT)
        {
            Debug.LogWarning("[插屏] 加载超时，本次放弃（实例不主动 Destroy，避免和 load Promise 撞车）");
            this.interLoading = false;
            this.interShowPending = false;
            this.interAdIns = null;
            this.interFailCooldownUntil = now + 30f;
        }

        if (this.rewardShowPending && this.rewardLoading && now - this.rewardLoadingStartTime > AD_LOAD_TIMEOUT)
        {
            Debug.LogWarning("[激励视频] 加载超时");
            this.FailReward("广告加载超时，请稍后再试");
        }
    }


    #region 插屏广告

    /// <summary>
    /// 展示插屏。加载没完成时记一个待展示标记，OnLoad 回调里自动补展示，
    /// 这样「第一次打开设置」也能看到广告，而不是只建了个实例就返回。
    /// </summary>
    public override void ShowInterstitialAd(string AdId)
    {
        Debug.Log("播放插屏广告");

        if (!this.IsSdkUsable())
        {
            Debug.LogWarning("[插屏] 抖音 SDK 尚未就绪，本次跳过");
            return;
        }

        // 广告位变化时重建实例，避免继续使用旧广告位
        if (!string.IsNullOrEmpty(AdId) && this.interAdId != AdId)
        {
            this.interAdId = AdId;
            this.interAdIns = null;   // 旧实例交给平台回收，不在这里 Destroy
            this.interLoading = false;
            this.interShowPending = false;
        }

        if (!this.CanShowInterstitial(out string reason))
        {
            Debug.LogWarning("[插屏] 本次不展示：" + reason);
            return;
        }

        var ad = this.EnsureInterstitialAd();
        if (ad == null)
            return;

        if (this.IsInterstitialLoaded(ad))
        {
            this.PresentInterstitial(ad);
        }
        else
        {
            this.interShowPending = true;
            Debug.Log("[插屏] 素材还在加载，加载完成后自动展示");
        }
    }

    private bool CanShowInterstitial(out string reason)
    {
        reason = null;
        float now = Time.realtimeSinceStartup;

        // 开发者工具展示的是模拟广告页，不校验宿主频控；真机必须按平台规则来，否则只会拿到 2001/2002
        if (this.IsDevTool())
            return true;

        if (now - this.moduleStartTime < INTER_LAUNCH_GUARD)
        {
            reason = $"启动 {INTER_LAUNCH_GUARD:0}s 内不展示插屏（还剩 {INTER_LAUNCH_GUARD - (now - this.moduleStartTime):0.0}s）";
            return false;
        }

        if (now - this.lastShowTime < INTER_MIN_INTERVAL)
        {
            reason = $"两次插屏间隔需大于 {INTER_MIN_INTERVAL:0}s（还剩 {INTER_MIN_INTERVAL - (now - this.lastShowTime):0.0}s）";
            return false;
        }

        if (now - this.lastRewardCloseTime < INTER_AFTER_REWARD)
        {
            reason = $"刚看完激励视频，{INTER_AFTER_REWARD:0}s 内不展示插屏";
            return false;
        }

        if (now < this.interFailCooldownUntil)
        {
            reason = "插屏刚刚失败，冷却中";
            return false;
        }

        return true;
    }

    /// <summary>按需创建插屏实例（有就直接复用）。创建后立即 Load，加载完由 OnLoad 接管。</summary>
    private TTInterstitialAd EnsureInterstitialAd()
    {
        if (this.interAdIns != null)
            return this.interAdIns;

        try
        {
            var ad = TT.CreateInterstitialAd(new CreateInterstitialAdParam { InterstitialAdId = this.interAdId });
            this.interAdIns = ad;
            this.interLoading = true;
            this.interLoadingStartTime = Time.realtimeSinceStartup;

            ad.OnLoad += () => this.OnInterstitialLoaded(ad);
            ad.OnError += (code, message) => this.OnInterstitialError(ad, code, message);
            ad.OnClose += () => this.OnInterstitialClosed(ad);

            ad.Load();
            Debug.Log("[插屏] 已创建并开始加载，广告位=" + this.interAdId);
            return ad;
        }
        catch (Exception e)
        {
            Debug.LogError($"[插屏] 创建失败：{e.Message}。请确认广告位 {this.interAdId}："
                           + "①属于当前小游戏 appid；②类型是「插屏」不是「激励视频」；③后台已启用。"
                           + "开发者工具 appid 取自 project.config.json，改完要关闭项目重新导入。");
            this.interAdIns = null;
            this.interLoading = false;
            this.interFailCooldownUntil = Time.realtimeSinceStartup + 30f;
            return null;
        }
    }

    private bool IsInterstitialLoaded(TTInterstitialAd ad)
    {
        try
        {
            return ad.IsLoaded();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[插屏] 查询加载状态异常：{e.Message}");
            return false;
        }
    }

    private void OnInterstitialLoaded(TTInterstitialAd ad)
    {
        if (ad != this.interAdIns)
            return;   // 换广告位后旧实例的回调，忽略，别把新实例弄没了

        this.interLoading = false;
        Debug.Log("[插屏] 素材加载完成");

        if (!this.interShowPending)
            return;

        if (this.CanShowInterstitial(out string reason))
            this.PresentInterstitial(ad);
        else
        {
            this.interShowPending = false;
            Debug.LogWarning("[插屏] 加载完成但已不满足展示条件：" + reason);
        }
    }

    private void PresentInterstitial(TTInterstitialAd ad)
    {
        this.interShowPending = false;
        try
        {
            ad.Show();
            this.lastShowTime = Time.realtimeSinceStartup;
            Debug.Log("[插屏] 已展示");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[插屏] 展示异常：{e.Message}");
            this.interFailCooldownUntil = Time.realtimeSinceStartup + 30f;
        }
    }

    private void OnInterstitialClosed(TTInterstitialAd ad)
    {
        if (ad != this.interAdIns)
            return;

        this.lastShowTime = Time.realtimeSinceStartup;
        this.interLoading = false;
        this.interShowPending = false;
        this.interAdIns = null;   // 插屏只能展示一次，平台会自动销毁；重建留到下次展示时按需做
        Debug.Log("[插屏] 已关闭");

        this.SendGameAddiction();
    }

    private void OnInterstitialError(TTInterstitialAd ad, int code, string message)
    {
        if (ad != this.interAdIns)
            return;

        // 只清引用、不 Destroy：load Promise 还没结束就来 Destroy 会多一条 Uncaught (in promise) API_ERROR
        this.interAdIns = null;
        this.interLoading = false;
        this.interShowPending = false;
        this.interFailCooldownUntil = Time.realtimeSinceStartup + 30f;

        if (code == 158902)
        {
            Debug.LogError($"[插屏] 广告位无效 code=158902（adUnitId 不存在/为空/已关闭/被封禁）：{this.interAdId}");
        }
        else if (code == 158987)
        {
            Debug.LogError($"[插屏] 当前宿主不支持插屏 code=158987（IDE / PC 版抖音常见）：{message}");
        }
        else if (code == 2001 || code == 2002 || code == 2003)
        {
            Debug.LogWarning($"[插屏] 频控 code={code}：{message}");
        }
        else
        {
            Debug.LogWarning($"[插屏] 加载/展示失败 code={code}, msg={message}, 广告位={this.interAdId}");
        }
    }

    #endregion


    #region 侧边栏

    public override void NavigateToScene(Action onSuccess, Action<string> onFail)
    {
        var data = new JsonData { ["scene"] = "sidebar" };
        TT.NavigateToScene(data,
            () =>
            {
                Debug.Log("侧边栏唤起成功");
                onSuccess?.Invoke();
            },
            () => { },
            (errCode, errMsg) =>
            {
                onFail?.Invoke(errMsg);
                Debug.LogError($"侧边栏显示异常:{errCode}, {errMsg}");
            });
    }

    #endregion

    #region 激励视频

    public override void ShowRewardedVideoAd(string videoAdId, Action onSuccess, Action<string> onFail)
    {
        Debug.Log("播放激励视频广告");

        this.prevTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        this.onRewardSuccess = onSuccess;
        this.onRewardFail = onFail;
        Time.timeScale = 0;   // 看广告需要暂停游戏

        if (!this.IsSdkUsable())
        {
            Debug.LogWarning("[激励视频] 抖音 SDK 尚未就绪");
            this.FailReward("广告服务未就绪，请稍后再试");
            return;
        }

        // 广告位变了必须重建，否则会继续播旧广告位
        if (this.rewardedVideoAd != null && this.videoAdId != videoAdId)
            this.DelRewardedVideoAd();

        this.videoAdId = videoAdId;

        if (this.rewardedVideoAd == null)
        {
            if (!this.CreateRewardedVideoAd())
            {
                this.FailReward("广告加载失败，请稍后再试");
                return;
            }
        }

        if (this.rewardLoaded)
        {
            this.ShowRewardedNow();
            return;
        }

        // 未加载完成：先 Load，OnLoad 里再 Show（直接 Show 未加载的实例会失败）
        this.rewardShowPending = true;
        if (!this.rewardLoading)
        {
            try
            {
                this.rewardLoading = true;
                this.rewardLoadingStartTime = Time.realtimeSinceStartup;
                this.rewardedVideoAd.Load();
                Debug.Log("[激励视频] 素材未就绪，开始加载，加载完成后自动播放");
            }
            catch (Exception e)
            {
                Debug.LogError($"[激励视频] 加载异常：{e.Message}");
                this.FailReward("广告加载失败，请稍后再试");
            }
        }
        else
        {
            Debug.Log("[激励视频] 素材加载中，加载完成后自动播放");
        }
    }

    /// <summary>创建激励视频实例并立刻 Load。失败返回 false，由调用方走失败回调。</summary>
    private bool CreateRewardedVideoAd()
    {
        try
        {
            var ad = TT.CreateRewardedVideoAd(new CreateRewardedVideoAdParam { AdUnitId = this.videoAdId });
            this.rewardedVideoAd = ad;
            this.rewardLoaded = false;
            this.rewardLoading = true;
            this.rewardLoadingStartTime = Time.realtimeSinceStartup;

            ad.OnLoad += () => this.OnRewardedLoaded(ad);
            ad.OnError += (code, message) => this.OnRewardedError(ad, code, message);
            ad.OnClose += (ended, count) => this.OnRewardedClosed(ended, count);

            ad.Load();
            Debug.Log("[激励视频] 已创建并开始加载，广告位=" + this.videoAdId);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[激励视频] 创建失败：{e.Message}。请确认广告位 {this.videoAdId} 属于当前小游戏 appid、类型是「激励视频」且已启用");
            this.rewardedVideoAd = null;
            this.rewardLoading = false;
            return false;
        }
    }

    private void OnRewardedLoaded(TTRewardedVideoAd ad)
    {
        if (ad != this.rewardedVideoAd)
            return;

        this.rewardLoaded = true;
        this.rewardLoading = false;
        Debug.Log("[激励视频] 素材加载完成");

        if (this.rewardShowPending)
            this.ShowRewardedNow();
    }

    private void ShowRewardedNow()
    {
        this.rewardShowPending = false;

        if (this.rewardedVideoAd == null)
        {
            this.FailReward("广告加载失败，请稍后再试");
            return;
        }

        try
        {
            this.rewardedVideoAd.Show();
            Debug.Log("[激励视频] 已展示");
        }
        catch (Exception e)
        {
            Debug.LogError($"[激励视频] 展示异常：{e.Message}");
            this.FailReward("广告展示失败，请稍后再试");
        }
    }

    private void OnRewardedClosed(bool ended, int count)
    {
        Debug.Log($"是否看完广告:{ended} 次数:{count}");

        this.rewardLoaded = false;   // 下次播放前要重新 Load
        this.rewardShowPending = false;
        this.rewardLoading = false;
        this.lastRewardCloseTime = Time.realtimeSinceStartup;

        // 先取回调再复位，最后恢复 timeScale —— 顺序不能反：
        // 业务回调里会关弹窗 / 播 DOTween，timeScale 还是 0 的话动画会卡住
        var success = this.onRewardSuccess;
        var fail = this.onRewardFail;
        this.onRewardSuccess = null;
        this.onRewardFail = null;
        Time.timeScale = this.prevTimeScale <= 0f ? 1f : this.prevTimeScale;

        if (ended)
        {
            if (success != null)
                success.Invoke();
            else
                Debug.LogWarning("[激励视频] 看完但业务回调为空，本次没有发奖");

            this.SendGameAddiction();
        }
        else
        {
            Debug.Log("广告未看完，不发放奖励");
            fail?.Invoke("广告未看完");
        }

        // 不在这里立刻重建实例：旧实例销毁是异步的，紧接着 create 容易拿到 createVideoAd:fail。
        // 下一次播放时 ShowRewardedVideoAd 会按需 Load。
    }

    private void OnRewardedError(TTRewardedVideoAd ad, int code, string message)
    {
        if (ad != this.rewardedVideoAd)
            return;

        this.rewardLoaded = false;
        this.rewardLoading = false;

        if (code == 1002)
        {
            Debug.LogError($"[激励视频] 广告位不属于当前小游戏或类型不对 code=1002，广告位={this.videoAdId}。"
                           + "广告位归属只由运行时的小游戏 appid 决定（StarkBuilderSetting.asset 的 _appId / project.config.json）。");
        }
        else
        {
            Debug.LogError($"[激励视频] 播放失败 code={code}, msg={message}, 广告位={this.videoAdId}");
        }

        string friendly = code == 1004 ? "暂无合适广告，请稍后再试" : "广告暂时无法播放，请稍后再试";
        this.FailReward(friendly);
    }

    /// <summary>把激励视频的失败路径统一收口：恢复 timeScale + 复位状态 + 回调业务侧，绝不静默吞掉。</summary>
    private void FailReward(string message)
    {
        var fail = this.onRewardFail;
        this.onRewardSuccess = null;
        this.onRewardFail = null;
        this.rewardShowPending = false;
        this.rewardLoading = false;
        Time.timeScale = this.prevTimeScale <= 0f ? 1f : this.prevTimeScale;

        Debug.LogWarning("[激励视频] 失败：" + message);
        fail?.Invoke(message);
    }

    private void DelRewardedVideoAd()
    {
        var ad = this.rewardedVideoAd;
        this.rewardedVideoAd = null;
        this.rewardLoaded = false;
        this.rewardLoading = false;
        this.rewardShowPending = false;

        try { ad?.Destroy(); }
        catch (Exception e) { Debug.LogWarning($"[激励视频] 销毁实例异常：{e.Message}"); }
    }

    #endregion


    #region 埋点

    private void Login()
    {
        // 1.获取系统信息
        systemInfo = TT.GetSystemInfo();
        if (systemInfo?.system == null)
            return;

        // 2.登录-获取临时登录凭证 
        TT.Login(this.onLoginSuccess, (errMsg) =>
        {
            Debug.LogError("登录失败:" + errMsg);
        }, true);
        // 3.初始化埋点
        this.InitMaidian();
    }

    private void onLoginSuccess(string code, string anonymousCode, bool isLogin)
    {
        // 2.1获取用户的基本信息
        TT.GetUserInfo((ref TTUserInfo scUserInfo) => this.onGetUserInfo(ref scUserInfo, code, anonymousCode),
            (msg) =>
            {
                Debug.LogError($"用户信息获取失败: {msg}");
            });
    }

    private void onGetUserInfo(ref TTUserInfo scUserInfo, string code, string anonymousCode)
    {
        // 2.2向ECPM后台上传设备信息
        Debug.Log(scUserInfo);
        if (AdManager.Instance == null)
            return;

        AdManager.Instance.StartCoroutine(this.SendOpenIdLog(
            miniAppid, scUserInfo.nickName, code, anonymousCode, systemInfo, (success, res) =>
            {
                if (success && res.d != null && res.d.user != null && !string.IsNullOrEmpty(res.d.user.openid))
                {
                    string useropenid = res.d.user.openid;
                    Debug.Log("openidlog==succ== " + res);
                    string currentDate = DateTime.UtcNow.ToString("o");
                    PlayerPrefs.SetString(miniAppid + TT.GetSystemInfo().model, currentDate);
                }
                else
                {
                    Debug.Log("openidlog==fail== " + res);
                }
            }));
    }

    private IEnumerator SendOpenIdLog(string appid, string nickName, string code, string anonymousCode, TTSystemInfo sysinfores, Action<bool, OpenIdResponse> callback)
    {
        var query = $"?appid={Uri.EscapeDataString(appid)}" +
                    $"&nickName={Uri.EscapeDataString(nickName)}" +
                    $"&code={Uri.EscapeDataString(code)}" +
                    $"&anonymousCode={Uri.EscapeDataString(anonymousCode)}" +
                    $"&system={Uri.EscapeDataString(sysinfores.system)}" +
                    $"&platform={Uri.EscapeDataString(sysinfores.platform)}" +
                    $"&brand={Uri.EscapeDataString(sysinfores.brand)}" +
                    $"&modelx={Uri.EscapeDataString(sysinfores.model)}" +
                    $"&version={Uri.EscapeDataString(sysinfores.ttVersion)}" +
                    $"&SDKVersion={Uri.EscapeDataString(sysinfores.sdkVersion)}" +
                    $"&appName={Uri.EscapeDataString(sysinfores.hostName)}";

        var www = UnityWebRequest.Get(this.ecpmURL + query);
        yield return www.SendWebRequest();
        if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.Log("openidlog==fail== " + www.error);
            callback(false, null);
        }
        else
        {
            try
            {
                var response = JsonUtility.FromJson<OpenIdResponse>(www.downloadHandler.text);
                callback(true, response);
            }
            catch (Exception ex)
            {
                Debug.LogError("SendOpenIdLog ERROR：" + ex.Message);
                callback(false, null);
            }
        }
    }

    private void InitMaidian()
    {
        // 3.1获取小游戏启动时的参数
        var launchOptions = TT.GetLaunchOptionsSync();
        Debug.Log($"小游戏启动参数: {launchOptions}");
        if (launchOptions == null || launchOptions.Query == null || !launchOptions.Query.ContainsKey("ad_params"))
        {
            Debug.LogWarning($"小游戏:{miniAppid}没添加埋点参数");
            return;
        }

        string decodedString = Uri.UnescapeDataString(launchOptions.Query["ad_params"]);
        AdParamsData jsonval = JsonUtility.FromJson<AdParamsData>(decodedString);
        string clickid = jsonval?.log_extra?.clickid;
        if (string.IsNullOrEmpty(clickid))
        {
            Debug.LogWarning($"小游戏:{miniAppid}没点击事件埋点");
            return;
        }

        Debug.Log($"clickid: {clickid}");
        PlayerPrefs.SetString(miniAppid + "ClickId", clickid);
        if (!CanClaimReward(miniAppid + "Active"))
        {
            Debug.LogWarning($"小游戏:{miniAppid}奖励已领取，无法重复领取");
            return;
        }

        if (AdManager.Instance != null)
        {
            // 3.2 API上报激活数据
            long timenow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            AdManager.Instance.StartCoroutine(SendConversionRequest(
                "active", clickid, timenow,
                (success, response) =>
                {
                    Debug.Log($"激活埋点事件==={(success ? "success" : "fail")}");
                    Debug.Log($"激活结果={response}");
                }
            ));
        }

        string currentDate = DateTime.UtcNow.ToString("o");
        PlayerPrefs.SetString(miniAppid + "Active", currentDate);
    }

    /*是否允许领取奖励*/
    private bool CanClaimReward(string key)
    {
        string lastClaimDateStr = PlayerPrefs.GetString(miniAppid + key);
        if (string.IsNullOrEmpty(lastClaimDateStr))
        {
            return true;
        }

        if (!DateTime.TryParse(lastClaimDateStr, out DateTime lastClaimDate))
        {
            return true;
        }
        DateTime oneMonthAgo = DateTime.Now.AddMonths(-1);
        return lastClaimDate < oneMonthAgo;
    }

    /// <summary>
    /// API上报关键行为
    /// </summary>
    private void SendGameAddiction()
    {
        string clickid = PlayerPrefs.GetString(miniAppid + "ClickId");
        if (string.IsNullOrEmpty(clickid))
        {
            Debug.LogWarning($"小游戏:{miniAppid}没点击事件埋点");
            return;
        }

        if (AdManager.Instance == null)
            return;

        long timenow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        AdManager.Instance.StartCoroutine(SendConversionRequest(
                "game_addiction", clickid, timenow,
                (success, response) =>
                {
                    Debug.Log($"埋点事件===={(success ? "success" : "fail")}");
                    Debug.Log($"关键行为结果={response}");
                }
            ));
        Debug.Log("已触发激活埋点");
    }

    /// <summary>
    /// 转化回传 API
    /// </summary>
    /// <param name="eventType">回传的事件：</param>
    /// <param name="clickid">clickid 参数</param>
    /// <param name="timestamp">事件发生的毫秒级时间戳</param>
    /// <param name="callback"></param>
    /// https://event-manager.oceanengine.com/docs/8650/app_api_docs
    /// <returns></returns>
    public IEnumerator SendConversionRequest(string eventType, string clickid, long timestamp, Action<bool, string> callback)
    {
        var data = new ConversionData
        {
            event_type = eventType,
            context = new Context { ad = new Ad { callback = clickid } },
            timestamp = timestamp
        };

        string json = JsonUtility.ToJson(data);
        byte[] postData = System.Text.Encoding.UTF8.GetBytes(json);

        Dictionary<string, string> headers = new Dictionary<string, string>();
        headers.Add("Content-Type", "application/json");

        using var www = new UnityWebRequest(conversionURL, "POST");
        www.uploadHandler = new UploadHandlerRaw(postData);
        www.downloadHandler = new DownloadHandlerBuffer();
        foreach (var header in headers)
        {
            www.SetRequestHeader(header.Key, header.Value);
        }
        yield return www.SendWebRequest();
        if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
        {
            callback(false, www.error);
        }
        else
        {
            callback(true, www.downloadHandler.text);
        }
    }
    public override void ShareGame(Action<bool> callBack)
    {
        // 通用分享
        JsonData shareJson = new JsonData();
        shareJson["channel"] = "video";
        shareJson["title"] = "Some Title";
        shareJson["extra"] = new JsonData();
        shareJson["extra"]["videoPath"] = "/xxx/xxx.mp4";//录屏分享的话，路径是 OnRecordComplete 拿到的路径
        JsonData videoTopics = new JsonData();
        videoTopics.SetJsonType(JsonType.Array);
        videoTopics.Add("Some Topic1");
        videoTopics.Add("Some Topic2");
        shareJson["extra"]["videoTopics"] = videoTopics;
        shareJson["extra"]["hashtag_list"] = videoTopics;

        Debug.Log($"ShareAppMessageBtnClicked jsonData: {videoTopics.ToJson()}");
        TT.ShareAppMessage(videoTopics, (data) =>
        {
            callBack?.Invoke(true);
            Debug.Log($"ShareAppMessage success: {videoTopics.ToJson()}");//
        },
            (errMsg) =>
            {
                callBack?.Invoke(false);
                Debug.Log($"ShareAppMessage failed: {errMsg}");
            },
            () =>
            {
                callBack?.Invoke(false);
                Debug.Log($"ShareAppMessage cancel");
            });
    }

    #endregion
}
