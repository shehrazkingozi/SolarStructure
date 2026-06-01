using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using EnhancedTouchSupport = UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport;
using InputSystemTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
#endif

public class MobileSolarFrameApp : MonoBehaviour
{
    private const float FallbackUiWidthRatio = 0.45f;
    private const float InputSystemMouseScrollStep = 120f;
    private const float MinCameraDistance = 10f;
    private const float MaxCameraDistance = 80f;
    private const float MinXRotation = -10f;
    private const float MaxXRotation = 85f;
    private const float HiddenPanelXMargin = 12f;
    private const float ToggleButtonVisibleX = 8f;
    private const float ToggleButtonSpacing = 8f;
    private const float SteelDensityLbPerFt3 = 490f;
    private const float ConcreteWeightLbFt3 = 145f;
    private const float PanelWeightLbEach = 34f;
    private const float SmartDesignBracePieceLengthFt = 20f;
    private const int SmartDesignBracePieceCount = 6;
    private const float SmartDesignTotalStockLengthFt = SmartDesignBracePieceLengthFt * SmartDesignBracePieceCount;

    private const int ColumnCount = 3;
    private const int SupportCount = 3;
    private const int RailCount = 4;
    private const int PanelRowCount = 2;
    private const int PanelColumnCount = 5;
    private const int GapCount = 4;

    // X-crossing bays:
    //   Back  bays (Z-plane): col0-col1 @ sup2, col1-col2 @ sup2          => 2 bays  [0,1]
    //   Front bays (Z-plane): col0-col1 @ sup0, col1-col2 @ sup0          => 2 bays  [2,3]
    //   Mid   bays (Z-plane): col0-col1 @ sup1, col1-col2 @ sup1          => 2 bays  [4,5]
    //   Side  bays col0 (X-plane): sup0→sup1, sup1→sup2                   => 2 bays  [6,7]
    //   Side  bays col1 (X-plane): sup0→sup1, sup1→sup2                   => 2 bays  [8,9]
    //   Side  bays col2 (X-plane): sup0→sup1, sup1→sup2                   => 2 bays  [10,11]
    // Total individual X-bays = 12, each has 2 diagonals => max 24 brace objects
    private const int XBayCount = 12;
    
    // Stay line heights from bottom (in feet)
    private const float StayLineHeight1 = 8f;
    private const float StayLineHeight2 = 7f;
    // Lines per row (Z-plane bays have 4 lines each)
    private const int StayLinesPerRow = 4;
    // Total stay line count: 3 rows (front/mid/back) x 4 lines = 12
    private const int StayLineCount = 12;
    // L-shaped angle bracket count (one per column at bottom)
    private const int AngleBracketCount = 3;

    private const string StructureRootName  = "Solar_Mobile_Structure";
    private const string PillarsRootName    = "Pillars";
    private const string BeamsRootName      = "TopBeams";
    private const string RailsRootName      = "Rails";
    private const string PanelsRootName     = "Panels";

    private static readonly float[] ColumnXPositions  = { -10f, 0f, 10f };
    private static readonly float[] SupportZPositions = { 0f, 7f, 14f };

    // ── Bay definitions ──────────────────────────────────────────────────
    // Each bay: (colA, colB, supportIndex, isZPlane)
    // isZPlane=true  → braces span X between colA and colB at fixed Z (support)
    // isZPlane=false → braces span Z between two supports at fixed X (colA)
    private struct BayDef
    {
        public int ColA, ColB, SupportA, SupportB;
        public string Name;
    }

    private static readonly BayDef[] BayDefs = new BayDef[XBayCount]
    {
        // [0,1] Back Z-plane bays (support 2)
        new BayDef { ColA=0, ColB=1, SupportA=2, SupportB=2, Name="Back_Bay0" },
        new BayDef { ColA=1, ColB=2, SupportA=2, SupportB=2, Name="Back_Bay1" },
        // [2,3] Front Z-plane bays (support 0)
        new BayDef { ColA=0, ColB=1, SupportA=0, SupportB=0, Name="Front_Bay0" },
        new BayDef { ColA=1, ColB=2, SupportA=0, SupportB=0, Name="Front_Bay1" },
        // [4,5] Mid Z-plane bays (support 1)
        new BayDef { ColA=0, ColB=1, SupportA=1, SupportB=1, Name="Mid_Bay0" },
        new BayDef { ColA=1, ColB=2, SupportA=1, SupportB=1, Name="Mid_Bay1" },
        // [6,7] Side col0: front→mid, mid→back
        new BayDef { ColA=0, ColB=0, SupportA=0, SupportB=1, Name="Side_L_FrontMid" },
        new BayDef { ColA=0, ColB=0, SupportA=1, SupportB=2, Name="Side_L_MidBack"  },
        // [8,9] Side col1 (center): front→mid, mid→back
        new BayDef { ColA=1, ColB=1, SupportA=0, SupportB=1, Name="Side_C_FrontMid" },
        new BayDef { ColA=1, ColB=1, SupportA=1, SupportB=2, Name="Side_C_MidBack"  },
        // [10,11] Side col2: front→mid, mid→back
        new BayDef { ColA=2, ColB=2, SupportA=0, SupportB=1, Name="Side_R_FrontMid" },
        new BayDef { ColA=2, ColB=2, SupportA=1, SupportB=2, Name="Side_R_MidBack"  },
    };

    private sealed class NumericControlBinding
    {
        public MobileSolarFrameFieldBinding View;
        public Func<float>   Getter;
        public Action<float> Setter;
    }

    private sealed class PillarVisual
    {
        public Transform Root;
        public Transform BasePlate;
        public Transform Pillar;
        public Transform TopPlate;
        public Transform ConcreteFooting;
        public TMP_Text  Label;
    }

    // One BraceVisual holds the two diagonals of a single X-bay
    private sealed class XBayVisual
    {
        public Transform DiagA; // always shown when bay is enabled
        public Transform DiagB; // shown only when showXCrossing is true
        public TMP_Text  LabelA;
        public TMP_Text  LabelB;
    }
    
    // Stay line visual - vertical structural lines
    private sealed class StayLineVisual
    {
        public Transform Root;
        public TMP_Text Label;
    }
    
    // L-shaped angle bracket visual for column base
    private sealed class AngleBracketVisual
    {
        public Transform Root;
        public Transform Web;  // vertical part
        public Transform Flange; // horizontal base part (L-shape)
        public TMP_Text Label;
    }

    private sealed class BeamVisual  { public Transform Root; }
    private sealed class RailVisual  { public Transform Root; }
    private sealed class PanelVisual { public Transform Root; public TMP_Text Label; }

    private struct BraceLayoutTotals
    {
        public int ActiveBayCount;
        public int FullXBayCount;
        public int SingleBayCount;
        public int ActiveDiagonalCount;
        public float TotalLengthFt;
        public float TotalWeightLb;
        public float WeightPerFootLb;
    }

    // ── Serialized UI References ─────────────────────────────────────────
    [Header("Scene UI References")]
    [SerializeField] private RectTransform uiPanelRect;
    [SerializeField] private RectTransform equalGapGroup;
    [SerializeField] private RectTransform individualGapGroup;
    [SerializeField] private Toggle        equalGapToggle;
    [SerializeField] private Button        resetButton;
    [SerializeField] private Button        panelToggleButton;
    [SerializeField] private TMP_Text      panelToggleButtonLabel;
    [SerializeField] private bool          isControlPanelVisible = true;
    // Axis view buttons
    [SerializeField] private Button        viewFrontButton;
    [SerializeField] private Button        viewSideButton;
    [SerializeField] private Button        viewTopButton;
    [SerializeField] private Button        viewIsoButton;
    [SerializeField] private Button        viewPerspOrthoButton;
    [SerializeField] private TMP_Text      viewPerspOrthoLabel;
    // Details panel
    [SerializeField] private RectTransform detailsPanelRect;
    [SerializeField] private TMP_Text      detailsText;
    [SerializeField] private Button        detailsToggleButton;
    [SerializeField] private bool          isDetailsPanelVisible = false;

    [Header("Structure Scene References")]
    [SerializeField] private Transform structureRoot;
    [SerializeField] private Transform labelsRoot;   // unscaled root for all world-space labels
    [SerializeField] private Button    smartDesignButton;
    [SerializeField] private bool      syncSceneValuesWithDefaults = true;

    [Header("UI Toggles — Visibility")]
    [SerializeField] private UnityEngine.UI.Toggle showConcreteFootingsToggle;
    // Per-bay X-crossing toggles (index matches BayDefs)
    [SerializeField] private UnityEngine.UI.Toggle[] bayToggle      = new UnityEngine.UI.Toggle[XBayCount];
    [SerializeField] private UnityEngine.UI.Toggle[] bayXToggle     = new UnityEngine.UI.Toggle[XBayCount];

    // ── Public Default Values (editable in Inspector) ────────────────────
    [Header("Default Values — Edit These Freely")]
    public float DefaultHeightFront          = 15f;
    public float DefaultHeightMid            = 16f;
    public float DefaultHeightBack           = 17f;
    public float DefaultTopBeamLength        = 15f;
    public float DefaultCChannelLength       = 21f;
    public float DefaultPanelLength          = 7.833f;
    public float DefaultPanelWidth           = 3.75f;
    public float DefaultRowGapZ              = 2f;
    public float DefaultPanelsOffset         = 0f;
    public float DefaultGlobalPanelGap       = 0.25f;
    public float DefaultConcretePillarHeight = 2f;
    public float DefaultConcretePillarWidth  = 1.5f;
    public float DefaultConcretePillarDepth  = 1.5f;
    public float DefaultBracingBottomClear   = 1f;
    public float DefaultBracingTopClear      = 0.5f;

    // ── Frame Settings ───────────────────────────────────────────────────
    [Header("Frame Settings")]
    [SerializeField] private bool    showConcretePillars = true;
    public enum BracingMode { XCrossing, StayLines }
    [SerializeField] private BracingMode bracingMode = BracingMode.XCrossing;
    [SerializeField] private Toggle bracingModeToggle;
    [SerializeField] private bool[]  bayEnabled          = new bool[XBayCount] { true,true,true,true,true,true,true,true,true,true,true,true };
    [SerializeField] private bool[]  bayXEnabled         = new bool[XBayCount] { true,true,true,true,true,true,true,true,true,true,true,true };
    [SerializeField] private float   panelsOffset        = 0f;
    [SerializeField] private float   panelsVerticalOffset= 0f;
    [SerializeField] private float   rowGapZ             = 2f;
    [SerializeField] private bool    useEqualPanelGaps   = true;
    [SerializeField] private float   globalPanelGap      = 0.25f;
    [SerializeField] private float[] individualPanelGaps = { 0.25f, 0.25f, 0.25f, 0.25f };

    [Header("Pillar Heights")]
    [SerializeField] private float heightFront = 15f;
    [SerializeField] private float heightMid   = 16f;
    [SerializeField] private float heightBack  = 17f;

    [Header("Top Beam")]
    [SerializeField] private float topBeamLength = 15f;
    [SerializeField] private float topBeamSize   = 5f / 12f;
    [SerializeField] private float topBeamThick  = 1.5f / 8f / 12f;

    [Header("C-Channel")]
    [SerializeField] private float cChannelLength = 21f;
    [SerializeField] private float cChannelSize   = 2.5f / 12f;
    [SerializeField] private float cChannelThick  = 0.02f;

    [Header("Panel")]
    [SerializeField] private float panelLength    = 7.833f;
    [SerializeField] private float panelWidth     = 3.75f;
    [SerializeField] private float panelThickness = 0.13f;

    [Header("Advanced / Bracing")]
    [SerializeField] private float concretePillarHeight  = 2f;
    [SerializeField] private float concretePillarWidth   = 1.5f;
    [SerializeField] private float concretePillarDepth   = 1.5f;
    [SerializeField] private float bracingBottomClearance= 1f;
    [SerializeField] private float bracingTopClearance   = 0.5f;
    [SerializeField] private float bracingSize           = 2f / 12f;
    [SerializeField] private float bracingThick          = 0.02f;

    // Camera state
    private bool isOrthographic = false;

    // ── Runtime ──────────────────────────────────────────────────────────
    private readonly List<NumericControlBinding> numericBindings = new List<NumericControlBinding>();
    private readonly PillarVisual[] pillarVisuals = new PillarVisual[ColumnCount * SupportCount];
    private readonly BeamVisual[]   beamVisuals   = new BeamVisual[ColumnCount];
    private readonly RailVisual[]   railVisuals   = new RailVisual[RailCount];
    private readonly PanelVisual[]  panelVisuals  = new PanelVisual[PanelRowCount * PanelColumnCount];
    private readonly XBayVisual[]   xBayVisuals   = new XBayVisual[XBayCount];
    private readonly StayLineVisual[] stayLineVisuals = new StayLineVisual[StayLineCount];
    private readonly AngleBracketVisual[] angleBracketVisuals = new AngleBracketVisual[AngleBracketCount];

    private Camera               mainCamera;
    private MaterialPropertyBlock stressPropertyBlock;
    private Transform            bracingRoot;
    private Transform            stayLinesRoot;
    private Transform            angleBracketsRoot;
    private Canvas               uiCanvas;
    private TMP_FontAsset        defaultFont;
    private bool                 uiInitialized;
    private bool                 structureMissingWarningShown;
    private bool                 structureShapeWarningShown;
    private bool                 panelExpandedPositionCaptured;
    private float                expandedPanelAnchoredX;
    private string               smartDesignSummary = string.Empty;

    private Transform pillarsRoot;
    private Transform beamsRoot;
    private Transform railsRoot;
    private Transform panelsRoot;

    private Material hGirderMaterial;
    private Material cChannelMaterial;
    private Material solarMaterial;
    private Material plateMaterial;
    private Material concreteMaterial;

    private Vector3 cameraTarget = new Vector3(0f, 10f, 7f);
    private float   distance     = 35f;
    private float   xRot         = 30f;
    private float   yRot         = 45f;

    private readonly float hSize         = 5f / 12f;
    private readonly float hThick        = 1.5f / 8f / 12f;
    private readonly float basePlateSize = 8f / 12f;
    private readonly float basePlateThick= 0.04f;
    private readonly float topPlateSize  = 6f / 12f;
    private readonly float topPlateThick = 0.04f;

    private float zoomSpeed   = 0.5f;
    private float rotateSpeed = 0.2f;
    private float panSpeed    = 0.05f;

#if ENABLE_LEGACY_INPUT_MANAGER
    private Vector2 lastMousePosition;
#endif
#if ENABLE_INPUT_SYSTEM
    private bool enhancedTouchEnabled;
#endif

    // ════════════════════════════════════════════════════════════════════
    //  Unity lifecycle
    // ════════════════════════════════════════════════════════════════════
    private void Awake()
    {
        mainCamera           = GetComponent<Camera>();
        stressPropertyBlock  = new MaterialPropertyBlock();
        EnsureBayArraySizes();
    }
    
    // Public method to force rebuild structure (called from editor menu)
    public void ForceRebuildStructure()
    {
        // Reset warning flags so we get fresh feedback
        structureMissingWarningShown = false;
        structureShapeWarningShown = false;
        uiInitialized = false;
        
        // Reinitialize all bindings and refresh
        TryAutoAssignSceneReferences();
        InitializeUiBindings();
        InitializeStructureBindings();
        RefreshAllControls();
        RefreshStructure();
        ApplyCameraTransform();
    }

    private void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        if (!enhancedTouchEnabled) { EnhancedTouchSupport.Enable(); enhancedTouchEnabled = true; }
#endif
    }

    private void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        if (enhancedTouchEnabled) { EnhancedTouchSupport.Disable(); enhancedTouchEnabled = false; }
