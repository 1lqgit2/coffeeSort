using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIBuyItem : UIDialog
{
    [Header("UI Components")]
    public Text uiTextTitle;
    public Text uiTextDesc;
    public Text uiTextReward1; 
    public Text uiTextReward2;
    public Text uiTextCoin;
    public Image rewardIcon1;
    public Image rewardIcon2;
    public Sprite[] rewardSpriteArr;

    public Button uiBtnBuy;    
    public Button uiBtnWatch; 
    public Button uiBtnClose; 

    private int boosterAmountCoin = 1;
    private int boosterAmountVideo = 1; 
    private int boosterPrice = 2000;

    private BoosterButton.BoosterType currentBoosterType;

    private void Start()
    {
        uiBtnBuy.onClick.AddListener(BuyBoosterWithCoin);
        uiBtnWatch.onClick.AddListener(BuyBoosterWithVideo);
        uiBtnClose.onClick.AddListener(Hide);
        boosterAmountCoin = 1;
        boosterAmountVideo = 1;
        boosterPrice = 2000;
    }

    /// <summary>
    /// Show Dialog with booster data
    /// </summary>
    public void Show(BoosterButton.BoosterType boosterType)
    {
        base.Show();
        GameManager.Instance.currentState = GameState.InPause;
        currentBoosterType = boosterType;

        uiTextTitle.text = GetBoosterName(boosterType);
        uiTextDesc.text = GetBoosterDesc(boosterType);
        uiTextReward1.text = boosterAmountCoin.ToString();
        uiTextReward2.text = "x " + boosterAmountVideo;
        uiTextCoin.text = boosterPrice.ToString();

        rewardIcon1.sprite = rewardSpriteArr[(int)boosterType];
        rewardIcon2.sprite = rewardSpriteArr[(int)boosterType];
    }

    /// <summary>
    /// 道具类型的中文标题。原来直接用 ToString()，界面上会显示 Slot / Order / Undo。
    /// </summary>
    private static string GetBoosterName(BoosterButton.BoosterType type)
    {
        switch (type)
        {
            case BoosterButton.BoosterType.Slot: return "空位";
            case BoosterButton.BoosterType.Order: return "订单";
            case BoosterButton.BoosterType.Undo: return "撤销";
            default: return type.ToString();
        }
    }

    /// <summary>
    /// 道具卡片下方的描述文案。
    /// </summary>
    private static string GetBoosterDesc(BoosterButton.BoosterType type)
    {
        switch (type)
        {
            case BoosterButton.BoosterType.Slot: return "增加一个空位";
            case BoosterButton.BoosterType.Order: return "增加一个订单";
            case BoosterButton.BoosterType.Undo: return "增加一次撤销";
            default: return string.Empty;
        }
    }

    /// <summary>
    /// Buy booster with coin
    /// </summary>
    private void BuyBoosterWithCoin()
    {
        if (GameManager.Instance.HasEnoughCoins(boosterPrice))
        {
            GameManager.Instance.SpendCoins(boosterPrice);
            GameManager.Instance.MoreBooster(boosterAmountCoin, currentBoosterType);
            Hide();
        }
        else
        {
            GameManager.Instance.uiManager.uiToast.ShowToast("金币不足！");
        }
    }

    /// <summary>
    /// Buy booster with video
    /// </summary>
    private void BuyBoosterWithVideo()
    {
        WatchAds();
    }

    public void WatchAds()
    {
        // 抖音激励视频：看完才发奖，中途退出或失败给提示
        AdsControl.Instance.ShowRewardedAd(
            () =>
            {
                GameManager.Instance.MoreBooster(boosterAmountVideo, currentBoosterType);
                RefreshBoosterUI();
                Hide();
                // 明确告诉玩家拿到了什么，避免「广告播完但界面看不出变化」
                GameManager.Instance.uiManager.uiToast.ShowToast(
                    "领取成功：" + GetBoosterName(currentBoosterType) + " x" + boosterAmountVideo, 1.5f);
            },
            err => GameManager.Instance.uiManager.uiToast.ShowToast("广告未看完，无法领取", 1.5f));
    }

    private void RefreshBoosterUI()
    {
        if (currentBoosterType == BoosterButton.BoosterType.Slot)
        {
           GameManager.Instance.uiManager.uiGame.btnAddSlot.UpdateBoosterState();
        }
        else  if (currentBoosterType == BoosterButton.BoosterType.Order)
        {
             GameManager.Instance.uiManager.uiGame.btnOrder.UpdateBoosterState();
        }
        else if (currentBoosterType == BoosterButton.BoosterType.Undo)
        {
            GameManager.Instance.uiManager.uiGame.btnUndo.UpdateBoosterState();
        }
    }

    public void Hide()
    {
        base.Hide();
        GameManager.Instance.currentState = GameState.Playing;
    }
}
