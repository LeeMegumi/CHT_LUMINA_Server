using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TossingWall : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int columns = 10; // 橫排10個
    [SerializeField] private int rows = 6; // 豎排6個
    [SerializeField] private Vector2 cellSize = new Vector2(154f, 154f); // 調整為適合1920x1080
    [SerializeField] private Vector2 spacing = new Vector2(15f, 15f); // 調整間距

    [Header("Color Settings")]
    [SerializeField] private Color clearColor = new Color(100f / 255f, 100f / 255f, 100f / 255f, 1f); // 灰色
    [SerializeField] private Color checkingColor = new Color(255f / 255f, 190f / 255f, 100f / 255f, 1f); // 黃色
    [SerializeField] private float transitionDuration = 0.5f; // 漸變時間
    [SerializeField] private float blinkInterval = 0.8f; // 閃爍間隔

    [Header("Prefab Settings")]
    [SerializeField] private GameObject imagePrefab; // 如果需要使用預製體

    private Dictionary<int, Image> imageDict = new Dictionary<int, Image>();
    private Dictionary<int, Text> textDict = new Dictionary<int, Text>();
    private Dictionary<int, Coroutine> transitionCoroutines = new Dictionary<int, Coroutine>();

    public enum WallState
    {
        Clear,
        Checking,
        Confirm
    }

    private WallState currentState = WallState.Clear;
    private int currentCheckingNumber = -1;

    void Start()
    {
        InitializeWall();
    }

    /// <summary>
    /// 初始化牆面UI
    /// </summary>
    void InitializeWall()
    {
        // 設置 Canvas
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            gameObject.AddComponent<CanvasScaler>();
            gameObject.AddComponent<GraphicRaycaster>();
        }

        // 創建Grid容器
        GameObject gridContainer = new GameObject("GridContainer");
        gridContainer.transform.SetParent(transform, false);

        RectTransform gridRect = gridContainer.AddComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0.5f, 0.5f);
        gridRect.anchorMax = new Vector2(0.5f, 0.5f);
        gridRect.pivot = new Vector2(0.5f, 0.5f);

        // 添加 GridLayoutGroup
        GridLayoutGroup gridLayout = gridContainer.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = cellSize;
        gridLayout.spacing = spacing;
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = columns;
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        gridLayout.childAlignment = TextAnchor.MiddleCenter;

        // 創建60個Image方塊
        int totalCells = rows * columns;
        for (int i = 1; i <= totalCells; i++)
        {
            CreateImageCell(i, gridContainer.transform);
        }

        // 調整容器大小以適應所有格子
        float totalWidth = (cellSize.x * columns) + (spacing.x * (columns - 1));
        float totalHeight = (cellSize.y * rows) + (spacing.y * (rows - 1));
        gridRect.sizeDelta = new Vector2(totalWidth, totalHeight);
    }

    /// <summary>
    /// 創建單個Image格子
    /// </summary>
    void CreateImageCell(int number, Transform parent)
    {
        GameObject cell = new GameObject(number.ToString());
        cell.transform.SetParent(parent, false);

        // 添加Image組件
        Image image = cell.AddComponent<Image>();
        image.color = clearColor;
        imageDict.Add(number, image);

        // 創建Text子物件顯示編號
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(cell.transform, false);

        Text text = textObj.AddComponent<Text>();
        text.text = number.ToString();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 24;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        textDict.Add(number, text);
    }

    /// <summary>
    /// 設置為Clear狀態 - 全部歸零為灰色
    /// </summary>
    public void SetClearState()
    {
        StopAllTransitions();
        currentState = WallState.Clear;
        currentCheckingNumber = -1;

        foreach (var kvp in imageDict)
        {
            StartTransition(kvp.Key, clearColor, false);
        }
    }

    /// <summary>
    /// 設置為Checking狀態 - 指定編號開始閃爍
    /// </summary>
    public void SetCheckingState(int number)
    {
        if (number < 1 || number > imageDict.Count)
        {
            Debug.LogWarning($"Invalid number: {number}. Must be between 1 and {imageDict.Count}");
            return;
        }

        // 先清除所有其他格子
        StopAllTransitions();
        foreach (var kvp in imageDict)
        {
            if (kvp.Key != number)
            {
                StartTransition(kvp.Key, clearColor, false);
            }
        }

        currentState = WallState.Checking;
        currentCheckingNumber = number;

        // 開始閃爍
        StartBlinking(number);
    }

    /// <summary>
    /// 設置為Confirm狀態 - 停止閃爍並固定在黃色
    /// </summary>
    public void SetConfirmState()
    {
        if (currentState != WallState.Checking || currentCheckingNumber == -1)
        {
            Debug.LogWarning("Cannot confirm: no number is currently checking");
            return;
        }

        currentState = WallState.Confirm;

        // 停止閃爍並固定在黃色
        StopTransition(currentCheckingNumber);
        StartTransition(currentCheckingNumber, checkingColor, false);
    }

    /// <summary>
    /// 開始閃爍效果
    /// </summary>
    void StartBlinking(int number)
    {
        if (!imageDict.ContainsKey(number)) return;

        StopTransition(number);
        Coroutine blinkCoroutine = StartCoroutine(BlinkCoroutine(number));
        transitionCoroutines[number] = blinkCoroutine;
    }

    /// <summary>
    /// 閃爍協程
    /// </summary>
    IEnumerator BlinkCoroutine(int number)
    {
        Image image = imageDict[number];
        bool toYellow = true;

        while (true)
        {
            Color targetColor = toYellow ? checkingColor : clearColor;
            float elapsed = 0f;
            Color startColor = image.color;

            while (elapsed < blinkInterval)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / blinkInterval;
                image.color = Color.Lerp(startColor, targetColor, t);
                yield return null;
            }

            image.color = targetColor;
            toYellow = !toYellow;
        }
    }

    /// <summary>
    /// 開始顏色漸變
    /// </summary>
    void StartTransition(int number, Color targetColor, bool isBlinking)
    {
        if (!imageDict.ContainsKey(number)) return;

        StopTransition(number);

        if (!isBlinking)
        {
            Coroutine transitionCoroutine = StartCoroutine(TransitionColorCoroutine(number, targetColor));
            transitionCoroutines[number] = transitionCoroutine;
        }
    }

    /// <summary>
    /// 顏色漸變協程
    /// </summary>
    IEnumerator TransitionColorCoroutine(int number, Color targetColor)
    {
        Image image = imageDict[number];
        Color startColor = image.color;
        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / transitionDuration;
            image.color = Color.Lerp(startColor, targetColor, t);
            yield return null;
        }

        image.color = targetColor;
        transitionCoroutines.Remove(number);
    }

    /// <summary>
    /// 停止指定編號的過渡效果
    /// </summary>
    void StopTransition(int number)
    {
        if (transitionCoroutines.ContainsKey(number))
        {
            if (transitionCoroutines[number] != null)
            {
                StopCoroutine(transitionCoroutines[number]);
            }
            transitionCoroutines.Remove(number);
        }
    }

    /// <summary>
    /// 停止所有過渡效果
    /// </summary>
    void StopAllTransitions()
    {
        foreach (var kvp in transitionCoroutines)
        {
            if (kvp.Value != null)
            {
                StopCoroutine(kvp.Value);
            }
        }
        transitionCoroutines.Clear();
    }

    // ========== 測試用公開方法 ==========

    /// <summary>
    /// 測試用：在Inspector中可以快速測試
    /// </summary>
    [ContextMenu("Test Clear")]
    public void TestClear()
    {
        SetClearState();
    }

    [ContextMenu("Test Checking 25")]
    public void TestChecking()
    {
        SetCheckingState(25);
    }

    [ContextMenu("Test Confirm")]
    public void TestConfirm()
    {
        SetConfirmState();
    }
}
