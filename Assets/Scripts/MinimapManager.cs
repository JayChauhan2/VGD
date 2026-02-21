using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MinimapManager : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private Vector2 roomIconSize = new Vector2(28, 14);
    [SerializeField] private float spacing = 3f;

    // Room state colors
    [SerializeField] private Color unvisitedColor  = new Color(0.40f, 0.40f, 0.40f, 0.85f); // gray
    [SerializeField] private Color currentColor    = new Color(0.20f, 0.55f, 1.00f, 1.00f); // blue
    [SerializeField] private Color visitedColor    = new Color(0.90f, 0.90f, 0.90f, 0.90f); // white

    // Background panel tint
    [SerializeField] private Color bgColor = Color.clear; // no background

    [Header("UI References")]
    [SerializeField] private RectTransform container; // inner panel that holds the icons

    private Room[,] roomGrid;
    private Dictionary<Room, Image> roomIcons = new Dictionary<Room, Image>();
    private Image currentRoomIcon;

    private static MinimapManager instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (instance == null)
        {
            GameObject go = new GameObject("MinimapManager");
            instance = go.AddComponent<MinimapManager>();
            DontDestroyOnLoad(go);
        }
    }

    private void Awake()
    {
        if (instance == null) instance = this;
        else if (instance != this) { Destroy(gameObject); return; }

        if (container == null)
        {
            CreateMinimapUI();
        }
    }

    private void CreateMinimapUI()
    {
        // ── Canvas ─────────────────────────────────────────────────────────────
        GameObject canvasObj = new GameObject("MinimapCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasObj);

        // ── Dark background panel ──────────────────────────────────────────────
        GameObject bgObj = new GameObject("MinimapBackground");
        bgObj.transform.SetParent(canvasObj.transform, false);

        Image bg = bgObj.AddComponent<Image>();
        bg.color = bgColor;

        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(1, 1);
        bgRect.anchorMax = new Vector2(1, 1);
        bgRect.pivot     = new Vector2(1, 1);
        // Positioned at top-right, shifted down to avoid coin/score UI
        bgRect.anchoredPosition = new Vector2(-10, -90);
        bgRect.sizeDelta        = new Vector2(220, 160);

        // ── Icon container (sits inside bg, slightly padded) ───────────────────
        GameObject containerObj = new GameObject("MinimapPanel");
        containerObj.transform.SetParent(bgObj.transform, false);

        // Transparent – we only use it as a layout anchor
        Image containerImg = containerObj.AddComponent<Image>();
        containerImg.color = Color.clear;

        container = containerObj.GetComponent<RectTransform>();
        container.anchorMin = new Vector2(0.5f, 0.5f);
        container.anchorMax = new Vector2(0.5f, 0.5f);
        container.pivot     = new Vector2(0.5f, 0.5f);
        container.anchoredPosition = Vector2.zero;
        container.sizeDelta        = bgRect.sizeDelta - new Vector2(16, 16); // 8 px padding each side
    }

    private void Start()
    {
        StartCoroutine(WaitForGeneration());
    }

    private System.Collections.IEnumerator WaitForGeneration()
    {
        while (RoomManager.Instance == null || !RoomManager.Instance.IsInitialized())
        {
            yield return null;
        }
        GenerateMinimap();
    }

    private void OnEnable()
    {
        Room.OnRoomEntered += OnRoomEntered;
    }

    private void OnDisable()
    {
        Room.OnRoomEntered -= OnRoomEntered;
    }

    private void GenerateMinimap()
    {
        roomGrid = RoomManager.Instance.GetRoomGrid();
        if (roomGrid == null) return;

        int gridSizeX = roomGrid.GetLength(0);
        int gridSizeY = roomGrid.GetLength(1);

        // Clear existing icons
        foreach (Transform child in container)
            Destroy(child.gameObject);
        roomIcons.Clear();

        // Resize background to fit the actual grid
        float totalW = gridSizeX * (roomIconSize.x + spacing) - spacing + 24f;
        float totalH = gridSizeY * (roomIconSize.y + spacing) - spacing + 24f;

        // Walk up to the background object (container's parent)
        RectTransform bgRect = container.parent as RectTransform;
        if (bgRect != null)
        {
            bgRect.sizeDelta = new Vector2(Mathf.Max(totalW, 80f), Mathf.Max(totalH, 40f));
            container.sizeDelta = bgRect.sizeDelta - new Vector2(16, 16);
        }

        // Create one icon per room
        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                Room room = roomGrid[x, y];
                if (room != null)
                    CreateRoomIcon(room, x, y, gridSizeX, gridSizeY);
            }
        }

        HighlightCurrentRoom();
    }

    private void CreateRoomIcon(Room room, int gridX, int gridY, int gridWidth, int gridHeight)
    {
        GameObject iconObj = new GameObject($"RoomIcon_{gridX}_{gridY}");
        iconObj.transform.SetParent(container, false);

        // Solid rectangle – no sprite needed
        Image img = iconObj.AddComponent<Image>();
        img.color = unvisitedColor;

        RectTransform rect = iconObj.GetComponent<RectTransform>();
        rect.sizeDelta = roomIconSize;

        // Center the whole grid inside the container
        float posX = gridX * (roomIconSize.x + spacing);
        float posY = gridY * (roomIconSize.y + spacing);

        posX -= ((gridWidth  - 1) * (roomIconSize.x + spacing)) / 2f;
        posY -= ((gridHeight - 1) * (roomIconSize.y + spacing)) / 2f;

        rect.anchoredPosition = new Vector2(posX, posY);

        roomIcons.Add(room, img);
    }

    private void HighlightCurrentRoom()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && RoomManager.Instance != null)
        {
            Room currentRoom = RoomManager.Instance.GetRoomAt(player.transform.position);
            if (currentRoom != null)
                OnRoomEntered(currentRoom);
        }
    }

    private void OnRoomEntered(Room room)
    {
        if (!roomIcons.ContainsKey(room)) return;

        // Mark the previous room as visited
        if (currentRoomIcon != null && currentRoomIcon != roomIcons[room])
            currentRoomIcon.color = visitedColor;

        currentRoomIcon = roomIcons[room];
        currentRoomIcon.color = currentColor;
    }
}
