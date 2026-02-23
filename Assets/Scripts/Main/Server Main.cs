using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using static WebRTCManager;
using Random = UnityEngine.Random;

public class ServerMain : MonoBehaviour
{
    public static ServerMain instance { get; private set; }
    [Header("Network System")]
    public TcpServerAdvanced TcpServer;
    [Header("TTS System")]
    public ElevenLabs_VAD TTS_System;

    [Header("Lumina Animator")]
    public LuminaCharatorAnimatorController Lumina_Animtor;

    public Lumina_Custom_Audio LuminaAudio;

    [Header("指引文字文字")]
    public Text UI_TipText;

    
    [Header("UI動畫")]
    public Animator UI_Animtor;

    [Header("問答次數顯示文字")]
    public Text QACountText;
    [Header("問答次數")]
    public int QACount;

    [Header("問答倒數系統")]
    public CountdownBarController CountDownTimer;

    [Header("ARD")]
    public ArduinoBasic ARD;

    public enum Stage
    {
        waitforDeal,
        Sleep,  //休眠等待
        Opening,
        Lottery,
        TossingGame,  //
        TossingFailed,
        TossingSuccessful,
        FreeQA,
        End
    }
    public Stage currentStage;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(instance == null) instance = this;
        _init();
    }

    // Update is called once per frame
    void Update()
    {
        if ((Input.GetKeyUp(KeyCode.T) || ARD.readMessage == "Coin")&& TTS_System.Talkbool())
        {
            ARD.readMessage = "";
            if (QACount > 0 || CountDownTimer.remainingTime <= 0)
            {
                QACount--;
                QACountText.text = "剩餘問答次數：" + QACount;
                TTS_System.ToggleRecording();
                AudioPlayer.instance.PlayAudio(0); //Play Coin Talk Audio
                AvatarSkipConversation();
            }
            else
            {
                StartCoroutine(ServerMain.instance.EndAction());
            }
        }
        if (Input.GetKeyUp(KeyCode.Y))
        {
            AvatarSkipConversation();
        }
        if (Input.GetKeyUp(KeyCode.R))
        {
            TcpServer.SendCommandToAll("RESET");
            ServerAllReset();
        }
        switch (currentStage)
        {
           case Stage.FreeQA:
                //問答中
                if ((Input.GetKeyUp(KeyCode.T) || ARD.readMessage == "Coin"))
                {
                    AvatarSkipConversation();
                    ARD.readMessage = "";
                    if (QACount > 0 || CountDownTimer.remainingTime <= 0)
                    {
                        QACount--;
                        QACountText.text = "剩餘問答次數：" + QACount;
                        TTS_System.ToggleRecording();
                        AudioPlayer.instance.PlayAudio(0); //Play Coin Talk Audio
                    }
                    else
                    {
                        StartCoroutine(ServerMain.instance.EndAction());
                    }
                }
                break;
        }
    }
    public void NextStage(Stage nextstage)=> currentStage = nextstage;

    void _init()
    {
        CountDownTimer.ResetAndPause(); //問答倒數重製
        currentStage = Stage.Sleep;
        Lumina_Animtor.IdleLoop = true;
        QACount = 5;
        QACountText.text = "剩餘問答次數：" + QACount;
    }
    /// <summary>
    /// 重製對話
    /// </summary>
    public void AvatarClearConversation()
    {
        var resetCommand = new CommandData
        {
            cmd = "res_1",
            arg = new ResetArg { reason = "conversation" },
            ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            v = 1
        };

        WebRTCManager.instance.SendJsonMessage(resetCommand, "command");
    }
    /// <summary>
    /// 跳過對話，進入下一段對話
    /// </summary>
    public void AvatarSkipConversation()
    {
        // 建立 skip 指令
        var skipCommand = new CommandData
        {
            cmd = "skip",
            arg = new SkipArg { reason = "user_interrupt" },
            ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            v = 1
        };

        // 發送
        WebRTCManager.instance.SendJsonMessage(skipCommand, "command");

    }

    //----------------------------------------Server發送指令至Client-------------------------------



    //----------------------------------------接收到Client端指令，執行功能-------------------------------
    //----------------------------------------------------//
    //所有由Client主程式傳送到Server的指令如下：
    //TcpClient.SendActionToServer("WAKEUP"); //傳送給Server，請LUMINA打招呼。
    //TcpClient.SendActionToServer("LOTTERY"); //傳送給Server，通知"現在要抽籤了"。
    //TcpClient.SendActionToServer("GETNUMBER", coinGame.LucykNumber); //傳遞"取得籤號"資訊給主機... (待定) 播放3D動畫、UI動畫、改變籤詩牆面燈號(打開閃爍)。
    //TcpClient.SendActionToServer("TOSSINGFAILED", coinGame.LucykNumber); //傳遞"擲筊失敗"資訊給主機... (待定) 播放3D動畫、UI動畫、改變籤詩牆面燈號(關閉閃爍)。
    //TcpClient.SendActionToServer("FREEQA",coinGame.LucykNumber); //傳遞籤號資訊給主機，要求進入解籤環節。  播放3D動畫、UI動畫、改變籤詩牆面燈號(恆亮)。
    //TcpClient.SendActionToServer("RESET"); //傳送給Server，狀態重置到初始狀態Sleep。

    /// <summary>
    /// Sleep中，搖晃設備喚醒
    /// </summary>
    public IEnumerator GotWakeUpAction()
    {
        NextStage(Stage.Opening);  //進入喚醒狀態
        AvatarSkipConversation();
        float audioLength = LuminaAudio.LuminaAudioClip_Open.length;
        LuminaAudio.PlayCustomAudio(LuminaAudio.LuminaAudioClip_Open); //嘴型跟音檔。
        Lumina_Animtor.PlaySingleAnimation("OP-01", true);
        UI_Animtor.Play("SleepToOpen");  //UI動畫
        yield return new WaitForSeconds(audioLength); //等待音檔播放完畢
        TcpServer.SendCommandToAll("SERVERCALLBACK");  //通知Client端，動畫結束了，可以進入下一步了。
        NextStage(Stage.Lottery);  //進入抽籤環節
        UI_TipText.text = "請搖晃手上的LUMINA籤筒！";   //Canvas 擲筊說明UI
    }
    /// <summary>
    /// 接收到搖晃籤筒訊號的動作
    /// </summary>
    public void StartLotteryAction()
    {
        UI_TipText.text = "搖啊搖，搖到什麼籤～";
        Lumina_Animtor.PlaySingleAnimation("HL-C00", true, () => //抽籤動畫。
        {
            //動畫結束後要做的事情
            //LuminaAudio.PlayCustomAudio(LuminaAudio.LuminaAudioClips[0]); //嘴型跟音檔。
            //ToStage(1); //進入擲筊環節
            //TcpServer.SendCommandToAll("Tossing");  //打開擲筊設備
            //Canvas 擲筊說明UI
        });
    }
    /// <summary>
    /// 抽到籤號後，提醒搖晃設備擲筊
    /// </summary>
    public IEnumerator GETLotteryNumberAction(int LuckyNumber)
    {
        UI_TipText.text = "請問LUMINA，我的命運是屬於是這支，第" + LuckyNumber + "籤嗎？";
        TossingWallManager.Instance.SetCheckingNumber(LuckyNumber);  //新增閃爍狀態
        int randomIndex = Random.Range(0, LuminaAudio.LuminaAudioClips_Tossing.Length);
        float audioLength = LuminaAudio.LuminaAudioClips_Tossing[randomIndex].length;
        LuminaAudio.PlayCustomAudio(LuminaAudio.LuminaAudioClips_Tossing[Random.Range(0, randomIndex)]); //嘴型跟音檔。
        Lumina_Animtor.PlaySingleAnimation("HL-C02", true); //抽到什麼籤的動畫。
        NextStage(Stage.waitforDeal);
        yield return new WaitForSeconds(audioLength); //等待音檔播放完畢
        TcpServer.SendCommandToAll("SERVERCALLBACK");
        NextStage(Stage.TossingGame);  //進入擲筊遊戲
        UI_TipText.text = "擲筊中！";
        NextStage(Stage.waitforDeal);
        UI_Animtor.Play("ToTossingGame");  //UI動畫
        yield return null; //等待音檔播放完畢
    }
    
    /// <summary>
    /// 擲筊失敗後的動作
    /// </summary>
    public IEnumerator TossingFailedAction(int LuckyNumber)
    {
        UI_TipText.text = "很可惜看來這支籤跟妳不是很合，我們重新再抽一支吧！";
        NextStage(Stage.TossingFailed);
        UI_Animtor.Play("TossingFaild");  //UI動畫
        
        TossingWallManager.Instance.ClearAll();  //清除閃爍狀態
        int randomIndex = Random.Range(0, LuminaAudio.LuminaAudioClips_TossingFailed.Length);
        float audioLength = LuminaAudio.LuminaAudioClips_TossingFailed[randomIndex].length;
        LuminaAudio.PlayCustomAudio(LuminaAudio.LuminaAudioClips_TossingFailed[Random.Range(0, randomIndex)]); //嘴型跟音檔。

        Lumina_Animtor.PlaySingleAnimation("Re-Q", true); //抽到什麼籤的動畫。
        yield return new WaitForSeconds(audioLength); //等待音檔播放完畢
        CoinFlipGame.instance.ResetCoins();
        TcpServer.SendCommandToAll("SERVERCALLBACK");  //通知Client端，動畫結束了，可以進入下一步了。
        NextStage(Stage.Lottery);  //進入擲筊遊戲                                                              
        //Canvas 擲筊說明UI

    }
    public void TossingSuccessfulAction(int LuckyNumber)
    {
        UI_TipText.text = "讓LUMINA幫你看看第" + LuckyNumber + "是什樣的命運吧～";
        NextStage(Stage.waitforDeal);
        UI_Animtor.Play("TossingSuccessful");  //UI動畫
        AudioPlayer.instance.PlayAudio(2, 3);
        TossingWallManager.Instance.ConfirmCurrent(LuckyNumber);  //清除閃爍狀態，確認籤詩
        SendLuckyNumToCHT(LuckyNumber);
        Lumina_Animtor.PlaySingleAnimation("HL-E01", true, () => //擲筊成功的動畫。
        {
            //動畫結束後要做的事情
            NextStage(Stage.FreeQA);  //進入擲筊遊戲                                                              
            UI_Animtor.Play("ToQA");  //UI動畫
            //Canvas 擲筊說明UI
            UI_TipText.text = "LUMINA正在為您解籤！";
            CountDownTimer.ResetAndStart(); //開始計時
        });
    }
    /// <summary>
    /// 傳送籤號給CHT AI後台
    /// </summary>
    /// <param name="luckynumData"></param>
    public void SendLuckyNumToCHT(int luckynumData)=> WebRTCManager.instance.SendMessage("我抽到了第" + luckynumData + "籤，可以幫我解籤嗎?", "chat");

    public IEnumerator EndAction()
    {
        NextStage(Stage.End);  //進入喚醒狀態
        UI_Animtor.Play("ToEnd");  //UI動畫
        int randomIndex = Random.Range(0, LuminaAudio.LuminaAudioClips_End.Length);
        float audioLength = LuminaAudio.LuminaAudioClips_End[randomIndex].length;
        LuminaAudio.PlayCustomAudio(LuminaAudio.LuminaAudioClips_End[randomIndex]); //嘴型跟音檔。
        Lumina_Animtor.PlaySingleAnimation("Reset", true);
        yield return new WaitForSeconds(audioLength); //等待音檔播放完畢
        NextStage(Stage.Sleep);  //進入抽籤環節
        UI_TipText.text = "請搖晃手上的LUMINA籤筒，喚醒LUMINA！";  //Canvas 擲筊說明UI
        TcpServer.SendCommandToAll("SERVERCALLBACK");  //通知Client端，動畫結束了，可以進入下一步了。
        TcpServer.SendCommandToAll("RESET");
        ServerAllReset();
    }

    /// <summary>
    /// 重製，並回到Sleep狀態。
    /// </summary>
    public void ServerAllReset()
    {
        currentStage = Stage.Sleep;
        Lumina_Animtor.PlaySingleAnimation("Reset",true);
        TossingWallManager.Instance.ClearAll();  //新增閃爍狀態
        QACount = 5;
        QACountText.text = "剩餘問答次數：" + QACount;
        ChatManager.instance.ClearAllMessages();
        CoinFlipGame.instance.ResetCoins();
        AvatarClearConversation();
    }
}
