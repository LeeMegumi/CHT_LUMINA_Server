using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public class LuminaCharatorAnimatorController : MonoBehaviour
{
    private Animator animator;
    private bool idleLoop = false;

    // 待機動畫名稱陣列
    public string[] idleAnimations = { "SB01", "SB02", "SB03", "SB04" };

    // 使用 Hash 提升效能
    private int[] idleAnimationHashes;

    // 融接時間（秒）
    [SerializeField] private float crossFadeDuration = 0.3f;

    private Coroutine idleCoroutine;
    private Coroutine singleAnimationCoroutine;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        // 預先計算 Hash 值
        idleAnimationHashes = new int[idleAnimations.Length];
    }
    void Start()
    {
        
        for (int i = 0; i < idleAnimations.Length; i++)
        {
            idleAnimationHashes[i] = Animator.StringToHash(idleAnimations[i]);
        }
    }

    // 設定待機循環開關
    public bool IdleLoop
    {
        get { return idleLoop; }
        set
        {
            idleLoop = value;

            if (idleLoop)
            {
                // 開始待機循環
                if(idleCoroutine == null)
                {
                        idleCoroutine = StartCoroutine(IdleLoopCoroutine());
                }
            }
            else
            {
                // 停止待機循環
                if (idleCoroutine != null)
                {
                    StopCoroutine(idleCoroutine);
                    idleCoroutine = null;
                }
            }
        }
    }

    /// <summary>
    /// 播放單次動畫，播放完畢後可選擇是否自動返回待機循環
    /// </summary>
    /// <param name="animationName">動畫狀態名稱</param>
    /// <param name="returnToIdleLoop">播放完畢後是否返回待機循環</param>
    /// <param name="onComplete">動畫播放完畢的回調函數</param>
    public void PlaySingleAnimation(string animationName, bool returnToIdleLoop = false, Action onComplete = null)
    {
        // 停止待機循環避免衝突
        IdleLoop = false;

        // 停止之前的單次動畫
        if (singleAnimationCoroutine != null)
        {
            StopCoroutine(singleAnimationCoroutine);
        }

        singleAnimationCoroutine = StartCoroutine(PlaySingleAnimationCoroutine(animationName, returnToIdleLoop, onComplete));
    }

    /// <summary>
    /// 播放單次動畫（使用 Hash）
    /// </summary>
    public void PlaySingleAnimation(int animationHash, bool returnToIdleLoop = false, Action onComplete = null)
    {
        IdleLoop = false;

        if (singleAnimationCoroutine != null)
        {
            StopCoroutine(singleAnimationCoroutine);
        }

        singleAnimationCoroutine = StartCoroutine(PlaySingleAnimationCoroutineByHash(animationHash, returnToIdleLoop, onComplete));
    }

    private IEnumerator PlaySingleAnimationCoroutineByHash(int animationHash, bool returnToIdleLoop, Action onComplete)
    {
        // 使用 CrossFadeInFixedTime 進行平滑融接
        animator.CrossFadeInFixedTime(animationHash, crossFadeDuration);

        // 等待融接完成
        yield return new WaitForSeconds(crossFadeDuration);

        // 等待動畫播放完成
        yield return StartCoroutine(WaitForAnimationCompleteByHash(animationHash));

        // 執行回調
        onComplete?.Invoke();

        // 如果需要返回待機循環
        if (returnToIdleLoop)
        {
            IdleLoop = true;
        }

        singleAnimationCoroutine = null;
    }

    private IEnumerator PlaySingleAnimationCoroutine(string animationName, bool returnToIdleLoop, Action onComplete)
    {
        // 使用 CrossFadeInFixedTime 進行平滑融接
        animator.CrossFadeInFixedTime(animationName, crossFadeDuration);

        // 等待融接完成
        yield return new WaitForSeconds(crossFadeDuration);

        // 等待動畫播放完成
        yield return StartCoroutine(WaitForAnimationComplete(animationName));

        // 執行回調
        onComplete?.Invoke();

        // 如果需要返回待機循環
        if (returnToIdleLoop)
        {
            IdleLoop = true;
        }

        singleAnimationCoroutine = null;
    }

    private IEnumerator IdleLoopCoroutine()
    {
        while (idleLoop)
        {
            // 隨機選擇一個待機動畫
            int randomIndex = Random.Range(0, idleAnimationHashes.Length);
            Debug.Log("播放待機動畫: " + idleAnimations[randomIndex]);

            // 使用 CrossFadeInFixedTime 進行平滑融接
            animator.CrossFadeInFixedTime(idleAnimationHashes[randomIndex], crossFadeDuration);

            // 等待融接完成
            yield return new WaitForSeconds(crossFadeDuration);

            // 等待動畫播放完成
            yield return StartCoroutine(WaitForAnimationComplete(idleAnimations[randomIndex]));
        }
    }

    private IEnumerator WaitForAnimationComplete(string animationName)
    {
        // 等待一幀確保動畫狀態更新
        yield return null;

        AnimatorStateInfo stateInfo;

        // 持續檢查動畫是否播放完成
        while (true)
        {
            stateInfo = animator.GetCurrentAnimatorStateInfo(0);

            // 確認當前動畫是否為目標動畫且已播放完成
            if (stateInfo.IsName(animationName) && stateInfo.normalizedTime >= 0.95f)
            {
                break;
            }

            yield return null;
        }
    }

    private IEnumerator WaitForAnimationCompleteByHash(int animationHash)
    {
        yield return null;

        AnimatorStateInfo stateInfo;

        while (true)
        {
            stateInfo = animator.GetCurrentAnimatorStateInfo(0);

            // 使用 Hash 比對
            if (stateInfo.shortNameHash == animationHash && stateInfo.normalizedTime >= 0.95f)
            {
                break;
            }

            yield return null;
        }
    }

    /// <summary>
    /// 立即停止所有動畫控制
    /// </summary>
    public void StopAllAnimations()
    {
        IdleLoop = false;

        if (singleAnimationCoroutine != null)
        {
            StopCoroutine(singleAnimationCoroutine);
            singleAnimationCoroutine = null;
        }
    }
}
