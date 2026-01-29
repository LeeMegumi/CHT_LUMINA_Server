using UnityEngine;
using OpenCC.Unity;

public class FontConvert : MonoBehaviour
{
    public static FontConvert Instance { get; private set; }
    OpenChineseConverter converter;
    private void Start()
    {
        Instance = this;
        converter = new OpenChineseConverter();
    }

    public string ConvertToTraditional(string sourceText)
    {
        return converter.S2TW(sourceText);
    }
}
