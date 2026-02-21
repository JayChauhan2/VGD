using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Multi-page tutorial overlay with per-character drop animation.
/// Uses Unity's TextGenerator to obtain real proportional font character positions,
/// eliminating irregular spacing caused by fixed-width estimates.
/// 
/// Click during animation  -> skip to full text
/// Click when done         -> next page
/// After last page         -> unlock start room doors and destroy self
/// </summary>
public class TutorialOverlay : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Tutorial pages
    // -------------------------------------------------------------------------
    private static readonly string[] Pages = new string[]
    {
        "You are trapped in a dungeon.\nYour goal is to escape.",
        "Move with WASD or the Arrow Keys.",
        "Left Click to shoot.\nHold Right Click to focus your aim.",
        "Press Shift to drop a Bomb.\nPress Space to Dash.",
        "Good luck, adventurer.\n\nClick to begin."
    };

    // -------------------------------------------------------------------------
    // Tuning (visible in Inspector if component is inspected at runtime)
    // -------------------------------------------------------------------------
    [Tooltip("Seconds between each newly revealed character.")]
    public float charInterval = 0.04f;

    [Tooltip("Seconds each character takes to drop from its offset position.")]
    public float dropDuration = 0.18f;

    [Tooltip("Pixels above final Y that each character starts at.")]
    public float dropStartOffsetY = 22f;

    // Font & size shared across all char objects
    private const int FontSize = 22;
    private Font _font;

    // -------------------------------------------------------------------------
    // Internal state
    // -------------------------------------------------------------------------
    private Room _startRoom;
    private int _currentPage = 0;
    private bool _animationComplete = false;

    private struct CharEntry
    {
        public RectTransform rect;
        public float elapsed;
        public Vector2 finalPos;
    }
    private readonly List<CharEntry> _chars = new List<CharEntry>();

    // UI references built once
    private RectTransform _panel;
    private RectTransform _textContainer;
    private Text _hintText;

    // Coroutine handle
    private Coroutine _typewriterCo;

    // -------------------------------------------------------------------------
    // Public init (called by RoomManager before Start fires)
    // -------------------------------------------------------------------------
    public void Initialize(Room startRoom)
    {
        _startRoom = startRoom;
    }

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------
    private void Awake()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void Start()
    {
        BuildUI();
        ShowPage(_currentPage);
    }

    private void Update()
    {
        // Animate each character dropping into place (unscaled so pause doesn't break it)
        for (int i = 0; i < _chars.Count; i++)
        {
            CharEntry c = _chars[i];
            if (c.rect == null) continue;

            c.elapsed += Time.unscaledDeltaTime;
            float t    = Mathf.Clamp01(c.elapsed / dropDuration);
            float ease = 1f - (1f - t) * (1f - t); // ease-out quad
            float y    = Mathf.Lerp(c.finalPos.y + dropStartOffsetY, c.finalPos.y, ease);
            c.rect.anchoredPosition = new Vector2(c.finalPos.x, y);
            _chars[i] = c;
        }

        if (Input.GetMouseButtonDown(0))
            HandleClick();
    }

    // -------------------------------------------------------------------------
    // Click
    // -------------------------------------------------------------------------
    private void HandleClick()
    {
        if (!_animationComplete)
        {
            // Skip animation — show everything right now
            if (_typewriterCo != null) { StopCoroutine(_typewriterCo); _typewriterCo = null; }
            RevealAll();
            _animationComplete = true;
        }
        else
        {
            _currentPage++;
            if (_currentPage >= Pages.Length)
                CompleteTutorial();
            else
                ShowPage(_currentPage);
        }
    }

    // -------------------------------------------------------------------------
    // Page management
    // -------------------------------------------------------------------------
    private void ShowPage(int index)
    {
        _animationComplete = false;
        ClearChars();

        if (_hintText != null)
            _hintText.text = (index == Pages.Length - 1) ? "" : "Click to continue...";

        if (_typewriterCo != null) StopCoroutine(_typewriterCo);
        _typewriterCo = StartCoroutine(TypewriterCo(Pages[index]));
    }

    private void ClearChars()
    {
        foreach (var c in _chars)
            if (c.rect != null) Destroy(c.rect.gameObject);
        _chars.Clear();
    }

    // -------------------------------------------------------------------------
    // Character layout — uses TextGenerator for accurate proportional positions
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns a list of (character, anchoredPosition) values obtained by letting
    /// Unity's own TextGenerator lay out the text with the real font metrics.
    /// </summary>
    private List<(char ch, Vector2 pos)> BuildCharPlan(string text)
    {
        // Container rect is not yet properly laid out in the first frame,
        // but we know the panel is 620 wide with 32px padding on each side → 556px usable.
        // Use that as generation extents.
        float extentX = _panel != null ? _panel.sizeDelta.x - 44f : 556f;
        float extentY = _panel != null ? _panel.sizeDelta.y : 260f;

        // Ensure font has the needed glyphs cached
        _font.RequestCharactersInTexture(text, FontSize, FontStyle.Normal);

        TextGenerationSettings tgs = new TextGenerationSettings
        {
            font                   = _font,
            fontSize               = FontSize,
            fontStyle              = FontStyle.Normal,
            color                  = Color.white,
            lineSpacing            = 1f,
            richText               = false,
            pivot                  = new Vector2(0.5f, 0.5f),
            generationExtents      = new Vector2(extentX, extentY),
            textAnchor             = TextAnchor.MiddleCenter,
            alignByGeometry        = false,
            resizeTextForBestFit   = false,
            updateBounds           = false,
            scaleFactor            = 1f,
            horizontalOverflow     = HorizontalWrapMode.Wrap,
            verticalOverflow       = VerticalWrapMode.Overflow,
            generateOutOfBounds    = false,
        };

        TextGenerator gen = new TextGenerator(text.Length + 4);
        gen.Populate(text, tgs);

        IList<UICharInfo> charInfos = gen.characters;
        var plan = new List<(char ch, Vector2 pos)>();

        int srcIndex = 0; // index into the original text string
        int genIndex = 0; // index into charInfos (can differ because \n inserts a glyph)

        while (srcIndex < text.Length && genIndex < charInfos.Count)
        {
            char c = text[srcIndex];
            UICharInfo ci = charInfos[genIndex];

            if (c == '\n')
            {
                // Newlines produce a glyph in the generator but we skip them visually
                srcIndex++;
                genIndex++;
                continue;
            }

            if (c != ' ')
            {
                // cursorPos.x = left edge of glyph, cursorPos.y = TOP of line in generator space
                // Generator uses (0,0) at center (with MiddleCenter pivot).
                // Y is positive upward; cursorPos.y is the top of the line.
                // We want the vertical center of the glyph → subtract half font size.
                float x = ci.cursorPos.x + ci.charWidth * 0.5f;
                float y = ci.cursorPos.y - FontSize * 0.5f;
                plan.Add((c, new Vector2(x, y)));
            }

            srcIndex++;
            genIndex++;
        }

        return plan;
    }

    // -------------------------------------------------------------------------
    // Typewriter coroutine
    // -------------------------------------------------------------------------
    private IEnumerator TypewriterCo(string text)
    {
        List<(char ch, Vector2 pos)> plan = BuildCharPlan(text);

        foreach (var (ch, pos) in plan)
        {
            SpawnChar(ch, pos);
            yield return new WaitForSecondsRealtime(charInterval);
        }

        _animationComplete = true;
        _typewriterCo = null;
    }

    // -------------------------------------------------------------------------
    // Skip — spawn all remaining chars immediately then snap to final position
    // -------------------------------------------------------------------------
    private void RevealAll()
    {
        // We need to spawn every char that wasn't spawned yet.
        // To know which ones are missing, rebuild the full plan and diff against _chars.Count.
        string text = Pages[_currentPage];
        List<(char ch, Vector2 pos)> plan = BuildCharPlan(text);

        int alreadySpawned = _chars.Count;
        for (int i = alreadySpawned; i < plan.Count; i++)
            SpawnChar(plan[i].ch, plan[i].pos);

        // Force all to final position (no animation)
        for (int i = 0; i < _chars.Count; i++)
        {
            CharEntry c = _chars[i];
            if (c.rect == null) continue;
            c.elapsed = dropDuration;
            c.rect.anchoredPosition = c.finalPos;
            _chars[i] = c;
        }
    }

    // -------------------------------------------------------------------------
    // Spawn a single character GameObject
    // -------------------------------------------------------------------------
    private void SpawnChar(char ch, Vector2 finalPos)
    {
        GameObject go = new GameObject($"c_{ch}");
        go.transform.SetParent(_textContainer, false);

        Text t         = go.AddComponent<Text>();
        t.text         = ch.ToString();
        t.font         = _font;
        t.fontSize     = FontSize;
        t.color        = Color.white;
        t.alignment    = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow   = VerticalWrapMode.Overflow;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta         = new Vector2(FontSize, FontSize + 8f);
        rt.anchorMin         = new Vector2(0.5f, 0.5f);
        rt.anchorMax         = new Vector2(0.5f, 0.5f);
        rt.pivot             = new Vector2(0.5f, 0.5f);
        // Start offset upward; Update() will animate to finalPos
        rt.anchoredPosition  = new Vector2(finalPos.x, finalPos.y + dropStartOffsetY);

        _chars.Add(new CharEntry { rect = rt, elapsed = 0f, finalPos = finalPos });
    }

    // -------------------------------------------------------------------------
    // Completion
    // -------------------------------------------------------------------------
    private void CompleteTutorial()
    {
        _startRoom?.UnlockTutorial();
        Destroy(gameObject);
    }

    // -------------------------------------------------------------------------
    // UI construction
    // -------------------------------------------------------------------------
    private void BuildUI()
    {
        // ── Canvas ──────────────────────────────────────────────────────────
        GameObject canvasGO = new GameObject("TutorialCanvas");
        canvasGO.transform.SetParent(transform);
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Panel ────────────────────────────────────────────────────────────
        // Wider, taller, pushed higher above center
        GameObject panelGO  = new GameObject("Panel");
        panelGO.transform.SetParent(canvasGO.transform, false);

        Image panelImg  = panelGO.AddComponent<Image>();
        panelImg.color  = new Color(0.04f, 0.04f, 0.08f, 0.90f);

        _panel = panelGO.GetComponent<RectTransform>();
        _panel.anchorMin        = new Vector2(0.5f, 0.5f);
        _panel.anchorMax        = new Vector2(0.5f, 0.5f);
        _panel.pivot            = new Vector2(0.5f, 0.5f);
        _panel.sizeDelta        = new Vector2(620f, 260f);   // larger panel
        _panel.anchoredPosition = new Vector2(0f, 200f);      // pushed much higher

        // ── Title bar (top strip) ─────────────────────────────────────────
        GameObject titleBarGO = new GameObject("TitleBar");
        titleBarGO.transform.SetParent(panelGO.transform, false);
        Image tbi      = titleBarGO.AddComponent<Image>();
        tbi.color      = new Color(0.10f, 0.10f, 0.20f, 1f);
        RectTransform tbRT = titleBarGO.GetComponent<RectTransform>();
        tbRT.anchorMin  = new Vector2(0f, 1f);
        tbRT.anchorMax  = new Vector2(1f, 1f);
        tbRT.pivot      = new Vector2(0.5f, 1f);
        tbRT.sizeDelta  = new Vector2(0f, 36f);
        tbRT.anchoredPosition = Vector2.zero;

        GameObject titleTxtGO = new GameObject("TitleText");
        titleTxtGO.transform.SetParent(titleBarGO.transform, false);
        Text tt      = titleTxtGO.AddComponent<Text>();
        tt.text      = "HOW TO PLAY";
        tt.font      = _font;
        tt.fontSize  = 15;
        tt.fontStyle = FontStyle.Bold;
        tt.color     = new Color(0.80f, 0.80f, 1f, 1f);
        tt.alignment = TextAnchor.MiddleCenter;
        RectTransform ttRT = titleTxtGO.GetComponent<RectTransform>();
        ttRT.anchorMin = Vector2.zero;
        ttRT.anchorMax = Vector2.one;
        ttRT.offsetMin = Vector2.zero;
        ttRT.offsetMax = Vector2.zero;

        // ── Separator line (below title bar) ─────────────────────────────
        GameObject sepGO = new GameObject("Sep");
        sepGO.transform.SetParent(panelGO.transform, false);
        Image si      = sepGO.AddComponent<Image>();
        si.color      = new Color(0.30f, 0.30f, 0.55f, 0.55f);
        RectTransform sRT = sepGO.GetComponent<RectTransform>();
        sRT.anchorMin  = new Vector2(0.04f, 1f);
        sRT.anchorMax  = new Vector2(0.96f, 1f);
        sRT.pivot      = new Vector2(0.5f, 1f);
        sRT.sizeDelta  = new Vector2(0f, 1f);
        sRT.anchoredPosition = new Vector2(0f, -36f);

        // ── Text container (where character GameObjects are spawned) ───────
        // Pivot = (0.5, 0.5) so that anchoredPosition (0,0) = center of this rect.
        // TextGenerator also outputs positions relative to the center when pivot=(0.5,0.5).
        GameObject containerGO = new GameObject("TextContainer");
        containerGO.transform.SetParent(panelGO.transform, false);
        _textContainer = containerGO.GetComponent<RectTransform>();
        if (_textContainer == null) _textContainer = containerGO.AddComponent<RectTransform>();
        _textContainer.anchorMin = new Vector2(0f, 0f);
        _textContainer.anchorMax = new Vector2(1f, 1f);
        _textContainer.offsetMin = new Vector2(22f, 38f);  // bottom padding (hint)
        _textContainer.offsetMax = new Vector2(-22f, -37f); // top padding (title bar)
        _textContainer.pivot     = new Vector2(0.5f, 0.5f);

        // ── Hint ("Click to continue…") ───────────────────────────────────
        GameObject hintGO = new GameObject("Hint");
        hintGO.transform.SetParent(panelGO.transform, false);
        _hintText           = hintGO.AddComponent<Text>();
        _hintText.font      = _font;
        _hintText.fontSize  = 12;
        _hintText.color     = new Color(0.55f, 0.55f, 0.65f, 1f);
        _hintText.alignment = TextAnchor.MiddleRight;
        _hintText.text      = "Click to continue...";
        RectTransform hRT   = hintGO.GetComponent<RectTransform>();
        hRT.anchorMin       = new Vector2(0f, 0f);
        hRT.anchorMax       = new Vector2(1f, 0f);
        hRT.pivot           = new Vector2(0.5f, 0f);
        hRT.sizeDelta       = new Vector2(0f, 30f);
        hRT.anchoredPosition = new Vector2(-14f, 5f);
    }
}
