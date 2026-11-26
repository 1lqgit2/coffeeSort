using UnityEngine;
using UnityEngine.UI;

public class UIHeartInfo : UIDialog
{
    [SerializeField] private Text uiHeartCount;
    [SerializeField] private Text uiTxtFull;
    [SerializeField] private GameObject uiTimerObject;
    [SerializeField] private Text timerText;
    [SerializeField] private GameObject buyBtnNode;
    [SerializeField] private Button uiBtnAd;
    [SerializeField] private Button uiBtnCoin;
    [SerializeField] private Button uiBtnClose;

    private void Start()
    {
        uiBtnAd.onClick.AddListener(OnWatchAd);
        uiBtnCoin.onClick.AddListener(OnBuyHeart);
        uiBtnClose.onClick.AddListener(Hide);
    }

    public override void Show()
    {
        base.Show();
        UpdateHeartUI();
    }

    /// <summary>
    /// Update UI Heart Info
    /// </summary>
    private void UpdateHeartUI()
    {
        int currentHeart = HeartManager.Instance.GetHeart();
        uiHeartCount.text = currentHeart.ToString();

        if (currentHeart >= 5)
        {
            buyBtnNode.SetActive(false);
            uiTxtFull.gameObject.SetActive(true);
            uiTimerObject.SetActive(false);
        }
        else
        {
            buyBtnNode.SetActive(true);
            uiTxtFull.gameObject.SetActive(false);
            uiTimerObject.SetActive(HeartManager.Instance.HasHeartTimer());

            if (uiTimerObject.activeSelf)
            {
                UpdateTimer();
            }
        }
    }

    private void UpdateTimer()
    {
        int remainingTime = HeartManager.Instance.GetHeartTimer();
        timerText.text = FormatTime(remainingTime);
        InvokeRepeating(nameof(UpdateCountdown), 1f, 1f);
    }

    private void UpdateCountdown()
    {
        int remainingTime = HeartManager.Instance.GetHeartTimer();
        timerText.text = FormatTime(remainingTime);

        if (remainingTime <= 0)
        {
            CancelInvoke(nameof(UpdateCountdown));
            HeartManager.Instance.AddHeart();
            UpdateHeartUI();
        }
    }

    private string FormatTime(int seconds)
    {
        int minutes = seconds / 60;
        int sec = seconds % 60;
        return $"{minutes:D2}:{sec:D2}";
    }

    private void OnWatchAd()
    {
        SoundManager.Instance.PlaySFX(SoundManager.Instance.clickSfx);
        WatchAds();
    }

    private void OnBuyHeart()
    {
        SoundManager.Instance.PlaySFX(SoundManager.Instance.clickSfx);
        if (GameManager.Instance.GetCoin() >= 500)
        {
            GameManager.Instance.SpendCoins(500);
            HeartManager.Instance.RefillHeart();
            UpdateHeartUI();
        }
    }

    private void OnClaimFreeHeart()
    {
        SoundManager.Instance.PlaySFX(SoundManager.Instance.clickSfx);
        WatchAds();
    }

    public override void Hide()
    {
        SoundManager.Instance.PlaySFX(SoundManager.Instance.clickSfx);
        base.Hide();
    }

    public void WatchAds()
    {
        // 抖音激励视频：看完才补一颗心
        AdsControl.Instance.ShowRewardedAd(
            () =>
            {
                HeartManager.Instance.AddHeart();
                UpdateHeartUI();
                GameManager.Instance.uiManager.uiToast.ShowToast("体力 +1", 1.5f);
            },
            err => GameManager.Instance.uiManager.uiToast.ShowToast("广告未看完，无法补充", 1.5f));
    }
}