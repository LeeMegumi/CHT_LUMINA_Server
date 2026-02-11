using UnityEngine;

public class TossingWallManager : MonoBehaviour
{
    [SerializeField] private TossingWall canvas1_30;
    [SerializeField] private TossingWall canvas31_60;

    // 清除所有
    public void ClearAll()
    {
        canvas1_30.SetClearState();
        canvas31_60.SetClearState();
    }

    // 設置任意編號為Checking狀態
    public void SetCheckingNumber(int number)
    {
        // 先清除兩個Canvas
        canvas1_30.SetClearState();
        canvas31_60.SetClearState();

        // 根據編號範圍選擇對應的Canvas
        if (number >= 1 && number <= 30)
        {
            canvas1_30.SetCheckingState(number);
        }
        else if (number >= 31 && number <= 60)
        {
            canvas31_60.SetCheckingState(number);
        }
    }

    // 確認當前選擇
    public void ConfirmCurrent()
    {
        // 嘗試在兩個Canvas上確認
        canvas1_30.SetConfirmState();
        canvas31_60.SetConfirmState();
    }
}
