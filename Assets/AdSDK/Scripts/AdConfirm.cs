using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 激励广告弹窗
/// </summary>
public class AdConfirm : MonoBehaviour
{
    private static AdConfirm instance;

    [SerializeField] private Button btnClose;
    [SerializeField] private Button btnConfirm;
    private Action onCompleted;
    private Action onClosed;

    /// <summary>
    /// 弹出激励视频确认框
    /// </summary>
    /// <param name="callBack">看完整条广告后执行（发奖励）</param>
    /// <param name="onClose">弹窗被关闭时执行（点返回或奖励发放完毕后都会触发），可空</param>
    public static void Show(Action callBack, Action onClose = null)
    {
        if (instance == null)
        {
            // 加载 AdConfirm 预制体
            GameObject prefab = Resources.Load<GameObject>("AdConfirm");
            if (prefab == null)
            {
                Debug.LogError("AdConfirm 预制体未找到，请确保其位于 Resources 文件夹中。");
                return;
            }

            // 实例化 AdConfirm
            GameObject adConfirmInstance = Instantiate(prefab);
            instance = adConfirmInstance.GetComponent<AdConfirm>();

            if (instance == null)
            {
                Debug.LogError("AdConfirm 脚本未附加到预制体上，请检查预制体设置。");
                Destroy(adConfirmInstance);
                return;
            }
        }

        // 设置回调并显示弹窗
        instance.onCompleted = callBack;
        instance.onClosed = onClose;
        instance.gameObject.SetActive(true);
        instance.transform.SetAsLastSibling();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        gameObject.SetActive(false);
        btnClose?.onClick.AddListener(this.OnClose);
        btnConfirm?.onClick.AddListener(this.OnConfirm);
    }


    private void OnClose()
    {
        this.gameObject.SetActive(false);

        var closed = onClosed;
        onClosed = null;
        closed?.Invoke();
    }

    void OnConfirm()
    {
#if UNITY_EDITOR
        onCompleted?.Invoke();
        this.OnClose();
#else
        // 优先走 AdsControl（它会在场景里没有 AdManager 时懒创建一个），
        // 直接摸 AdManager.Instance 在"本局还没播过广告"时是 null，会白失败一次。
        if (AdsControl.Instance != null)
        {
            AdsControl.Instance.ShowRewardedAd(OnAdWatched, OnAdError);
            return;
        }

        if (AdManager.Instance != null)
        {
            AdManager.Instance.ShowRewardedVideoAd(OnAdWatched, OnAdError);
            return;
        }

        Debug.LogError("[广告] 未找到 AdsControl / AdManager，无法播放广告");
        OnAdError("广告服务未初始化");
#endif
    }

    private void OnAdWatched()
    {
        Debug.Log("广告观看完成");
        onCompleted?.Invoke();
        OnClose();
    }

    private void OnAdError(string errorMessage)
    {
        Debug.LogError($"广告播放失败: {errorMessage}");
        // 加载 AdError 预制体
        GameObject adErrorPrefab = Resources.Load<GameObject>("AdError");
        if (adErrorPrefab != null)
        {
            // 实例化 AdError
            GameObject adErrorInstance = Instantiate(adErrorPrefab, transform.position, Quaternion.identity);
            // 1 秒后销毁实例
            Destroy(adErrorInstance, 1.5f);
        }
        else
        {
            Debug.LogError("AdError 预制体未找到，请确保其位于 Resources 文件夹中。");
        }
    }
}