#endif
    }

    private void OnValidate()
    {
        EnsureGapArraySize();
        EnsureBayArraySizes();
        SyncSceneStateFromDefaultsIfNeeded();
        NormalizeFrameSettings();
    }

    private void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        defaultFont = TMP_Settings.defaultFontAsset;
        if (defaultFont == null)
            defaultFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        SyncSceneStateFromDefaultsIfNeeded();
        EnsureGapArraySize();
        EnsureBayArraySizes();
        NormalizeFrameSettings();
        TryAutoAssignSceneReferences();
        InitializeUiBindings();
        InitializeStructureBindings();
        RefreshAllControls();
        RefreshStructure();
        ApplyCameraTransform();

        // Initialise camera mode state
        isOrthographic = false;
        if (mainCamera != null) mainCamera.orthographic = false;
        // Set initial button label
        if (viewPerspOrthoLabel == null && viewPerspOrthoButton != null)
            viewPerspOrthoLabel = viewPerspOrthoButton.GetComponentInChildren<TMP_Text>(true);
        if (viewPerspOrthoLabel != null)
            viewPerspOrthoLabel.text = "Ortho";
    }

    private void Update()
    {
        HandleCameraControls();
        UpdateLabelFacing();
    }

    // Make all world-space labels face the camera every frame
    private void UpdateLabelFacing()
    {
        if (mainCamera == null) return;
        Vector3 camPos = mainCamera.transform.position;

        // Pillar labels
        for (int i = 0; i < pillarVisuals.Length; i++)
        {
            var pv = pillarVisuals[i];
            if (pv?.Label == null) continue;
            pv.Label.transform.LookAt(camPos);
            pv.Label.transform.Rotate(0f, 180f, 0f);
        }
        // Brace labels (X-Crossing mode)
        for (int i = 0; i < XBayCount; i++)
        {
            var xv = xBayVisuals[i];
            if (xv == null) continue;
            if (xv.LabelA != null && xv.DiagA != null && xv.DiagA.gameObject.activeSelf)
            { xv.LabelA.transform.LookAt(camPos); xv.LabelA.transform.Rotate(0f, 180f, 0f); }
            if (xv.LabelB != null && xv.DiagB != null && xv.DiagB.gameObject.activeSelf)
            { xv.LabelB.transform.LookAt(camPos); xv.LabelB.transform.Rotate(0f, 180f, 0f); }
        }
        // Stay line labels
        for (int i = 0; i < StayLineCount; i++)
        {
            var sl = stayLineVisuals[i];
            if (sl?.Label != null && sl.Root != null && sl.Root.gameObject.activeSelf)
            { sl.Label.transform.LookAt(camPos); sl.Label.transform.Rotate(0f, 180f, 0f); }
        }
        // Angle bracket labels
        for (int i = 0; i < AngleBracketCount; i++)
        {
            var ab = angleBracketVisuals[i];
            if (ab?.Label != null && ab.Root != null && ab.Root.gameObject.activeSelf)
            { ab.Label.transform.LookAt(camPos); ab.Label.transform.Rotate(0f, 180f, 0f); }
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  Array helpers
    // ════════════════════════════════════════════════════════════════════
    private void EnsureBayArraySizes()
    {
        if (bayEnabled == null || bayEnabled.Length != XBayCount)
        {
            bool[] n = new bool[XBayCount];
            if (bayEnabled != null)
                for (int i = 0; i < Mathf.Min(XBayCount, bayEnabled.Length); i++) n[i] = bayEnabled[i];
            else { for (int i = 0; i < XBayCount; i++) n[i] = true; }
            bayEnabled = n;
        }
        if (bayXEnabled == null || bayXEnabled.Length != XBayCount)
        {
            bool[] n = new bool[XBayCount];
            if (bayXEnabled != null)
                for (int i = 0; i < Mathf.Min(XBayCount, bayXEnabled.Length); i++) n[i] = bayXEnabled[i];
            else { for (int i = 0; i < XBayCount; i++) n[i] = true; }
            bayXEnabled = n;
        }
        if (bayToggle  == null || bayToggle.Length  != XBayCount) bayToggle  = new UnityEngine.UI.Toggle[XBayCount];
        if (bayXToggle == null || bayXToggle.Length != XBayCount) bayXToggle = new UnityEngine.UI.Toggle[XBayCount];
    }

    private void EnsureGapArraySize()
    {
        if (individualPanelGaps != null && individualPanelGaps.Length == GapCount) return;
        float[] r = new float[GapCount];
        if (individualPanelGaps != null)
            for (int i = 0; i < Mathf.Min(GapCount, individualPanelGaps.Length); i++) r[i] = individualPanelGaps[i];
        for (int i = 0; i < r.Length; i++) if (Mathf.Approximately(r[i], 0f)) r[i] = globalPanelGap;
        individualPanelGaps = r;
    }

    // ════════════════════════════════════════════════════════════════════
    //  Defaults / Normalize
    // ════════════════════════════════════════════════════════════════════
    private void ResetToDefaults()
    {
        panelsOffset         = DefaultPanelsOffset;
        panelsVerticalOffset = 0f;
        rowGapZ              = DefaultRowGapZ;
        useEqualPanelGaps    = true;
        globalPanelGap       = DefaultGlobalPanelGap;
        ApplyEqualPanelGap(globalPanelGap);

        heightFront = DefaultHeightFront;
        heightMid   = DefaultHeightMid;
        heightBack  = DefaultHeightBack;

        topBeamLength = DefaultTopBeamLength;
        topBeamSize   = 5f/12f;
        topBeamThick  = 1.5f/8f/12f;
        cChannelLength= DefaultCChannelLength;
        cChannelSize  = 2.5f/12f;
        cChannelThick = 0.02f;
        panelLength   = DefaultPanelLength;
        panelWidth    = DefaultPanelWidth;
        panelThickness= 0.13f;

        concretePillarHeight   = DefaultConcretePillarHeight;
        concretePillarWidth    = DefaultConcretePillarWidth;
        concretePillarDepth    = DefaultConcretePillarDepth;
        bracingBottomClearance = DefaultBracingBottomClear;
        bracingTopClearance    = DefaultBracingTopClear;
        bracingSize            = 2f/12f;
        bracingThick           = 0.02f;

        showConcretePillars = true;
        for (int i = 0; i < XBayCount; i++) { bayEnabled[i] = true; bayXEnabled[i] = true; }

        distance = 35f; xRot = 30f; yRot = 45f;
        cameraTarget = new Vector3(0f, 10f, 7f);
        isOrthographic = false;
        if (mainCamera != null) mainCamera.orthographic = false;
        if (viewPerspOrthoLabel == null && viewPerspOrthoButton != null)
            viewPerspOrthoLabel = viewPerspOrthoButton.GetComponentInChildren<TMP_Text>(true);
        if (viewPerspOrthoLabel != null)
            viewPerspOrthoLabel.text = "Ortho";
    }

    private void NormalizeFrameSettings()
    {
        heightFront = Mathf.Max(0.1f, heightFront);
        heightMid   = Mathf.Max(0.1f, heightMid);
        heightBack  = Mathf.Max(0.1f, heightBack);
        panelWidth  = Mathf.Max(0.1f, panelWidth);
        panelLength = Mathf.Max(0.1f, panelLength);
        panelThickness = Mathf.Max(0.01f, panelThickness);
        rowGapZ     = Mathf.Max(0f, rowGapZ);
        topBeamLength= Mathf.Max(0.1f, topBeamLength);
        topBeamSize  = Mathf.Max(0.05f, topBeamSize);
        topBeamThick = Mathf.Max(0.001f, topBeamThick);
        cChannelLength= Mathf.Max(0.1f, cChannelLength);
        cChannelSize  = Mathf.Max(0.05f, cChannelSize);
        cChannelThick = Mathf.Max(0.001f, cChannelThick);
        globalPanelGap= Mathf.Max(0f, globalPanelGap);
        concretePillarHeight   = Mathf.Max(0.1f, concretePillarHeight);
        concretePillarWidth    = Mathf.Max(0.1f, concretePillarWidth);
        concretePillarDepth    = Mathf.Max(0.1f, concretePillarDepth);
        bracingBottomClearance = Mathf.Max(0f, bracingBottomClearance);
        bracingTopClearance    = Mathf.Max(0f, bracingTopClearance);
        bracingSize  = Mathf.Max(0.05f, bracingSize);
        bracingThick = Mathf.Max(0.001f, bracingThick);
        for (int i = 0; i < individualPanelGaps.Length; i++) individualPanelGaps[i] = Mathf.Max(0f, individualPanelGaps[i]);
        if (useEqualPanelGaps) ApplyEqualPanelGap(globalPanelGap);
    }

    private void ApplySceneState(bool preserveSmartDesignSummary = false)
    {
        if (!preserveSmartDesignSummary) smartDesignSummary = string.Empty;
        NormalizeFrameSettings();
        RefreshAllControls();
        RefreshStructure();
    }

    private void SyncSceneStateFromDefaultsIfNeeded()
    {
        if (Application.isPlaying || !syncSceneValuesWithDefaults) return;

        panelsOffset         = DefaultPanelsOffset;
        panelsVerticalOffset = 0f;
        rowGapZ              = DefaultRowGapZ;
        useEqualPanelGaps    = true;
        ApplyEqualPanelGap(DefaultGlobalPanelGap);

        heightFront = DefaultHeightFront;
        heightMid   = DefaultHeightMid;
        heightBack  = DefaultHeightBack;

        topBeamLength = DefaultTopBeamLength;
        topBeamSize   = 5f / 12f;
        topBeamThick  = 1.5f / 8f / 12f;

        cChannelLength = DefaultCChannelLength;
        cChannelSize   = 2.5f / 12f;
        cChannelThick  = 0.02f;

        panelLength    = DefaultPanelLength;
        panelWidth     = DefaultPanelWidth;
        panelThickness = 0.13f;

        concretePillarHeight   = DefaultConcretePillarHeight;
        concretePillarWidth    = DefaultConcretePillarWidth;
        concretePillarDepth    = DefaultConcretePillarDepth;
        bracingBottomClearance = DefaultBracingBottomClear;
        bracingTopClearance    = DefaultBracingTopClear;
        bracingSize            = 2f / 12f;
        bracingThick           = 0.02f;
    }

    // ════════════════════════════════════════════════════════════════════
    //  Scene reference auto-assign
    // ════════════════════════════════════════════════════════════════════
    private void TryAutoAssignSceneReferences()
    {
        if (uiPanelRect == null)
        {
            GameObject o = GameObject.Find("ControlPanel");
            if (o != null) uiPanelRect = o.GetComponent<RectTransform>();
        }
        if (equalGapGroup == null && uiPanelRect != null)
        {
            Transform g = uiPanelRect.Find("Scroll View/Viewport/Content/Section_SolarPanels/EqualGapGroup");
            if (g != null) equalGapGroup = g as RectTransform;
        }
        if (individualGapGroup == null && uiPanelRect != null)
        {
            Transform g = uiPanelRect.Find("Scroll View/Viewport/Content/Section_SolarPanels/IndividualGapGroup");
            if (g != null) individualGapGroup = g as RectTransform;
        }
        if (equalGapToggle == null) { var o = GameObject.Find("Toggle_EqualPanelGaps"); if (o) equalGapToggle = o.GetComponent<Toggle>(); }
        if (resetButton    == null) { var o = GameObject.Find("Button_ResetDefaults");  if (o) resetButton    = o.GetComponent<Button>(); }
        if (panelToggleButton == null) { var o = GameObject.Find("Button_TogglePanel"); if (o) panelToggleButton = o.GetComponent<Button>(); }
        if (viewFrontButton == null) { var o = GameObject.Find("Button_ViewFront"); if (o) viewFrontButton = o.GetComponent<Button>(); }
        if (viewSideButton  == null) { var o = GameObject.Find("Button_ViewSide");  if (o) viewSideButton  = o.GetComponent<Button>(); }
        if (viewTopButton   == null) { var o = GameObject.Find("Button_ViewTop");   if (o) viewTopButton   = o.GetComponent<Button>(); }
        if (viewIsoButton   == null) { var o = GameObject.Find("Button_ViewIso");   if (o) viewIsoButton   = o.GetComponent<Button>(); }
        if (viewPerspOrthoButton == null) { var o = GameObject.Find("Button_ViewPerspOrtho"); if (o) viewPerspOrthoButton = o.GetComponent<Button>(); }
        if (viewPerspOrthoLabel  == null && viewPerspOrthoButton != null) viewPerspOrthoLabel = viewPerspOrthoButton.GetComponentInChildren<TMP_Text>(true);
        if (detailsToggleButton == null) { var o = GameObject.Find("Button_ToggleDetails"); if (o) detailsToggleButton = o.GetComponent<Button>(); }
        if (detailsPanelRect == null) { var o = GameObject.Find("DetailsPanel"); if (o) detailsPanelRect = o.GetComponent<RectTransform>(); }
        if (detailsText == null && detailsPanelRect != null) { var o = detailsPanelRect.Find("Scroll View/Viewport/Content/DetailsText"); if (o) detailsText = o.GetComponent<TMP_Text>(); }
        if (smartDesignButton == null) { var o = GameObject.Find("Button_SmartDesign"); if (o) smartDesignButton = o.GetComponent<Button>(); }
        if (labelsRoot == null && structureRoot != null) { var o = structureRoot.Find("LabelsRoot"); if (o) labelsRoot = o; }
        if (panelToggleButtonLabel == null && panelToggleButton != null)
            panelToggleButtonLabel = panelToggleButton.GetComponentInChildren<TMP_Text>(true);
        if (showConcreteFootingsToggle == null)
        { var o = GameObject.Find("Toggle_ShowConcreteFootings"); if (o) showConcreteFootingsToggle = o.GetComponent<UnityEngine.UI.Toggle>(); }
        if (bracingModeToggle == null)
        { var o = GameObject.Find("Toggle_BracingMode"); if (o) bracingModeToggle = o.GetComponent<Toggle>(); }

        EnsureBayArraySizes();
        for (int i = 0; i < XBayCount; i++)
        {
            if (bayToggle[i]  == null) { var o = GameObject.Find($"Toggle_Bay{i}");  if (o) bayToggle[i]  = o.GetComponent<UnityEngine.UI.Toggle>(); }
            if (bayXToggle[i] == null) { var o = GameObject.Find($"Toggle_BayX{i}"); if (o) bayXToggle[i] = o.GetComponent<UnityEngine.UI.Toggle>(); }
        }
        if (uiCanvas == null && uiPanelRect != null) uiCanvas = uiPanelRect.GetComponentInParent<Canvas>();
        if (structureRoot == null)
        { var o = GameObject.Find(StructureRootName); if (o) structureRoot = o.transform; }
    }

    // ════════════════════════════════════════════════════════════════════
    //  UI Bindings
    // ════════════════════════════════════════════════════════════════════
    private void InitializeUiBindings()
    {
        if (uiInitialized) return;
        uiInitialized = true;
        numericBindings.Clear();

        if (resetButton       != null) resetButton.onClick.AddListener(HandleResetButtonPressed);
        if (panelToggleButton != null) panelToggleButton.onClick.AddListener(HandlePanelTogglePressed);
        if (equalGapToggle    != null) equalGapToggle.onValueChanged.AddListener(HandleEqualGapToggleChanged);
        if (showConcreteFootingsToggle != null)
            showConcreteFootingsToggle.onValueChanged.AddListener(v => { showConcretePillars = v; ApplySceneState(); });
        if (bracingModeToggle != null)
            bracingModeToggle.onValueChanged.AddListener(v => { bracingMode = v ? BracingMode.StayLines : BracingMode.XCrossing; ApplySceneState(); });

        // Axis view buttons
        if (viewFrontButton != null) viewFrontButton.onClick.AddListener(() => SetCameraView(CameraView.Front));
        if (viewSideButton  != null) viewSideButton.onClick.AddListener(()  => SetCameraView(CameraView.Side));
        if (viewTopButton   != null) viewTopButton.onClick.AddListener(()   => SetCameraView(CameraView.Top));
        if (viewIsoButton   != null) viewIsoButton.onClick.AddListener(()   => SetCameraView(CameraView.Iso));
        if (viewPerspOrthoButton != null) viewPerspOrthoButton.onClick.AddListener(TogglePerspectiveOrtho);
        if (detailsToggleButton  != null) detailsToggleButton.onClick.AddListener(ToggleDetailsPanel);
        if (smartDesignButton    != null) smartDesignButton.onClick.AddListener(ApplySmartDesign);

        EnsureBayArraySizes();
        for (int i = 0; i < XBayCount; i++)
        {
            int idx = i;
            if (bayToggle[i]  != null) bayToggle[i].onValueChanged.AddListener(v  => { bayEnabled[idx]  = v; ApplySceneState(); });
            if (bayXToggle[i] != null) bayXToggle[i].onValueChanged.AddListener(v => { bayXEnabled[idx] = v; ApplySceneState(); });
        }

        if (uiPanelRect == null) { Debug.LogWarning("MobileSolarFrameApp: ControlPanel not found."); return; }

        MobileSolarFrameFieldBinding[] fieldViews = uiPanelRect.GetComponentsInChildren<MobileSolarFrameFieldBinding>(true);
        foreach (var fv in fieldViews)
        {
            if (!TryCreateNumericBinding(fv, out NumericControlBinding b)) continue;
            numericBindings.Add(b);
            WireNumericBinding(b);
        }
    }

    private bool TryCreateNumericBinding(MobileSolarFrameFieldBinding view, out NumericControlBinding binding)
    {
        binding = null;
        if (view == null || view.Slider == null || view.InputField == null) return false;
        if (!TryGetFieldAccessors(view.FieldId, out Func<float> getter, out Action<float> setter)) return false;
        view.Slider.minValue = view.MinValue;
        view.Slider.maxValue = view.MaxValue;
        view.Slider.wholeNumbers = false;
        view.InputField.contentType = TMP_InputField.ContentType.DecimalNumber;
        binding = new NumericControlBinding { View = view, Getter = getter, Setter = setter };
        return true;
    }

    private void WireNumericBinding(NumericControlBinding b)
    {
        b.View.Slider.onValueChanged.AddListener(v => { b.Setter(Mathf.Clamp(v, b.View.MinValue, b.View.MaxValue)); ApplySceneState(); });
        b.View.InputField.onEndEdit.AddListener(t => HandleInputFieldSubmit(b, t));
        b.View.InputField.onSubmit.AddListener(t  => HandleInputFieldSubmit(b, t));
    }

    private void HandleInputFieldSubmit(NumericControlBinding b, string text)
    {
        if (!TryParseFloat(text, out float v)) { UpdateBindingDisplay(b); return; }
        b.Setter(Mathf.Clamp(v, b.View.MinValue, b.View.MaxValue));
        ApplySceneState();
    }

    private bool TryGetFieldAccessors(MobileSolarFieldId id, out Func<float> getter, out Action<float> setter)
    {
        getter = null; setter = null;
        switch (id)
        {
            case MobileSolarFieldId.HeightFront:          getter=()=>heightFront;          setter=v=>heightFront=v;          return true;
            case MobileSolarFieldId.HeightMid:            getter=()=>heightMid;            setter=v=>heightMid=v;            return true;
            case MobileSolarFieldId.HeightBack:           getter=()=>heightBack;           setter=v=>heightBack=v;           return true;
            case MobileSolarFieldId.PanelWidth:           getter=()=>panelWidth;           setter=v=>panelWidth=v;           return true;
            case MobileSolarFieldId.PanelLength:          getter=()=>panelLength;          setter=v=>panelLength=v;          return true;
            case MobileSolarFieldId.PanelThickness:       getter=()=>panelThickness;       setter=v=>panelThickness=v;       return true;
            case MobileSolarFieldId.RowGapZ:              getter=()=>rowGapZ;              setter=v=>rowGapZ=v;              return true;
            case MobileSolarFieldId.GlobalGap:            getter=()=>globalPanelGap;       setter=ApplyEqualPanelGap;        return true;
            case MobileSolarFieldId.Gap1:                 getter=()=>individualPanelGaps[0]; setter=v=>individualPanelGaps[0]=v; return true;
            case MobileSolarFieldId.Gap2:                 getter=()=>individualPanelGaps[1]; setter=v=>individualPanelGaps[1]=v; return true;
            case MobileSolarFieldId.Gap3:                 getter=()=>individualPanelGaps[2]; setter=v=>individualPanelGaps[2]=v; return true;
            case MobileSolarFieldId.Gap4:                 getter=()=>individualPanelGaps[3]; setter=v=>individualPanelGaps[3]=v; return true;
            case MobileSolarFieldId.TopBeamLength:        getter=()=>topBeamLength;        setter=v=>topBeamLength=v;        return true;
            case MobileSolarFieldId.TopBeamSize:          getter=()=>topBeamSize;          setter=v=>topBeamSize=v;          return true;
            case MobileSolarFieldId.TopBeamThick:         getter=()=>topBeamThick;         setter=v=>topBeamThick=v;         return true;
            case MobileSolarFieldId.CChannelLength:       getter=()=>cChannelLength;       setter=v=>cChannelLength=v;       return true;
            case MobileSolarFieldId.CChannelSize:         getter=()=>cChannelSize;         setter=v=>cChannelSize=v;         return true;
            case MobileSolarFieldId.CChannelThick:        getter=()=>cChannelThick;        setter=v=>cChannelThick=v;        return true;
            case MobileSolarFieldId.PanelsOffset:         getter=()=>panelsOffset;         setter=v=>panelsOffset=v;         return true;
            case MobileSolarFieldId.PanelsVerticalOffset: getter=()=>panelsVerticalOffset; setter=v=>panelsVerticalOffset=v; return true;
            case MobileSolarFieldId.ConcretePillarHeight: getter=()=>concretePillarHeight; setter=v=>concretePillarHeight=v; return true;
            case MobileSolarFieldId.ConcretePillarWidth:  getter=()=>concretePillarWidth;  setter=v=>concretePillarWidth=v;  return true;
            case MobileSolarFieldId.ConcretePillarDepth:  getter=()=>concretePillarDepth;  setter=v=>concretePillarDepth=v;  return true;
            case MobileSolarFieldId.BracingBottomClearance: getter=()=>bracingBottomClearance; setter=v=>bracingBottomClearance=v; return true;
            case MobileSolarFieldId.BracingTopClearance:  getter=()=>bracingTopClearance;  setter=v=>bracingTopClearance=v;  return true;
            case MobileSolarFieldId.BracingSize:          getter=()=>bracingSize;          setter=v=>bracingSize=v;          return true;
            case MobileSolarFieldId.BracingThick:         getter=()=>bracingThick;         setter=v=>bracingThick=v;         return true;
            default: return false;
        }
    }

    private void HandleResetButtonPressed()  { ResetToDefaults(); ApplySceneState(); }
    private void HandlePanelTogglePressed()  { isControlPanelVisible = !isControlPanelVisible; RefreshPanelVisibility(); }

    private void HandleEqualGapToggleChanged(bool isOn)
    {
        useEqualPanelGaps = isOn;
        if (useEqualPanelGaps) ApplyEqualPanelGap(globalPanelGap);
        ApplySceneState();
    }

    private void ApplyEqualPanelGap(float value)
    {
        globalPanelGap = value;
        EnsureGapArraySize();
        for (int i = 0; i < individualPanelGaps.Length; i++) individualPanelGaps[i] = value;
    }

    private void RefreshAllControls()
    {
        foreach (var b in numericBindings) UpdateBindingDisplay(b);
        if (equalGapToggle != null) equalGapToggle.SetIsOnWithoutNotify(useEqualPanelGaps);
        if (showConcreteFootingsToggle != null) showConcreteFootingsToggle.SetIsOnWithoutNotify(showConcretePillars);
        EnsureBayArraySizes();
        for (int i = 0; i < XBayCount; i++)
        {
            if (bayToggle[i]  != null) bayToggle[i].SetIsOnWithoutNotify(bayEnabled[i]);
            if (bayXToggle[i] != null) bayXToggle[i].SetIsOnWithoutNotify(bayXEnabled[i]);
        }
        if (equalGapGroup      != null) equalGapGroup.gameObject.SetActive(useEqualPanelGaps);
        if (individualGapGroup != null) individualGapGroup.gameObject.SetActive(!useEqualPanelGaps);
        if (uiPanelRect        != null) LayoutRebuilder.ForceRebuildLayoutImmediate(uiPanelRect);
        RefreshPanelVisibility();
    }

    private void UpdateBindingDisplay(NumericControlBinding b)
    {
        float v = Mathf.Clamp(b.Getter(), b.View.MinValue, b.View.MaxValue);
        b.View.Slider.SetValueWithoutNotify(v);
        b.View.InputField.SetTextWithoutNotify(FormatValue(v));
    }

    private bool TryParseFloat(string text, out float value)
    {
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return true;
        return float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    private string FormatValue(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    // ════════════════════════════════════════════════════════════════════
    //  Camera controls
    // ════════════════════════════════════════════════════════════════════
    private enum CameraView { Iso, Front, Side, Top }

    private void TogglePerspectiveOrtho()
    {
        isOrthographic = !isOrthographic;
        if (mainCamera != null)
        {
            mainCamera.orthographic = isOrthographic;
            if (isOrthographic)
                mainCamera.orthographicSize = distance * 0.5f;
        }
        if (viewPerspOrthoLabel == null && viewPerspOrthoButton != null)
            viewPerspOrthoLabel = viewPerspOrthoButton.GetComponentInChildren<TMP_Text>(true);
        if (viewPerspOrthoLabel != null)
            viewPerspOrthoLabel.text = isOrthographic ? "Persp" : "Ortho";
    }

    private void ToggleDetailsPanel()
    {
        isDetailsPanelVisible = !isDetailsPanelVisible;
        if (detailsPanelRect != null)
            detailsPanelRect.gameObject.SetActive(isDetailsPanelVisible);
        if (isDetailsPanelVisible)
            RefreshDetailsPanel();
    }

    // ════════════════════════════════════════════════════════════════════
    //  Details Panel — full structure breakdown
    // ════════════════════════════════════════════════════════════════════
    private void RefreshDetailsPanel()
    {
        if (detailsText == null) return;
        detailsText.text = BuildDetailsText();
        return;
#if false

        // ── Weight constants (lbs per ft for steel sections) ─────────────
        const float SteelDensityLbPerFt3 = 490f;   // steel ~490 lb/ft³
        const float ConcreteWeightLbFt3  = 145f;   // concrete ~145 lb/ft³
        const float PanelWeightLbEach    = 34f;     // ~34 lbs per panel

        // ── Pillar H-girder weight ────────────────────────────────────────
        // Cross-section area of H-girder (web + 2 flanges)
        float hWebArea    = hThick * (hSize - 2f * hThick);
        float hFlangeArea = hSize  * hThick * 2f;
        float hSectionArea= hWebArea + hFlangeArea;                // ft²
        float pillarWeightPerFt = hSectionArea * SteelDensityLbPerFt3;

        float totalPillarWeight = 0f;
        for (int col = 0; col < ColumnCount; col++)
            for (int sup = 0; sup < SupportCount; sup++)
                totalPillarWeight += GetSupportHeight(sup) * pillarWeightPerFt;

        // ── Top beam H-girder weight ──────────────────────────────────────
        float beamSectionArea   = hSectionArea; // same profile
        float beamWeightEach    = topBeamLength * beamSectionArea * SteelDensityLbPerFt3;
        float totalBeamWeight   = beamWeightEach * ColumnCount;

        // ── C-channel rail weight ─────────────────────────────────────────
        float cWebArea    = cChannelThick * (cChannelSize - 2f * cChannelThick);
        float cFlangeArea = cChannelSize  * cChannelThick * 2f;
        float cSectionArea= cWebArea + cFlangeArea;
        float railWeightEach  = cChannelLength * cSectionArea * SteelDensityLbPerFt3;
        float totalRailWeight = railWeightEach * RailCount;

        // ── X-crossing brace weight ───────────────────────────────────────
        float totalBraceWeight = 0f;
        int   activeBraceCount = 0;
        for (int i = 0; i < XBayCount; i++)
        {
            if (!bayEnabled[i]) continue;
            BayDef bd = BayDefs[i];
            bool isSide = (bd.ColA == bd.ColB);
            float diagLen;
            if (isSide)
            {
                float dz = SupportZPositions[bd.SupportB] - SupportZPositions[bd.SupportA];
                float dy = Mathf.Abs(GetSupportTopY(bd.SupportB) - GetSupportTopY(bd.SupportA));
                diagLen  = Mathf.Sqrt(dz*dz + dy*dy);
            }
            else
            {
                float dx = Mathf.Abs(ColumnXPositions[bd.ColB] - ColumnXPositions[bd.ColA]);
                float dy = Mathf.Abs(GetSupportTopY(bd.SupportA) - (basePlateThick + bracingBottomClearance));
                diagLen  = Mathf.Sqrt(dx*dx + dy*dy);
            }
            float braceW = diagLen * cSectionArea * SteelDensityLbPerFt3;
            totalBraceWeight += braceW;
            if (bayXEnabled[i]) { totalBraceWeight += braceW; activeBraceCount += 2; }
            else activeBraceCount += 1;
        }

        // ── Solar panel weight ────────────────────────────────────────────
        int   totalPanels      = PanelRowCount * PanelColumnCount;
        float totalPanelWeight = totalPanels * PanelWeightLbEach;

        // ── Concrete footing weight ───────────────────────────────────────
        float footingVolFt3    = concretePillarWidth * concretePillarDepth * concretePillarHeight;
        float footingWeightEach= footingVolFt3 * ConcreteWeightLbFt3;
        int   footingCount     = ColumnCount * SupportCount;
        float totalFootingWeight = showConcretePillars ? footingWeightEach * footingCount : 0f;

        // ── Base & top plates ─────────────────────────────────────────────
        float basePlateVol    = basePlateSize * basePlateSize * basePlateThick;
        float topPlateVol     = topPlateSize  * topPlateSize  * topPlateThick;
        float plateWeightEach = (basePlateVol + topPlateVol) * SteelDensityLbPerFt3;
        float totalPlateWeight= plateWeightEach * footingCount;

        // ── Totals ────────────────────────────────────────────────────────
        float totalStructureWeight = totalPillarWeight + totalBeamWeight + totalRailWeight
                                   + totalBraceWeight  + totalPanelWeight + totalFootingWeight
                                   + totalPlateWeight;

        // ── Stress summary ────────────────────────────────────────────────
        float panelWeight2   = PanelWeightLbEach * totalPanels;
        float ironWeight2    = cChannelLength * RailCount * 1.6f + topBeamLength * ColumnCount * 1.6f;
        float totalLoad      = panelWeight2 + ironWeight2;
        float loadPerPillar  = totalLoad / (ColumnCount * SupportCount);
        float windLoad       = 15f * panelLength * panelWidth * PanelRowCount * PanelColumnCount
                             / (ColumnCount * SupportCount);

        // ── Build text ────────────────────────────────────────────────────
        var sb = new StringBuilder();

        sb.AppendLine("<b><size=110%>═══ STRUCTURE DETAILS ═══</size></b>\n");

        // ── OVERALL ──────────────────────────────────────────────────────
        sb.AppendLine("<b><color=#4BBFEE>▶ TOTAL STRUCTURE</color></b>");
        sb.AppendLine($"  Total Weight      : <b>{totalStructureWeight:0.#} lbs</b>  ({totalStructureWeight/2.205f:0.#} kg)");
        sb.AppendLine($"  Steel Weight      : {(totalPillarWeight+totalBeamWeight+totalRailWeight+totalBraceWeight+totalPlateWeight):0.#} lbs");
        sb.AppendLine($"  Panel Weight      : {totalPanelWeight:0.#} lbs");
        sb.AppendLine($"  Concrete Weight   : {totalFootingWeight:0.#} lbs");
        sb.AppendLine($"  Gravity Load      : {totalLoad:0.#} lbs");
        sb.AppendLine($"  Wind Load (est.)  : {windLoad * ColumnCount * SupportCount:0.#} lbs");
        sb.AppendLine();

        // ── PILLARS ───────────────────────────────────────────────────────
        sb.AppendLine("<b><color=#4BBFEE>▶ PILLARS  (H-Girder, 9 total)</color></b>");
        sb.AppendLine($"  Profile Size      : {hSize*12f:0.##}\" × {hSize*12f:0.##}\"");
        sb.AppendLine($"  Web Thickness     : {hThick*12f:0.###}\"");
        sb.AppendLine($"  Section Area      : {hSectionArea*144f:0.##} in²");
        sb.AppendLine($"  Weight/ft         : {pillarWeightPerFt:0.##} lbs/ft");
        sb.AppendLine($"  Heights           : Front {heightFront:0.#}ft  Mid {heightMid:0.#}ft  Back {heightBack:0.#}ft");
        sb.AppendLine($"  Total Weight      : {totalPillarWeight:0.#} lbs");
        sb.AppendLine();

        // Per-pillar stress
        sb.AppendLine("  <i>Stress per pillar (base moment):</i>");
        string[] colNames = { "Left", "Center", "Right" };
        string[] supNames = { "Front", "Mid", "Back" };
        for (int col = 0; col < ColumnCount; col++)
        {
            for (int sup = 0; sup < SupportCount; sup++)
            {
                float h  = GetSupportHeight(sup);
                float rf = ComputePillarReductionFactor(col, sup);
                float moment = (loadPerPillar + windLoad) * h * rf;
                float maxM   = (loadPerPillar + windLoad) * Mathf.Max(heightFront, heightMid, heightBack);
                float pct    = maxM > 0f ? moment / maxM * 100f : 0f;
                string stressTag = pct < 40f ? "<color=#22EE44>" : pct < 70f ? "<color=#FFAA00>" : "<color=#FF3333>";
                sb.AppendLine($"  {colNames[col]}-{supNames[sup]}: h={h:0.#}ft  M={moment:0.#}lb·ft  {stressTag}{pct:0.#}% stress</color>");
            }
        }
        sb.AppendLine();

        // ── TOP BEAMS ─────────────────────────────────────────────────────
        sb.AppendLine("<b><color=#4BBFEE>▶ TOP BEAMS  (H-Girder, 3 total)</color></b>");
        sb.AppendLine($"  Length            : {topBeamLength:0.##} ft");
        sb.AppendLine($"  Profile Size      : {topBeamSize*12f:0.##}\" × {topBeamSize*12f:0.##}\"");
        sb.AppendLine($"  Thickness         : {topBeamThick*12f:0.###}\"");
        sb.AppendLine($"  Weight each       : {beamWeightEach:0.#} lbs");
        sb.AppendLine($"  Total Weight      : {totalBeamWeight:0.#} lbs");
        float beamMidMom = (totalLoad / ColumnCount) * topBeamLength * 0.25f;
        float maxBeamMom = totalLoad * topBeamLength * 0.25f;
        float beamStressPct = maxBeamMom > 0f ? beamMidMom / maxBeamMom * 100f : 0f;
        string beamTag = beamStressPct < 40f ? "<color=#22EE44>" : beamStressPct < 70f ? "<color=#FFAA00>" : "<color=#FF3333>";
        sb.AppendLine($"  Mid-span Moment   : {beamMidMom:0.#} lb·ft  {beamTag}{beamStressPct:0.#}% stress</color>");
        sb.AppendLine();

        // ── C-CHANNEL RAILS ───────────────────────────────────────────────
        sb.AppendLine("<b><color=#4BBFEE>▶ C-CHANNEL RAILS  (4 total)</color></b>");
        sb.AppendLine($"  Length            : {cChannelLength:0.##} ft");
        sb.AppendLine($"  Size              : {cChannelSize*12f:0.##}\" × {cChannelSize*12f:0.##}\"");
        sb.AppendLine($"  Thickness         : {cChannelThick*12f:0.###}\"");
        sb.AppendLine($"  Weight each       : {railWeightEach:0.#} lbs");
        sb.AppendLine($"  Total Weight      : {totalRailWeight:0.#} lbs");
        float railLoad   = totalLoad / RailCount;
        float railMom    = railLoad * cChannelLength * 0.25f;
        float maxRailMom = totalLoad * cChannelLength * 0.25f;
        float railStressPct = maxRailMom > 0f ? railMom / maxRailMom * 100f : 0f;
        string railTag = railStressPct < 40f ? "<color=#22EE44>" : railStressPct < 70f ? "<color=#FFAA00>" : "<color=#FF3333>";
        sb.AppendLine($"  Mid-span Moment   : {railMom:0.#} lb·ft  {railTag}{railStressPct:0.#}% stress</color>");
        sb.AppendLine();

        // ── X-CROSSING BRACES ─────────────────────────────────────────────
        sb.AppendLine("<b><color=#4BBFEE>▶ X-CROSSING BRACES</color></b>");
        sb.AppendLine($"  Active diagonals  : {activeBraceCount}");
        sb.AppendLine($"  C-Channel Size    : {bracingSize*12f:0.##}\"");
        sb.AppendLine($"  Bottom Clearance  : {bracingBottomClearance:0.#} ft");
        sb.AppendLine($"  Top Clearance     : {bracingTopClearance:0.#} ft");
        sb.AppendLine($"  Total Weight      : {totalBraceWeight:0.#} lbs");
        float bayShear    = windLoad * 1.2f;
        float diagForce   = bayShear * 1.4f;
        float maxDiag     = windLoad * 2f;
        float braceStressPct = maxDiag > 0f ? diagForce / maxDiag * 100f : 0f;
        string braceTag = braceStressPct < 40f ? "<color=#22EE44>" : braceStressPct < 70f ? "<color=#FFAA00>" : "<color=#FF3333>";
        sb.AppendLine($"  Diagonal Force    : {diagForce:0.#} lbs  {braceTag}{braceStressPct:0.#}% stress</color>");
        sb.AppendLine();
        // Per-bay detail
        sb.AppendLine("  <i>Active bays:</i>");
        for (int i = 0; i < XBayCount; i++)
        {
            if (!bayEnabled[i]) continue;
            BayDef bd = BayDefs[i];
            bool isSide = (bd.ColA == bd.ColB);
            float dz = isSide
                ? SupportZPositions[bd.SupportB] - SupportZPositions[bd.SupportA]
                : 0f;
            float dx = isSide ? 0f : Mathf.Abs(ColumnXPositions[bd.ColB] - ColumnXPositions[bd.ColA]);
            float dy = Mathf.Abs(GetSupportTopY(isSide ? bd.SupportB : bd.SupportA)
                     - (basePlateThick + bracingBottomClearance));
            float len = Mathf.Sqrt(dx*dx + dz*dz + dy*dy);
            string xMark = bayXEnabled[i] ? "X" : "╱";
            sb.AppendLine($"  [{xMark}] {bd.Name,-20} L={len:0.##}ft");
        }
        sb.AppendLine();

        // ── SOLAR PANELS ──────────────────────────────────────────────────
        sb.AppendLine("<b><color=#4BBFEE>▶ SOLAR PANELS  (2 rows × 5 = 10 total)</color></b>");
        sb.AppendLine($"  Size (L×W)        : {panelLength:0.##} ft × {panelWidth:0.##} ft");
        sb.AppendLine($"  Thickness         : {panelThickness*12f:0.##}\"");
        sb.AppendLine($"  Area each         : {panelLength*panelWidth:0.##} ft²");
        sb.AppendLine($"  Total Area        : {panelLength*panelWidth*totalPanels:0.##} ft²");
        sb.AppendLine($"  Weight each       : {PanelWeightLbEach:0.#} lbs");
        sb.AppendLine($"  Total Weight      : {totalPanelWeight:0.#} lbs");
        sb.AppendLine($"  Row Gap           : {rowGapZ:0.##} ft");
        sb.AppendLine($"  Wind Uplift (edge): {15f*panelLength*panelWidth*1.4f:0.#} lbs/panel");
        sb.AppendLine($"  Wind Uplift (mid) : {15f*panelLength*panelWidth:0.#} lbs/panel");
        sb.AppendLine();

        // ── CONCRETE FOOTINGS ─────────────────────────────────────────────
        sb.AppendLine("<b><color=#4BBFEE>▶ CONCRETE FOOTINGS  (9 total)</color></b>");
        if (showConcretePillars)
        {
            sb.AppendLine($"  Size (W×D×H)      : {concretePillarWidth:0.##}ft × {concretePillarDepth:0.##}ft × {concretePillarHeight:0.##}ft");
            sb.AppendLine($"  Volume each       : {footingVolFt3:0.##} ft³");
            sb.AppendLine($"  Weight each       : {footingWeightEach:0.#} lbs  ({footingWeightEach/2.205f:0.#} kg)");
            sb.AppendLine($"  Total Weight      : {totalFootingWeight:0.#} lbs  ({totalFootingWeight/2.205f:0.#} kg)");
            // Footing stress per pillar
            sb.AppendLine("  <i>Footing stress:</i>");
            for (int col = 0; col < ColumnCount; col++)
            {
                for (int sup = 0; sup < SupportCount; sup++)
                {
                    float h   = GetSupportHeight(sup);
                    float rf  = ComputePillarReductionFactor(col, sup);
                    float cap = concretePillarHeight * concretePillarWidth * concretePillarDepth;
                    float dem = loadPerPillar * h * rf;
                    float fs  = cap > 0f ? dem / (cap * 25f) * 100f : 100f;
                    string ftag = fs < 40f ? "<color=#22EE44>" : fs < 70f ? "<color=#FFAA00>" : "<color=#FF3333>";
                    sb.AppendLine($"  {colNames[col]}-{supNames[sup]}: {ftag}{fs:0.#}% capacity used</color>");
                }
            }
        }
        else
        {
            sb.AppendLine("  <i>(Hidden — toggle on to see details)</i>");
        }
        sb.AppendLine();

        // ── BASE & TOP PLATES ─────────────────────────────────────────────
        sb.AppendLine("<b><color=#4BBFEE>▶ BASE & TOP PLATES  (9 pairs)</color></b>");
        sb.AppendLine($"  Base Plate Size   : {basePlateSize*12f:0.##}\" × {basePlateSize*12f:0.##}\" × {basePlateThick*12f:0.###}\"");
        sb.AppendLine($"  Top Plate Size    : {topPlateSize*12f:0.##}\" × {topPlateSize*12f:0.##}\" × {topPlateThick*12f:0.###}\"");
        sb.AppendLine($"  Total Weight      : {totalPlateWeight:0.#} lbs");
        sb.AppendLine();

        // ── STRESS LEGEND ─────────────────────────────────────────────────
        sb.AppendLine("<b><color=#4BBFEE>▶ STRESS LEGEND</color></b>");
        sb.AppendLine("  <color=#22EE44>■ Green  </color>: < 40%  — Safe");
        sb.AppendLine("  <color=#FFAA00>■ Orange </color>: 40–70% — Warning");
        sb.AppendLine("  <color=#FF3333>■ Red    </color>: > 70%  — High Stress");
        sb.AppendLine();
        sb.AppendLine($"<size=80%><i>Last updated: {System.DateTime.Now:HH:mm:ss}</i></size>");

        detailsText.text = sb.ToString();
#endif
    }

    private string BuildDetailsText()
    {
        float pillarSectionArea    = GetHGirderSectionArea(hSize, hThick);
        float pillarWeightPerFt    = pillarSectionArea * SteelDensityLbPerFt3;
        float totalPillarLength    = ColumnCount * (heightFront + heightMid + heightBack);
        float totalPillarWeight    = totalPillarLength * pillarWeightPerFt;

        float beamSectionArea      = GetHGirderSectionArea(topBeamSize, topBeamThick);
        float beamWeightPerFt      = beamSectionArea * SteelDensityLbPerFt3;
        float beamWeightEach       = topBeamLength * beamWeightPerFt;
        float totalBeamWeight      = beamWeightEach * ColumnCount;

        float railSectionArea      = GetCChannelSectionArea(cChannelSize, cChannelThick);
        float railWeightPerFt      = railSectionArea * SteelDensityLbPerFt3;
        float railWeightEach       = cChannelLength * railWeightPerFt;
        float totalRailWeight      = railWeightEach * RailCount;

        BraceLayoutTotals braceTotals = CalculateBraceLayoutTotals();

        int totalPanels            = PanelRowCount * PanelColumnCount;
        float totalPanelArea       = panelLength * panelWidth * totalPanels;
        float totalPanelWeight     = totalPanels * PanelWeightLbEach;

        int footingCount           = ColumnCount * SupportCount;
        float footingVolFt3        = concretePillarWidth * concretePillarDepth * concretePillarHeight;
        float footingWeightEach    = footingVolFt3 * ConcreteWeightLbFt3;
        float totalFootingWeight   = showConcretePillars ? footingWeightEach * footingCount : 0f;

        float basePlateVol         = basePlateSize * basePlateSize * basePlateThick;
        float topPlateVol          = topPlateSize * topPlateSize * topPlateThick;
        float plateWeightEach      = (basePlateVol + topPlateVol) * SteelDensityLbPerFt3;
        float totalPlateWeight     = plateWeightEach * footingCount;

        float totalSteelWeight     = totalPillarWeight + totalBeamWeight + totalRailWeight + braceTotals.TotalWeightLb + totalPlateWeight;
        float totalStructureWeight = totalSteelWeight + totalPanelWeight + totalFootingWeight;
        float totalSupportedWeight = totalSteelWeight + totalPanelWeight;
        float loadPerPillar        = totalSupportedWeight / (ColumnCount * SupportCount);
        float totalWindLoad        = 15f * totalPanelArea;
        float windLoadPerPillar    = totalWindLoad / (ColumnCount * SupportCount);

        string[] colNames = { "Left", "Center", "Right" };
        string[] supNames = { "Front", "Mid", "Back" };

        var sb = new StringBuilder();
        sb.AppendLine("<b><size=110%>STRUCTURE DETAILS</size></b>");
        sb.AppendLine();

        sb.AppendLine("<b><color=#4BBFEE>OVERVIEW</color></b>");
        sb.AppendLine($"  Total Weight      : <b>{totalStructureWeight:0.#} lbs</b> ({LbToKg(totalStructureWeight):0.#} kg)");
        sb.AppendLine($"  Steel Weight      : {totalSteelWeight:0.#} lbs");
        sb.AppendLine($"  Panel Weight      : {totalPanelWeight:0.#} lbs");
        sb.AppendLine($"  Concrete Weight   : {totalFootingWeight:0.#} lbs");
        sb.AppendLine($"  Supported Load    : {totalSupportedWeight:0.#} lbs");
        sb.AppendLine($"  Wind Load (est.)  : {totalWindLoad:0.#} lbs");
        if (!string.IsNullOrEmpty(smartDesignSummary))
            sb.AppendLine($"  Smart Layout      : {smartDesignSummary}");
        sb.AppendLine();

        sb.AppendLine("<b><color=#4BBFEE>PILLARS (9 total)</color></b>");
        sb.AppendLine($"  Profile Size      : {hSize * 12f:0.##}\" x {hSize * 12f:0.##}\"");
        sb.AppendLine($"  Web Thickness     : {hThick * 12f:0.###}\"");
        sb.AppendLine($"  Total Length      : {totalPillarLength:0.##} ft");
        sb.AppendLine($"  Weight / ft       : {pillarWeightPerFt:0.##} lbs/ft");
        sb.AppendLine($"  Heights           : Front {heightFront:0.#} ft, Mid {heightMid:0.#} ft, Back {heightBack:0.#} ft");
        sb.AppendLine($"  Total Weight      : {totalPillarWeight:0.#} lbs");
        sb.AppendLine();
        sb.AppendLine("  <i>Stress per pillar (base moment)</i>");
        for (int col = 0; col < ColumnCount; col++)
        {
            for (int sup = 0; sup < SupportCount; sup++)
            {
                float h = GetSupportHeight(sup);
                float rf = ComputePillarReductionFactor(col, sup);
                float moment = (loadPerPillar + windLoadPerPillar) * h * rf;
                float maxMoment = (loadPerPillar + windLoadPerPillar) * Mathf.Max(heightFront, heightMid, heightBack);
                float pct = maxMoment > 0f ? moment / maxMoment * 100f : 0f;
                string tag = pct < 40f ? "<color=#22EE44>" : pct < 70f ? "<color=#FFAA00>" : "<color=#FF3333>";
                sb.AppendLine($"  {colNames[col]}-{supNames[sup]}: M={moment:0.#} lb*ft  {tag}{pct:0.#}%</color>");
            }
        }
        sb.AppendLine();

        sb.AppendLine("<b><color=#4BBFEE>TOP BEAMS (3 total)</color></b>");
        sb.AppendLine($"  Length Each       : {topBeamLength:0.##} ft");
        sb.AppendLine($"  Profile Size      : {topBeamSize * 12f:0.##}\" x {topBeamSize * 12f:0.##}\"");
        sb.AppendLine($"  Thickness         : {topBeamThick * 12f:0.###}\"");
        sb.AppendLine($"  Weight / ft       : {beamWeightPerFt:0.##} lbs/ft");
        sb.AppendLine($"  Weight Each       : {beamWeightEach:0.#} lbs");
        sb.AppendLine($"  Total Weight      : {totalBeamWeight:0.#} lbs");
        sb.AppendLine($"  Mid-span Moment   : {((totalPanelWeight + totalRailWeight) / ColumnCount) * topBeamLength * 0.25f:0.#} lb*ft");
        sb.AppendLine();

        sb.AppendLine("<b><color=#4BBFEE>C-CHANNEL RAILS (4 total)</color></b>");
        sb.AppendLine($"  Length Each       : {cChannelLength:0.##} ft");
        sb.AppendLine($"  Profile Size      : {cChannelSize * 12f:0.##}\"");
        sb.AppendLine($"  Thickness         : {cChannelThick * 12f:0.###}\"");
        sb.AppendLine($"  Weight / ft       : {railWeightPerFt:0.##} lbs/ft");
        sb.AppendLine($"  Weight Each       : {railWeightEach:0.#} lbs");
        sb.AppendLine($"  Total Weight      : {totalRailWeight:0.#} lbs");
        sb.AppendLine($"  Mid-span Moment   : {(totalPanelWeight / RailCount) * cChannelLength * 0.25f:0.#} lb*ft");
        sb.AppendLine();

        sb.AppendLine("<b><color=#4BBFEE>X-CROSSING BRACES</color></b>");
        sb.AppendLine($"  Active Bays       : {braceTotals.ActiveBayCount}");
        sb.AppendLine($"  Full X Bays       : {braceTotals.FullXBayCount}");
        sb.AppendLine($"  Single Bays       : {braceTotals.SingleBayCount}");
        sb.AppendLine($"  Active Diagonals  : {braceTotals.ActiveDiagonalCount}");
        sb.AppendLine($"  C-Channel Size    : {bracingSize * 12f:0.##}\"");
        sb.AppendLine($"  Bottom Clearance  : {bracingBottomClearance:0.#} ft");
        sb.AppendLine($"  Top Clearance     : {bracingTopClearance:0.#} ft");
        sb.AppendLine($"  Total Length      : {braceTotals.TotalLengthFt:0.##} ft");
        sb.AppendLine($"  Weight / ft       : {braceTotals.WeightPerFootLb:0.##} lbs/ft");
        sb.AppendLine($"  Total Weight      : {braceTotals.TotalWeightLb:0.#} lbs ({LbToKg(braceTotals.TotalWeightLb):0.#} kg)");
        sb.AppendLine($"  20ft Pieces       : {braceTotals.ActiveDiagonalCount} lengths");
        sb.AppendLine("  <i>Active brace bays</i>");
        for (int i = 0; i < XBayCount; i++)
        {
            if (!bayEnabled[i]) continue;
            float diagALength = GetBayDiagonalLength(i, false);
            float diagBLength = bayXEnabled[i] ? GetBayDiagonalLength(i, true) : 0f;
            float totalBayLength = diagALength + diagBLength;
            float totalBayWeight = totalBayLength * braceTotals.WeightPerFootLb;
            string mode = bayXEnabled[i] ? "X" : "Single";
            sb.AppendLine($"  [{mode}] {BayDefs[i].Name}: {totalBayLength:0.##} ft, {totalBayWeight:0.#} lbs");
        }
        sb.AppendLine();

        sb.AppendLine("<b><color=#4BBFEE>SOLAR PANELS (10 total)</color></b>");
        sb.AppendLine($"  Size (L x W)      : {panelLength:0.##} ft x {panelWidth:0.##} ft");
        sb.AppendLine($"  Thickness         : {panelThickness * 12f:0.##}\"");
        sb.AppendLine($"  Area Each         : {panelLength * panelWidth:0.##} ft2");
        sb.AppendLine($"  Total Area        : {totalPanelArea:0.##} ft2");
        sb.AppendLine($"  Weight Each       : {PanelWeightLbEach:0.#} lbs");
        sb.AppendLine($"  Total Weight      : {totalPanelWeight:0.#} lbs");
        sb.AppendLine($"  Row Gap           : {rowGapZ:0.##} ft");
        sb.AppendLine($"  Wind Uplift Edge  : {15f * panelLength * panelWidth * 1.4f:0.#} lbs/panel");
        sb.AppendLine($"  Wind Uplift Mid   : {15f * panelLength * panelWidth:0.#} lbs/panel");
        sb.AppendLine();

        sb.AppendLine("<b><color=#4BBFEE>CONCRETE FOOTINGS (9 total)</color></b>");
        if (showConcretePillars)
        {
            sb.AppendLine($"  Size (W x D x H)  : {concretePillarWidth:0.##} ft x {concretePillarDepth:0.##} ft x {concretePillarHeight:0.##} ft");
            sb.AppendLine($"  Volume Each       : {footingVolFt3:0.##} ft3");
            sb.AppendLine($"  Weight Each       : {footingWeightEach:0.#} lbs ({LbToKg(footingWeightEach):0.#} kg)");
            sb.AppendLine($"  Total Weight      : {totalFootingWeight:0.#} lbs ({LbToKg(totalFootingWeight):0.#} kg)");
            sb.AppendLine("  <i>Footing demand</i>");
            for (int col = 0; col < ColumnCount; col++)
            {
                for (int sup = 0; sup < SupportCount; sup++)
                {
                    float h = GetSupportHeight(sup);
                    float rf = ComputePillarReductionFactor(col, sup);
                    float cap = concretePillarHeight * concretePillarWidth * concretePillarDepth;
                    float demand = loadPerPillar * h * rf;
                    float used = cap > 0f ? demand / (cap * 25f) * 100f : 100f;
                    string tag = used < 40f ? "<color=#22EE44>" : used < 70f ? "<color=#FFAA00>" : "<color=#FF3333>";
                    sb.AppendLine($"  {colNames[col]}-{supNames[sup]}: {tag}{used:0.#}%</color>");
                }
            }
        }
        else
        {
            sb.AppendLine("  <i>Hidden - toggle on to inspect footing weight.</i>");
        }
        sb.AppendLine();

        sb.AppendLine("<b><color=#4BBFEE>BASE AND TOP PLATES (9 pairs)</color></b>");
        sb.AppendLine($"  Base Plate Size   : {basePlateSize * 12f:0.##}\" x {basePlateSize * 12f:0.##}\" x {basePlateThick * 12f:0.###}\"");
        sb.AppendLine($"  Top Plate Size    : {topPlateSize * 12f:0.##}\" x {topPlateSize * 12f:0.##}\" x {topPlateThick * 12f:0.###}\"");
        sb.AppendLine($"  Total Weight      : {totalPlateWeight:0.#} lbs");
        sb.AppendLine();

        sb.AppendLine("<b><color=#4BBFEE>STRESS LEGEND</color></b>");
        sb.AppendLine("  <color=#22EE44>Green</color>  : under 40% - Safe");
        sb.AppendLine("  <color=#FFAA00>Orange</color> : 40% to 70% - Watch");
        sb.AppendLine("  <color=#FF3333>Red</color>    : over 70% - High");
        sb.AppendLine();
        sb.AppendLine($"<size=80%><i>Last updated: {System.DateTime.Now:HH:mm:ss}</i></size>");

        return sb.ToString();
    }

    private void ApplySmartDesign()
    {
        EnsureBayArraySizes();

        for (int i = 0; i < XBayCount; i++)
        {
            bayEnabled[i] = false;
            bayXEnabled[i] = false;
        }

        int usedPieces = 0;
        float usedLength = 0f;

        while (usedPieces < SmartDesignBracePieceCount)
        {
            float currentScore = ComputeFrameStabilityScore();
            int bestBay = -1;
            bool bestAddsSecondDiagonal = false;
            float bestLength = 0f;
            float bestGain = 0f;

            for (int i = 0; i < XBayCount; i++)
            {
                if (!bayEnabled[i])
                {
                    float length = GetBayDiagonalLength(i, false);
                    if (length > SmartDesignBracePieceLengthFt + 0.001f) continue;

                    float gain = EvaluateSmartDesignGain(i, enableBay: true, enableX: false, currentScore);
                    if (gain > bestGain + 0.0001f || (Mathf.Abs(gain - bestGain) <= 0.0001f && (bestBay < 0 || length < bestLength)))
                    {
                        bestBay = i;
                        bestAddsSecondDiagonal = false;
                        bestLength = length;
                        bestGain = gain;
                    }
                }
                else if (!bayXEnabled[i])
                {
                    float length = GetBayDiagonalLength(i, true);
                    if (length > SmartDesignBracePieceLengthFt + 0.001f) continue;

                    float gain = EvaluateSmartDesignGain(i, enableBay: true, enableX: true, currentScore);
                    if (gain > bestGain + 0.0001f || (Mathf.Abs(gain - bestGain) <= 0.0001f && length < bestLength))
                    {
                        bestBay = i;
                        bestAddsSecondDiagonal = true;
                        bestLength = length;
                        bestGain = gain;
                    }
                }
            }

            if (bestBay < 0 || bestGain <= 0f) break;

            bayEnabled[bestBay] = true;
            if (bestAddsSecondDiagonal) bayXEnabled[bestBay] = true;
            else bayXEnabled[bestBay] = false;

            usedPieces++;
            usedLength += bestLength;
        }

        float spareLength = Mathf.Max(0f, SmartDesignTotalStockLengthFt - usedLength);
        smartDesignSummary = $"{usedPieces}/{SmartDesignBracePieceCount} pieces used, {usedLength:0.#}/{SmartDesignTotalStockLengthFt:0.#} ft, spare {spareLength:0.#} ft";
        ApplySceneState(preserveSmartDesignSummary: true);
        Debug.Log($"MobileSolarFrameApp: Smart brace layout applied. {smartDesignSummary}");
    }

    private float ComputeFrameStabilityScore()
    {
        float score = 0f;
        for (int col = 0; col < ColumnCount; col++)
        {
            for (int sup = 0; sup < SupportCount; sup++)
            {
                float rf = ComputePillarReductionFactor(col, sup);
                score += (1f - rf) * Mathf.Max(1f, GetSupportHeight(sup));
            }
        }

        return score;
    }

    private float EvaluateSmartDesignGain(int bayIndex, bool enableBay, bool enableX, float currentScore)
    {
        bool oldBay = bayEnabled[bayIndex];
        bool oldX = bayXEnabled[bayIndex];

        bayEnabled[bayIndex] = enableBay;
        bayXEnabled[bayIndex] = enableBay && enableX;
        float gain = ComputeFrameStabilityScore() - currentScore;

        bayEnabled[bayIndex] = oldBay;
        bayXEnabled[bayIndex] = oldX;
        return gain;
    }

    private static float LbToKg(float pounds) => pounds / 2.205f;

    private void SetCameraView(CameraView view)
    {
        float cx = 0f;
        float cy = (heightFront + heightMid + heightBack) / 3f * 0.5f;
        float cz = SupportZPositions[SupportCount - 1] * 0.5f;
        cameraTarget = new Vector3(cx, cy, cz);

        switch (view)
        {
            case CameraView.Front:
                xRot = 0f; yRot = 0f; distance = 40f;
                break;
            case CameraView.Side:
                xRot = 0f; yRot = 90f; distance = 40f;
                break;
            case CameraView.Top:
                xRot = 89f; yRot = 0f; distance = 45f;
                break;
            case CameraView.Iso:
            default:
                xRot = 30f; yRot = 45f; distance = 35f;
                break;
        }
        if (mainCamera != null && isOrthographic)
            mainCamera.orthographicSize = distance * 0.5f;
        ApplyCameraTransform();
    }

    private void HandleCameraControls()
    {
        float uiW = GetUiInteractionBoundary();
#if ENABLE_INPUT_SYSTEM
        if (!HandleTouchCameraControls(uiW)) HandleMouseCameraControls(uiW);
#elif ENABLE_LEGACY_INPUT_MANAGER
        HandleLegacyCameraControls(uiW);
#endif
        ApplyCameraTransform();
    }

    private float GetUiInteractionBoundary()
    {
        float sf = 1f;
        if (uiCanvas == null && uiPanelRect != null) uiCanvas = uiPanelRect.GetComponentInParent<Canvas>();
        if (uiCanvas != null) sf = uiCanvas.scaleFactor;
        if (uiPanelRect != null)
        {
            CaptureExpandedPanelPositionIfNeeded();
            if (isControlPanelVisible) return Mathf.Min(Screen.width, (expandedPanelAnchoredX + uiPanelRect.rect.width) * sf);
        }
        if (panelToggleButton != null)
        {
            var br = panelToggleButton.transform as RectTransform;
            if (br != null) return Mathf.Min(Screen.width, (br.anchoredPosition.x + br.rect.width) * sf);
        }
        return Screen.width * FallbackUiWidthRatio;
    }

#if ENABLE_INPUT_SYSTEM
    private bool HandleTouchCameraControls(float uiW)
    {
        if (!TryGetInProgressTouches(out InputSystemTouch t0, out InputSystemTouch t1, out int tc)) return false;
        if (tc == 1) { if (t0.screenPosition.x > uiW && t0.delta.sqrMagnitude > 0f) RotateCamera(t0.delta); }
        else
        {
            if (t0.screenPosition.x > uiW || t1.screenPosition.x > uiW)
            {
                float cd = Vector2.Distance(t0.screenPosition, t1.screenPosition);
                float pd = Vector2.Distance(t0.screenPosition - t0.delta, t1.screenPosition - t1.delta);
                ZoomCamera(cd - pd);
                if (t0.delta.sqrMagnitude > 0f && t1.delta.sqrMagnitude > 0f) PanCamera((t0.delta + t1.delta) * 0.5f);
            }
        }
        return true;
    }

    private bool TryGetInProgressTouches(out InputSystemTouch f, out InputSystemTouch s, out int count)
    {
        f = default; s = default; count = 0;
        var at = InputSystemTouch.activeTouches;
        for (int i = 0; i < at.Count; i++)
        {
            if (!at[i].isInProgress) continue;
            if (count == 0) f = at[i]; else if (count == 1) s = at[i];
            if (++count == 2) break;
        }
        return count > 0;
    }

    private void HandleMouseCameraControls(float uiW)
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 mp = mouse.position.ReadValue();
        if (mp.x <= uiW) return;
        Vector2 md = mouse.delta.ReadValue();
        if (mouse.leftButton.isPressed  && md.sqrMagnitude > 0f) RotateCamera(md);
        else if (mouse.rightButton.isPressed && md.sqrMagnitude > 0f) PanCamera(md);
        float sd = NormalizeInputSystemMouseScroll(mouse.scroll.ReadValue().y);
        if (!Mathf.Approximately(sd, 0f)) ZoomCamera(sd * 5f);
    }

    private float NormalizeInputSystemMouseScroll(float r) { if (Mathf.Abs(r) > 1f) r /= InputSystemMouseScrollStep; return r; }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
    private void HandleLegacyCameraControls(float uiW)
    {
        if (Input.touchSupported && Input.touchCount > 0)
        {
            if (Input.touchCount == 1)
            {
                Touch t = Input.GetTouch(0);
                if (t.position.x > uiW && t.phase == TouchPhase.Moved) RotateCamera(t.deltaPosition);
            }
            else if (Input.touchCount == 2)
            {
                Touch t0 = Input.GetTouch(0), t1 = Input.GetTouch(1);
                if (t0.position.x > uiW || t1.position.x > uiW)
                {
                    Vector2 p0 = t0.position - t0.deltaPosition, p1 = t1.position - t1.deltaPosition;
                    ZoomCamera(Vector2.Distance(t0.position, t1.position) - Vector2.Distance(p0, p1));
                    if (t0.phase == TouchPhase.Moved && t1.phase == TouchPhase.Moved) PanCamera((t0.deltaPosition + t1.deltaPosition) * 0.5f);
                }
            }
        }
        else
        {
            Vector2 mp = Input.mousePosition;
            if (mp.x > uiW)
            {
                Vector2 md = mp - lastMousePosition;
                if (Input.GetMouseButton(0)) RotateCamera(md);
                else if (Input.GetMouseButton(1)) PanCamera(md);
                float sd = Input.mouseScrollDelta.y;
                if (!Mathf.Approximately(sd, 0f)) ZoomCamera(sd * 5f);
            }
            lastMousePosition = Input.mousePosition;
        }
    }
#endif

    private void RotateCamera(Vector2 d) { yRot += d.x * rotateSpeed; xRot = Mathf.Clamp(xRot - d.y * rotateSpeed, MinXRotation, MaxXRotation); }
    private void PanCamera(Vector2 d)    { cameraTarget -= (transform.right * d.x + transform.up * d.y) * panSpeed; }
    private void ZoomCamera(float z)     { distance = Mathf.Clamp(distance - z * zoomSpeed, MinCameraDistance, MaxCameraDistance); }
    private void ApplyCameraTransform()
    {
        Quaternion r = Quaternion.Euler(xRot, yRot, 0f);
        transform.position = cameraTarget - r * Vector3.forward * distance;
        transform.LookAt(cameraTarget);
    }

    private void RefreshPanelVisibility()
    {
        if (uiPanelRect == null) return;
        CaptureExpandedPanelPositionIfNeeded();
        float tx = isControlPanelVisible ? expandedPanelAnchoredX : -(uiPanelRect.rect.width + HiddenPanelXMargin);
        uiPanelRect.anchoredPosition = new Vector2(tx, uiPanelRect.anchoredPosition.y);
        if (panelToggleButton != null)
        {
            var br = panelToggleButton.transform as RectTransform;
            if (br != null)
            {
                float bx = isControlPanelVisible ? expandedPanelAnchoredX + uiPanelRect.rect.width + ToggleButtonSpacing : ToggleButtonVisibleX;
                br.anchoredPosition = new Vector2(bx, br.anchoredPosition.y);
            }
        }
        if (panelToggleButtonLabel == null && panelToggleButton != null)
            panelToggleButtonLabel = panelToggleButton.GetComponentInChildren<TMP_Text>(true);
        if (panelToggleButtonLabel != null) panelToggleButtonLabel.text = isControlPanelVisible ? "<" : ">";
    }

    private void CaptureExpandedPanelPositionIfNeeded()
    {
        if (panelExpandedPositionCaptured || uiPanelRect == null) return;
        expandedPanelAnchoredX = uiPanelRect.anchoredPosition.x;
        panelExpandedPositionCaptured = true;
    }

    // ════════════════════════════════════════════════════════════════════
    //  Structure initialization
    // ════════════════════════════════════════════════════════════════════
    private bool InitializeStructureBindings()
    {
        TryAutoAssignSceneReferences();
        if (structureRoot == null)
        {
            if (!structureMissingWarningShown) { Debug.LogWarning("MobileSolarFrameApp: Solar_Mobile_Structure not found."); structureMissingWarningShown = true; }
            return false;
        }

        pillarsRoot = structureRoot.Find(PillarsRootName);
        beamsRoot   = structureRoot.Find(BeamsRootName);
        railsRoot   = structureRoot.Find(RailsRootName);
        panelsRoot  = structureRoot.Find(PanelsRootName);
        bracingRoot = structureRoot.Find("CrossBracing");
        if (bracingRoot == null)
        {
            bracingRoot = new GameObject("CrossBracing").transform;
            bracingRoot.SetParent(structureRoot, false);
        }
        
        // Stay lines root
        stayLinesRoot = structureRoot.Find("StayLines");
        if (stayLinesRoot == null)
        {
            stayLinesRoot = new GameObject("StayLines").transform;
            stayLinesRoot.SetParent(structureRoot, false);
        }
        
        // Angle brackets root
        angleBracketsRoot = structureRoot.Find("AngleBrackets");
        if (angleBracketsRoot == null)
        {
            angleBracketsRoot = new GameObject("AngleBrackets").transform;
            angleBracketsRoot.SetParent(structureRoot, false);
        }

        if (pillarsRoot == null || beamsRoot == null || railsRoot == null || panelsRoot == null)
        {
            if (!structureShapeWarningShown) { Debug.LogWarning("MobileSolarFrameApp: Incomplete structure hierarchy."); structureShapeWarningShown = true; }
            return false;
        }

        for (int col = 0; col < ColumnCount; col++)
        {
            for (int sup = 0; sup < SupportCount; sup++)
                pillarVisuals[GetPillarIndex(col, sup)] = CreatePillarVisual(pillarsRoot.Find($"Support_{col}_{sup}"));
            beamVisuals[col] = CreateBeamVisual(beamsRoot.Find($"Beam_{col}"));
        }
        for (int r = 0; r < RailCount; r++) railVisuals[r] = CreateRailVisual(railsRoot.Find($"Rail_{r}"));
        for (int row = 0; row < PanelRowCount; row++)
            for (int col = 0; col < PanelColumnCount; col++)
                panelVisuals[GetPanelIndex(row, col)] = CreatePanelVisual(panelsRoot.Find($"Panel_{row}_{col}"));

        // Ensure X-bay visual pool
        for (int i = 0; i < XBayCount; i++)
        {
            if (xBayVisuals[i] == null) xBayVisuals[i] = CreateXBayVisual(i);
        }
        
        // Ensure stay line visual pool (for StayLine bracing mode)
        for (int i = 0; i < StayLineCount; i++)
        {
            if (stayLineVisuals[i] == null) stayLineVisuals[i] = CreateStayLineVisual(i);
        }
        
        // Ensure angle bracket visual pool
        for (int i = 0; i < AngleBracketCount; i++)
        {
            if (angleBracketVisuals[i] == null) angleBracketVisuals[i] = CreateAngleBracketVisual(i);
        }

        EnsureStructureMaterials();
        // Note: ApplyStructureMaterials() is called by RefreshStructure() after all geometry updates
        return true;
    }

    private PillarVisual CreatePillarVisual(Transform root)
    {
        if (root == null) return null;
        return new PillarVisual { Root=root, BasePlate=root.Find("BasePlate"), Pillar=root.Find("Pillar"),
            TopPlate=root.Find("TopPlate"), ConcreteFooting=root.Find("ConcreteFooting"), Label=FindText(root,"Label") };
    }
    private BeamVisual  CreateBeamVisual(Transform root)  { return root == null ? null : new BeamVisual  { Root = root }; }
    private RailVisual  CreateRailVisual(Transform root)  { return root == null ? null : new RailVisual  { Root = root }; }
    private PanelVisual CreatePanelVisual(Transform root) { return root == null ? null : new PanelVisual { Root = root, Label = FindText(root, "Label") }; }

    private XBayVisual CreateXBayVisual(int bayIdx)
    {
        Transform bayRoot = bracingRoot.Find($"XBay_{bayIdx}");
        if (bayRoot == null)
        {
            bayRoot = new GameObject($"XBay_{bayIdx}").transform;
            bayRoot.SetParent(bracingRoot, false);
        }
        Transform dA = GetOrCreateCChannel(bayRoot, "DiagA");
        Transform dB = GetOrCreateCChannel(bayRoot, "DiagB");
        return new XBayVisual
        {
            DiagA  = dA,
            DiagB  = dB,
            LabelA = GetOrCreateLabel(dA),
            LabelB = GetOrCreateLabel(dB)
        };
    }
    
    // Create a stay line visual - vertical structural line (using C-channel shape)
    private StayLineVisual CreateStayLineVisual(int lineIdx)
    {
        Transform lineRoot = stayLinesRoot.Find($"StayLine_{lineIdx}");
        if (lineRoot == null)
        {
            lineRoot = new GameObject($"StayLine_{lineIdx}").transform;
            lineRoot.SetParent(stayLinesRoot, false);
        }
        // Create vertical line with C-channel profile
        Transform verticalPart = GetOrCreateCChannel(lineRoot, "Vertical");
        return new StayLineVisual
        {
            Root = lineRoot,
            Label = GetOrCreateLabel(verticalPart)
        };
    }
    
    // Create L-shaped angle bracket visual for column base
    private AngleBracketVisual CreateAngleBracketVisual(int colIdx)
    {
        Transform bracketRoot = angleBracketsRoot.Find($"AngleBracket_{colIdx}");
        if (bracketRoot == null)
        {
            bracketRoot = new GameObject($"AngleBracket_{colIdx}").transform;
            bracketRoot.SetParent(angleBracketsRoot, false);
        }
        // L-shape: vertical web + horizontal flange
        Transform web = GetOrCreateCChannel(bracketRoot, "Web");
        Transform flange = GetOrCreateCChannel(bracketRoot, "Flange");
        return new AngleBracketVisual
        {
            Root = bracketRoot,
            Web = web,
            Flange = flange,
            Label = GetOrCreateLabel(web)
        };
    }

    private Transform GetOrCreateCChannel(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t != null) return t;
        t = new GameObject(name).transform;
        t.SetParent(parent, false);
        foreach (string part in new[]{"Back","Top","Bot"})
        {
            GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            c.name = part; c.transform.SetParent(t, false);
            UnityEngine.Object.DestroyImmediate(c.GetComponent<Collider>());
        }
        return t;
    }

    private TMP_Text GetOrCreateLabel(Transform parent)
    {
        Transform lt = parent.Find("Label");
        if (lt != null) return lt.GetComponent<TMP_Text>();
        GameObject lo = new GameObject("Label", typeof(TextMeshPro));
        lo.transform.SetParent(parent, false);
        return lo.GetComponent<TextMeshPro>();
    }

    private TMP_Text FindText(Transform root, string childName)
    {
        Transform c = root.Find(childName);
        return c != null ? c.GetComponent<TMP_Text>() : null;
    }

    private int GetPillarIndex(int col, int sup) => col * SupportCount + sup;
    private int GetPanelIndex(int row, int col)  => row * PanelColumnCount + col;

    // ════════════════════════════════════════════════════════════════════
    //  Structure refresh
    // ════════════════════════════════════════════════════════════════════
    private void RefreshStructure()
    {
        if (!InitializeStructureBindings()) return;
        for (int col = 0; col < ColumnCount; col++)
        {
            for (int sup = 0; sup < SupportCount; sup++) UpdatePillarVisual(pillarVisuals[GetPillarIndex(col, sup)], col, sup);
            UpdateBeamVisual(beamVisuals[col], col);
        }
        for (int r = 0; r < RailCount; r++) UpdateRailVisual(railVisuals[r], r);
        UpdatePanelVisuals();
        UpdateBracingVisuals();
        UpdateStayLineVisuals();
        UpdateAngleBracketVisuals();
        // Apply base materials ONCE after all geometry is placed,
        // then apply stress colors on top — order matters because
        // sharedMaterial assignment clears MaterialPropertyBlock.
        ApplyStructureMaterials();
        ApplyStressPhysicsColors();
        // Refresh details panel if visible
        if (isDetailsPanelVisible) RefreshDetailsPanel();
    }

    // ── Pillar ───────────────────────────────────────────────────────────
    private void UpdatePillarVisual(PillarVisual v, int col, int sup)
    {
        if (v == null || v.Root == null) return;
        float h = GetSupportHeight(sup);
        v.Root.localPosition = new Vector3(ColumnXPositions[col], 0f, SupportZPositions[sup]);
        v.Root.localRotation = Quaternion.identity;

        UpdateCubeTransform(v.BasePlate, new Vector3(0f, basePlateThick * 0.5f, 0f), new Vector3(basePlateSize, basePlateThick, basePlateSize));
        ApplyHGirderVisual(v.Pillar, h, hSize, hThick, false);
        if (v.Pillar != null) { v.Pillar.localPosition = new Vector3(0f, basePlateThick + h * 0.5f, 0f); v.Pillar.localRotation = Quaternion.identity; }
        UpdateCubeTransform(v.TopPlate, new Vector3(0f, basePlateThick + h + topPlateThick * 0.5f, 0f), new Vector3(topPlateSize, topPlateThick, topPlateSize));

        // Concrete footing: sits ON TOP of the base plate (above ground level).
        // Real rooftop solar: the footing is a concrete block poured on the roof surface,
        // the pillar base plate is bolted to it. Taller footing = stronger anchor.
        if (v.ConcreteFooting != null)
        {
            v.ConcreteFooting.gameObject.SetActive(showConcretePillars);
            if (showConcretePillars)
            {
                // Footing sits above base plate on the roof surface.
                // Width (X), Depth (Z), Height (Y) all independently controlled.
                UpdateCubeTransform(v.ConcreteFooting,
                    new Vector3(0f, basePlateThick + concretePillarHeight * 0.5f, 0f),
                    new Vector3(concretePillarWidth, concretePillarHeight, concretePillarDepth));
            }
        }

        if (v.Label != null)
        {
            ConfigureWorldText(v.Label, false);
            v.Label.text = $"Pillar\n{h:0.#} ft";
            // localScale 0.1 set at creation — position in world units
            // sizeDelta in local units (divide world size by scale 0.1 = multiply by 10)
            v.Label.rectTransform.sizeDelta = new Vector2(72f, 24f);
            v.Label.transform.localPosition = new Vector3(0f, basePlateThick + h + 0.8f, -0.8f);
            v.Label.transform.localRotation = Quaternion.identity;
            v.Label.transform.localScale    = Vector3.one * 0.1f;
        }
    }

    // ── Beam ─────────────────────────────────────────────────────────────
    private void UpdateBeamVisual(BeamVisual v, int col)
    {
        if (v == null || v.Root == null) return;
        float sy = GetSupportTopY(0), ey = GetSupportTopY(2);
        Vector3 sp = new Vector3(ColumnXPositions[col], sy, SupportZPositions[0]);
        Vector3 ep = new Vector3(ColumnXPositions[col], ey, SupportZPositions[2]);
        Vector3 dir = (ep - sp).normalized;
        Vector3 up  = GetUpNormal(dir);
        v.Root.localPosition = (sp + ep) * 0.5f + up * (topBeamSize * 0.5f);
        v.Root.localRotation = Quaternion.LookRotation(dir, up);
        ApplyHGirderVisual(v.Root, topBeamLength, topBeamSize, topBeamThick, true);
    }

    // ── Rails — positioned to match panel assembly location ──────────────
    // Rails run left-right (X axis) across the frame.
    // Their Z position is derived from the panel assembly, not fixed rail indices,
    // so they always sit under the panels regardless of panelsOffset.
    private void UpdateRailVisual(RailVisual v, int railIndex)
    {
        if (v == null || v.Root == null) return;

        // Compute the two panel row Z positions (same logic as UpdatePanelVisuals)
        float profileLength       = GetProfileLength();
        float totalAssemblyLength = 2f * panelLength + rowGapZ;
        float startDist           = Mathf.Max(0f, (profileLength - totalAssemblyLength) * 0.5f) + panelsOffset;

        float distRow0 = startDist + panelLength * 0.5f;
        float distRow1 = startDist + panelLength + rowGapZ + panelLength * 0.5f;

        // 4 rails: 0 = front edge row0, 1 = back edge row0 / front edge row1, 2 = back edge row1, 3 = far back
        // Better: place 2 rails per row at ±panelLength*0.4 from row centre
        float[] railDists;
        switch (railIndex)
        {
            case 0: railDists = new[]{ distRow0 - panelLength * 0.4f }; break;
            case 1: railDists = new[]{ distRow0 + panelLength * 0.4f }; break;
            case 2: railDists = new[]{ distRow1 - panelLength * 0.4f }; break;
            default: railDists = new[]{ distRow1 + panelLength * 0.4f }; break;
        }

        float railDist = railDists[0];
        GetProfileSample(railDist, out float railZ, out float railBaseY, out float railAngle);
        float railY = railBaseY + topBeamSize + cChannelSize * 0.5f + panelsVerticalOffset;

        v.Root.localPosition = new Vector3(0f, railY, railZ);
        v.Root.localRotation = Quaternion.Euler(-railAngle, 0f, 0f);
        ApplyCChannelVisual(v.Root, cChannelLength, cChannelSize, cChannelThick);
    }

    // ── Panels ───────────────────────────────────────────────────────────
    private void UpdatePanelVisuals()
    {
        float profileLength       = GetProfileLength();
        float totalAssemblyLength = 2f * panelLength + rowGapZ;
        float startDist           = Mathf.Max(0f, (profileLength - totalAssemblyLength) * 0.5f) + panelsOffset;

        float totalRowWidth = PanelColumnCount * panelWidth
            + individualPanelGaps[0] + individualPanelGaps[1]
            + individualPanelGaps[2] + individualPanelGaps[3];
        float startX = -(totalRowWidth * 0.5f) + panelWidth * 0.5f;

        for (int row = 0; row < PanelRowCount; row++)
        {
            float panelDist = row == 0
                ? startDist + panelLength * 0.5f
                : startDist + panelLength + rowGapZ + panelLength * 0.5f;

            GetProfileSample(panelDist, out float pz, out float pBaseY, out float slopeAngle);
            float panelY = pBaseY + topBeamSize + cChannelSize + panelThickness * 0.5f + panelsVerticalOffset;

            // Row 0 (front/lower) shifts slightly toward front (-Z),
            // Row 1 (back/upper) shifts slightly toward back (+Z).
            // This gives a natural "spread" look matching real installations.
            float rowZShift = (row == 0) ? -panelLength * 0.05f : panelLength * 0.05f;
            float curX = startX;

            for (int col = 0; col < PanelColumnCount; col++)
            {
                PanelVisual visual = panelVisuals[GetPanelIndex(row, col)];
                if (visual != null && visual.Root != null)
                {
                    visual.Root.localPosition = new Vector3(curX, panelY, pz + rowZShift);
                    visual.Root.localRotation = Quaternion.Euler(-slopeAngle, 0f, 0f);
                    visual.Root.localScale    = new Vector3(panelWidth, panelThickness, panelLength);
                    if (visual.Label != null)
                    {
                        ConfigureWorldText(visual.Label, true);
                        visual.Label.text = $"Panel\n{panelLength:0.#}x{panelWidth:0.#}ft";
                        // Compensate for parent scale so label appears at world scale ~0.1
                        // Parent scale = (panelWidth, panelThickness, panelLength)
                        float sx = panelWidth  > 0.001f ? 0.1f / panelWidth  : 0.1f;
                        float sy = panelThickness > 0.001f ? 0.1f / panelThickness : 0.1f;
                        float sz = panelLength > 0.001f ? 0.1f / panelLength : 0.1f;
                        visual.Label.transform.localScale    = new Vector3(sx, sy, sz);
                        // sizeDelta in label's local space (world size / label world scale)
                        visual.Label.rectTransform.sizeDelta = new Vector2(Mathf.Max(90f, panelWidth * 24f), Mathf.Max(42f, panelLength * 9f));
                        visual.Label.transform.localPosition = new Vector3(0f, 0.5f + 0.02f / panelThickness, 0f);
                        visual.Label.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                    }
                }
                if (col < GapCount) curX += panelWidth + individualPanelGaps[col];
            }
        }
    }

    private void UpdatePanelVisual(PanelVisual v, float px, float py, float pz, float angle)
    {
        if (v == null || v.Root == null) return;
        v.Root.localPosition = new Vector3(px, py, pz);
        v.Root.localRotation = Quaternion.Euler(-angle, 0f, 0f);
        v.Root.localScale    = new Vector3(panelWidth, panelThickness, panelLength);
        if (v.Label != null)
        {
            ConfigureWorldText(v.Label, true);
            v.Label.text = $"Panel\n{panelLength:0.#}x{panelWidth:0.#}ft";
            float sx = panelWidth     > 0.001f ? 0.1f / panelWidth     : 0.1f;
            float sy = panelThickness > 0.001f ? 0.1f / panelThickness : 0.1f;
            float sz = panelLength    > 0.001f ? 0.1f / panelLength    : 0.1f;
            v.Label.transform.localScale    = new Vector3(sx, sy, sz);
            v.Label.rectTransform.sizeDelta = new Vector2(Mathf.Max(90f, panelWidth * 24f), Mathf.Max(42f, panelLength * 9f));
            v.Label.transform.localPosition = new Vector3(0f, 0.5f + 0.02f / panelThickness, 0f);
            v.Label.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        }
    }

    // ── X-Crossing bracing ───────────────────────────────────────────────
    private float GetHGirderSectionArea(float size, float thick)
    {
        float webArea = thick * Mathf.Max(0f, size - 2f * thick);
        float flangeArea = size * thick * 2f;
        return webArea + flangeArea;
    }

    private float GetCChannelSectionArea(float size, float thick)
    {
        float webArea = thick * Mathf.Max(0f, size - 2f * thick);
        float flangeArea = size * thick * 2f;
        return webArea + flangeArea;
    }

    private BraceLayoutTotals CalculateBraceLayoutTotals()
    {
        BraceLayoutTotals totals = default;
        totals.WeightPerFootLb = GetCChannelSectionArea(bracingSize, bracingThick) * SteelDensityLbPerFt3;

        for (int i = 0; i < XBayCount; i++)
        {
            if (!bayEnabled[i]) continue;

            totals.ActiveBayCount++;
            totals.SingleBayCount++;
            totals.ActiveDiagonalCount++;
            totals.TotalLengthFt += GetBayDiagonalLength(i, false);

            if (bayXEnabled[i])
            {
                totals.SingleBayCount--;
                totals.FullXBayCount++;
                totals.ActiveDiagonalCount++;
                totals.TotalLengthFt += GetBayDiagonalLength(i, true);
            }
        }

        totals.TotalWeightLb = totals.TotalLengthFt * totals.WeightPerFootLb;
        return totals;
    }

    private void GetBayCornerPoints(BayDef bd, out Vector3 cornerBL, out Vector3 cornerBR, out Vector3 cornerTL, out Vector3 cornerTR)
    {
        bool isSideBay = (bd.ColA == bd.ColB);

        if (isSideBay)
        {
            float x     = ColumnXPositions[bd.ColA];
            float zA    = SupportZPositions[bd.SupportA];
            float zB    = SupportZPositions[bd.SupportB];
            float topYA = GetSupportTopY(bd.SupportA) - topPlateThick - bracingTopClearance;
            float topYB = GetSupportTopY(bd.SupportB) - topPlateThick - bracingTopClearance;
            float botY  = basePlateThick + bracingBottomClearance;

            cornerBL = new Vector3(x, botY, zA);
            cornerTR = new Vector3(x, topYB, zB);
            cornerBR = new Vector3(x, botY, zB);
            cornerTL = new Vector3(x, topYA, zA);
            return;
        }

        float z      = SupportZPositions[bd.SupportA];
        float xL     = ColumnXPositions[bd.ColA];
        float xR     = ColumnXPositions[bd.ColB];
        float topY   = GetSupportTopY(bd.SupportA) - topPlateThick - bracingTopClearance;
        float bot    = basePlateThick + bracingBottomClearance;

        cornerBL = new Vector3(xL, bot, z);
        cornerTR = new Vector3(xR, topY, z);
        cornerBR = new Vector3(xR, bot, z);
        cornerTL = new Vector3(xL, topY, z);
    }

    private float GetBayDiagonalLength(int bayIndex, bool diagonalB)
    {
        GetBayCornerPoints(BayDefs[bayIndex], out Vector3 cornerBL, out Vector3 cornerBR, out Vector3 cornerTL, out Vector3 cornerTR);
        return Vector3.Distance(diagonalB ? cornerBR : cornerBL, diagonalB ? cornerTL : cornerTR);
    }

    private void UpdateBracingVisuals()
    {
        if (bracingRoot == null) return;
        EnsureBayArraySizes();

        // Only show X-crossing bracing when in XCrossing mode
        bool showXCrossing = bracingMode == BracingMode.XCrossing;

        for (int i = 0; i < XBayCount; i++)
        {
            if (xBayVisuals[i] == null) xBayVisuals[i] = CreateXBayVisual(i);
            XBayVisual bv = xBayVisuals[i];
            bool bayOn = bayEnabled[i];

            // Hide all X bay visuals when in StayLine mode
            if (!showXCrossing)
            {
                if (bv.DiagA != null) bv.DiagA.gameObject.SetActive(false);
                if (bv.DiagB != null) bv.DiagB.gameObject.SetActive(false);
                continue;
            }

            if (!bayOn)
            {
                if (bv.DiagA != null) bv.DiagA.gameObject.SetActive(false);
                if (bv.DiagB != null) bv.DiagB.gameObject.SetActive(false);
                continue;
            }

            GetBayCornerPoints(BayDefs[i], out Vector3 cornerBL, out Vector3 cornerBR, out Vector3 cornerTL, out Vector3 cornerTR);

            PositionDiagonal(bv.DiagA, bv.LabelA, cornerBL, cornerTR);
            bv.DiagA.gameObject.SetActive(true);

            PositionDiagonal(bv.DiagB, bv.LabelB, cornerBR, cornerTL);
            bv.DiagB.gameObject.SetActive(bayXEnabled[i]);
        }
        // Materials and stress colors are applied by RefreshStructure after all updates
    }
    
    // ── Stay Line Visuals (vertical structural lines) ─────────────────
    // Stay lines are vertical lines that provide structural support
    // Each row (front/mid/back) has 4 lines connecting adjacent columns
    // Line 1: bottom at 8ft, Line 2: top at 7ft (1ft overlap in middle)
    private void UpdateStayLineVisuals()
    {
        if (stayLinesRoot == null) return;
        
        // Only show stay lines when in StayLine mode
        bool showStayLines = bracingMode == BracingMode.StayLines;
        
        float lineSize = bracingSize;
        float lineThick = bracingThick;
        
        for (int i = 0; i < StayLineCount; i++)
        {
            if (stayLineVisuals[i] == null) stayLineVisuals[i] = CreateStayLineVisual(i);
            StayLineVisual sv = stayLineVisuals[i];
            
            sv.Root.gameObject.SetActive(showStayLines);
            if (!showStayLines) continue;
            
            // Determine which row and line type
            int row = i / StayLinesPerRow; // 0, 1, or 2 (front, mid, back)
            int lineIdxInRow = i % StayLinesPerRow; // 0, 1, 2, or 3
            
            float topY = GetSupportTopY(row) - topPlateThick;
            
            // Column positions for this line
            int colA = (lineIdxInRow < 2) ? 0 : 1;
            int colB = (lineIdxInRow < 2) ? 1 : 2;
            
            float xPos = (ColumnXPositions[colA] + ColumnXPositions[colB]) * 0.5f;
            float zPos = SupportZPositions[row];
            
            // Line 0, 2: 8ft height from bottom
            // Line 1, 3: 7ft height from top (so from topY-7ft to topY)
            bool isUpperLine = (lineIdxInRow % 2 == 1);
            
            float lineLength;
            float startY;
            
            if (isUpperLine)
            {
                // 7ft line starting from top (topY - 7ft to topY)
                lineLength = StayLineHeight2; // 7ft
                startY = topY - StayLineHeight2;
            }
            else
            {
                // 8ft line from bottom (basePlateThick to basePlateThick + 8ft)
                lineLength = StayLineHeight1; // 8ft
                startY = basePlateThick + bracingBottomClearance;
            }
            
            if (lineLength < 0.001f)
            {
                sv.Root.gameObject.SetActive(false);
                continue;
            }
            
            // Position at middle of the line (centered vertically)
            float midY = startY + lineLength * 0.5f;
            
            // Stay line runs VERTICALLY (along Y axis) between two columns
            sv.Root.localPosition = new Vector3(xPos, midY, zPos);
            sv.Root.localRotation = Quaternion.identity; // C-channel runs along local X, which is world Y when unrotated
            
            // Apply C-channel visual (runs along local X = world Y when identity rotation)
            Transform verticalChild = sv.Root.Find("Vertical");
            if (verticalChild != null)
            {
                ApplyCChannelVisual(verticalChild, lineLength, lineSize, lineThick);
            }
            
            // Update label
            if (sv.Label != null)
            {
                ConfigureWorldText(sv.Label, false);
                sv.Label.text = $"Stay {lineLength:0.#}ft";
                sv.Label.rectTransform.sizeDelta = new Vector2(72f, 24f);
                sv.Label.transform.localPosition = new Vector3(lineSize * 5f, 0f, lineSize * 2f);
                sv.Label.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
                sv.Label.transform.localScale = Vector3.one * 0.1f;
            }
        }
    }
    
    // ── Angle Bracket Visuals (L-shape at column base) ─────────────────
    // L-shaped 2-inch angle brackets at the bottom of each column
    private void UpdateAngleBracketVisuals()
    {
        if (angleBracketsRoot == null) return;
        
        float angleSize = 2f / 12f; // 2 inches
        float angleThick = 0.02f;
        
        for (int col = 0; col < AngleBracketCount; col++)
        {
            if (angleBracketVisuals[col] == null) angleBracketVisuals[col] = CreateAngleBracketVisual(col);
            AngleBracketVisual av = angleBracketVisuals[col];
            
            av.Root.gameObject.SetActive(true);
            
            float xPos = ColumnXPositions[col];
            float baseY = basePlateThick + bracingBottomClearance;
            float zOffset = 0.5f; // offset from pillar
            
            // L-shape position at column base
            av.Root.localPosition = new Vector3(xPos, baseY, zOffset);
            av.Root.localRotation = Quaternion.identity;
            
            // L-shape: vertical web (along Y) + horizontal flange (along Z)
            // Web: vertical part - goes UP from base
            if (av.Web != null)
            {
                ApplyCChannelVisual(av.Web, StayLineHeight1, angleSize, angleThick);
                av.Web.localPosition = new Vector3(0f, StayLineHeight1 * 0.5f, 0f);
                av.Web.localRotation = Quaternion.identity;
            }
            
            // Flange: horizontal part - extends OUTWARD in Z direction
            if (av.Flange != null)
            {
                ApplyCChannelVisual(av.Flange, StayLineHeight1, angleSize, angleThick);
                av.Flange.localPosition = new Vector3(0f, 0f, StayLineHeight1 * 0.5f);
                av.Flange.localRotation = Quaternion.identity;
            }
            
            // Update label
            if (av.Label != null)
            {
                ConfigureWorldText(av.Label, false);
                av.Label.text = "L-Angle";
                av.Label.rectTransform.sizeDelta = new Vector2(72f, 24f);
                av.Label.transform.localPosition = new Vector3(0f, StayLineHeight1 + 0.5f, StayLineHeight1 * 0.5f);
                av.Label.transform.localRotation = Quaternion.identity;
                av.Label.transform.localScale = Vector3.one * 0.1f;
            }
        }
    }

    private void PositionDiagonal(Transform diag, TMP_Text label, Vector3 start, Vector3 end)
    {
        if (diag == null) return;
        Vector3 dir    = end - start;
        float   length = dir.magnitude;
        if (length < 0.001f) { diag.gameObject.SetActive(false); return; }
        Vector3 dn = dir.normalized;

        diag.localPosition = start + dir * 0.5f;
        diag.localRotation = Quaternion.FromToRotation(Vector3.right, dn);
        ApplyCChannelVisual(diag, length, bracingSize, bracingThick);

        float angleFromHoriz = Mathf.Asin(Mathf.Clamp01(Mathf.Abs(dn.y))) * Mathf.Rad2Deg;
        if (label != null)
        {
            ConfigureWorldText(label, false);
            label.text = $"L:{length:0.##}ft\n{angleFromHoriz:0.#}\u00b0\nBot:{bracingBottomClearance:0.#} Top:{bracingTopClearance:0.#}";
            label.rectTransform.sizeDelta = new Vector2(180f, 56f);  // local units (scale 0.1)
            label.transform.localPosition = new Vector3(0f, bracingSize * 30f, 0f);
            label.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            label.transform.localScale    = Vector3.one * 0.1f;
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  Stress colours — per-part, physics-based
    // ════════════════════════════════════════════════════════════════════
    private void ApplyStressPhysicsColors()
    {
        // ── Load model ───────────────────────────────────────────────────
        // Panel weight ~34 lbs each, iron ~1.6 lb/ft
        float panelWeight   = 34f * (PanelRowCount * PanelColumnCount);
        float ironWeight    = cChannelLength * RailCount * 1.6f
                            + topBeamLength  * ColumnCount * 1.6f;
        float totalLoad     = panelWeight + ironWeight;
        float loadPerColumn = totalLoad / ColumnCount;
        float loadPerPillar = loadPerColumn / SupportCount;

        // Wind/lateral load estimate: 15 psf × panel area
        float panelArea     = panelLength * panelWidth * PanelRowCount * PanelColumnCount;
        float windLoad      = 15f * panelArea / ColumnCount / SupportCount;

        Color safeColor   = new Color(0.2f, 0.22f, 0.25f);
        Color warnColor   = new Color(1.0f, 0.55f, 0.0f);
        Color dangerColor = Color.red;

        // ── Per-pillar bracing reduction ─────────────────────────────────
        // For each (col, sup) pair, count how many active bays touch it
        // and whether X-crossing is enabled — this gives a reduction factor
        float[,] pillarRF = new float[ColumnCount, SupportCount];
        for (int col = 0; col < ColumnCount; col++)
            for (int sup = 0; sup < SupportCount; sup++)
                pillarRF[col, sup] = ComputePillarReductionFactor(col, sup);

        float maxH = Mathf.Max(heightFront, heightMid, heightBack);

        // ── Pillars — per-part stress ────────────────────────────────────
        for (int col = 0; col < ColumnCount; col++)
        {
            for (int sup = 0; sup < SupportCount; sup++)
            {
                PillarVisual pv = pillarVisuals[GetPillarIndex(col, sup)];
                if (pv == null) continue;

                float h  = GetSupportHeight(sup);
                float rf = pillarRF[col, sup];

                // Axial load (compression) — uniform along pillar height
                float axialLoad   = loadPerPillar;
                // Bending moment at base = (gravity + wind) × height × rf
                float baseMoment  = (loadPerPillar + windLoad) * h * rf;
                // Bending moment at mid = half of base moment
                float midMoment   = baseMoment * 0.5f;
                // Top of pillar: near zero bending (pinned at top plate)
                float topMoment   = baseMoment * 0.05f;

                // Reference: max possible moment (tallest pillar, no bracing)
                float maxMoment   = (loadPerPillar + windLoad) * maxH;

                // ── Web (carries shear + axial) ──────────────────────────
                // Shear is highest at base, decreases linearly to top
                // Color the web by base shear stress
                float webShear    = (loadPerPillar + windLoad) * rf;
                float webStress   = Mathf.Clamp01(webShear / (loadPerPillar + windLoad));
                if (pv.Pillar != null)
                {
                    Transform web = pv.Pillar.Find("Web");
                    if (web != null)
                    {
                        // Split web into 3 zones: bottom (high), mid, top (low)
                        // We color the whole web by average, but use base moment
                        float ws = Mathf.Clamp01(baseMoment / maxMoment);
                        ApplyColorToSingleRenderer(web.GetComponent<Renderer>(),
                            StressColor(ws, safeColor, warnColor, dangerColor));
                    }

                    // ── Flanges (carry bending moment) ───────────────────
                    // F1 = compression flange (higher stress at base)
                    // F2 = tension flange
                    Transform f1 = pv.Pillar.Find("F1");
                    Transform f2 = pv.Pillar.Find("F2");
                    float flangeBaseStress = Mathf.Clamp01(baseMoment / maxMoment);
                    float flangeMidStress  = Mathf.Clamp01(midMoment  / maxMoment);
                    // F1 = compression side (slightly higher due to buckling risk)
                    if (f1 != null) ApplyColorToSingleRenderer(f1.GetComponent<Renderer>(),
                        StressColor(Mathf.Clamp01(flangeBaseStress * 1.1f), safeColor, warnColor, dangerColor));
                    // F2 = tension side
                    if (f2 != null) ApplyColorToSingleRenderer(f2.GetComponent<Renderer>(),
                        StressColor(flangeMidStress, safeColor, warnColor, dangerColor));
                }

                // ── Base plate (bearing + shear transfer) ────────────────
                if (pv.BasePlate != null)
                {
                    // Base plate stress = axial + moment / section modulus proxy
                    float bpStress = Mathf.Clamp01((axialLoad * rf + baseMoment * 0.3f) / (maxMoment * 1.5f));
                    ApplyColorToSingleRenderer(pv.BasePlate.GetComponent<Renderer>(),
                        StressColor(bpStress, safeColor, warnColor, dangerColor));
                }

                // ── Top plate (connection to beam — lower stress) ─────────
                if (pv.TopPlate != null)
                {
                    float tpStress = Mathf.Clamp01(topMoment / maxMoment);
                    ApplyColorToSingleRenderer(pv.TopPlate.GetComponent<Renderer>(),
                        StressColor(tpStress, safeColor, warnColor, dangerColor));
                }

                // ── Concrete footing (anchor pullout + bearing) ───────────
                if (pv.ConcreteFooting != null && pv.ConcreteFooting.gameObject.activeSelf)
                {
                    float cap = concretePillarHeight * concretePillarWidth * concretePillarDepth;
                    // Demand = base moment + axial (pullout risk)
                    float dem = (baseMoment + axialLoad * rf * 0.5f);
                    float fs  = cap > 0f ? Mathf.Clamp01(dem / (cap * 25f)) : 1f;
                    ApplyColorToSingleRenderer(pv.ConcreteFooting.GetComponent<Renderer>(),
                        StressColor(fs, new Color(0.6f,0.6f,0.6f), warnColor, dangerColor));
                }
            }
        }

        // ── Top beams — per-part stress ───────────────────────────────────
        // Beam carries bending (panels + rails) and torsion from wind
        for (int col = 0; col < ColumnCount; col++)
        {
            BeamVisual bv = beamVisuals[col];
            if (bv == null || bv.Root == null) continue;

            float beamLoad    = loadPerColumn;
            float halfSpan    = topBeamLength * 0.5f;
            // Max bending moment at mid-span
            float beamMidMom  = beamLoad * halfSpan * 0.25f; // simply supported
            float beamEndMom  = beamMidMom * 0.1f;           // near supports
            float maxBeamMom  = totalLoad * halfSpan * 0.25f;

            Transform bWeb = bv.Root.Find("Web");
            Transform bF1  = bv.Root.Find("F1");
            Transform bF2  = bv.Root.Find("F2");

            // Web: shear stress (highest near supports)
            if (bWeb != null)
            {
                float ws = Mathf.Clamp01(beamLoad / totalLoad);
                ApplyColorToSingleRenderer(bWeb.GetComponent<Renderer>(),
                    StressColor(ws, safeColor, warnColor, dangerColor));
            }
            // F1: compression flange (top of beam under gravity load)
            if (bF1 != null)
            {
                float fs = Mathf.Clamp01(beamMidMom / maxBeamMom);
                ApplyColorToSingleRenderer(bF1.GetComponent<Renderer>(),
                    StressColor(fs, safeColor, warnColor, dangerColor));
            }
            // F2: tension flange (bottom of beam)
            if (bF2 != null)
            {
                float fs = Mathf.Clamp01(beamMidMom / maxBeamMom * 0.9f);
                ApplyColorToSingleRenderer(bF2.GetComponent<Renderer>(),
                    StressColor(fs, safeColor, warnColor, dangerColor));
            }
        }

        // ── C-channel rails — per-part stress ────────────────────────────
        // Each rail carries 1/4 of panel load in bending (simply supported)
        for (int r = 0; r < railVisuals.Length; r++)
        {
            RailVisual rv = railVisuals[r];
            if (rv == null || rv.Root == null) continue;

            float railLoad   = totalLoad / RailCount;
            float railHalf   = cChannelLength * 0.5f;
            float railMidMom = railLoad * railHalf * 0.25f;
            float maxRailMom = totalLoad * railHalf * 0.25f;

            Transform rBack = rv.Root.Find("Back");
            Transform rTop  = rv.Root.Find("Top");
            Transform rBot  = rv.Root.Find("Bot");

            // Back web: shear
            if (rBack != null)
            {
                float ws = Mathf.Clamp01(railLoad / totalLoad * 2f);
                ApplyColorToSingleRenderer(rBack.GetComponent<Renderer>(),
                    StressColor(ws, new Color(0.5f,0.55f,0.6f), warnColor, dangerColor));
            }
            // Top flange: compression (panels push down)
            if (rTop != null)
            {
                float fs = Mathf.Clamp01(railMidMom / maxRailMom);
                ApplyColorToSingleRenderer(rTop.GetComponent<Renderer>(),
                    StressColor(fs, new Color(0.5f,0.55f,0.6f), warnColor, dangerColor));
            }
            // Bottom flange: tension
            if (rBot != null)
            {
                float fs = Mathf.Clamp01(railMidMom / maxRailMom * 0.85f);
                ApplyColorToSingleRenderer(rBot.GetComponent<Renderer>(),
                    StressColor(fs, new Color(0.5f,0.55f,0.6f), warnColor, dangerColor));
            }
        }

        // ── X-crossing braces — per-diagonal stress ───────────────────────
        // Only apply stress colors when in X-Crossing mode
        if (bracingMode == BracingMode.XCrossing)
        {
            // Diagonal A: tension member (carries lateral load)
            // Diagonal B: compression member (higher stress, buckling risk)
            for (int i = 0; i < XBayCount; i++)
            {
                XBayVisual xv = xBayVisuals[i];
                if (xv == null) continue;

                BayDef bd = BayDefs[i];
                bool isSide = (bd.ColA == bd.ColB);

                // Lateral shear in this bay
                float bayShear = windLoad * (isSide ? 1.2f : 1.0f);
                // Diagonal force = shear / cos(angle) — approximate with 1.4 factor
                float diagForce = bayShear * 1.4f;
                float maxDiagForce = windLoad * 2f;

                // Tension diagonal (DiagA)
                if (xv.DiagA != null && xv.DiagA.gameObject.activeSelf)
                {
                    float ts = Mathf.Clamp01(diagForce / maxDiagForce);
                    ColorCChannel(xv.DiagA, ts, new Color(0.5f,0.55f,0.6f), warnColor, dangerColor);
                }
                // Compression diagonal (DiagB) — higher stress due to buckling
                if (xv.DiagB != null && xv.DiagB.gameObject.activeSelf)
                {
                    float cs = Mathf.Clamp01(diagForce * 1.35f / maxDiagForce);
                    ColorCChannel(xv.DiagB, cs, new Color(0.5f,0.55f,0.6f), warnColor, dangerColor);
                }
            }
        }
        
        // ── Stay Line braces — per-line stress ────────────────────────────
        if (bracingMode == BracingMode.StayLines)
        {
            // Calculate stress based on pillar height and load
            float maxHeight = Mathf.Max(heightFront, heightMid, heightBack);
            
            for (int i = 0; i < StayLineCount; i++)
            {
                StayLineVisual sv = stayLineVisuals[i];
                if (sv == null || sv.Root == null || !sv.Root.gameObject.activeSelf) continue;
                
                int row = i / StayLinesPerRow;
                float supportHeight = GetSupportHeight(row);
                
                // Stay lines are vertical tension members
                // Higher lines (7ft from top) have more tension
                bool isUpperLine = (i % 2 == 1);
                
                // Calculate stress based on position and load
                float heightFactor = supportHeight / maxHeight;
                float positionFactor = isUpperLine ? 0.8f : 0.5f;
                float loadFactor = loadPerPillar / 100f; // normalize load
                
                float stress = Mathf.Clamp01(heightFactor * positionFactor * loadFactor * 2f);
                
                // Apply stress color to the stay line (C-channel: Back, Top, Bot)
                Transform verticalChild = sv.Root.Find("Vertical");
                if (verticalChild != null)
                    ColorCChannel(verticalChild, stress, new Color(0.3f,0.6f,0.4f), warnColor, dangerColor);
            }
            
            // ── Angle brackets — base support stress ───────────────────────
            for (int col = 0; col < AngleBracketCount; col++)
            {
                AngleBracketVisual av = angleBracketVisuals[col];
                if (av == null || av.Root == null || !av.Root.gameObject.activeSelf) continue;
                
                // Angle brackets bear the load transfer from pillars to concrete
                // Stress is higher at outer columns due to moment
                float colFactor = (col == 0 || col == 2) ? 1.2f : 1.0f;
                float baseStress = loadPerPillar / 100f;
                float stress = Mathf.Clamp01(baseStress * colFactor * 1.5f);
                
                if (av.Web != null) ColorCChannel(av.Web, stress, new Color(0.4f,0.5f,0.6f), warnColor, dangerColor);
                if (av.Flange != null) ColorCChannel(av.Flange, stress * 0.9f, new Color(0.4f,0.5f,0.6f), warnColor, dangerColor);
            }
        }

        // ── Solar panels — wind uplift stress ─────────────────────────────
        // Panels at edges have higher wind uplift than center panels
        for (int row = 0; row < PanelRowCount; row++)
        {
            for (int col = 0; col < PanelColumnCount; col++)
            {
                PanelVisual pv = panelVisuals[GetPanelIndex(row, col)];
                if (pv == null || pv.Root == null) continue;
                // Edge panels (col 0 or 4) have ~40% higher wind load
                bool isEdge = (col == 0 || col == PanelColumnCount - 1);
                float uplift = 15f * panelLength * panelWidth * (isEdge ? 1.4f : 1.0f);
                float maxUplift = 15f * panelLength * panelWidth * 1.4f * PanelColumnCount;
                float ps = Mathf.Clamp01(uplift / maxUplift * 3f);
                ApplyColorToSingleRenderer(pv.Root.GetComponent<Renderer>(),
                    StressColor(ps, new Color(0.05f,0.15f,0.3f), new Color(0.3f,0.1f,0.5f), dangerColor));
            }
        }
    }

    // Compute bracing reduction factor for a specific pillar (col, sup)
    private float ComputePillarReductionFactor(int col, int sup)
    {
        float rf = 1.0f;

        // In StayLine mode, stay lines provide different structural support
        // Stay lines act as continuous vertical members, reducing moment more effectively
        if (bracingMode == BracingMode.StayLines)
        {
            // Stay lines provide strong lateral support
            // Each stay line reduces moment by ~30%
            int stayLineCount = StayLinesPerRow; // 4 lines per row
            rf *= Mathf.Pow(0.70f, stayLineCount);
            
            // Stay lines start from 8ft, so there's less unbraced length at base
            float stayLineStart = StayLineHeight2; // 7ft from bottom
            rf *= Mathf.Lerp(1.0f, 0.6f, Mathf.Clamp01(stayLineStart / GetSupportHeight(sup)));
        }
        else
        {
            // X-Crossing bracing mode
            // Count bays that directly brace this pillar
            int bayCount = 0, xBayCount2 = 0;
            for (int i = 0; i < XBayCount; i++)
            {
                if (!bayEnabled[i]) continue;
                BayDef bd = BayDefs[i];
                bool isSide = (bd.ColA == bd.ColB);

                bool touches = false;
                if (isSide)
                {
                    // Side bay touches this pillar if same column and spans this support
                    touches = (bd.ColA == col) &&
                              (bd.SupportA == sup || bd.SupportB == sup);
                }
                else
                {
                    // Z-plane bay touches this pillar if same support and adjacent column
                    touches = (bd.SupportA == sup) &&
                              (bd.ColA == col || bd.ColB == col);
                }

                if (touches)
                {
                    bayCount++;
                    if (bayXEnabled[i]) xBayCount2++;
                }
            }

            // Each single diagonal reduces moment by ~25%
            // Each X-crossing (2 diagonals) reduces by ~40%
            if (bayCount > 0)
            {
                rf *= Mathf.Pow(0.75f, bayCount);
                if (xBayCount2 > 0) rf *= Mathf.Pow(0.80f, xBayCount2);
            }

            // Clearance effect: larger bottom clearance = less effective bracing
            float unbraced = bracingBottomClearance / GetSupportHeight(sup);
            rf *= Mathf.Lerp(1.0f, 1.3f, Mathf.Clamp01(unbraced));

            // Top clearance: less effect but still reduces brace effectiveness
            float topUnbraced = bracingTopClearance / GetSupportHeight(sup);
            rf *= Mathf.Lerp(1.0f, 1.15f, Mathf.Clamp01(topUnbraced));
        }

        // Concrete footing benefit (applies to both modes)
        if (showConcretePillars && concretePillarHeight > 0f)
        {
            float volRatio = Mathf.Clamp01(
                (concretePillarHeight / 4f) * 0.5f +
                (concretePillarWidth  / 3f) * 0.25f +
                (concretePillarDepth  / 3f) * 0.25f);
            rf *= Mathf.Lerp(1.0f, 0.35f, volRatio);
        }

        return Mathf.Clamp(rf, 0.05f, 1.0f);
    }

    private Color StressColor(float t, Color safe, Color warn, Color danger)
    {
        t = Mathf.Clamp01(t);
        return t < 0.5f
            ? Color.Lerp(safe, warn, t * 2f)
            : Color.Lerp(warn, danger, (t - 0.5f) * 2f);
    }

    private void ColorCChannel(Transform root, float stress, Color safe, Color warn, Color danger)
    {
        if (root == null) return;
        // Back web: shear stress
        var back = root.Find("Back");
        if (back != null) ApplyColorToSingleRenderer(back.GetComponent<Renderer>(),
            StressColor(stress * 0.8f, safe, warn, danger));
        // Top flange: compression
        var top = root.Find("Top");
        if (top != null) ApplyColorToSingleRenderer(top.GetComponent<Renderer>(),
            StressColor(stress, safe, warn, danger));
        // Bottom flange: tension (slightly less)
        var bot = root.Find("Bot");
        if (bot != null) ApplyColorToSingleRenderer(bot.GetComponent<Renderer>(),
            StressColor(stress * 0.9f, safe, warn, danger));
    }
    
    // Apply stress color to H-Girder visual (Web, F1, F2)
    private void ApplyHGirderStress(Transform root, float stress, Color safe, Color warn, Color danger)
    {
        if (root == null) return;
        var web = root.Find("Web");
        if (web != null) ApplyColorToSingleRenderer(web.GetComponent<Renderer>(),
            StressColor(stress * 0.9f, safe, warn, danger));
        var f1 = root.Find("F1");
        if (f1 != null) ApplyColorToSingleRenderer(f1.GetComponent<Renderer>(),
            StressColor(stress, safe, warn, danger));
        var f2 = root.Find("F2");
        if (f2 != null) ApplyColorToSingleRenderer(f2.GetComponent<Renderer>(),
            StressColor(stress * 0.95f, safe, warn, danger));
    }

    private void ApplyColorToSingleRenderer(Renderer r, Color color)
    {
        if (r == null) return;
        r.GetPropertyBlock(stressPropertyBlock);
        stressPropertyBlock.SetColor("_Color", color);
        r.SetPropertyBlock(stressPropertyBlock);
    }

    // ════════════════════════════════════════════════════════════════════
    //  Materials
    // ════════════════════════════════════════════════════════════════════
    private void EnsureStructureMaterials()
    {
        if (hGirderMaterial  == null) hGirderMaterial  = CreateMat(new Color(0.2f, 0.22f, 0.25f));
        if (cChannelMaterial == null) cChannelMaterial = CreateMat(new Color(0.5f, 0.55f, 0.6f));
        if (solarMaterial    == null) solarMaterial    = CreateMat(new Color(0.05f, 0.15f, 0.3f));
        if (plateMaterial    == null) plateMaterial    = CreateMat(new Color(0.1f, 0.1f, 0.1f));
        if (concreteMaterial == null) concreteMaterial = CreateMat(new Color(0.72f, 0.70f, 0.65f));
    }

    private void ApplyStructureMaterials()
    {
        foreach (var p in pillarVisuals)
        {
            if (p == null) continue;
            ApplyMaterialToSingleRenderer(p.BasePlate, plateMaterial);
            ApplyMaterialToSingleRenderer(p.TopPlate,  plateMaterial);
            ApplyMaterialToBranch(p.Pillar, hGirderMaterial);
            ApplyMaterialToSingleRenderer(p.ConcreteFooting, concreteMaterial);
        }
        foreach (var b in beamVisuals)  ApplyMaterialToBranch(b?.Root, hGirderMaterial);
        foreach (var r in railVisuals)  ApplyMaterialToBranch(r?.Root, cChannelMaterial);
        foreach (var p in panelVisuals) ApplyMaterialToSingleRenderer(p?.Root, solarMaterial);
        foreach (var x in xBayVisuals)
        {
            if (x == null) continue;
            ApplyMaterialToBranch(x.DiagA, cChannelMaterial);
            ApplyMaterialToBranch(x.DiagB, cChannelMaterial);
        }
        foreach (var sl in stayLineVisuals)
        {
            if (sl == null || sl.Root == null) continue;
            ApplyMaterialToBranch(sl.Root, cChannelMaterial);
        }
        foreach (var ab in angleBracketVisuals)
        {
            if (ab == null || ab.Root == null) continue;
            ApplyMaterialToBranch(ab.Root, cChannelMaterial);
        }
    }

    private void ApplyMaterialToBranch(Transform root, Material mat)
    {
        if (root == null || mat == null) return;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = mat;
    }

    private void ApplyMaterialToSingleRenderer(Transform t, Material mat)
    {
        if (t == null || mat == null) return;
        var r = t.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = mat;
    }

    private Material CreateMat(Color color)
    {
        Shader sh = Shader.Find("Standard") ?? Shader.Find("Mobile/Diffuse");
        Material m = new Material(sh);
        if (m.HasProperty("_Color")) m.color = color;
        return m;
    }

    // ════════════════════════════════════════════════════════════════════
    //  Geometry helpers
    // ════════════════════════════════════════════════════════════════════
    private void UpdateCubeTransform(Transform cube, Vector3 lp, Vector3 ls)
    {
        if (cube == null) return;
        cube.localPosition = lp;
        cube.localRotation = Quaternion.identity;
        cube.localScale    = ls;
    }

    private void ApplyHGirderVisual(Transform root, float length, float size, float thick, bool alongZ)
    {
        if (root == null) return;
        Transform web = root.Find("Web"), f1 = root.Find("F1"), f2 = root.Find("F2");
        UpdateCubeTransform(web, Vector3.zero,
            alongZ ? new Vector3(thick, size - 2f*thick, length) : new Vector3(thick, length, size - 2f*thick));
        Vector3 fo = alongZ ? new Vector3(0f, size*0.5f - thick*0.5f, 0f) : new Vector3(0f, 0f, size*0.5f - thick*0.5f);
        Vector3 fs = alongZ ? new Vector3(size, thick, length) : new Vector3(size, length, thick);
        UpdateCubeTransform(f1, -fo, fs);
        UpdateCubeTransform(f2,  fo, fs);
    }

    // C-channel runs along local X axis
    private void ApplyCChannelVisual(Transform root, float length, float size, float thick)
    {
        if (root == null) return;
        UpdateCubeTransform(root.Find("Back"), new Vector3(0f, 0f, -(size*0.5f - thick*0.5f)), new Vector3(length, size, thick));
        UpdateCubeTransform(root.Find("Top"),  new Vector3(0f,  size*0.5f - thick*0.5f, 0f),   new Vector3(length, thick, size));
        UpdateCubeTransform(root.Find("Bot"),  new Vector3(0f, -(size*0.5f - thick*0.5f), 0f), new Vector3(length, thick, size));
    }

    private void ConfigureWorldText(TMP_Text text, bool isPanelLabel)
    {
        if (text == null) return;
        if (defaultFont != null && text.font != defaultFont) text.font = defaultFont;
        text.alignment          = TextAlignmentOptions.Center;
        text.enableWordWrapping = isPanelLabel;
        text.enableAutoSizing   = true;
        text.fontSizeMin        = isPanelLabel ? 4f  : 3f;
        text.fontSizeMax        = isPanelLabel ? 14f : 9f;
        text.fontSize           = isPanelLabel ? 14f : 9f;
        text.color              = Color.white;
        text.outlineColor       = new Color32(0, 0, 0, 255);
        text.outlineWidth       = 0.4f;
        text.raycastTarget      = false;
        text.overflowMode       = TextOverflowModes.Overflow;
        // Ensure rect is large enough to show text
        // (localScale is 0.1 so sizeDelta in local units = world units / 0.1)
        if (text.rectTransform != null)
        {
            text.rectTransform.sizeDelta = isPanelLabel
                ? new Vector2(Mathf.Max(80f, panelWidth * 25f), Mathf.Max(30f, panelLength * 8f))
                : new Vector2(55f, 20f);
        }
    }

    private float GetSupportHeight(int sup) { switch(sup){case 0:return heightFront;case 1:return heightMid;default:return heightBack;} }
    private float GetSupportTopY(int sup)   => basePlateThick + GetSupportHeight(sup) + topPlateThick;

    private float GetTopProfileYAtZ(float z)
    {
        if (z <= SupportZPositions[1])
            return Mathf.Lerp(GetSupportTopY(0), GetSupportTopY(1), Mathf.InverseLerp(SupportZPositions[0], SupportZPositions[1], z));
        return Mathf.Lerp(GetSupportTopY(1), GetSupportTopY(2), Mathf.InverseLerp(SupportZPositions[1], SupportZPositions[2], z));
    }

    private float GetProfileAngleAtZ(float z)
    {
        if (z <= SupportZPositions[1])
            return Mathf.Atan2(GetSupportTopY(1)-GetSupportTopY(0), SupportZPositions[1]-SupportZPositions[0]) * Mathf.Rad2Deg;
        return Mathf.Atan2(GetSupportTopY(2)-GetSupportTopY(1), SupportZPositions[2]-SupportZPositions[1]) * Mathf.Rad2Deg;
    }

    private float GetProfileLength()
    {
        return Vector2.Distance(new Vector2(SupportZPositions[0], GetSupportTopY(0)), new Vector2(SupportZPositions[1], GetSupportTopY(1)))
             + Vector2.Distance(new Vector2(SupportZPositions[1], GetSupportTopY(1)), new Vector2(SupportZPositions[2], GetSupportTopY(2)));
    }

    private void GetProfileSample(float dist, out float z, out float y, out float angle)
    {
        float seg1 = Vector2.Distance(new Vector2(SupportZPositions[0], GetSupportTopY(0)), new Vector2(SupportZPositions[1], GetSupportTopY(1)));
        float seg2 = Vector2.Distance(new Vector2(SupportZPositions[1], GetSupportTopY(1)), new Vector2(SupportZPositions[2], GetSupportTopY(2)));
        float d    = Mathf.Clamp(dist, 0f, seg1 + seg2);
        if (d <= seg1 || Mathf.Approximately(seg2, 0f))
        {
            float t = seg1 > 0f ? d / seg1 : 0f;
            z = Mathf.Lerp(SupportZPositions[0], SupportZPositions[1], t);
            y = Mathf.Lerp(GetSupportTopY(0), GetSupportTopY(1), t);
            angle = Mathf.Atan2(GetSupportTopY(1)-GetSupportTopY(0), SupportZPositions[1]-SupportZPositions[0]) * Mathf.Rad2Deg;
            return;
        }
        float t2 = seg2 > 0f ? (d - seg1) / seg2 : 0f;
        z = Mathf.Lerp(SupportZPositions[1], SupportZPositions[2], t2);
        y = Mathf.Lerp(GetSupportTopY(1), GetSupportTopY(2), t2);
        angle = Mathf.Atan2(GetSupportTopY(2)-GetSupportTopY(1), SupportZPositions[2]-SupportZPositions[1]) * Mathf.Rad2Deg;
    }

    private Vector3 GetUpNormal(Vector3 dir)
    {
        Vector3 u = new Vector3(0f, dir.z, -dir.y);
        return u.sqrMagnitude > 0f ? u.normalized : Vector3.up;
    }
}
