using UnityEngine;
using UnityEngine.UI;

public class UISetting : UIDialog
{
    [Header("Buttons")]
    public Button btnClose;
    public Button btnHome;
    public Button btnSound;
    public Button btnMusic;
    public Button btnShock;
    public Button btnRemoveAds;
    public Button btnRestorePurchase;
    public Button btnTerm;
    public Button btnPolicy;

    [Header("Icons")]
    public Image iconSound;
    public Image iconMusic;
    public Image iconShock;

    [Header("Sprites")]
    public Sprite spriteOn;
    public Sprite spriteOff;

    /// <summary>上次请求插屏的时间，防止一次打开被重复请求（InitUI / Show 会连着调两次）</summary>
    private float lastInterstitialRequestTime = -999f;

    private void Start()
    {
        // Gán sự kiện nút
        btnClose.onClick.AddListener(Hide);
        btnHome.onClick.AddListener(OnHomePressed);
        btnSound.onClick.AddListener(ToggleSound);
        btnMusic.onClick.AddListener(ToggleMusic);
        btnShock.onClick.AddListener(ToggleShock);
        btnRemoveAds.onClick.AddListener(RemoveAds);
        btnRestorePurchase.onClick.AddListener(RestorePurchase);
        btnTerm.onClick.AddListener(Term);
        btnPolicy.onClick.AddListener(Policy);

        UpdateUI();
    }

    public void InitUI()
    {
        if (GameManager.Instance.currentState == GameState.Playing)
            Show(true);
        else
            Show(false);
    }

    /// <summary>
    /// Update UI when opening Setting
    /// </summary>
    public void Show(bool isGameScene)
    {
        base.Show();
        if (GameManager.Instance.currentState == GameState.Playing)
            GameManager.Instance.currentState = GameState.InPause;
        btnHome.gameObject.SetActive(isGameScene);

        // 打开设置面板时展示一次插屏（已去广告 / 频控未过会由广告层自动跳过）
        TryShowInterstitial();
    }

    /// <summary>
    /// 打开设置时请求插屏广告。真正的频控（启动30s / 两次间隔60s / 激励后60s）在 TikTokPlatform 里，
    /// 这里只做「同一次打开不要重复请求」的去重。
    /// </summary>
    private void TryShowInterstitial()
    {
        float now = Time.realtimeSinceStartup;
        if (now - lastInterstitialRequestTime < 3f)
            return;

        lastInterstitialRequestTime = now;

        if (AdsControl.Instance == null)
        {
            Debug.LogWarning("[设置面板] 场景里没有 AdsControl，插屏跳过");
            return;
        }

        AdsControl.Instance.ShowInterstitialAd();
    }

    /// <summary>
    /// Update UI of sound, music, shock
    /// </summary>
    private void UpdateUI()
    {
        iconSound.sprite = GameManager.Instance.IsSoundOn() ? spriteOn : spriteOff;
        iconMusic.sprite = GameManager.Instance.IsMusicOn() ? spriteOn : spriteOff;
        iconShock.sprite = GameManager.Instance.IsShockOn() ? spriteOn : spriteOff;
    }

    /// <summary>
    /// Handle toggle sound
    /// </summary>
    private void ToggleSound()
    {
        GameManager.Instance.ToggleSound();
        UpdateUI();
    }

    /// <summary>
    /// Handle toggle music
    /// </summary>
    private void ToggleMusic()
    {
        SoundManager.Instance.PlaySFX(SoundManager.Instance.clickSfx);
        GameManager.Instance.ToggleMusic();
        UpdateUI();
    }

    /// <summary>
    /// Handle toggle shock
    /// </summary>
    private void ToggleShock()
    {
        SoundManager.Instance.PlaySFX(SoundManager.Instance.clickSfx);
        GameManager.Instance.ToggleShock();
        UpdateUI();
    }

    /// <summary>
    /// Handle Remove Ads
    /// </summary>
    private void RemoveAds()
    {
        SoundManager.Instance.PlaySFX(SoundManager.Instance.clickSfx);

        if (AdsControl.Instance.IsRemoveAds())
        {
            GameManager.Instance.uiManager.uiToast.ShowToast("已去除广告", 1.5f);
            return;
        }

        // 内购已移除：去广告改为看激励视频领取
        AdsControl.Instance.ShowRewardedAd(
            () =>
            {
                AdsControl.Instance.RemoveAds();
                GameManager.Instance.uiManager.uiToast.ShowToast("已去除广告", 1.5f);
            },
            err => GameManager.Instance.uiManager.uiToast.ShowToast("广告未看完，无法领取", 1.5f));
    }

    /// <summary>
    /// Handle Restore Purchase
    /// </summary>
    private void RestorePurchase()
    {
        SoundManager.Instance.PlaySFX(SoundManager.Instance.clickSfx);
        // 内购已移除，抖音小游戏无需恢复购买
        GameManager.Instance.uiManager.uiToast.ShowToast("小游戏无需恢复购买", 1.5f);
    }

    /// <summary>
    /// Handle Home Button → Return to UI Home
    /// </summary>
    private void OnHomePressed()
    {
        //SoundManager.Instance.PlaySFX(SoundManager.Instance.clickSfx);
        Hide();
        GameManager.Instance.ReturnToHome();
    }

    public override void Hide()
    {
        SoundManager.Instance.PlaySFX(SoundManager.Instance.clickSfx);
        base.Hide();
    }

    /// <summary>
    /// Opens the Terms of Service URL
    /// </summary>
    public void Term()
    {
        string termsUrl = "https://www.google.com"; // Replace with actual URL
        Application.OpenURL(termsUrl);
    }

    /// <summary>
    /// Opens the Privacy Policy URL
    /// </summary>
    public void Policy()
    {
        string policyUrl = "https://www.google.com"; // Replace with actual URL
        Application.OpenURL(policyUrl);
    }
}