using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.IO;
using System;
using System.Text.RegularExpressions;

public class ElevenLabs_VAD : MonoBehaviour
{
    [Header("ElevenLabs 設定")]
    [SerializeField] private string apiKey = "YOUR_API_KEY_HERE";

    [Header("UI 元件")]
    [SerializeField] private Button recordButton;
    [SerializeField] private Text statusText;
    [SerializeField] private Text transcriptionText;
    [SerializeField] private Text currentMicname;
    [SerializeField] private Image vadIndicator; // VAD 指示器（可選）
    [SerializeField] private Sprite[] vadIndicatorSprites; // VAD 指示器（可選）
    [SerializeField] private Animator TalkLight_Animator; // VAD 指示器（可選）


    [Header("錄音設定")]
    [SerializeField] private int maxRecordingTime = 30;
    [SerializeField] private int sampleRate = 16000;

    [Header("VAD 設定")]
    [SerializeField] private bool useVAD = true; // 是否啟用 VAD
    [SerializeField] private float vadUpdateRate = 0.1f; // VAD 更新頻率（秒）
    [SerializeField] private float vadCheckWindow = 1.5f; // VAD 檢查窗口（秒）
    [SerializeField] private float vadEnergyThreshold = 0.005f; // 能量閾值（已調高）
    [SerializeField] private float vadStopTime = 2.5f; // 靜音多久後自動停止（秒）
    [SerializeField] private bool dropSilencePart = true; // 是否裁掉最後的靜音段

    [Header("麥克風重連設定")]
    [SerializeField] private float micHealthCheckInterval = 1.0f;  // 每幾秒檢查一次麥克風健康
    [SerializeField] private float micStuckPositionTimeout = 3.0f; // position 幾秒沒變化視為斷線
    [SerializeField] private int maxReconnectAttempts = 3;         // 最多嘗試重連次數
    [SerializeField] private float reconnectDelay = 1.5f;          // 每次重連間隔（秒）

    private AudioClip recordedClip;
    private string microphoneDevice;
    public bool isRecording = false;
    private bool isVoiceDetected = false;
    private float? vadStopBegin = null;
    private float lastVadCheckTime = 0f;
    private int recordingStartPosition = 0;

    // 麥克風重連相關
    private float lastMicHealthCheckTime = 0f;
    private int lastKnownMicPosition = -1;
    private float lastPositionChangeTime = 0f;
    private bool isMicDisconnected = false;
    private int reconnectAttemptCount = 0;
    private bool isReconnecting = false;

    private const string SCRIBE_API_URL = "https://api.elevenlabs.io/v1/speech-to-text";

    [Header("操作防彈跳")]
    [SerializeField] private float toggleCooldown = 5f; // NEW: 冷卻時間（秒）
    private float lastToggleTime = -999f;               // NEW: 上一次觸發時間

    void Start()
    {
        if (Microphone.devices.Length > 0)
        {
            for (int i = 0; i < Microphone.devices.Length; i++)
            {
                Debug.Log(i + Microphone.devices[i]);
            }
            microphoneDevice = Microphone.devices[0];

            UpdateStatus("準備就緒，點擊按鈕開始錄音");
            currentMicname.text = "Mic Name : " + microphoneDevice;
        }
        else
        {
            UpdateStatus("錯誤：找不到麥克風裝置");
            currentMicname.text = "Mic Name : No mic";
            recordButton.interactable = false;
            return;
        }

        //recordButton.onClick.AddListener(ToggleRecording);
        UpdateButtonText();
    }

    void Update()
    {
        if (!isRecording || !useVAD) return;

        // ── 麥克風健康檢查 ──
        if (Time.time - lastMicHealthCheckTime >= micHealthCheckInterval)
        {
            lastMicHealthCheckTime = Time.time;
            CheckMicrophoneHealth();
        }

        // 如果麥克風已斷線，不繼續做 VAD
        if (isMicDisconnected) return;

        // 定期執行 VAD 檢測
        if (Time.time - lastVadCheckTime >= vadUpdateRate)
        {
            lastVadCheckTime = Time.time;
            CheckVoiceActivity();
        }

        // 檢查是否需要自動停止
        if (vadStopBegin.HasValue)
        {
            float silenceDuration = Time.time - vadStopBegin.Value;
            if (silenceDuration >= vadStopTime)
            {
                Debug.Log($"檢測到 {silenceDuration:F2} 秒靜音，自動停止錄音");
                StopRecording();
            }
        }
    }

