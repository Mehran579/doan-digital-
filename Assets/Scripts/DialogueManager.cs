using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;

    public static event System.Action OnDialogueStarted;
    public static event System.Action OnDialogueEnded;

    [Header("UI")]
    public GameObject dialoguePanel;
    public TMP_Text speakerText;
    public TMP_Text bodyText;
    public Image portraitImage;

    [Header("Settings")]
    public float typeSpeed = 0.03f;
    public InputAction advance;
    Queue<DialogueLine> lines = new Queue<DialogueLine>();
    Coroutine typing;
    string currentFullText;
    bool isTyping;
    int startFrame = -1;

    [Header("Click Hint (optional)")]
    public Image clickHint;
    public int hintForFirstLines = 6;
    public float hintDelay = 0.4f;
    public float hintMinAlpha = 0.3f;
    public float hintMaxAlpha = 0.65f;

    Coroutine hintRoutine;
    int linesShown;
    PlayerMovement playerMovement;
    CompanionGroupController companionGroup;
    bool movementPausedForDialogue;
    bool playerWasAlreadyLocked;

    public bool IsActive => dialoguePanel != null && dialoguePanel.activeSelf;

    void Reset()
    {
        advance = new InputAction("Advance", InputActionType.Button);
        advance.AddBinding("<Keyboard>/e");
        advance.AddBinding("<Keyboard>/enter");
        advance.AddBinding("<Mouse>/leftButton");
        advance.AddBinding("<Gamepad>/buttonSouth");
    }

    void Awake()
    {
        Instance = this;
        playerMovement = GetComponent<PlayerMovement>();
        companionGroup = GetComponent<CompanionGroupController>();
        EnsurePlaceholderUI();
        dialoguePanel.SetActive(false);
        SetHintAlpha(0f);
    }

    void EnsurePlaceholderUI()
    {
        if (dialoguePanel != null && speakerText != null && bodyText != null)
            return;

        GameObject canvasObject = new("Dialogue Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Sprite whiteSprite = CreateWhiteSprite();
        GameObject panelObject = CreateRect("Dialogue Panel", canvasObject.transform,
            new Vector2(0.06f, 0.045f), new Vector2(0.94f, 0.29f));
        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.sprite = whiteSprite;
        panelImage.color = new Color(0.035f, 0.055f, 0.045f, 0.96f);
        dialoguePanel = panelObject;

        GameObject portraitObject = CreateRect("Portrait", panelObject.transform,
            new Vector2(0.025f, 0.15f), new Vector2(0.16f, 0.86f));
        portraitImage = portraitObject.AddComponent<Image>();
        portraitImage.sprite = whiteSprite;
        portraitImage.color = new Color(0.12f, 0.16f, 0.12f, 1f);
        portraitImage.preserveAspect = true;
        portraitImage.enabled = false;

        speakerText = CreateText("Speaker", panelObject.transform,
            new Vector2(0.19f, 0.66f), new Vector2(0.95f, 0.9f),
            32f, new Color(0.93f, 0.78f, 0.42f, 1f), FontStyles.Bold);
        bodyText = CreateText("Dialogue Text", panelObject.transform,
            new Vector2(0.19f, 0.18f), new Vector2(0.95f, 0.65f),
            25f, new Color(0.92f, 0.92f, 0.84f, 1f), FontStyles.Normal);
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.enableWordWrapping = true;

        GameObject hintObject = CreateRect("Advance Hint", panelObject.transform,
            new Vector2(0.73f, 0.055f), new Vector2(0.77f, 0.11f));
        clickHint = hintObject.AddComponent<Image>();
        clickHint.sprite = whiteSprite;
        clickHint.color = new Color(0.94f, 0.82f, 0.52f, 1f);
        CreateText("Advance Hint Label", panelObject.transform,
            new Vector2(0.78f, 0.035f), new Vector2(0.96f, 0.13f),
            16f, new Color(0.82f, 0.84f, 0.76f, 1f), FontStyles.Normal).text = "ENTER / CLICK";
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

    static Sprite CreateWhiteSprite()
    {
        Texture2D texture = new(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        sprite.name = "Dialogue UI White Pixel";
        return sprite;
    }

    void OnEnable()
    {
        EnsureAdvanceAction();
        advance.Enable();
        OnDialogueStarted += PauseExplorationForDialogue;
        OnDialogueEnded += ResumeExplorationAfterDialogue;
    }

    void OnDisable()
    {
        OnDialogueStarted -= PauseExplorationForDialogue;
        OnDialogueEnded -= ResumeExplorationAfterDialogue;
        if (advance != null)
            advance.Disable();
        ResumeExplorationAfterDialogue();
    }

    void EnsureAdvanceAction()
    {
        if (advance != null && advance.bindings.Count > 0)
            return;

        advance = new InputAction("Advance", InputActionType.Button);
        advance.AddBinding("<Keyboard>/e");
        advance.AddBinding("<Keyboard>/enter");
        advance.AddBinding("<Keyboard>/numpadEnter");
        advance.AddBinding("<Mouse>/leftButton");
        advance.AddBinding("<Gamepad>/buttonSouth");
    }

    void PauseExplorationForDialogue()
    {
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();
        if (companionGroup == null)
            companionGroup = GetComponent<CompanionGroupController>();

        if (playerMovement != null)
        {
            if (!movementPausedForDialogue)
                playerWasAlreadyLocked = playerMovement.IsMovementLocked;
            playerMovement.SetMovementLocked(true);
            movementPausedForDialogue = true;
        }

        if (companionGroup != null)
            companionGroup.BeginDialoguePause(Camera.main, transform.position);
    }

    void ResumeExplorationAfterDialogue()
    {
        if (movementPausedForDialogue && playerMovement != null)
            playerMovement.SetMovementLocked(playerWasAlreadyLocked);
        movementPausedForDialogue = false;

        if (companionGroup == null)
            companionGroup = GetComponent<CompanionGroupController>();
        if (companionGroup != null)
            companionGroup.EndDialoguePause();
    }

    void Update()
    {
        if (!IsActive || Time.frameCount == startFrame) return;

        if (AdvancePressedThisFrame())
        {
            //AudioManager.Play(SFX.DialogueAdvance);
            if (isTyping)
            {
                StopCoroutine(typing);
                bodyText.text = currentFullText;
                isTyping = false;
                ShowHint();
            }
            else ShowNextLine();
        }
    }

    bool AdvancePressedThisFrame()
    {
        if (advance != null && advance.enabled && advance.WasPressedThisFrame())
            return true;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null &&
            (keyboard.eKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame ||
             keyboard.numpadEnterKey.wasPressedThisFrame))
            return true;

        Mouse mouse = Mouse.current;
        return mouse != null && mouse.leftButton.wasPressedThisFrame;
    }

    System.Action onComplete;
    public void StartDialogue(DialogueData data, System.Action callback = null)
    {
        if (data == null || data.lines == null || data.lines.Length == 0 || IsActive) return;
        this.onComplete = callback;
        startFrame = Time.frameCount;
        lines.Clear();
        foreach (var line in data.lines) lines.Enqueue(line);
        dialoguePanel.SetActive(true);
        //AudioManager.Play(SFX.DialogueOpen);
        OnDialogueStarted?.Invoke();
        ShowNextLine();
    }

    void ShowNextLine()
    {
        HideHint();
        linesShown++;

        if (lines.Count == 0) { EndDialogue(); return; }

        DialogueLine line = lines.Dequeue();
        speakerText.text = line.speaker;
        currentFullText = line.text;

        if (portraitImage != null)
        {
            portraitImage.sprite = line.portrait;
            portraitImage.enabled = line.portrait != null;
        }

        typing = StartCoroutine(TypeText(line.text));
    }

    IEnumerator TypeText(string text)
    {
        isTyping = true;
        bodyText.text = "";
        int charCount = 0;
        foreach (char c in text)
        {
            bodyText.text += c;
            //if (c != ' ' && charCount % 2 == 1) AudioManager.Play(SFX.DialogueBlip);
            if (c != ' ') charCount++;
            yield return new WaitForSecondsRealtime(typeSpeed);
        }
        isTyping = false;
        ShowHint();
    }

    void SetHintAlpha(float a)
    {
        if (clickHint == null) return;
        Color c = clickHint.color;
        c.a = a;
        clickHint.color = c;
    }

    void ShowHint()
    {

        if (clickHint == null) return;
        if (hintForFirstLines > 0 && linesShown > hintForFirstLines) return;
        if (hintRoutine != null) StopCoroutine(hintRoutine);
        hintRoutine = StartCoroutine(HintPulse());
    }

    void HideHint()
    {
        if (hintRoutine != null) { StopCoroutine(hintRoutine); hintRoutine = null; }
        SetHintAlpha(0f);
    }

    IEnumerator HintPulse()
    {
        SetHintAlpha(0f);
        yield return new WaitForSecondsRealtime(hintDelay);

        float t = 0f;
        while (true)
        {
            t += Time.unscaledDeltaTime * 2f;
            float target = Mathf.Lerp(hintMinAlpha, hintMaxAlpha, (Mathf.Sin(t) + 1f) * 0.5f);
            SetHintAlpha(Mathf.MoveTowards(clickHint.color.a, target, Time.unscaledDeltaTime * 3f));
            yield return null;
        }
    }

    void EndDialogue()
    {
        HideHint();
        dialoguePanel.SetActive(false);
        //AudioManager.Play(SFX.DialogueClose);
        OnDialogueEnded?.Invoke();
        onComplete?.Invoke();
    }

    // Closes the panel immediately WITHOUT running the pending callback.
    // Used to skip straight to another dialogue (e.g. the boss death dialogue).
    public void ForceClose()
    {
        if (!IsActive) return;
        if (typing != null) StopCoroutine(typing);
        isTyping = false;
        lines.Clear();
        onComplete = null;
        HideHint();
        dialoguePanel.SetActive(false);
        OnDialogueEnded?.Invoke();
    }
}
