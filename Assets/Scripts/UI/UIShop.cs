using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using System;
using DG.Tweening;

public class UIShop : UIPanel
{
    [Header("Shop Items")]
    public List<ShopItem> shopItems; 
    public List<Transform> shopItemObjects;

    public Button shopPack;

    public Button closeBtn;

    public Button restoreBtn;

    [Header("Animate Items")]
    [SerializeField] private GameObject[] shopAnimateItems; 
    [SerializeField] private float itemDelay = 0.05f; 
    [SerializeField] private float animationDuration = 0.5f;
    [SerializeField] private Ease animationEase = Ease.OutBack;

    private void Start()
    {
        LoadShopItems();
    }

    private void LoadShopItems()
    {
        shopPack.onClick.AddListener(() => BuyPack());
        restoreBtn.onClick.AddListener(() => Restore());
        closeBtn.onClick.AddListener(() => Hide());

        for (int i = 0; i < shopItems.Count; i++)
        {
            if (i >= shopItemObjects.Count)
                continue;

            ShopItem item = shopItems[i];
            Transform itemUI = shopItemObjects[i];

            //itemUI.Find("BG/Icon").GetComponent<Image>().sprite = item.icon;
            itemUI.Find("BG/uiTxtValue").GetComponent<Text>().text = item.coinAmount > 0 ? $"x{item.coinAmount}" : "";
            itemUI.Find("BG/uiBtnPurchase/uiTxtValue").GetComponent<Text>().text = item.isPurchased ? "已拥有" : "看广告";

            Button purchaseButton = itemUI.Find("BG/uiBtnPurchase").GetComponent<Button>();
            purchaseButton.onClick.RemoveAllListeners();
            purchaseButton.onClick.AddListener(() => PurchaseItem(item, itemUI));

            // Xử lý Countdown Timer
            if (item.isLimitedTime)
            {
                itemUI.Find("BG/uiBtnPurchase/uiTxtValue").GetComponent<Text>().text = "免费";
                StartCoroutine(HandleLimitedTimeItem(item, itemUI));
            }
        }
    }

    public void ShowEffect()
    {
        
        foreach (var item in shopAnimateItems)
        {
            item.transform.localScale = Vector3.zero;
            item.gameObject.SetActive(false);
        }

        ShowShop();
    }

    /// <summary>
    /// Show UI Shop with effect
    /// </summary>
    public void ShowShop()
    {
        StartCoroutine(AnimateShopItems());
    }

    private IEnumerator AnimateShopItems()
    {
        for (int i = 0; i < shopAnimateItems.Length; i++)
        {
            shopAnimateItems[i].gameObject.SetActive(true);
            shopAnimateItems[i].transform.DOScale(Vector3.one, animationDuration)
                .SetEase(animationEase);
            yield return new WaitForSeconds(itemDelay);
        }
    }

    private void BuyPack()
    {
        SoundManager.Instance.PlaySFX(SoundManager.Instance.clickSfx);
    }

    private void Restore()
    {
        SoundManager.Instance.PlaySFX(SoundManager.Instance.clickSfx);
        // 内购已移除，抖音小游戏无跨设备购买记录可恢复
        GameManager.Instance.uiManager.uiToast.ShowToast("小游戏无需恢复购买", 1.5f);
    }

    private IEnumerator HandleLimitedTimeItem(ShopItem item, Transform itemUI)
    {
        Transform cooldownUI = itemUI.Find("BG/Cooldown");
        Text cooldownText = cooldownUI.Find("Text").GetComponent<Text>();
        Button purchaseButton = itemUI.Find("BG/uiBtnPurchase").GetComponent<Button>();

        while (true)
        {
            float remainingTime = item.GetRemainingTime();

            if (remainingTime > 0)
            {
                purchaseButton.gameObject.SetActive(false);
                cooldownUI.gameObject.SetActive(true);
                cooldownText.text = FormatTime(remainingTime);
            }
            else
            {
                purchaseButton.gameObject.SetActive(true);
                cooldownUI.gameObject.SetActive(false);
            }

            yield return new WaitForSeconds(1f);
        }
    }

