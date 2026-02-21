using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays a multi-page tutorial overlay at the start of the game.
/// Each page types in letter-by-letter with a drop-from-top animation per character.
/// Click during animation  -> skip to full text
/// Click after animation   -> advance to next page
/// After last page         -> destroy overlay and unlock the start room doors.
/// </summary>
public class TutorialOverlay : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Tutorial content
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
    // Tuning
    // -------------------------------------------------------------------------
    [Tooltip("Seconds between each new character being revealed.")]
    public float charInterval = 0.04f;

    [Tooltip("Duration in seconds for a character to drop from offset to its final position.")]
    public float dropDuration = 0.18f;

    [Tooltip("How many pixels above the final position each character starts.")]
    public float dropStartOffsetY = 22f;

    // -------------------------------------------------------------------------
    // Internal state
    // -------------------------------------------------------------------------
    private Room _startRoom;
    private int _currentPage = 0;
    private bool _animationComplete = false;

    // Per-character visual data
    private struct CharEntry
    {
        public RectTransform rect;
        public float elapsed;      // Time since this char was revealed
        public Vector2 finalPos;
    }
    private List<CharEntry> _chars = new List<CharEntry>();

    // UI references
    private Canvas _canvas;
    private RectTransform _panel;
    private RectTransform _textContainer;
    private Text _hintText;

    // Coroutine handle
    private Coroutine _typewriterCoroutine;

    // -------------------------------------------------------------------------
    // Public init (called by RoomManager)
    // -------------------------------------------------------------------------
    public void Initialize(Room startRoom)
    {
        _startRoom = startRoom;
    }

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------
    private void Start()
    {
        BuildUI();
        ShowPage(_currentPage);
    }

    private void Update()
    {
        // Animate dropping characters each frame
        for (int i = 0; i < _chars.Count; i++)
        {
            CharEntry c = _chars[i];
            if (c.rect == null) continue;

            c.elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(c.elapsed / dropDuration);
            // Smooth ease-out
            float ease = 1f - (1f - t) * (1f - t);
            float startY = c.finalPos.y + dropStartOffsetY;
            float y = Mathf.Lerp(startY, c.finalPos.y, ease);
            c.rect.anchoredPosition = new Vector2(c.finalPos.x, y);
            _chars[i] = c;
        }

        // Detect click
        if (Input.GetMouseButtonDown(0))
        {
            OnClick();
        }
    }

    // -------------------------------------------------------------------------
    // Click handling
    // -------------------------------------------------------------------------
    private void OnClick()
    {
        if (!_animationComplete)
        {
            // Skip animation: reveal all remaining characters immediately
            if (_typewriterCoroutine != null)
            {
                StopCoroutine(_typewriterCoroutine);
                _typewriterCoroutine = null;
            }
            RevealAllChars();
            _animationComplete = true;
        }
        else
        {
            // Advance to next page
            _currentPage++;
            if (_currentPage >= Pages.Length)
            {
                CompleteTutorial();
            }
            else
            {
                ShowPage(_currentPage);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Page display
    // -------------------------------------------------------------------------
    private void ShowPage(int index)
    {
        _animationComplete = false;

        // Clear existing char objects
        foreach (var c in _chars)
        {
            if (c.rect != null) Destroy(c.rect.gameObject);
        }
        _chars.Clear();

        // Update hint
        if (_hintText != null)
        {
            _hintText.text = (index == Pages.Length - 1) ? "" : "Click to continue...";
        }

        // Start typewriter
        if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
        _typewriterCoroutine = StartCoroutine(TypewriterRoutine(Pages[index]));
    }

    // -------------------------------------------------------------------------
    // Typewriter coroutine
    // -------------------------------------------------------------------------
    private IEnumerator TypewriterRoutine(string text)
    {
        // Build planned positions for all characters first so we know layout
        float containerWidth = _textContainer.rect.width;
        float containerHeight = _textContainer.rect.height;
        float fontSize = 22f;
        float lineHeight = fontSize * 1.5f;
        float charWidth = fontSize * 0.6f; // approximate fixed width per char
        float spaceWidth = fontSize * 0.35f;

        // Parse lines
        string[] lines = text.Split('\n');

        // Calculate total block height
        float totalHeight = lines.Length * lineHeight;
        float startY = totalHeight / 2f - lineHeight / 2f;

        // Build flat list of (char, worldX, worldY)
        List<(char ch, float x, float y)> plan = new List<(char, float, float)>();

        for (int li = 0; li < lines.Length; li++)
        {
            string line = lines[li];
            // Measure line width
            float lineWidth = 0f;
            foreach (char ch in line)
            {
                lineWidth += (ch == ' ') ? spaceWidth : charWidth;
            }
            float xCursor = -lineWidth / 2f;
            float yPos = startY - li * lineHeight;

            foreach (char ch in line)
            {
                float w = (ch == ' ') ? spaceWidth : charWidth;
                if (ch != ' ')
                {
                    plan.Add((ch, xCursor + w / 2f, yPos));
                }
                xCursor += w;
            }
        }

        // Reveal characters one by one
        foreach (var (ch, x, y) in plan)
        {
            SpawnChar(ch, x, y);
            yield return new WaitForSecondsRealtime(charInterval);
        }

        _animationComplete = true;
        _typewriterCoroutine = null;
    }

    // -------------------------------------------------------------------------
    // Character spawning helpers
    // -------------------------------------------------------------------------
    private void SpawnChar(char ch, float x, float y)
    {
        GameObject go = new GameObject($"char_{ch}");
        go.transform.SetParent(_textContainer, false);

        Text t = go.AddComponent<Text>();
        t.text = ch.ToString();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 22;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(20f, 30f);

        Vector2 finalPos = new Vector2(x, y);
        rt.anchoredPosition = new Vector2(x, y + dropStartOffsetY); // starts offset

        CharEntry entry = new CharEntry
        {
            rect = rt,
            elapsed = 0f,
            finalPos = finalPos
        };
        _chars.Add(entry);
    }

    private void RevealAllChars()
    {
        // Get the remaining text from the current page that hasn't been revealed yet,
        // and spawn all of it at once, then force all to final position immediately.
        string text = Pages[_currentPage];
        float fontSize = 22f;
        float lineHeight = fontSize * 1.5f;
        float charWidth = fontSize * 0.6f;
        float spaceWidth = fontSize * 0.35f;

        string[] lines = text.Split('\n');
        float totalHeight = lines.Length * lineHeight;
        float startY = totalHeight / 2f - lineHeight / 2f;

        // Count already revealed chars
        int alreadyRevealed = _chars.Count;

        List<(char ch, float x, float y)> plan = new List<(char, float, float)>();
        for (int li = 0; li < lines.Length; li++)
        {
            string line = lines[li];
            float lineWidth = 0f;
            foreach (char ch in line)
                lineWidth += (ch == ' ') ? spaceWidth : charWidth;

            float xCursor = -lineWidth / 2f;
            float yPos = startY - li * lineHeight;

            foreach (char ch in line)
            {
                float w = (ch == ' ') ? spaceWidth : charWidth;
                if (ch != ' ')
                {
                    plan.Add((ch, xCursor + w / 2f, yPos));
                }
                xCursor += w;
            }
        }

        // Spawn all unrevealed characters
        for (int i = alreadyRevealed; i < plan.Count; i++)
        {
            var (ch, x, y) = plan[i];
            SpawnChar(ch, x, y);
        }

        // Force all chars to final position immediately
        for (int i = 0; i < _chars.Count; i++)
        {
            CharEntry c = _chars[i];
            if (c.rect != null)
            {
                c.elapsed = dropDuration; // mark as done
                c.rect.anchoredPosition = c.finalPos;
                _chars[i] = c;
            }
        }
    }

    // -------------------------------------------------------------------------
    // Tutorial completion
    // -------------------------------------------------------------------------
    private void CompleteTutorial()
    {
        if (_startRoom != null)
        {
            _startRoom.UnlockTutorial();
        }
        Destroy(gameObject);
    }

    // -------------------------------------------------------------------------
    // UI Construction
    // -------------------------------------------------------------------------
    private void BuildUI()
    {
        // Canvas
        GameObject canvasGO = new GameObject("TutorialCanvas");
        canvasGO.transform.SetParent(transform);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // ---- Page number / top label ----------------------------------------
        // (omitted to keep it clean)

        // ---- Panel ----------------------------------------------------------
        // Centered X, slightly above center Y
        GameObject panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(canvasGO.transform, false);

        Image panelImg = panelGO.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.82f);

        _panel = panelGO.GetComponent<RectTransform>();
        // Anchored at center
        _panel.anchorMin = new Vector2(0.5f, 0.5f);
        _panel.anchorMax = new Vector2(0.5f, 0.5f);
        _panel.pivot = new Vector2(0.5f, 0.5f);
        _panel.sizeDelta = new Vector2(560f, 220f);
        // Slightly above center
        _panel.anchoredPosition = new Vector2(0f, 50f);

        // Rounded feel via a border image (keep panel sharp is fine too)

        // ---- Title bar -------------------------------------------------------
        GameObject titleGO = new GameObject("TitleBar");
        titleGO.transform.SetParent(panelGO.transform, false);

        Image titleImg = titleGO.AddComponent<Image>();
        titleImg.color = new Color(0.12f, 0.12f, 0.18f, 1f);

        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f, 1f);
        titleRT.anchorMax = new Vector2(1f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.sizeDelta = new Vector2(0f, 34f);
        titleRT.anchoredPosition = Vector2.zero;

        // Title text
        GameObject titleTxtGO = new GameObject("TitleText");
        titleTxtGO.transform.SetParent(titleGO.transform, false);
        Text titleTxt = titleTxtGO.AddComponent<Text>();
        titleTxt.text = "HOW TO PLAY";
        titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleTxt.fontSize = 14;
        titleTxt.fontStyle = FontStyle.Bold;
        titleTxt.color = new Color(0.85f, 0.85f, 1f, 1f);
        titleTxt.alignment = TextAnchor.MiddleCenter;
        RectTransform titleTxtRT = titleTxtGO.GetComponent<RectTransform>();
        titleTxtRT.anchorMin = Vector2.zero;
        titleTxtRT.anchorMax = Vector2.one;
        titleTxtRT.offsetMin = Vector2.zero;
        titleTxtRT.offsetMax = Vector2.zero;

        // ---- Text container (where chars are spawned) ------------------------
        GameObject containerGO = new GameObject("TextContainer");
        containerGO.transform.SetParent(panelGO.transform, false);
        _textContainer = containerGO.GetComponent<RectTransform>();
        if (_textContainer == null) _textContainer = containerGO.AddComponent<RectTransform>();
        _textContainer.anchorMin = new Vector2(0f, 0f);
        _textContainer.anchorMax = new Vector2(1f, 1f);
        _textContainer.offsetMin = new Vector2(16f, 40f);  // leave room at bottom for hint
        _textContainer.offsetMax = new Vector2(-16f, -36f); // leave room at top for title
        // Center pivot so anchoredPosition zero = center
        _textContainer.pivot = new Vector2(0.5f, 0.5f);

        // ---- Hint text -------------------------------------------------------
        GameObject hintGO = new GameObject("HintText");
        hintGO.transform.SetParent(panelGO.transform, false);
        _hintText = hintGO.AddComponent<Text>();
        _hintText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _hintText.fontSize = 12;
        _hintText.color = new Color(0.6f, 0.6f, 0.7f, 1f);
        _hintText.alignment = TextAnchor.MiddleRight;
        _hintText.text = "Click to continue...";

        RectTransform hintRT = hintGO.GetComponent<RectTransform>();
        hintRT.anchorMin = new Vector2(0f, 0f);
        hintRT.anchorMax = new Vector2(1f, 0f);
        hintRT.pivot = new Vector2(0.5f, 0f);
        hintRT.sizeDelta = new Vector2(0f, 28f);
        hintRT.anchoredPosition = new Vector2(-12f, 6f);

        // ---- Decorative separator -------------------------------------------
        GameObject sepGO = new GameObject("Separator");
        sepGO.transform.SetParent(panelGO.transform, false);
        Image sepImg = sepGO.AddComponent<Image>();
        sepImg.color = new Color(0.3f, 0.3f, 0.5f, 0.6f);
        RectTransform sepRT = sepGO.GetComponent<RectTransform>();
        sepRT.anchorMin = new Vector2(0.05f, 0f);
        sepRT.anchorMax = new Vector2(0.95f, 0f);
        sepRT.pivot = new Vector2(0.5f, 0f);
        sepRT.sizeDelta = new Vector2(0f, 1f);
        sepRT.anchoredPosition = new Vector2(0f, 34f);
    }
}
