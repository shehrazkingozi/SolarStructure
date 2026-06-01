using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public static class MobileSolarFrameSceneSetup
{
    private const string ScenePath            = "Assets/Scenes/SampleScene.unity";
    private const string UiRootName           = "SolarFrameUI";
    private const string PanelName            = "ControlPanel";
    private const string PanelToggleButtonName= "Button_TogglePanel";
    private const string AutoSetupSessionKey  = "MobileSolarFrameSceneSetup.AutoApplied";
    private const string StructureRootName    = "Solar_Mobile_Structure";
    private const string PillarsRootName      = "Pillars";
    private const string BeamsRootName        = "TopBeams";
    private const string RailsRootName        = "Rails";
    private const string PanelsRootName       = "Panels";
    private const string BracingRootName      = "CrossBracing";
    private const string SmartDesignButtonName= "Button_SmartDesign";
    private const int    XBayCount            = 12;

    private static readonly string[] BayNames = {
        "[0] Back Bay L (col0-col1 @ back)",
        "[1] Back Bay R (col1-col2 @ back)",
        "[2] Front Bay L (col0-col1 @ front)",
        "[3] Front Bay R (col1-col2 @ front)",
        "[4] Mid Bay L (col0-col1 @ mid)",
        "[5] Mid Bay R (col1-col2 @ mid)",
        "[6] Side Left: Front→Mid",
        "[7] Side Left: Mid→Back",
        "[8] Side Center: Front→Mid",
        "[9] Side Center: Mid→Back",
        "[10] Side Right: Front→Mid",
        "[11] Side Right: Mid→Back",
    };

    private struct FieldConfig
    {
        public MobileSolarFieldId Id; public string Label; public float Min, Max;
        public FieldConfig(MobileSolarFieldId id, string label, float min, float max)
        { Id=id; Label=label; Min=min; Max=max; }
    }

    [MenuItem("Tools/Mobile Solar Frame/Setup Sample Scene UI")]
    public static void SetupSampleSceneUi() => RunSetup(saveScene: true);

    [InitializeOnLoadMethod]
    private static void AutoSetupInOpenEditor()
    {
        if (Application.isBatchMode) return;
        EditorApplication.delayCall += TryAutoSetupActiveSampleScene;
    }

    public static void RunBatchSetup() => RunSetup(saveScene: true);

    private static void TryAutoSetupActiveSampleScene()
    {
        if (SessionState.GetBool(AutoSetupSessionKey, false)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        SessionState.SetBool(AutoSetupSessionKey, true);
        RunSetup(saveScene: true);
    }

    private static void RunSetup(bool saveScene)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        MobileSolarFrameApp app = UnityEngine.Object.FindObjectOfType<MobileSolarFrameApp>();
        if (app == null) throw new InvalidOperationException("SampleScene must contain MobileSolarFrameApp on Main Camera.");

        TMP_FontAsset font     = LoadFont();
        Sprite        uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        GameObject existingRoot = GameObject.Find(UiRootName);
        if (existingRoot != null) UnityEngine.Object.DestroyImmediate(existingRoot);

        EnsureEventSystem();

        // ── Canvas ──────────────────────────────────────────────────────
        RectTransform canvasRect = CreateUiObject(UiRootName, null, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasRect.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 50;
        var scaler = canvasRect.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1440f, 900f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.3f; scaler.referencePixelsPerUnit = 100f;
        canvasRect.GetComponent<GraphicRaycaster>().ignoreReversedGraphics = true;

        // ── Control Panel ────────────────────────────────────────────────
        RectTransform panelRect = CreateUiObject(PanelName, canvasRect, typeof(Image), typeof(VerticalLayoutGroup));
        panelRect.anchorMin = new Vector2(0f,0f); panelRect.anchorMax = new Vector2(0f,1f);
        panelRect.pivot = new Vector2(0f,0.5f); panelRect.anchoredPosition = new Vector2(18f,0f);
        panelRect.sizeDelta = new Vector2(450f,-36f);
        var pi = panelRect.GetComponent<Image>(); pi.color = new Color(0.08f,0.11f,0.15f,0.96f); pi.sprite = uiSprite; pi.type = Image.Type.Sliced;
        var pl = panelRect.GetComponent<VerticalLayoutGroup>();
        pl.padding = new RectOffset(18,18,18,18); pl.spacing = 14f;
        pl.childAlignment = TextAnchor.UpperLeft; pl.childControlWidth = true; pl.childControlHeight = false;
        pl.childForceExpandWidth = true; pl.childForceExpandHeight = false;

        CreateText(panelRect, "SOLAR FRAME APP", font, 34f, FontStyles.Bold, Color.white, TextAlignmentOptions.Left);
        CreateText(panelRect, "Slider aur direct value input dono available hain.", font, 20f, FontStyles.Normal, new Color(0.72f,0.79f,0.85f,1f), TextAlignmentOptions.Left);

        Button resetButton = CreateButton(panelRect, "Button_ResetDefaults", "RESET TO DEFAULT", font, uiSprite);
        Button panelToggleButton = CreatePanelToggleButton(canvasRect, panelRect, font, uiSprite, out TMP_Text panelToggleButtonLabel);

        // ── Axis view buttons (top-right of canvas) ──────────────────────
        Button viewFrontButton = CreateViewButton(canvasRect, "Button_ViewFront", "Front\n(Y)", font, uiSprite, 0);
        Button viewSideButton  = CreateViewButton(canvasRect, "Button_ViewSide",  "Side\n(X)",  font, uiSprite, 1);
        Button viewTopButton   = CreateViewButton(canvasRect, "Button_ViewTop",   "Top\n(Z)",   font, uiSprite, 2);
        Button viewIsoButton   = CreateViewButton(canvasRect, "Button_ViewIso",   "3D\nIso",    font, uiSprite, 3);
        Button viewPerspOrthoButton = CreateViewButton(canvasRect, "Button_ViewPerspOrtho", "Ortho", font, uiSprite, 4);

        // ── Details toggle button (bottom-right of canvas) ───────────────
        Button detailsToggleButton = CreateDetailsToggleButton(canvasRect, font, uiSprite);

        // ── Details panel (right side, hidden by default) ─────────────────
        RectTransform detailsPanelRect;
        TMP_Text      detailsText;
        CreateDetailsPanel(canvasRect, font, uiSprite, out detailsPanelRect, out detailsText);

        CreateScrollView(panelRect, uiSprite, out RectTransform scrollContent);

        // ── Section 1: Pillar Heights ────────────────────────────────────
        var s1 = CreateSection(scrollContent, "Section_Pillars", "1. Pillar Heights (Ft)", font, uiSprite);
        CreateNumericRow(s1, new FieldConfig(MobileSolarFieldId.HeightFront, "Front",  10f, 20f), font, uiSprite);
        CreateNumericRow(s1, new FieldConfig(MobileSolarFieldId.HeightMid,   "Middle", 10f, 20f), font, uiSprite);
        CreateNumericRow(s1, new FieldConfig(MobileSolarFieldId.HeightBack,  "Back",   10f, 20f), font, uiSprite);

        // ── Section 2: Solar Panels ──────────────────────────────────────
        var s2 = CreateSection(scrollContent, "Section_SolarPanels", "2. Solar Panels", font, uiSprite);
        CreateNumericRow(s2, new FieldConfig(MobileSolarFieldId.PanelWidth,           "Plate Width",       2f,   6f),  font, uiSprite);
        CreateNumericRow(s2, new FieldConfig(MobileSolarFieldId.PanelLength,          "Plate Length",      4f,   10f), font, uiSprite);
        CreateNumericRow(s2, new FieldConfig(MobileSolarFieldId.PanelThickness,       "Plate Thickness",   0.05f,0.4f),font, uiSprite);
        CreateNumericRow(s2, new FieldConfig(MobileSolarFieldId.RowGapZ,              "Row Distance",      0f,   5f),  font, uiSprite);
        CreateNumericRow(s2, new FieldConfig(MobileSolarFieldId.PanelsOffset,         "Slide Along Rail",  -10f, 10f), font, uiSprite);        CreateNumericRow(s2, new FieldConfig(MobileSolarFieldId.PanelsVerticalOffset, "Lift Up / Down",    -3f,  3f),  font, uiSprite);

        Toggle equalGapToggle = CreateToggleRow(s2, "Toggle_EqualPanelGaps", "Use Equal Panel Gaps", font, uiSprite);
        RectTransform equalGapGroup      = CreateSubGroup(s2, "EqualGapGroup");
        CreateNumericRow(equalGapGroup, new FieldConfig(MobileSolarFieldId.GlobalGap, "Global Gap", 0f, 2f), font, uiSprite);
        RectTransform individualGapGroup = CreateSubGroup(s2, "IndividualGapGroup");
        CreateNumericRow(individualGapGroup, new FieldConfig(MobileSolarFieldId.Gap1, "Gap 1", 0f, 2f), font, uiSprite);
        CreateNumericRow(individualGapGroup, new FieldConfig(MobileSolarFieldId.Gap2, "Gap 2", 0f, 2f), font, uiSprite);
        CreateNumericRow(individualGapGroup, new FieldConfig(MobileSolarFieldId.Gap3, "Gap 3", 0f, 2f), font, uiSprite);
        CreateNumericRow(individualGapGroup, new FieldConfig(MobileSolarFieldId.Gap4, "Gap 4", 0f, 2f), font, uiSprite);

        // ── Section 3: Top Beam ──────────────────────────────────────────
        var s3 = CreateSection(scrollContent, "Section_TopBeam", "3. Top Beam", font, uiSprite);
        CreateNumericRow(s3, new FieldConfig(MobileSolarFieldId.TopBeamLength, "Length",    10f,  25f),  font, uiSprite);
        CreateNumericRow(s3, new FieldConfig(MobileSolarFieldId.TopBeamSize,   "Size",      0.1f, 1f),   font, uiSprite);
        CreateNumericRow(s3, new FieldConfig(MobileSolarFieldId.TopBeamThick,  "Thickness", 0.005f,0.08f),font,uiSprite);

        // ── Section 4: C-Channel Rails ───────────────────────────────────
        var s4 = CreateSection(scrollContent, "Section_CChannel", "4. C-Channel Rails", font, uiSprite);
        CreateNumericRow(s4, new FieldConfig(MobileSolarFieldId.CChannelLength, "Length",    10f,  30f),  font, uiSprite);
        CreateNumericRow(s4, new FieldConfig(MobileSolarFieldId.CChannelSize,   "Size",      0.1f, 0.5f), font, uiSprite);
        CreateNumericRow(s4, new FieldConfig(MobileSolarFieldId.CChannelThick,  "Thickness", 0.005f,0.1f),font, uiSprite);

        // ── Section 5: Concrete Footings ─────────────────────────────────
        var s5 = CreateSection(scrollContent, "Section_Concrete", "5. Concrete Footings", font, uiSprite);
        Toggle showConcreteFootingsToggle = CreateToggleRow(s5, "Toggle_ShowConcreteFootings", "Show Concrete Footings", font, uiSprite);
        CreateNumericRow(s5, new FieldConfig(MobileSolarFieldId.ConcretePillarHeight, "Height (ft) — anchor strength", 0.2f, 6f), font, uiSprite);
        CreateNumericRow(s5, new FieldConfig(MobileSolarFieldId.ConcretePillarWidth,  "Width X (ft)",                  0.2f, 5f), font, uiSprite);
        CreateNumericRow(s5, new FieldConfig(MobileSolarFieldId.ConcretePillarDepth,  "Depth Z (ft)",                  0.2f, 5f), font, uiSprite);

        // ── Section 6: X-Crossing Bracing ────────────────────────────────
        var s6 = CreateSection(scrollContent, "Section_Bracing", "6. X-Crossing Bracing", font, uiSprite);
        CreateNumericRow(s6, new FieldConfig(MobileSolarFieldId.BracingBottomClearance, "Bottom Clearance (ft)", 0f,   8f),  font, uiSprite);
        CreateNumericRow(s6, new FieldConfig(MobileSolarFieldId.BracingTopClearance,    "Top Clearance (ft)",    0f,   4f),  font, uiSprite);
        CreateNumericRow(s6, new FieldConfig(MobileSolarFieldId.BracingSize,            "C-Channel Size",        0.1f, 0.5f),font, uiSprite);
        CreateNumericRow(s6, new FieldConfig(MobileSolarFieldId.BracingThick,           "Thickness",             0.005f,0.1f),font,uiSprite);

        // Per-bay toggles
        var bayToggleArr  = new Toggle[XBayCount];
        var bayXToggleArr = new Toggle[XBayCount];
        for (int i = 0; i < XBayCount; i++)
        {
            // Default: all bays on
            bayToggleArr[i]  = CreateToggleRow(s6, $"Toggle_Bay{i}",  $"  {BayNames[i]}",            font, uiSprite);
            bayXToggleArr[i] = CreateToggleRow(s6, $"Toggle_BayX{i}", $"    + X-Cross {BayNames[i]}", font, uiSprite);
            bayToggleArr[i].isOn  = true;
            bayXToggleArr[i].isOn = true;
        }
        Button smartDesignButton = CreateButton(s6, SmartDesignButtonName, "SMART 6 x 20FT BRACING", font, uiSprite);

        // ── Structure ────────────────────────────────────────────────────
        Transform structureRoot = CreateOrReplaceStructureRoot(font);

        // ── Wire SerializedObject ────────────────────────────────────────
        SerializedObject so = new SerializedObject(app);
        so.FindProperty("uiPanelRect").objectReferenceValue           = panelRect;
        so.FindProperty("equalGapGroup").objectReferenceValue         = equalGapGroup;
        so.FindProperty("individualGapGroup").objectReferenceValue    = individualGapGroup;
        so.FindProperty("equalGapToggle").objectReferenceValue        = equalGapToggle;
        so.FindProperty("resetButton").objectReferenceValue           = resetButton;
        so.FindProperty("panelToggleButton").objectReferenceValue     = panelToggleButton;
        so.FindProperty("panelToggleButtonLabel").objectReferenceValue= panelToggleButtonLabel;
        so.FindProperty("isControlPanelVisible").boolValue            = true;
        so.FindProperty("structureRoot").objectReferenceValue         = structureRoot;
        so.FindProperty("showConcreteFootingsToggle").objectReferenceValue = showConcreteFootingsToggle;
        so.FindProperty("viewFrontButton").objectReferenceValue       = viewFrontButton;
        so.FindProperty("viewSideButton").objectReferenceValue        = viewSideButton;
        so.FindProperty("viewTopButton").objectReferenceValue         = viewTopButton;
        so.FindProperty("viewIsoButton").objectReferenceValue         = viewIsoButton;
        so.FindProperty("viewPerspOrthoButton").objectReferenceValue  = viewPerspOrthoButton;
        // Wire the label inside the Persp/Ortho button
        TMP_Text perspOrthoLbl = viewPerspOrthoButton != null
            ? viewPerspOrthoButton.GetComponentInChildren<TMP_Text>(true) : null;
        so.FindProperty("viewPerspOrthoLabel").objectReferenceValue   = perspOrthoLbl;
        so.FindProperty("detailsPanelRect").objectReferenceValue      = detailsPanelRect;
        so.FindProperty("detailsText").objectReferenceValue           = detailsText;
        so.FindProperty("detailsToggleButton").objectReferenceValue   = detailsToggleButton;
        so.FindProperty("smartDesignButton").objectReferenceValue     = smartDesignButton;
        SerializedProperty syncDefaultsProp = so.FindProperty("syncSceneValuesWithDefaults");
        if (syncDefaultsProp != null) syncDefaultsProp.boolValue = true;

        SerializedProperty bayToggleProp  = so.FindProperty("bayToggle");
        SerializedProperty bayXToggleProp = so.FindProperty("bayXToggle");
        bayToggleProp.arraySize  = XBayCount;
        bayXToggleProp.arraySize = XBayCount;
        for (int i = 0; i < XBayCount; i++)
        {
            bayToggleProp.GetArrayElementAtIndex(i).objectReferenceValue  = bayToggleArr[i];
            bayXToggleProp.GetArrayElementAtIndex(i).objectReferenceValue = bayXToggleArr[i];
        }
        ApplyDefaultAppState(so);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(app);
        EditorUtility.SetDirty(canvasRect.gameObject);
        EditorUtility.SetDirty(panelRect.gameObject);
        EditorUtility.SetDirty(structureRoot.gameObject);
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);

        if (saveScene) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets(); }
        Debug.Log("SampleScene UI setup complete.");
    }

    // ════════════════════════════════════════════════════════════════════
    //  Structure hierarchy builder
    // ════════════════════════════════════════════════════════════════════
    private static void ApplyDefaultAppState(SerializedObject so)
    {
        CopyFloatProperty(so, "heightFront", "DefaultHeightFront");
        CopyFloatProperty(so, "heightMid", "DefaultHeightMid");
        CopyFloatProperty(so, "heightBack", "DefaultHeightBack");
        CopyFloatProperty(so, "topBeamLength", "DefaultTopBeamLength");
        CopyFloatProperty(so, "cChannelLength", "DefaultCChannelLength");
        CopyFloatProperty(so, "panelLength", "DefaultPanelLength");
        CopyFloatProperty(so, "panelWidth", "DefaultPanelWidth");
        CopyFloatProperty(so, "rowGapZ", "DefaultRowGapZ");
        CopyFloatProperty(so, "panelsOffset", "DefaultPanelsOffset");
        CopyFloatProperty(so, "globalPanelGap", "DefaultGlobalPanelGap");
        CopyFloatProperty(so, "concretePillarHeight", "DefaultConcretePillarHeight");
        CopyFloatProperty(so, "concretePillarWidth", "DefaultConcretePillarWidth");
        CopyFloatProperty(so, "concretePillarDepth", "DefaultConcretePillarDepth");
        CopyFloatProperty(so, "bracingBottomClearance", "DefaultBracingBottomClear");
        CopyFloatProperty(so, "bracingTopClearance", "DefaultBracingTopClear");

        so.FindProperty("panelsVerticalOffset").floatValue = 0f;
        so.FindProperty("panelThickness").floatValue = 0.13f;
        so.FindProperty("topBeamSize").floatValue = 5f / 12f;
        so.FindProperty("topBeamThick").floatValue = 1.5f / 8f / 12f;
        so.FindProperty("cChannelSize").floatValue = 2.5f / 12f;
        so.FindProperty("cChannelThick").floatValue = 0.02f;
        so.FindProperty("bracingSize").floatValue = 2f / 12f;
        so.FindProperty("bracingThick").floatValue = 0.02f;
        so.FindProperty("useEqualPanelGaps").boolValue = true;
        so.FindProperty("showConcretePillars").boolValue = true;

        SerializedProperty gapsProp = so.FindProperty("individualPanelGaps");
        gapsProp.arraySize = 4;
        float gap = so.FindProperty("DefaultGlobalPanelGap").floatValue;
        for (int i = 0; i < gapsProp.arraySize; i++)
            gapsProp.GetArrayElementAtIndex(i).floatValue = gap;

        SerializedProperty bayEnabledProp = so.FindProperty("bayEnabled");
        SerializedProperty bayXEnabledProp = so.FindProperty("bayXEnabled");
        bayEnabledProp.arraySize = XBayCount;
        bayXEnabledProp.arraySize = XBayCount;
        for (int i = 0; i < XBayCount; i++)
        {
            bayEnabledProp.GetArrayElementAtIndex(i).boolValue = true;
            bayXEnabledProp.GetArrayElementAtIndex(i).boolValue = true;
        }
    }

    private static void CopyFloatProperty(SerializedObject so, string targetName, string sourceName)
    {
        SerializedProperty target = so.FindProperty(targetName);
        SerializedProperty source = so.FindProperty(sourceName);
        if (target != null && source != null) target.floatValue = source.floatValue;
    }

    private static Transform CreateOrReplaceStructureRoot(TMP_FontAsset font)
    {
        var existing = GameObject.Find(StructureRootName);
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing);

        Transform root     = new GameObject(StructureRootName).transform;
        Transform pillars  = new GameObject(PillarsRootName).transform; pillars.SetParent(root, false);
        Transform beams    = new GameObject(BeamsRootName).transform;   beams.SetParent(root, false);
        Transform rails    = new GameObject(RailsRootName).transform;   rails.SetParent(root, false);
        Transform panels   = new GameObject(PanelsRootName).transform;  panels.SetParent(root, false);
        Transform bracing  = new GameObject(BracingRootName).transform; bracing.SetParent(root, false);

        for (int col = 0; col < 3; col++)
        {
            for (int sup = 0; sup < 3; sup++)
            {
                Transform sr = new GameObject($"Support_{col}_{sup}").transform;
                sr.SetParent(pillars, false);
                CreateCubeObject("ConcreteFooting", sr);
                CreateCubeObject("BasePlate", sr);
                CreateHGirderRoot("Pillar", sr);
                CreateCubeObject("TopPlate", sr);
                CreateWorldText("Label", sr, font, false);
            }
            CreateHGirderRoot($"Beam_{col}", beams);
        }
        for (int r = 0; r < 4; r++) CreateCChannelRoot($"Rail_{r}", rails);
        for (int row = 0; row < 2; row++)
            for (int col = 0; col < 5; col++)
            {
                var p = CreateCubeObject($"Panel_{row}_{col}", panels);
                CreateWorldText("Label", p.transform, font, true);
            }
        for (int i = 0; i < XBayCount; i++)
        {
            Transform bayRoot = new GameObject($"XBay_{i}").transform;
            bayRoot.SetParent(bracing, false);
            Transform diagA = CreateCChannelRoot("DiagA", bayRoot);
            Transform diagB = CreateCChannelRoot("DiagB", bayRoot);
            CreateWorldText("Label", diagA, font, false);
            CreateWorldText("Label", diagB, font, false);
        }
        return root;
    }

    // ════════════════════════════════════════════════════════════════════
    //  UI helpers
    // ════════════════════════════════════════════════════════════════════
    private static void EnsureEventSystem()
    {
        EventSystem es = UnityEngine.Object.FindObjectOfType<EventSystem>();
        if (es == null) { var go = new GameObject("EventSystem", typeof(EventSystem)); es = go.GetComponent<EventSystem>(); }
#if ENABLE_INPUT_SYSTEM
        if (es.GetComponent<InputSystemUIInputModule>() == null) es.gameObject.AddComponent<InputSystemUIInputModule>();
        var old = es.GetComponent<StandaloneInputModule>();
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
#else
        if (es.GetComponent<StandaloneInputModule>() == null) es.gameObject.AddComponent<StandaloneInputModule>();
#endif
    }

    private static TMP_FontAsset LoadFont()
    {
        var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset")
             ?? TMP_Settings.defaultFontAsset;
        if (f == null) throw new InvalidOperationException("Could not load TMP font.");
        return f;
    }

    private static RectTransform CreateSection(RectTransform parent, string name, string title, TMP_FontAsset font, Sprite sprite)
    {
        var r = CreateUiObject(name, parent, typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        var img = r.GetComponent<Image>(); img.color = new Color(1f,1f,1f,0.06f); img.sprite = sprite; img.type = Image.Type.Sliced;
        var vl = r.GetComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(14,14,14,14); vl.spacing = 10f;
        vl.childAlignment = TextAnchor.UpperLeft; vl.childControlWidth = true; vl.childControlHeight = false;
        vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
        var csf = r.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize; csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        CreateText(r, title, font, 24f, FontStyles.Bold, Color.white, TextAlignmentOptions.Left);
        return r;
    }

    private static RectTransform CreateSubGroup(RectTransform parent, string name)
    {
        var r = CreateUiObject(name, parent, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        var vl = r.GetComponent<VerticalLayoutGroup>();
        vl.spacing = 8f; vl.childAlignment = TextAnchor.UpperLeft;
        vl.childControlWidth = true; vl.childControlHeight = false;
        vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
        var csf = r.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize; csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        return r;
    }

    private static MobileSolarFrameFieldBinding CreateNumericRow(RectTransform parent, FieldConfig cfg, TMP_FontAsset font, Sprite sprite)
    {
        var row = CreateUiObject("Field_"+cfg.Id, parent, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(MobileSolarFrameFieldBinding));
        var vl = row.GetComponent<VerticalLayoutGroup>();
        vl.spacing = 6f; vl.childAlignment = TextAnchor.UpperLeft;
        vl.childControlWidth = true; vl.childControlHeight = false;
        vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
        var csf = row.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize; csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        var topRow = CreateUiObject("TopRow", row, typeof(HorizontalLayoutGroup));
        var hl = topRow.GetComponent<HorizontalLayoutGroup>();
        hl.spacing = 10f; hl.childAlignment = TextAnchor.MiddleLeft;
        hl.childControlWidth = false; hl.childControlHeight = false;
        hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;

        var lbl = CreateText(topRow, cfg.Label, font, 21f, FontStyles.Normal, Color.white, TextAlignmentOptions.MidlineLeft);
        var le = lbl.gameObject.AddComponent<LayoutElement>(); le.flexibleWidth = 1f; le.minHeight = 28f;

        var input  = CreateInputField(topRow, font, sprite);
        var slider = CreateSlider(row, sprite);

        var b = row.GetComponent<MobileSolarFrameFieldBinding>();
        b.FieldId = cfg.Id; b.MinValue = cfg.Min; b.MaxValue = cfg.Max; b.Slider = slider; b.InputField = input;
        return b;
    }

    private static Toggle CreateToggleRow(RectTransform parent, string name, string label, TMP_FontAsset font, Sprite sprite)
    {
        var row = CreateUiObject(name, parent, typeof(Image), typeof(Toggle), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.GetComponent<LayoutElement>().preferredHeight = 44f;
        var img = row.GetComponent<Image>(); img.color = new Color(1f,1f,1f,0.04f); img.sprite = sprite; img.type = Image.Type.Sliced;
        var hl = row.GetComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(10,10,8,8); hl.spacing = 12f; hl.childAlignment = TextAnchor.MiddleLeft;
        hl.childControlWidth = false; hl.childControlHeight = false;
        hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;

        var cbr = CreateUiObject("CheckBackground", row, typeof(Image));
        cbr.sizeDelta = new Vector2(24f,24f);
        var cbi = cbr.GetComponent<Image>(); cbi.color = new Color(0.04f,0.06f,0.09f,0.96f); cbi.sprite = sprite; cbi.type = Image.Type.Sliced;
        var cmr = CreateUiObject("Checkmark", cbr, typeof(Image));
        StretchRect(cmr, 5f,5f,5f,5f);
        var cmi = cmr.GetComponent<Image>(); cmi.color = new Color(0.24f,0.73f,0.92f,1f); cmi.sprite = sprite; cmi.type = Image.Type.Sliced;

        var lt = CreateText(row, label, font, 19f, FontStyles.Normal, Color.white, TextAlignmentOptions.MidlineLeft);
        lt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        var tog = row.GetComponent<Toggle>(); tog.targetGraphic = cbi; tog.graphic = cmi;
        return tog;
    }

    private static Button CreateButton(RectTransform parent, string name, string label, TMP_FontAsset font, Sprite sprite)
    {
        var r = CreateUiObject(name, parent, typeof(Image), typeof(Button), typeof(LayoutElement));
        r.GetComponent<LayoutElement>().preferredHeight = 50f;
        var img = r.GetComponent<Image>(); img.color = new Color(0.24f,0.73f,0.92f,1f); img.sprite = sprite; img.type = Image.Type.Sliced;
        var btn = r.GetComponent<Button>(); btn.transition = Selectable.Transition.ColorTint;
        btn.colors = CreateSelectableColors(new Color(0.24f,0.73f,0.92f,1f), new Color(0.3f,0.82f,1f,1f), new Color(0.16f,0.56f,0.72f,1f));
        CreateText(r, label, font, 22f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        return btn;
    }

    private static Button CreatePanelToggleButton(RectTransform canvasRect, RectTransform panelRect, TMP_FontAsset font, Sprite sprite, out TMP_Text labelText)
    {
        var r = CreateUiObject(PanelToggleButtonName, canvasRect, typeof(Image), typeof(Button));
        r.anchorMin = new Vector2(0f,0.5f); r.anchorMax = new Vector2(0f,0.5f); r.pivot = new Vector2(0f,0.5f);
        r.anchoredPosition = new Vector2(panelRect.anchoredPosition.x + panelRect.sizeDelta.x + 8f, 0f);
        r.sizeDelta = new Vector2(36f,108f);
        var img = r.GetComponent<Image>(); img.color = new Color(0.16f,0.2f,0.28f,0.98f); img.sprite = sprite; img.type = Image.Type.Sliced;
        var btn = r.GetComponent<Button>(); btn.transition = Selectable.Transition.ColorTint;
        btn.colors = CreateSelectableColors(new Color(0.16f,0.2f,0.28f,0.98f), new Color(0.22f,0.28f,0.38f,1f), new Color(0.1f,0.14f,0.2f,1f));
        labelText = CreateText(r, "<", font, 30f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        StretchRect(labelText.rectTransform, 0f,0f,0f,0f);
        return btn;
    }

    // View buttons: stacked vertically in top-right corner of canvas
    private static Button CreateViewButton(RectTransform canvasRect, string name, string label, TMP_FontAsset font, Sprite sprite, int index)
    {
        var r = CreateUiObject(name, canvasRect, typeof(Image), typeof(Button));
        r.anchorMin = new Vector2(1f, 1f); r.anchorMax = new Vector2(1f, 1f);
        r.pivot     = new Vector2(1f, 1f);
        float btnSize = 72f;
        float margin  = 10f;
        r.anchoredPosition = new Vector2(-margin, -(margin + index * (btnSize + 6f)));
        r.sizeDelta = new Vector2(btnSize, btnSize);
        var img = r.GetComponent<Image>();
        img.color = new Color(0.12f, 0.16f, 0.22f, 0.95f); img.sprite = sprite; img.type = Image.Type.Sliced;
        var btn = r.GetComponent<Button>(); btn.transition = Selectable.Transition.ColorTint;
        btn.colors = CreateSelectableColors(
            new Color(0.12f,0.16f,0.22f,0.95f),
            new Color(0.24f,0.73f,0.92f,1f),
            new Color(0.16f,0.56f,0.72f,1f));
        var lbl = CreateText(r, label, font, 18f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        StretchRect(lbl.rectTransform, 4f, 4f, 4f, 4f);
        return btn;
    }

    // Details toggle button — bottom-right corner
    private static Button CreateDetailsToggleButton(RectTransform canvasRect, TMP_FontAsset font, Sprite sprite)
    {
        var r = CreateUiObject("Button_ToggleDetails", canvasRect, typeof(Image), typeof(Button));
        r.anchorMin = new Vector2(1f, 0f); r.anchorMax = new Vector2(1f, 0f);
        r.pivot     = new Vector2(1f, 0f);
        r.anchoredPosition = new Vector2(-10f, 10f);
        r.sizeDelta = new Vector2(160f, 52f);
        var img = r.GetComponent<Image>();
        img.color = new Color(0.10f, 0.55f, 0.30f, 0.95f); img.sprite = sprite; img.type = Image.Type.Sliced;
        var btn = r.GetComponent<Button>(); btn.transition = Selectable.Transition.ColorTint;
        btn.colors = CreateSelectableColors(
            new Color(0.10f,0.55f,0.30f,0.95f),
            new Color(0.15f,0.75f,0.40f,1f),
            new Color(0.07f,0.38f,0.20f,1f));
        var lbl = CreateText(r, "Details  ▶", font, 20f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        StretchRect(lbl.rectTransform, 6f, 6f, 6f, 6f);
        return btn;
    }

    // Details panel — right side of canvas, hidden by default
    private static void CreateDetailsPanel(RectTransform canvasRect, TMP_FontAsset font, Sprite sprite,
        out RectTransform panelRect, out TMP_Text detailsText)
    {
        // Panel container — anchored to right edge, full height
        var panel = CreateUiObject("DetailsPanel", canvasRect, typeof(Image));
        panel.anchorMin = new Vector2(1f, 0f);
        panel.anchorMax = new Vector2(1f, 1f);
        panel.pivot     = new Vector2(1f, 0.5f);
        panel.anchoredPosition = new Vector2(0f, 0f);
        panel.sizeDelta = new Vector2(500f, -20f);
        var panelImg = panel.GetComponent<Image>();
        panelImg.color  = new Color(0.04f, 0.07f, 0.11f, 0.98f);
        panelImg.sprite = sprite;
        panelImg.type   = Image.Type.Sliced;
        panel.gameObject.SetActive(false); // hidden by default

        // Header bar
        var header = CreateUiObject("Header", panel, typeof(Image));
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = new Vector2(1f, 1f);
        header.pivot     = new Vector2(0.5f, 1f);
        header.anchoredPosition = Vector2.zero;
        header.sizeDelta = new Vector2(0f, 56f);
        header.GetComponent<Image>().color = new Color(0.07f, 0.42f, 0.22f, 1f);
        var headerTxt = CreateText(header, "STRUCTURE DETAILS", font, 26f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        StretchRect(headerTxt.rectTransform, 8f, 8f, 4f, 4f);

        // Scroll view — fills panel below header
        var scrollObj = CreateUiObject("Scroll View", panel, typeof(Image), typeof(ScrollRect));
        scrollObj.anchorMin = new Vector2(0f, 0f);
        scrollObj.anchorMax = new Vector2(1f, 1f);
        scrollObj.offsetMin = new Vector2(0f, 0f);
        scrollObj.offsetMax = new Vector2(0f, -56f);
        scrollObj.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

        // Viewport
        var viewport = CreateUiObject("Viewport", scrollObj, typeof(Image), typeof(RectMask2D));
        StretchRect(viewport, 0f, 0f, 0f, 0f);
        viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);

        // Content — grows with text
        var content = CreateUiObject("Content", viewport, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot     = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        var vl = content.GetComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(14, 14, 14, 14);
        vl.spacing = 0f;
        vl.childAlignment = TextAnchor.UpperLeft;
        vl.childControlWidth  = true;
        vl.childControlHeight = false;
        vl.childForceExpandWidth  = true;
        vl.childForceExpandHeight = false;

        var csf = content.GetComponent<ContentSizeFitter>();
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // Text object — inside content, auto-sizes
        var textObj = CreateUiObject("DetailsText", content, typeof(TextMeshProUGUI), typeof(LayoutElement), typeof(ContentSizeFitter));

        var le2 = textObj.GetComponent<LayoutElement>();
        le2.flexibleWidth = 1f;

        var csf2 = textObj.GetComponent<ContentSizeFitter>();
        csf2.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        csf2.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // Stretch to fill content width
        textObj.anchorMin = new Vector2(0f, 1f);
        textObj.anchorMax = new Vector2(1f, 1f);
        textObj.pivot     = new Vector2(0.5f, 1f);
        textObj.anchoredPosition = Vector2.zero;
        textObj.sizeDelta = Vector2.zero;

        var txt = textObj.GetComponent<TextMeshProUGUI>();
        txt.font               = font;
        txt.fontSize           = 17f;
        txt.color              = new Color(0.88f, 0.92f, 0.96f, 1f);
        txt.enableWordWrapping = true;
        txt.richText           = true;
        txt.raycastTarget      = false;
        txt.overflowMode       = TextOverflowModes.Overflow;
        txt.text               = "Press the Details button to load...";

        // Wire scroll rect
        var scroll = scrollObj.GetComponent<ScrollRect>();
        scroll.viewport    = viewport;
        scroll.content     = content;
        scroll.horizontal  = false;
        scroll.vertical    = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;
        scroll.verticalNormalizedPosition = 1f;

        panelRect   = panel;
        detailsText = txt;
    }

    private static ScrollRect CreateScrollView(RectTransform parent, Sprite sprite, out RectTransform contentRect)
    {
        var sr = CreateUiObject("Scroll View", parent, typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
        var le = sr.GetComponent<LayoutElement>(); le.flexibleHeight = 1f; le.minHeight = 100f;
        sr.GetComponent<Image>().color = new Color(0f,0f,0f,0f);
        var vp = CreateUiObject("Viewport", sr, typeof(Image), typeof(RectMask2D));
        StretchRect(vp, 0f,0f,0f,0f); vp.GetComponent<Image>().color = new Color(0f,0f,0f,0.001f);
        contentRect = CreateUiObject("Content", vp, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentRect.anchorMin = new Vector2(0f,1f); contentRect.anchorMax = new Vector2(1f,1f);
        contentRect.pivot = new Vector2(0.5f,1f); contentRect.anchoredPosition = Vector2.zero; contentRect.sizeDelta = Vector2.zero;
        var cl = contentRect.GetComponent<VerticalLayoutGroup>();
        cl.spacing = 12f; cl.childAlignment = TextAnchor.UpperLeft;
        cl.childControlWidth = true; cl.childControlHeight = false;
        cl.childForceExpandWidth = true; cl.childForceExpandHeight = false;
        var csf = contentRect.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize; csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        var scroll = sr.GetComponent<ScrollRect>();
        scroll.viewport = vp; scroll.content = contentRect;
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30f; scroll.verticalNormalizedPosition = 1f;
        return scroll;
    }

    private static Slider CreateSlider(RectTransform parent, Sprite sprite)
    {
        var sr = CreateUiObject("Slider", parent, typeof(Image), typeof(Slider), typeof(LayoutElement));
        sr.GetComponent<LayoutElement>().preferredHeight = 24f;
        var bg = sr.GetComponent<Image>(); bg.color = new Color(1f,1f,1f,0.14f); bg.sprite = sprite; bg.type = Image.Type.Sliced;
        var fa = CreateUiObject("Fill Area", sr); StretchRect(fa, 10f,10f,7f,7f);
        var fi = CreateUiObject("Fill", fa, typeof(Image)); StretchRect(fi, 0f,0f,0f,0f);
        var fii = fi.GetComponent<Image>(); fii.color = new Color(0.24f,0.73f,0.92f,1f); fii.sprite = sprite; fii.type = Image.Type.Sliced;
        var ha = CreateUiObject("Handle Slide Area", sr); StretchRect(ha, 10f,10f,0f,0f);
        var hr = CreateUiObject("Handle", ha, typeof(Image)); hr.sizeDelta = new Vector2(20f,20f);
        var hi = hr.GetComponent<Image>(); hi.color = Color.white; hi.sprite = sprite; hi.type = Image.Type.Sliced;
        var sl = sr.GetComponent<Slider>(); sl.fillRect = fi; sl.handleRect = hr; sl.targetGraphic = hi;
        sl.direction = Slider.Direction.LeftToRight; sl.transition = Selectable.Transition.ColorTint;
        sl.colors = CreateSelectableColors(Color.white, Color.white, new Color(0.8f,0.86f,0.92f,1f));
        return sl;
    }

    private static TMP_InputField CreateInputField(RectTransform parent, TMP_FontAsset font, Sprite sprite)
    {
        var ir = CreateUiObject("InputField", parent, typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
        var le = ir.GetComponent<LayoutElement>(); le.preferredWidth = 110f; le.preferredHeight = 42f;
        var bg = ir.GetComponent<Image>(); bg.color = new Color(0.04f,0.06f,0.09f,0.96f); bg.sprite = sprite; bg.type = Image.Type.Sliced;
        var inf = ir.GetComponent<TMP_InputField>();
        inf.contentType = TMP_InputField.ContentType.DecimalNumber; inf.lineType = TMP_InputField.LineType.SingleLine;
        inf.characterLimit = 10; inf.customCaretColor = true; inf.caretColor = Color.white;
        inf.selectionColor = new Color(0.24f,0.73f,0.92f,0.35f); inf.onFocusSelectAll = true; inf.restoreOriginalTextOnEscape = false;
        var vp = CreateUiObject("Text Area", ir, typeof(RectMask2D)); StretchRect(vp, 10f,10f,6f,6f);
        var tc = CreateText(vp, string.Empty, font, 20f, FontStyles.Normal, Color.white, TextAlignmentOptions.Center);
        StretchRect(tc.rectTransform, 0f,0f,0f,0f); tc.enableWordWrapping = false;
        var ph = CreateText(vp, "0", font, 20f, FontStyles.Italic, new Color(0.72f,0.79f,0.85f,0.6f), TextAlignmentOptions.Center);
        StretchRect(ph.rectTransform, 0f,0f,0f,0f); ph.enableWordWrapping = false;
        inf.textViewport = vp; inf.textComponent = tc; inf.placeholder = ph;
        return inf;
    }

    // ════════════════════════════════════════════════════════════════════
    //  Structure primitive helpers
    // ════════════════════════════════════════════════════════════════════
    private static GameObject CreateCubeObject(string name, Transform parent)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name; go.transform.SetParent(parent, false);
        var col = go.GetComponent<Collider>(); if (col) UnityEngine.Object.DestroyImmediate(col);
        return go;
    }

    private static Transform CreateHGirderRoot(string name, Transform parent)
    {
        var r = new GameObject(name).transform; r.SetParent(parent, false);
        CreateCubeObject("Web", r); CreateCubeObject("F1", r); CreateCubeObject("F2", r);
        return r;
    }

    private static Transform CreateCChannelRoot(string name, Transform parent)
    {
        var r = new GameObject(name).transform; r.SetParent(parent, false);
        CreateCubeObject("Back", r); CreateCubeObject("Top", r); CreateCubeObject("Bot", r);
        return r;
    }

    private static TextMeshPro CreateWorldText(string name, Transform parent, TMP_FontAsset font, bool isPanelLabel)
    {
        var go = new GameObject(name, typeof(TextMeshPro));
        go.transform.SetParent(parent, false);
        // Scale down — TMP world units are large by default
        go.transform.localScale = Vector3.one * 0.1f;

        var t = go.GetComponent<TextMeshPro>();
        t.font               = font;
        t.alignment          = TextAlignmentOptions.Center;
        t.enableAutoSizing   = true;
        t.fontSizeMin        = isPanelLabel ? 4f  : 3f;
        t.fontSizeMax        = isPanelLabel ? 14f : 9f;
        t.fontSize           = isPanelLabel ? 14f : 9f;
        t.color              = Color.white;
        t.outlineColor       = new Color32(0, 0, 0, 255);
        t.outlineWidth       = 0.4f;
        t.raycastTarget      = false;
        t.enableWordWrapping = isPanelLabel;
        t.overflowMode       = TextOverflowModes.Overflow;
        t.text               = isPanelLabel ? "Panel" : "Pillar";
        // Larger rect so text has room to render
        t.rectTransform.sizeDelta = isPanelLabel ? new Vector2(80f, 30f) : new Vector2(55f, 20f);
        return t;
    }

    private static TextMeshProUGUI CreateText(RectTransform parent, string text, TMP_FontAsset font, float size, FontStyles style, Color color, TextAlignmentOptions align)
    {
        var r = CreateUiObject("Text", parent, typeof(TextMeshProUGUI));
        var t = r.GetComponent<TextMeshProUGUI>();
        t.font = font; t.text = text; t.fontSize = size; t.color = color; t.fontStyle = style;
        t.alignment = align; t.enableWordWrapping = true; t.raycastTarget = false;
        return t;
    }

    private static RectTransform CreateUiObject(string name, Transform parent, params Type[] components)
    {
        var types = new List<Type> { typeof(RectTransform) };
        types.AddRange(components);
        var go = new GameObject(name, types.ToArray()); go.layer = 5;
        var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false); rt.localScale = Vector3.one;
        return rt;
    }

    private static void StretchRect(RectTransform rt, float l, float r, float t, float b)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
    }

    private static ColorBlock CreateSelectableColors(Color normal, Color highlighted, Color pressed)
    {
        var cb = ColorBlock.defaultColorBlock;
        cb.normalColor = normal; cb.highlightedColor = highlighted; cb.selectedColor = highlighted;
        cb.pressedColor = pressed; cb.disabledColor = new Color(normal.r, normal.g, normal.b, 0.35f);
        cb.colorMultiplier = 1f; cb.fadeDuration = 0.1f;
        return cb;
    }
}