    // ════════════════════════════════════════════
    //  麥克風健康檢查與重連
    // ════════════════════════════════════════════

    /// <summary>檢查目前 microphoneDevice 是否仍在系統裝置清單中</summary>
    bool IsMicrophoneConnected(string deviceName)
    {
        foreach (string d in Microphone.devices)
        {
            if (d == deviceName) return true;
        }
        return false;
    }
    /// <summary>在錄音中定期呼叫，偵測 position 是否停止增長（表示斷線）</summary>
    void CheckMicrophoneHealth()
    {
        // 先確認裝置是否還在清單
        if (!IsMicrophoneConnected(microphoneDevice))
        {
            Debug.LogWarning($"[Mic] 裝置 '{microphoneDevice}' 已從系統消失！");
            HandleMicDisconnect("麥克風裝置已拔除");
            return;
        }

        int currentPos = Microphone.GetPosition(microphoneDevice);

        // GetPosition 回傳 -1 或與上次完全相同超過 timeout → 視為異常
        if (currentPos < 0)
        {
            Debug.LogWarning("[Mic] GetPosition 回傳 -1，麥克風可能斷線");
            HandleMicDisconnect("麥克風訊號中斷 (pos = -1)");
            return;
        }

        if (currentPos != lastKnownMicPosition)
        {
            // position 有在動，正常
            lastKnownMicPosition = currentPos;
            lastPositionChangeTime = Time.time;
        }
        else
        {
            // position 卡住，計時
            float stuck = Time.time - lastPositionChangeTime;
            if (stuck >= micStuckPositionTimeout)
            {
                Debug.LogWarning($"[Mic] Position 已 {stuck:F1} 秒未變化，視為斷線");
                HandleMicDisconnect($"麥克風無訊號超過 {micStuckPositionTimeout} 秒");
            }
        }
    }
    /// <summary>斷線處理：停止錄音狀態並啟動重連流程</summary>
    void HandleMicDisconnect(string reason)
    {
        if (isMicDisconnected) return; // 避免重複觸發

        isMicDisconnected = true;
        Debug.LogError($"[Mic] 斷線原因：{reason}");
        UpdateStatus($"⚠ 麥克風斷線：{reason}，嘗試重連...");

        // 安全地結束現有錄音（不送 API，直接清理）
        if (Microphone.IsRecording(microphoneDevice))
        {
            Microphone.End(microphoneDevice);
        }

        // 重置 UI 指示
        if (vadIndicator != null)
        {
            vadIndicator.sprite = vadIndicatorSprites[0];
            TalkLight_Animator.Play("Talk_Disable");
        }

        reconnectAttemptCount = 0;
        StartCoroutine(ReconnectCoroutine());
    }
    /// <summary>依設定間隔不斷嘗試重連，直到成功或超過最大次數</summary>
    IEnumerator ReconnectCoroutine()
    {
        isReconnecting = true;
        recordButton.interactable = false;

        while (reconnectAttemptCount < maxReconnectAttempts)
        {
            reconnectAttemptCount++;
            UpdateStatus($"🔄 重連中... ({reconnectAttemptCount}/{maxReconnectAttempts})");
            Debug.Log($"[Mic] 重連嘗試 {reconnectAttemptCount}/{maxReconnectAttempts}");

            yield return new WaitForSeconds(reconnectDelay);

            // 重新掃描裝置清單
            string[] devices = Microphone.devices;
            if (devices.Length == 0)
            {
                Debug.LogWarning("[Mic] 仍找不到任何麥克風裝置");
                continue;
            }

            // 優先找回原本的裝置；找不到就用第一個可用裝置
            string newDevice = microphoneDevice;
            bool foundOriginal = false;
            foreach (string d in devices)
            {
                if (d == microphoneDevice) { foundOriginal = true; break; }
            }
            if (!foundOriginal)
            {
                newDevice = devices[0];
                Debug.Log($"[Mic] 原裝置未找回，改用：{newDevice}");
            }

            // 嘗試測試錄音（短暫 Start 確認裝置可用）
            AudioClip testClip = Microphone.Start(newDevice, false, 1, sampleRate);
            yield return new WaitForSeconds(0.3f);
            int testPos = Microphone.GetPosition(newDevice);
            Microphone.End(newDevice);

            if (testPos > 0)
            {
                // 重連成功
                microphoneDevice = newDevice;
                currentMicname.text = "Mic Name : " + microphoneDevice;
                isMicDisconnected = false;
                isReconnecting = false;
                reconnectAttemptCount = 0;
                lastPositionChangeTime = Time.time;
                lastKnownMicPosition = 0;
                recordButton.interactable = true;

                // 若原本正在錄音，自動恢復錄音
                if (isRecording)
                {
                    Debug.Log("[Mic] 重連成功，恢復錄音");
                    UpdateStatus("✅ 麥克風已重連，繼續錄音");
                    RestartRecordingAfterReconnect();
                }
                else
                {
                    UpdateStatus("✅ 麥克風已重連，準備就緒");
                }
                yield break;
            }
            else
            {
                Debug.LogWarning($"[Mic] 重連嘗試 {reconnectAttemptCount} 失敗（pos={testPos}）");
            }
        }

        // 超過最大重連次數
        isRecording = false;
        isReconnecting = false;
        UpdateStatus("❌ 麥克風重連失敗，請手動重新插上裝置後按下「重新整理」");
        UpdateButtonText();
        recordButton.interactable = true; // 仍開放讓使用者手動觸發
        Debug.LogError("[Mic] 超過最大重連次數，放棄重連");
    }
    /// <summary>重連成功後，重新啟動錄音（保持 isRecording = true 的狀態）</summary>
    void RestartRecordingAfterReconnect()
    {
        isVoiceDetected = false;
        vadStopBegin = null;
        lastVadCheckTime = Time.time;
        lastPositionChangeTime = Time.time;
        lastKnownMicPosition = 0;

        recordedClip = Microphone.Start(microphoneDevice, false, maxRecordingTime, sampleRate);
        recordingStartPosition = 0;

        if (vadIndicator != null)
        {
            vadIndicator.sprite = vadIndicatorSprites[1];
            TalkLight_Animator.Play("Talk_Active");
        }
    }
    void CheckVoiceActivity()
    {
        int currentPosition = Microphone.GetPosition(microphoneDevice);
        Debug.Log($"【VAD Debug】Position: {currentPosition}, StartPos: {recordingStartPosition}");  // 新增

        if (currentPosition < 0 || recordedClip == null) return;

        // 計算要分析的樣本數量（確保正值）
        int samplesToCheck = Mathf.Min(
            (int)(vadCheckWindow * sampleRate),
            Mathf.Max(0, currentPosition - recordingStartPosition)  // 修正：確保正值
        );

        if (samplesToCheck <= 0) return;

        // 修正：處理循環position（Microphone常wrap around）
        int totalSamples = recordedClip.samples;
        int startPosition = (currentPosition - samplesToCheck + totalSamples) % totalSamples;
        Debug.Log($"【VAD Debug】SamplesCheck: {samplesToCheck}, StartPos: {startPosition}");  // 新增

        // 取得最近的音訊資料
        float[] samples = new float[samplesToCheck * recordedClip.channels];
        recordedClip.GetData(samples, startPosition);

        // 新增：檢查是否全零
        bool allZero = true;
        for (int i = 0; i < samples.Length; i++)
        {
            if (Mathf.Abs(samples[i]) > 0.001f)
            {
                allZero = false;
                break;
            }
        }
        Debug.Log($"【VAD Debug】AllZero: {allZero}");

        // 計算音訊能量（RMS）
        float energy = CalculateRMS(samples);
        Debug.Log($"【VAD Debug】能量: {energy:F4}, 閾值: {vadEnergyThreshold:F4}");

        bool voiceDetected = energy > vadEnergyThreshold;
        // 狀態改變時的處理
        if (voiceDetected != isVoiceDetected)
        {
            isVoiceDetected = voiceDetected;

            if (voiceDetected)
            {
                // 偵測到語音，重置靜音計時器
                vadStopBegin = null;
                UpdateStatus("錄音中... (偵測到語音)");
            }
            else
            {
                // 偵測到靜音，開始計時
                vadStopBegin = Time.time;
                UpdateStatus("錄音中... (靜音中)");
            }

            // 更新 VAD 指示器顏色
            UpdateVADIndicator(voiceDetected);

            Debug.Log($"VAD 狀態改變: {(voiceDetected ? "語音" : "靜音")}, 能量: {energy:F4}");
        }
    }

