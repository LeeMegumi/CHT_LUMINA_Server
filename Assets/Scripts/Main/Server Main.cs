using NUnit.Framework.Internal;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public class ServerMain : MonoBehaviour
{
    [Header("Network System")]
    public TcpServerAdvanced TcpServer;
    [Header("TTS System")]
    public ElevenLabs_VAD TTS_System;

    [Header("Lumina Animator")]
    public LuminaCharatorAnimatorController Lumina_Animtor;

    public Lumina_Custom_Audio LuminaAudio;

    [Header("測試用文字")]
    public Text UIText;
    [Header("籤詩牆")]
    public TossingWall tossingwall;

    public enum LuminaState
    {
        Taking,  //說話中
        Sleep,  //休眠待機
        Idle,  //一般無說話待機
    }

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
        currentStage = Stage.Sleep;
        //TcpServer Send- update current Stage
        Lumina_Animtor.IdleLoop = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyUp(KeyCode.Space))
        {
            //TcpServer.SendCommandToAll("To_Tossing_Loop");  
            Lumina_Animtor.PlaySingleAnimation("HL-G01", returnToIdleLoop: true);
            Lumina_Animtor.PlaySingleAnimation("HL-G01", false, () =>
            {
                //動畫結束後要做的事情
            }
            );
        }
        if (Input.GetKeyUp(KeyCode.T))
        {
            TTS_System.ToggleRecording();
        }
        if (Input.GetKeyUp(KeyCode.Y))
        {
            WebRTCManager.instance.SendMessage("請問你叫什麼名字?", "chat");
        }
        switch (currentStage)
        {
           

        }
    }
    public void NextStage(Stage nextstage)=> currentStage = nextstage;

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
    public void GotWakeUpAction()
    {
        NextStage(Stage.Opening);  //進入喚醒狀態
        LuminaAudio.PlayCustomAudio(LuminaAudio.LuminaAudioClips[0]); //嘴型跟音檔。
        Lumina_Animtor.PlaySingleAnimation("OP-01", true, () =>
        {
            //動畫結束後要做的事情
            NextStage(Stage.Lottery);  //進入抽籤環節

            //Canvas 擲筊說明UI
            UIText.text = "請搖晃手上的LUMINA籤筒，抽出屬於你的幸運號碼！";
        });
    }
    /// <summary>
    /// 接收到搖晃籤筒訊號的動作
    /// </summary>
    public void StartLotteryAction()
    {
        UIText.text = "搖啊搖，搖到什麼籤～";
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
    public void GETLotteryNumberAction(int LuckyNumber)
    {
        UIText.text = "請問LUMINA，我的命運是屬於是這支，第" + LuckyNumber + "籤嗎？\n搖動手上的籤筒來擲筊吧～";
        tossingwall.SetCheckingState(LuckyNumber);  //新增閃爍狀態
        NextStage(Stage.waitforDeal);
        //LuminaAudio.PlayCustomAudio(LuminaAudio.LuminaAudioClips[0]); //嘴型跟音檔。
        Lumina_Animtor.PlaySingleAnimation("HL-C02", true, () => //抽到什麼籤的動畫。
        {
            //動畫結束後要做的事情
            NextStage(Stage.TossingGame);  //進入擲筊遊戲                                                              

            //Canvas 擲筊說明UI
        });
    }
    /// <summary>
    /// 擲筊失敗後的動作
    /// </summary>
    public void TossingFailedAction(int LuckyNumber)
    {
        UIText.text = "很可惜看來這支籤跟妳不是很合，我們重新再抽一支吧！";
        NextStage(Stage.TossingFailed);
        tossingwall.SetClearState();  //清除閃爍狀態
        //LuminaAudio.PlayCustomAudio(LuminaAudio.LuminaAudioClips[0]); //嘴型跟音檔。
        Lumina_Animtor.PlaySingleAnimation("Re-Q", true, () => //抽到什麼籤的動畫。
        {
            //動畫結束後要做的事情
            NextStage(Stage.Lottery);  //進入擲筊遊戲                                                              
            //Canvas 擲筊說明UI

        });
    }
    public void TossingSuccessfulAction(int LuckyNumber)
    {
        UIText.text = "讓LUMINA幫你看看第" + LuckyNumber + "是什樣的命運吧～";
        NextStage(Stage.waitforDeal);
        tossingwall.SetConfirmState();  //清除閃爍狀態，確認籤詩

        //LuminaAudio.PlayCustomAudio(LuminaAudio.LuminaAudioClips[0]); //嘴型跟音檔。
        Lumina_Animtor.PlaySingleAnimation("HL-E01", true, () => //擲筊成功的動畫。
        {
            //動畫結束後要做的事情
            NextStage(Stage.FreeQA);  //進入擲筊遊戲                                                              
            SendLuckyNumToCHT(LuckyNumber);
            //Canvas 擲筊說明UI
            UIText.text = "LUMINA正在為您解籤！";
        });
    }
    /// <summary>
    /// 傳送籤號給CHT AI後台
    /// </summary>
    /// <param name="luckynumData"></param>
    public void SendLuckyNumToCHT(int luckynumData)=> WebRTCManager.instance.SendMessage("我抽到了第" + luckynumData + "籤，可以幫我看看嗎?", "chat");

    /// <summary>
    /// 重製，並回到Sleep狀態。
    /// </summary>
    public void AllReset()
    {
        currentStage = Stage.Sleep;
        Lumina_Animtor.PlaySingleAnimation("Reset",true);
        tossingwall.SetClearState();  //新增閃爍狀態

    }
}
