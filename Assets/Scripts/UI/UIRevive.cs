using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class UIRevive : UIDialog
{
    public Button btnPlayByAd;
    public Button btnPlayByCoin;
    public Button btnClose;

    private void Start()
    {
        btnPlayByAd.onClick.AddListener(OnPlayByAd);
        btnPlayByCoin.onClick.AddListener(OnPlayByCoin);
        btnClose.onClick.AddListener(OnClose);
    }

    private void OnPlayByAd()
    {
        SoundManager.Instance.PlaySFX(SoundManager.Instance.clickSfx);
        WatchAds();

    }

    private void OnPlayByCoin()
    {
        SoundManager.Instance.PlaySFX(SoundManager.Instance.clickSfx);
        if (GameManager.Instance.GetCoin() >= 900)
        {
            GameManager.Instance.SpendCoins(900);
            GameManager.Instance.UnlockSlot();
            HideWithEffect();
        }
        else
        {
            GameManager.Instance.uiManager.uiShop.Show();
            GameManager.Instance.uiManager.uiShop.ShowEffect();
        }
    }

    private void OnClose()
    {
        SoundManager.Instance.PlaySFX(SoundManager.Instance.clickSfx);
        HideWithEffect();
        GameManager.Instance.uiManager.uiGameFail.Show();

    }

    public void WatchAds()
    {
        // 抖音激励视频：看完才复活
        AdsControl.Instance.ShowRewardedAd(
            () =>
            {
                GameManager.Instance.UnlockSlot();
                HideWithEffect();
            },
            err => GameManager.Instance.uiManager.uiToast.ShowToast("广告未看完，无法复活", 1.5f));
    }
}