using System.IO;
using DoNotStray;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DoNotStray.Editor
{
    [InitializeOnLoad]
    public static class NightTaskUIBuilder
    {
        const string ScenePath = "Assets/Scenes/NightTaskAssigner.unity";
        const string CanvasName = "NightTaskCanvas";
        const string PrefabDirectory = "Assets/Prefabs";
        const string PrefabPath = "Assets/Prefabs/NightTaskCanvas.prefab";

        static NightTaskUIBuilder()
        {
            EditorApplication.delayCall += AutoCheckAndBuild;
        }

        static void AutoCheckAndBuild()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path == ScenePath || activeScene.name == "NightTaskAssigner")
            {
                var existingCanvas = GameObject.Find(CanvasName);
                if (existingCanvas == null)
                {
                    Debug.Log("[NightTaskUIBuilder] Auto-generating placeholder NightTaskCanvas...");
                    BuildUI(false);
                }
            }
        }

        [MenuItem("Tools/Night Task/Build Placeholder UI", false, 1)]
        public static void BuildUIMenu()
        {
            BuildUI(true);
        }

        [MenuItem("Tools/Night Task/Clear UI", false, 2)]
        public static void ClearUIMenu()
        {
            var existing = GameObject.Find(CanvasName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
                Debug.Log("[NightTaskUIBuilder] Removed NightTaskCanvas.");
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
        }

        public static void BuildUI(bool forceRebuild)
        {
            // Ensure NightTaskAssigner scene is active
            var currentScene = EditorSceneManager.GetActiveScene();
            if (currentScene.path != ScenePath && currentScene.name != "NightTaskAssigner")
            {
                if (File.Exists(ScenePath))
                {
                    currentScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                }
            }

            // Find or setup NightUIManager & TaskAssigner in the scene
            var managerGO = GameObject.Find("NightUIManager");
            if (managerGO == null)
            {
                managerGO = new GameObject("NightUIManager");
                Undo.RegisterCreatedObjectUndo(managerGO, "Create NightUIManager");
            }

            var nightUI = managerGO.GetComponent<NightUIManager>();
            if (nightUI == null)
            {
                nightUI = Undo.AddComponent<NightUIManager>(managerGO);
            }

            var taskAssigner = managerGO.GetComponent<TaskAssigner>();
            if (taskAssigner == null)
            {
                taskAssigner = Undo.AddComponent<TaskAssigner>(managerGO);
            }

            // Disable old legacy Canvas if present
            var oldCanvasGO = GameObject.Find("Canvas");
            if (oldCanvasGO != null && oldCanvasGO.name == "Canvas")
            {
                oldCanvasGO.SetActive(false);
                Debug.Log("[NightTaskUIBuilder] Disabled old Canvas.");
            }

            // Handle existing canvas
            var existingNewCanvas = GameObject.Find(CanvasName);
            if (existingNewCanvas != null)
            {
                if (forceRebuild)
                {
                    Undo.DestroyObjectImmediate(existingNewCanvas);
                }
                else
                {
                    Debug.Log("[NightTaskUIBuilder] NightTaskCanvas already exists.");
                    return;
                }
            }

            // Standard built-in Unity UI Sprites
            var panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            var buttonSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            // TMP Font Asset
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (fontAsset == null)
            {
                fontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            }

            // Default Unity placeholder colors
            Color panelColor = new Color(0.18f, 0.18f, 0.18f, 0.75f);
            Color innerBoxColor = new Color(0.12f, 0.12f, 0.12f, 0.85f);
            Color textWhite = Color.white;
            Color textGrey = new Color(0.8f, 0.8f, 0.8f, 1f);
            Color buttonTextColor = new Color(0.15f, 0.15f, 0.15f, 1f);

            // 1. Create Canvas
            var canvasGO = new GameObject(CanvasName);
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create NightTaskCanvas");

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();
            var bridge = canvasGO.AddComponent<NightTaskUIBridge>();

            // 2. Top Header Bar (Default Unity grey panel)
            var topBar = CreatePanel(canvasGO.transform, "TopBar", panelColor, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -30), new Vector2(-40, 50), new Vector2(0.5f, 0.5f), panelSprite);
            
            var nightText = CreateTMP(topBar.transform, "NightText", "Night 1", 22, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, textWhite, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(25, 0), new Vector2(140, 40), fontAsset);
            var levelText = CreateTMP(topBar.transform, "LevelText", "Level 1", 20, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, textGrey, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(180, 0), new Vector2(140, 40), fontAsset);
            var campWarningText = CreateTMP(topBar.transform, "CampWarningText", "", 18, FontStyles.Bold, TextAlignmentOptions.MidlineRight, textWhite, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-25, 0), new Vector2(900, 40), fontAsset);

            // Main Content Area
            var mainArea = CreateEmpty(canvasGO.transform, "MainArea", new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, -35), new Vector2(-40, -90), new Vector2(0.5f, 0.5f));

            // ==========================================
            // LEFT PANEL: Inventory & Selected Survivor
            // ==========================================
            var leftPanel = CreatePanel(mainArea.transform, "LeftPanel", panelColor, new Vector2(0, 0), new Vector2(0.32f, 1), Vector2.zero, new Vector2(-10, 0), new Vector2(0.5f, 0.5f), panelSprite);

            // Inventory Section
            CreateTMP(leftPanel.transform, "Header_Inventory", "Inventory", 20, FontStyles.Bold, TextAlignmentOptions.Left, textWhite, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -25), new Vector2(-40, 30), fontAsset);
            var invBox = CreatePanel(leftPanel.transform, "InventoryBox", innerBoxColor, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -100), new Vector2(-40, 120), new Vector2(0.5f, 0.5f), panelSprite);
            
            var firewoodText = CreateTMP(invBox.transform, "FirewoodText", "Firewood: -", 17, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, textWhite, new Vector2(0, 1), new Vector2(1, 1), new Vector2(15, -22), new Vector2(-30, 28), fontAsset);
            var waterText = CreateTMP(invBox.transform, "WaterText", "Water: -", 17, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, textWhite, new Vector2(0, 1), new Vector2(1, 1), new Vector2(15, -58), new Vector2(-30, 28), fontAsset);
            var foodText = CreateTMP(invBox.transform, "FoodText", "Food: -", 17, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, textWhite, new Vector2(0, 1), new Vector2(1, 1), new Vector2(15, -94), new Vector2(-30, 28), fontAsset);

            // Selected Survivor Details Section
            CreateTMP(leftPanel.transform, "Header_Survivor", "Selected Survivor", 20, FontStyles.Bold, TextAlignmentOptions.Left, textWhite, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -185), new Vector2(-40, 30), fontAsset);
            var survBox = CreatePanel(leftPanel.transform, "SurvivorDetailsBox", innerBoxColor, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -380), new Vector2(-40, 340), new Vector2(0.5f, 0.5f), panelSprite);

            var selNameText = CreateTMP(survBox.transform, "SelNameText", "Click a survivor below", 20, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, textWhite, new Vector2(0, 1), new Vector2(1, 1), new Vector2(15, -22), new Vector2(-30, 28), fontAsset);
            var selStatusText = CreateTMP(survBox.transform, "SelStatusText", "Status: -", 16, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, textGrey, new Vector2(0, 1), new Vector2(1, 1), new Vector2(15, -54), new Vector2(-30, 26), fontAsset);
            var selLuckText = CreateTMP(survBox.transform, "SelLuckText", "Luck today: -", 16, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, textWhite, new Vector2(0, 1), new Vector2(1, 1), new Vector2(15, -86), new Vector2(-30, 26), fontAsset);
            var selEscapeText = CreateTMP(survBox.transform, "SelEscapeText", "Escape: -", 16, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, textWhite, new Vector2(0, 1), new Vector2(1, 1), new Vector2(15, -118), new Vector2(-30, 26), fontAsset);
            var selCapacityText = CreateTMP(survBox.transform, "SelCapacityText", "Carries up to: - items", 16, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, textWhite, new Vector2(0, 1), new Vector2(1, 1), new Vector2(15, -150), new Vector2(-30, 26), fontAsset);
            var selFindText = CreateTMP(survBox.transform, "SelFindText", "Finds: -", 15, FontStyles.Normal, TextAlignmentOptions.TopLeft, textWhite, new Vector2(0, 1), new Vector2(1, 1), new Vector2(15, -186), new Vector2(-30, 48), fontAsset);
            var selRiskText = CreateTMP(survBox.transform, "SelRiskText", "Risk: -", 18, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, textWhite, new Vector2(0, 1), new Vector2(1, 1), new Vector2(15, -238), new Vector2(-30, 28), fontAsset);

            // Quick Assign buttons row
            var survBtnRow = CreateEmpty(survBox.transform, "AssignButtonsRow", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 28), new Vector2(-20, 42), new Vector2(0.5f, 0.5f));
            var hlgSurv = survBtnRow.AddComponent<HorizontalLayoutGroup>();
            hlgSurv.spacing = 8;
            hlgSurv.childControlWidth = true;
            hlgSurv.childControlHeight = true;
            hlgSurv.childForceExpandWidth = true;
            hlgSurv.childForceExpandHeight = true;

            var btnAssignWood = CreateDefaultButton(survBtnRow.transform, "BtnAssignWood", "Wood", buttonTextColor, 15, buttonSprite, fontAsset);
            var btnAssignWater = CreateDefaultButton(survBtnRow.transform, "BtnAssignWater", "Water", buttonTextColor, 15, buttonSprite, fontAsset);
            var btnAssignFood = CreateDefaultButton(survBtnRow.transform, "BtnAssignFood", "Food", buttonTextColor, 15, buttonSprite, fontAsset);
            var btnUnassign = CreateDefaultButton(survBtnRow.transform, "BtnUnassign", "Unassign", buttonTextColor, 15, buttonSprite, fontAsset);

            // ==========================================
            // CENTER PANEL: Tasks, Roster & Assignments
            // ==========================================
            var centerPanel = CreatePanel(mainArea.transform, "CenterPanel", panelColor, new Vector2(0.33f, 0), new Vector2(0.67f, 1), Vector2.zero, new Vector2(-10, 0), new Vector2(0.5f, 0.5f), panelSprite);

            // 1. Task Buttons Section
            CreateTMP(centerPanel.transform, "Header_Tasks", "Task Selection", 20, FontStyles.Bold, TextAlignmentOptions.Left, textWhite, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -25), new Vector2(-40, 30), fontAsset);
            CreateTMP(centerPanel.transform, "Subtitle_Tasks", "Select a task, then click a survivor below to toggle assignment", 14, FontStyles.Normal, TextAlignmentOptions.Left, textGrey, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -50), new Vector2(-40, 20), fontAsset);

            var taskRow = CreateEmpty(centerPanel.transform, "TaskButtonsRow", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -95), new Vector2(-40, 48), new Vector2(0.5f, 0.5f));
            var hlgTasks = taskRow.AddComponent<HorizontalLayoutGroup>();
            hlgTasks.spacing = 12;
            hlgTasks.childControlWidth = true;
            hlgTasks.childControlHeight = true;
            hlgTasks.childForceExpandWidth = true;
            hlgTasks.childForceExpandHeight = true;

            var btnTaskWood = CreateDefaultButton(taskRow.transform, "TaskWoodBtn", "Firewood", buttonTextColor, 17, buttonSprite, fontAsset);
            var btnTaskWater = CreateDefaultButton(taskRow.transform, "TaskWaterBtn", "Water", buttonTextColor, 17, buttonSprite, fontAsset);
            var btnTaskFood = CreateDefaultButton(taskRow.transform, "TaskFoodBtn", "Food", buttonTextColor, 17, buttonSprite, fontAsset);

            // 2. Survivor Roster Grid
            CreateTMP(centerPanel.transform, "Header_Roster", "Survivors", 20, FontStyles.Bold, TextAlignmentOptions.Left, textWhite, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -145), new Vector2(-40, 30), fontAsset);

            var rosterBox = CreateEmpty(centerPanel.transform, "SurvivorGrid", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -230), new Vector2(-40, 125), new Vector2(0.5f, 0.5f));
            var grid = rosterBox.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(185, 52);
            grid.spacing = new Vector2(10, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.MiddleCenter;

            var btnSurv0 = CreateDefaultButton(rosterBox.transform, "Survivor_0", "Nurse\n(Escape: 25)", buttonTextColor, 15, buttonSprite, fontAsset);
            var btnSurv1 = CreateDefaultButton(rosterBox.transform, "Survivor_1", "Botanist\n(Escape: 20)", buttonTextColor, 15, buttonSprite, fontAsset);
            var btnSurv2 = CreateDefaultButton(rosterBox.transform, "Survivor_2", "Detective\n(Escape: 65)", buttonTextColor, 15, buttonSprite, fontAsset);
            var btnSurv3 = CreateDefaultButton(rosterBox.transform, "Survivor_3", "Scout\n(Escape: 45)", buttonTextColor, 15, buttonSprite, fontAsset);
            var btnSurv4 = CreateDefaultButton(rosterBox.transform, "Survivor_4", "Strong guy\n(Escape: 85)", buttonTextColor, 15, buttonSprite, fontAsset);
            var btnSurv5 = CreateDefaultButton(rosterBox.transform, "Survivor_5", "Tutorial guy\n(Escape: 30)", buttonTextColor, 15, buttonSprite, fontAsset);

            // 3. Current Assignments Section
            CreateTMP(centerPanel.transform, "Header_Assignments", "Current Assignments", 20, FontStyles.Bold, TextAlignmentOptions.Left, textWhite, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -310), new Vector2(-40, 30), fontAsset);

            var assignBox = CreatePanel(centerPanel.transform, "AssignmentsBox", innerBoxColor, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, -185), new Vector2(-40, -370), new Vector2(0.5f, 0.5f), panelSprite);
            var assignmentsText = CreateTMP(assignBox.transform, "AssignmentsText", "Assignments will appear here...", 17, FontStyles.Normal, TextAlignmentOptions.TopLeft, textWhite, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-30, -30), fontAsset);
            assignmentsText.textWrappingMode = TextWrappingModes.Normal;

            // ==========================================
            // RIGHT PANEL: Plan Preview & Morning Report
            // ==========================================
            var rightPanel = CreatePanel(mainArea.transform, "RightPanel", panelColor, new Vector2(0.68f, 0), new Vector2(1, 1), Vector2.zero, new Vector2(-10, 0), new Vector2(0.5f, 0.5f), panelSprite);

            // Plan Preview Section
            CreateTMP(rightPanel.transform, "Header_Plan", "Plan Preview (1,000 runs)", 20, FontStyles.Bold, TextAlignmentOptions.Left, textWhite, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -25), new Vector2(-40, 30), fontAsset);
            var planBox = CreatePanel(rightPanel.transform, "PlanPreviewBox", innerBoxColor, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -135), new Vector2(-40, 180), new Vector2(0.5f, 0.5f), panelSprite);

            var planSafeText = CreateTMP(planBox.transform, "PlanSafeText", "Everyone safe: -", 16, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, textWhite, new Vector2(0, 1), new Vector2(1, 1), new Vector2(15, -20), new Vector2(-30, 26), fontAsset);
            var planTakenText = CreateTMP(planBox.transform, "PlanTakenText", "Expected taken: -", 16, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, textWhite, new Vector2(0, 1), new Vector2(1, 1), new Vector2(15, -52), new Vector2(-30, 26), fontAsset);
            var planFirewoodText = CreateTMP(planBox.transform, "PlanFirewoodText", "Enough firewood: -", 16, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, textWhite, new Vector2(0, 1), new Vector2(1, 1), new Vector2(15, -84), new Vector2(-30, 26), fontAsset);
            var planWaterText = CreateTMP(planBox.transform, "PlanWaterText", "Enough water: -", 16, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, textWhite, new Vector2(0, 1), new Vector2(1, 1), new Vector2(15, -116), new Vector2(-30, 26), fontAsset);
            var planFoodText = CreateTMP(planBox.transform, "PlanFoodText", "Enough food: -", 16, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, textWhite, new Vector2(0, 1), new Vector2(1, 1), new Vector2(15, -148), new Vector2(-30, 26), fontAsset);

            // Morning Report Section
            CreateTMP(rightPanel.transform, "Header_Report", "Morning Report", 20, FontStyles.Bold, TextAlignmentOptions.Left, textWhite, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -250), new Vector2(-40, 30), fontAsset);
            var reportBox = CreatePanel(rightPanel.transform, "MorningReportBox", innerBoxColor, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, -115), new Vector2(-40, -420), new Vector2(0.5f, 0.5f), panelSprite);
            var morningReportText = CreateTMP(reportBox.transform, "MorningReportText", "Morning report appears here after the night.", 16, FontStyles.Normal, TextAlignmentOptions.TopLeft, textWhite, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-20, -20), fontAsset);
            morningReportText.textWrappingMode = TextWrappingModes.Normal;

            // Action Buttons
            var btnStartNight = CreateDefaultButton(rightPanel.transform, "BtnStartNight", "Start Night", buttonTextColor, 18, buttonSprite, fontAsset);
            var rtStart = btnStartNight.GetComponent<RectTransform>();
            rtStart.anchorMin = new Vector2(0, 0);
            rtStart.anchorMax = new Vector2(1, 0);
            rtStart.anchoredPosition = new Vector2(0, 85);
            rtStart.sizeDelta = new Vector2(-40, 48);

            var btnNextEvening = CreateDefaultButton(rightPanel.transform, "BtnNextEvening", "Next Evening", buttonTextColor, 17, buttonSprite, fontAsset);
            var rtNext = btnNextEvening.GetComponent<RectTransform>();
            rtNext.anchorMin = new Vector2(0, 0);
            rtNext.anchorMax = new Vector2(1, 0);
            rtNext.anchoredPosition = new Vector2(0, 30);
            rtNext.sizeDelta = new Vector2(-40, 44);

            // ==========================================
            // SERIALIZE REFERENCES TO NightUIManager
            // ==========================================
            var soUI = new SerializedObject(nightUI);
            soUI.FindProperty("nightText").objectReferenceValue = nightText;
            soUI.FindProperty("levelText").objectReferenceValue = levelText;
            soUI.FindProperty("campWarningText").objectReferenceValue = campWarningText;

            soUI.FindProperty("firewoodText").objectReferenceValue = firewoodText;
            soUI.FindProperty("waterText").objectReferenceValue = waterText;
            soUI.FindProperty("foodText").objectReferenceValue = foodText;

            soUI.FindProperty("selNameText").objectReferenceValue = selNameText;
            soUI.FindProperty("selStatusText").objectReferenceValue = selStatusText;
            soUI.FindProperty("selLuckText").objectReferenceValue = selLuckText;
            soUI.FindProperty("selEscapeText").objectReferenceValue = selEscapeText;
            soUI.FindProperty("selCapacityText").objectReferenceValue = selCapacityText;
            soUI.FindProperty("selFindText").objectReferenceValue = selFindText;
            soUI.FindProperty("selRiskText").objectReferenceValue = selRiskText;

            soUI.FindProperty("assignmentsText").objectReferenceValue = assignmentsText;

            soUI.FindProperty("planSafeText").objectReferenceValue = planSafeText;
            soUI.FindProperty("planTakenText").objectReferenceValue = planTakenText;
            soUI.FindProperty("planFirewoodText").objectReferenceValue = planFirewoodText;
            soUI.FindProperty("planWaterText").objectReferenceValue = planWaterText;
            soUI.FindProperty("planFoodText").objectReferenceValue = planFoodText;

            soUI.FindProperty("morningReportText").objectReferenceValue = morningReportText;
            soUI.ApplyModifiedProperties();

            // ==========================================
            // SERIALIZE REFERENCES TO TaskAssigner
            // ==========================================
            var soAssigner = new SerializedObject(taskAssigner);
            soAssigner.FindProperty("ui").objectReferenceValue = nightUI;
            soAssigner.ApplyModifiedProperties();

            // ==========================================
            // SERIALIZE REFERENCES TO NightTaskUIBridge
            // ==========================================
            var soBridge = new SerializedObject(bridge);
            soBridge.FindProperty("btnTaskWood").objectReferenceValue = btnTaskWood;
            soBridge.FindProperty("btnTaskWater").objectReferenceValue = btnTaskWater;
            soBridge.FindProperty("btnTaskFood").objectReferenceValue = btnTaskFood;

            var survProp = soBridge.FindProperty("btnSurvivors");
            survProp.arraySize = 6;
            survProp.GetArrayElementAtIndex(0).objectReferenceValue = btnSurv0;
            survProp.GetArrayElementAtIndex(1).objectReferenceValue = btnSurv1;
            survProp.GetArrayElementAtIndex(2).objectReferenceValue = btnSurv2;
            survProp.GetArrayElementAtIndex(3).objectReferenceValue = btnSurv3;
            survProp.GetArrayElementAtIndex(4).objectReferenceValue = btnSurv4;
            survProp.GetArrayElementAtIndex(5).objectReferenceValue = btnSurv5;

            soBridge.FindProperty("btnAssignWood").objectReferenceValue = btnAssignWood;
            soBridge.FindProperty("btnAssignWater").objectReferenceValue = btnAssignWater;
            soBridge.FindProperty("btnAssignFood").objectReferenceValue = btnAssignFood;
            soBridge.FindProperty("btnUnassign").objectReferenceValue = btnUnassign;

            soBridge.FindProperty("btnStartNight").objectReferenceValue = btnStartNight;
            soBridge.FindProperty("btnNextEvening").objectReferenceValue = btnNextEvening;
            soBridge.FindProperty("assigner").objectReferenceValue = taskAssigner;
            soBridge.ApplyModifiedProperties();

            // Save Prefab
            if (!Directory.Exists(PrefabDirectory))
            {
                Directory.CreateDirectory(PrefabDirectory);
            }
            PrefabUtility.SaveAsPrefabAssetAndConnect(canvasGO, PrefabPath, InteractionMode.AutomatedAction);

            // Mark Scene Dirty and Save
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("[NightTaskUIBuilder] Successfully created and wired placeholder NightTaskCanvas!");
        }

        static GameObject CreateEmpty(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            return go;
        }

        static GameObject CreatePanel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta, Vector2 pivot, Sprite sprite)
        {
            var go = CreateEmpty(parent, name, anchorMin, anchorMax, anchoredPos, sizeDelta, pivot);
            var img = go.AddComponent<Image>();
            img.color = color;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
            }
            img.raycastTarget = true;
            return go;
        }

        static TextMeshProUGUI CreateTMP(Transform parent, string name, string text, float fontSize, FontStyles style, TextAlignmentOptions alignment, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta, TMP_FontAsset font)
        {
            var go = CreateEmpty(parent, name, anchorMin, anchorMax, anchoredPos, sizeDelta, new Vector2(0.5f, 0.5f));
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.color = color;
            tmp.richText = true;
            tmp.raycastTarget = false;
            return tmp;
        }

        // Standard default Unity Button
        static Button CreateDefaultButton(Transform parent, string name, string label, Color textColor, float fontSize, Sprite sprite, TMP_FontAsset font)
        {
            var go = CreateEmpty(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160, 40), new Vector2(0.5f, 0.5f));
            var img = go.AddComponent<Image>();
            img.color = Color.white;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
            }
            img.raycastTarget = true;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.selectedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
            btn.colors = colors;

            var labelGO = CreateEmpty(go.transform, "Text (TMP)", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = label;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Normal;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = textColor;
            tmp.raycastTarget = false;

            return btn;
        }
    }
}
