using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class DialoguePrefabBuilder
{
    const string PrefabPath = "Assets/Resources/DialoguePanel.prefab";

    [InitializeOnLoadMethod]
    static void CreateOnEditorImport()
    {
        EditorApplication.delayCall += () =>
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))
                Create();
        };
    }

    [MenuItem("Tools/Do Not Stray/Create Dialogue Panel Prefab")]
    public static void Create()
    {
        EnsureFolder("Assets", "Resources");

        GameObject canvasObject = new("Dialogue Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;
        canvasRect.localScale = Vector3.one;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        GameObject panel = CreateRect("Dialogue Panel", canvasObject.transform,
            new Vector2(0.06f, 0.045f), new Vector2(0.94f, 0.29f));
        Image panelImage = panel.AddComponent<Image>();
        panelImage.sprite = uiSprite;
        panelImage.color = new Color(0.035f, 0.055f, 0.045f, 0.96f);

        GameObject portrait = CreateRect("Portrait", panel.transform,
            new Vector2(0.025f, 0.15f), new Vector2(0.16f, 0.86f));
        Image portraitImage = portrait.AddComponent<Image>();
        portraitImage.sprite = uiSprite;
        portraitImage.color = new Color(0.12f, 0.16f, 0.12f, 1f);
        portraitImage.preserveAspect = true;
        portraitImage.enabled = false;

        CreateText("Speaker", panel.transform,
            new Vector2(0.19f, 0.66f), new Vector2(0.95f, 0.9f),
            32f, new Color(0.93f, 0.78f, 0.42f, 1f), FontStyles.Bold);
        TMP_Text body = CreateText("Dialogue Text", panel.transform,
            new Vector2(0.19f, 0.18f), new Vector2(0.95f, 0.65f),
            25f, new Color(0.92f, 0.92f, 0.84f, 1f), FontStyles.Normal);
        body.alignment = TextAlignmentOptions.TopLeft;
        body.textWrappingMode = TextWrappingModes.Normal;

        GameObject hint = CreateRect("Advance Hint", panel.transform,
            new Vector2(0.73f, 0.055f), new Vector2(0.77f, 0.11f));
        Image hintImage = hint.AddComponent<Image>();
        hintImage.sprite = uiSprite;
        hintImage.color = new Color(0.94f, 0.82f, 0.52f, 1f);
        CreateText("Advance Hint Label", panel.transform,
            new Vector2(0.78f, 0.035f), new Vector2(0.96f, 0.13f),
            16f, new Color(0.82f, 0.84f, 0.76f, 1f), FontStyles.Normal).text = "ENTER / CLICK";

        PrefabUtility.SaveAsPrefabAsset(canvasObject, PrefabPath);
        Object.DestroyImmediate(canvasObject);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Created dialogue panel prefab at {PrefabPath}");
    }

    static GameObject CreateRect(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject child = new(objectName, typeof(RectTransform));
        child.layer = 5;
        child.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)child.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return child;
    }

    static TMP_Text CreateText(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float fontSize,
        Color color,
        FontStyles style)
    {
        GameObject child = CreateRect(objectName, parent, anchorMin, anchorMax);
        TextMeshProUGUI text = child.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.margin = new Vector4(8f, 4f, 8f, 4f);
        return text;
    }

    static void EnsureFolder(string parent, string folder)
    {
        string path = $"{parent}/{folder}";
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, folder);
    }
}
