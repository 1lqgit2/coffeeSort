using System;

public abstract class IAdPlatform
{
    /* 调起侧边栏 */
    public abstract void NavigateToScene(Action onSuccess, Action<string> onFail);

    /* 激励视频广告 */
    public abstract void ShowRewardedVideoAd(string adId, Action onSuccess, Action<string> onFail);

    /* 插屏广告 */
    public abstract void ShowInterstitialAd(string adId);

    /** 游戏分享 */
    public abstract void ShareGame(Action<bool> callBack);

    /// <summary>
    /// 每帧心跳，由 AdManager.Update 驱动，用来做「加载超时」「挂起回调兜底」。
    /// 默认空实现，平台各自按需重写。用 realtime，激励视频期间 Time.timeScale=0 也能跑。
    /// </summary>
    public virtual void Tick() { }
}
