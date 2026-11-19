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
    [SerializeField] GameObject dialoguePanelPrefab;
    public GameObject dialoguePanel;
    GameObject dialogueCanvasInstance;
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
        CreateDialogueUIFromPrefab();
        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
        SetHintAlpha(0f);
    }

    void CreateDialogueUIFromPrefab()
    {
        if (dialoguePanelPrefab == null)
            dialoguePanelPrefab = Resources.Load<GameObject>("DialoguePanel");

        if (dialoguePanelPrefab == null)
        {
            Debug.LogError("DialoguePanel prefab was not found in Assets/Resources. Open the Unity Editor once so the DialoguePrefabBuilder can generate it.", this);
            return;
        }

        dialogueCanvasInstance = Instantiate(dialoguePanelPrefab);
        Transform panel = dialogueCanvasInstance.transform.Find("Dialogue Panel");
        if (panel == null)
        {
            Debug.LogError("DialoguePanel prefab is missing its 'Dialogue Panel' child.", dialogueCanvasInstance);
            Destroy(dialogueCanvasInstance);
            dialogueCanvasInstance = null;
            return;
        }

        dialoguePanel = panel.gameObject;
        speakerText = FindChildComponent<TMP_Text>(panel, "Speaker");
        bodyText = FindChildComponent<TMP_Text>(panel, "Dialogue Text");
        portraitImage = FindChildComponent<Image>(panel, "Portrait");
        clickHint = FindChildComponent<Image>(panel, "Advance Hint");
        dialoguePanel.SetActive(false);
    }

    static T FindChildComponent<T>(Transform parent, string childName) where T : Component
    {
        Transform child = parent.Find(childName);
        return child != null ? child.GetComponent<T>() : null;
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
        if (dialoguePanel == null)
        {
            Debug.LogError("Dialogue cannot start because the DialoguePanel prefab is unavailable.", this);
            return;
        }
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

    void OnDestroy()
    {
        if (dialogueCanvasInstance != null)
            Destroy(dialogueCanvasInstance);
    }
}