    private void PurchaseItem(ShopItem item, Transform itemUI)
    {
        SoundManager.Instance.PlaySFX(SoundManager.Instance.clickSfx);
        if (item.isPurchased)
        {
            Debug.Log($"{item.itemName} 已购买过");
            return;
        }

        if (item.isLimitedTime)
        {
            long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            PlayerPrefs.SetString(item.GetLastPurchaseKey(), currentTime.ToString());
            PlayerPrefs.Save();
            WatchAds();
            StartCoroutine(HandleLimitedTimeItem(item, itemUI));
            return;
        }

        // 内购（IAP）已移除：所有礼包统一改成看激励视频领取
        AdsControl.Instance.ShowRewardedAd(
            () => GrantPackage(item),
            err => GameManager.Instance.uiManager.uiToast.ShowToast("广告未看完，无法领取", 1.5f));
    }

    /// <summary>
    /// 看完广告后发放礼包内容。
    /// </summary>
    private void GrantPackage(ShopItem item)
    {
        if (item.itemId == Config.IAPPackageID.NoAdsPack)
        {
            // 去广告礼包附赠的金币必须跟 Inspector 配置一致。
            // 这里以前写死 AddCoins(2000)，导致「x1000」档位实发 2000。
            int coins = item.coinAmount > 0 ? item.coinAmount : 2000;   // 没配金币时才退回 2000 兜底
            GameManager.Instance.AddCoins(coins);
            AdsControl.Instance.RemoveAds();
            GameManager.Instance.uiManager.uiToast.ShowToast($"+{coins} 金币，已去除广告", 1.5f);
        }
        else if (item.itemId == Config.IAPPackageID.NoAds)
        {
            AdsControl.Instance.RemoveAds();
            GameManager.Instance.uiManager.uiToast.ShowToast("已去除广告", 1.5f);
        }
        else if (item.coinAmount > 0)
        {
            GameManager.Instance.AddCoins(item.coinAmount);
            GameManager.Instance.uiManager.uiToast.ShowToast("+" + item.coinAmount + " 金币", 1.5f);
        }

        Debug.Log($"领取成功: {item.itemName}");
    }

    private string FormatTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60);
        int secs = Mathf.FloorToInt(seconds % 60);
        return $"{minutes:D2}:{secs:D2}";
    }

    public override void Show()
    {
        base.Show();
        GameManager.Instance.currentState = GameState.InPause;
    }

    public override void Hide()
    {
        SoundManager.Instance.PlaySFX(SoundManager.Instance.clickSfx);
        base.Hide();
        GameManager.Instance.currentState = GameState.Playing;
    }

    /// <summary>
    /// 限时免费项：看激励视频领 50 金币
    /// </summary>
    public void WatchAds()
    {
        AdsControl.Instance.ShowRewardedAd(
            () => GameManager.Instance.AddCoins(50),
            err => GameManager.Instance.uiManager.uiToast.ShowToast("广告未看完，无法领取", 1.5f));
    }
}

[System.Serializable]
public class ShopItem
{
    public Config.IAPPackageID itemId;
    public string itemName; 
    public Sprite icon; 
    public int coinAmount; 
    public float price;
    public bool isLimitedTime;
    public bool isPurchased;
    private const int cooldownDuration = 3600;

    public string GetLastPurchaseKey() => $"LastPurchase_{itemId}";

    public float GetRemainingTime()
    {
        if (!PlayerPrefs.HasKey(GetLastPurchaseKey()))
            return 0;

        long lastPurchaseTime = long.Parse(PlayerPrefs.GetString(GetLastPurchaseKey(), "0"));
        long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        long elapsedTime = currentTime - lastPurchaseTime;
        return Mathf.Max(cooldownDuration - elapsedTime, 0);
    }
}