    float CalculateRMS(float[] samples)
    {
        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += samples[i] * samples[i];
        }
        return Mathf.Sqrt(sum / samples.Length);
    }

    void UpdateVADIndicator(bool isActive)
    {
        if (vadIndicator != null)
        {
            vadIndicator.sprite = isActive ? vadIndicatorSprites[1] : vadIndicatorSprites[0];
        }
    }

    public bool Talkbool()
    {
        if (Time.time - lastToggleTime < toggleCooldown)
        {
            float remain = toggleCooldown - (Time.time - lastToggleTime);
            Debug.Log($"ToggleRecording 冷卻中，剩餘 {remain:F2} 秒");
            return false;
        }
        lastToggleTime = Time.time;
        return true;
    }

    public void ToggleRecording()
    {
        if (isRecording)
        {
            StopRecording();
        }
        else
        {
            StartRecording();
        }
    }

    void StartRecording()
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_API_KEY_HERE")
        {
            UpdateStatus("錯誤：請先設定 API Key");
            return;
        }

        isRecording = true;
        isVoiceDetected = false;
        vadStopBegin = null;
        lastVadCheckTime = Time.time;

        UpdateStatus("錄音中...");
        UpdateButtonText();
        transcriptionText.text = "";

        // 開始錄音
        recordedClip = Microphone.Start(microphoneDevice, false, maxRecordingTime, sampleRate);
        recordingStartPosition = 0;

        // 初始化 VAD 指示器
        if (vadIndicator != null)
        {
            vadIndicator.sprite = vadIndicatorSprites[1];
            TalkLight_Animator.Play("Talk_Active");
        }
    }

    void StopRecording()
    {
        if (!isRecording) return;

        isRecording = false;
        int recordPosition = Microphone.GetPosition(microphoneDevice);
        Microphone.End(microphoneDevice);

        UpdateStatus("處理中...");
        UpdateButtonText();

        // 重置 VAD 指示器
        if (vadIndicator != null)
        {
            vadIndicator.sprite = vadIndicatorSprites[0];
            TalkLight_Animator.Play("Talk_Disable");
        }

        // 計算實際錄音長度並裁掉靜音部分
        float dropTime = 0f;
        if (useVAD && dropSilencePart && vadStopBegin.HasValue)
        {
            dropTime = vadStopTime;
        }

        // 傳送音訊到 ElevenLabs
        StartCoroutine(SendAudioToElevenLabs(recordPosition, dropTime));
    }

    IEnumerator SendAudioToElevenLabs(int recordPosition = -1, float dropTimeSec = 0f)
    {
        // 將 AudioClip 轉換為 WAV 格式
        byte[] wavData = ConvertAudioClipToWav(recordedClip, recordPosition, dropTimeSec);

        if (wavData == null || wavData.Length == 0)
        {
            UpdateStatus("錯誤：音訊轉換失敗");
            yield break;
        }

        List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
        formData.Add(new MultipartFormFileSection("file", wavData, "recording.wav", "audio/wav"));
        formData.Add(new MultipartFormDataSection("model_id", "scribe_v1"));
        formData.Add(new MultipartFormDataSection("language_code", "zh"));

        UnityWebRequest request = UnityWebRequest.Post(SCRIBE_API_URL, formData);
        request.SetRequestHeader("xi-api-key", apiKey);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string jsonResponse = request.downloadHandler.text;
            Debug.Log($"API 回應：{jsonResponse}");
            ProcessTranscription(jsonResponse);
        }
        else
        {
            UpdateStatus($"錯誤：{request.error}");
            Debug.LogError($"API 錯誤：{request.downloadHandler.text}");
        }
    }

    void ProcessTranscription(string jsonResponse)
    {
        try
        {
            ScribeResponse response = JsonUtility.FromJson<ScribeResponse>(jsonResponse);

            if (!string.IsNullOrEmpty(response.text))
            {
                // 同時移除：半形括號 (...) 與 全形括號 （...）
                // \([^)]*\)   處理半形 ()
                // |           或
                // （[^）]*）  處理全形 （）
                string tempwords = Regex.Replace(
                    response.text,
                    @"\([^)]*\)|（[^）]*）",
                    ""
                ); 

            // 去掉前後空白
            tempwords = tempwords.Trim();

                string trad = FontConvert.Instance.ConvertToTraditional(tempwords);
                Debug.Log(trad);
                transcriptionText.text = trad;

                WebRTCManager.instance.SendMessage(transcriptionText.text);
                ChatManager.instance.AddUserMessage(transcriptionText.text);
                UpdateStatus("辨識完成！");
            }
            else
            {
                UpdateStatus("未辨識到文字內容");
            }
        }
        catch (Exception e)
        {
            UpdateStatus("錯誤：解析回應失敗");
            Debug.LogError($"解析錯誤：{e.Message}\n回應內容：{jsonResponse}");
        }
    }

    byte[] ConvertAudioClipToWav(AudioClip clip, int recordPosition = -1, float dropTimeSec = 0f)
    {
        if (clip == null) return null;

        // 計算實際要使用的樣本數量
        int totalSamples = recordPosition > 0 ? recordPosition : clip.samples;
        int dropSamples = Mathf.RoundToInt(dropTimeSec * clip.frequency);
        int useSamples = Mathf.Max(0, totalSamples - dropSamples);

        if (useSamples == 0)
        {
            Debug.LogWarning("裁剪後音訊長度為 0");
            return null;
        }

        Debug.Log($"原始樣本: {totalSamples}, 裁掉: {dropSamples}, 使用: {useSamples}");

        float[] samples = new float[useSamples * clip.channels];
        clip.GetData(samples, 0);

        // 轉換為 16-bit PCM
        short[] intData = new short[samples.Length];
        byte[] bytesData = new byte[samples.Length * 2];

        float rescaleFactor = 32767;

        for (int i = 0; i < samples.Length; i++)
        {
            intData[i] = (short)(samples[i] * rescaleFactor);
            byte[] byteArr = BitConverter.GetBytes(intData[i]);
            byteArr.CopyTo(bytesData, i * 2);
        }

        // 建立 WAV 檔案
        using (MemoryStream stream = new MemoryStream())
        {
            int channels = clip.channels;
            int sampleRate = clip.frequency;

            stream.Write(System.Text.Encoding.UTF8.GetBytes("RIFF"), 0, 4);
            stream.Write(BitConverter.GetBytes(bytesData.Length + 36), 0, 4);
            stream.Write(System.Text.Encoding.UTF8.GetBytes("WAVE"), 0, 4);
            stream.Write(System.Text.Encoding.UTF8.GetBytes("fmt "), 0, 4);
            stream.Write(BitConverter.GetBytes(16), 0, 4);
            stream.Write(BitConverter.GetBytes((short)1), 0, 2);
            stream.Write(BitConverter.GetBytes((short)channels), 0, 2);
            stream.Write(BitConverter.GetBytes(sampleRate), 0, 4);
            stream.Write(BitConverter.GetBytes(sampleRate * channels * 2), 0, 4);
            stream.Write(BitConverter.GetBytes((short)(channels * 2)), 0, 2);
            stream.Write(BitConverter.GetBytes((short)16), 0, 2);
            stream.Write(System.Text.Encoding.UTF8.GetBytes("data"), 0, 4);
            stream.Write(BitConverter.GetBytes(bytesData.Length), 0, 4);
            stream.Write(bytesData, 0, bytesData.Length);

            return stream.ToArray();
        }
    }

    void UpdateStatus(string message)
    {
        statusText.text = message;
        Debug.Log(message);
    }

    void UpdateButtonText()
    {
        Text buttonText = recordButton.GetComponentInChildren<Text>();
        if (buttonText != null)
        {
            buttonText.text = isRecording ? "停止錄音" : "開始錄音";
        }
    }

    void OnDestroy()
    {
        if (isRecording)
        {
            Microphone.End(microphoneDevice);
        }
    }

    [System.Serializable]
    public class ScribeResponse
    {
        public string text;
        public string language;
    }
}
