using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MinimapManager : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private Vector2 roomIconSize = new Vector2(32, 16);
    [SerializeField] private float spacing = 2f;
    [SerializeField] private Color defaultRoomColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
    [SerializeField] private Color currentRoomColor = new Color(0f, 1f, 0f, 0.8f);
    [SerializeField] private Color visitedRoomColor = new Color(0.8f, 0.8f, 0.8f, 0.6f);

    [Header("UI References")]
    [SerializeField] private RectTransform container; // The panel that holds the icons

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
        // 1. Create Canvas
        GameObject canvasObj = new GameObject("MinimapCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // On top of everything
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasObj);

        // 2. Create Background Panel (Container)
        GameObject panelObj = new GameObject("MinimapPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        
        Image bg = panelObj.AddComponent<Image>();
        bg.color = Color.clear; // Invisible background

        container = panelObj.GetComponent<RectTransform>();
        
        // Align Top-Right
        container.anchorMin = new Vector2(1, 1);
        container.anchorMax = new Vector2(1, 1);
        container.pivot = new Vector2(1, 1);
        container.anchoredPosition = new Vector2(-20, -100); // Shifted down to avoid coin UI
        container.sizeDelta = new Vector2(300, 200); // Fixed size for now, or dynamic?
    }

    private void Start()
    {
        // Wait for generation to complete
        // Since RoomManager runs in Start, we might need to wait a frame or check IsInitialized
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

        // Clear existing
        foreach (Transform child in container)
        {
            Destroy(child.gameObject);
        }
        roomIcons.Clear();

        // Find center for centering in container
        // Or simply anchor relative to a pivot. 
        // Let's assume container is Top-Right anchor.
        
        // We need to map grid coordinates to UI local position.
        // Let's assume Grid (0,0) is bottom-left. 
        // We want to center the whole map in the container? 
        // Or just map it directly. Let's map directly relative to center of container for now and see.

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                Room room = roomGrid[x, y];
                if (room != null)
                {
                    CreateRoomIcon(room, x, y, gridSizeX, gridSizeY);
                }
            }
        }
        
        HighlightCurrentRoom();
    }

    private void CreateRoomIcon(Room room, int gridX, int gridY, int gridWidth, int gridHeight)
    {
        GameObject iconObj = new GameObject($"RoomIcon_{gridX}_{gridY}");
        iconObj.transform.SetParent(container, false);

        Image img = iconObj.AddComponent<Image>();
        img.color = defaultRoomColor;

        RectTransform rect = iconObj.GetComponent<RectTransform>();
        rect.sizeDelta = roomIconSize;

        // Calculate position based on grid
        // Center the map in the container
        float mapWidth = gridWidth * (roomIconSize.x + spacing);
        float mapHeight = gridHeight * (roomIconSize.y + spacing);

        float posX = (gridX * (roomIconSize.x + spacing));
        float posY = (gridY * (roomIconSize.y + spacing));

        // Offset so that the middle of the grid is at (0,0) of the container
        posX -= ((gridWidth - 1) * (roomIconSize.x + spacing)) / 2f;
        posY -= ((gridHeight - 1) * (roomIconSize.y + spacing)) / 2f;

        rect.anchoredPosition = new Vector2(posX, posY);

        roomIcons.Add(room, img);
    }

    // Attempt to find and highlight the player's current room immediately after generation
    private void HighlightCurrentRoom()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && RoomManager.Instance != null)
        {
            Room currentRoom = RoomManager.Instance.GetRoomAt(player.transform.position);
            if (currentRoom != null)
            {
                OnRoomEntered(currentRoom);
            }
        }
    }

    private void OnRoomEntered(Room room)
    {
        if (roomIcons.ContainsKey(room))
        {
            // Reset previous if needed (optional, effectively "visited" state)
            if (currentRoomIcon != null && currentRoomIcon != roomIcons[room])
            {
                currentRoomIcon.color = visitedRoomColor;
            }

            currentRoomIcon = roomIcons[room];
            currentRoomIcon.color = currentRoomColor;
        }
    }
}
