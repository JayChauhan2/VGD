using UnityEngine;
using UnityEngine.UI;

public class GameHUD : MonoBehaviour
{
    public static GameHUD Instance { get; private set; }

    [Header("Configuration")]
    public Vector2 coinPosition = new Vector2(-20, -20);
    public Vector2 healthPosition = new Vector2(-20, -50);
    public int fontSize = 24;
    public Color textColor = Color.black;

    private Text coinText;
    private Text healthText;
    private int coinCount = 0;
    private PlayerHealth playerHealth;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }

        CreateUI();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("GameHUD");
            Instance = go.AddComponent<GameHUD>();
            DontDestroyOnLoad(go);
        }
    }

    private Text winText; // Reference to the Win Text component
    
    private void OnDestroy()
    {
         Room.OnRoomCleared -= CheckWinCondition;
    }
    
    private void Start()
    {
         Room.OnRoomCleared += CheckWinCondition;

        // Find player health
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
        }
        else
        {
            // Fallback for finding by type
            playerHealth = FindFirstObjectByType<PlayerHealth>();
        }

        UpdateCoinDisplay();
        
        // Hide Win Text just in case
        if (winText != null) winText.gameObject.SetActive(false);
    }
    
    private void CheckWinCondition(Room room)
    {
        // Check if ALL rooms in Room.AllRooms are cleared
        bool allCleared = true;
        foreach(var r in Room.AllRooms)
        {
            if (!r.IsCleared) 
            {
                allCleared = false;
                break;
            }
        }
        
        if (allCleared)
        {
            ShowWinScreen();
        }
    }
    
    private void ShowWinScreen()
    {
        if (winText != null) 
        {
            winText.gameObject.SetActive(true);
            winText.text = "Congrats you won!";
        }
        Time.timeScale = 0; // Pause game
    }

    private void Update()
    {
        UpdateHealthDisplay();
    }

    private void CreateUI()
    {
        GameObject canvasObj = new GameObject("GameHUDCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 101; // Above Minimap
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasObj);

        // Coin Text
        GameObject coinObj = new GameObject("CoinText");
        coinObj.transform.SetParent(canvasObj.transform, false);
        coinText = coinObj.AddComponent<Text>();
        coinText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // Default font
        if (coinText.font == null) coinText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        coinText.fontSize = fontSize;
        coinText.color = textColor;
        coinText.alignment = TextAnchor.UpperRight;
        coinText.horizontalOverflow = HorizontalWrapMode.Overflow;
        coinText.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform coinRect = coinObj.GetComponent<RectTransform>();
        coinRect.anchorMin = Vector2.one; // Top Right
        coinRect.anchorMax = Vector2.one;
        coinRect.pivot = Vector2.one;
        coinRect.anchoredPosition = coinPosition;

        // Health Text
        GameObject healthObj = new GameObject("HealthText");
        healthObj.transform.SetParent(canvasObj.transform, false);
        healthText = healthObj.AddComponent<Text>();
        healthText.font = coinText.font;
        healthText.fontSize = fontSize - 4; // Slightly smaller
        healthText.color = textColor;
        healthText.alignment = TextAnchor.UpperRight;
        healthText.horizontalOverflow = HorizontalWrapMode.Overflow;
        healthText.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform healthRect = healthObj.GetComponent<RectTransform>();
        healthRect.anchorMin = Vector2.one; // Top Right
        healthRect.anchorMax = Vector2.one;
        healthRect.pivot = Vector2.one;
        // Positioned under coin text
        healthRect.anchoredPosition = new Vector2(coinPosition.x, coinPosition.y - 30); 
        
        // Win Text
        GameObject winObj = new GameObject("WinText");
        winObj.transform.SetParent(canvasObj.transform, false);
        winText = winObj.AddComponent<Text>();
        winText.font = coinText.font;
        winText.fontSize = 60; 
        winText.color = Color.yellow;
        winText.alignment = TextAnchor.MiddleCenter;
        winText.horizontalOverflow = HorizontalWrapMode.Overflow;
        winText.verticalOverflow = VerticalWrapMode.Overflow;
        
        RectTransform winRect = winObj.GetComponent<RectTransform>();
        winRect.anchorMin = new Vector2(0.5f, 0.5f); // Center
        winRect.anchorMax = new Vector2(0.5f, 0.5f);
        winRect.pivot = new Vector2(0.5f, 0.5f);
        winRect.anchoredPosition = Vector2.zero;
        
        winObj.SetActive(false); // Hidden by default
    }

    public void AddCoin(int amount = 1)
    {
        coinCount += amount;
        UpdateCoinDisplay();
    }

    private void UpdateCoinDisplay()
    {
        if (coinText != null)
        {
            coinText.text = $"Coins: {coinCount}";
        }
    }

    private void UpdateHealthDisplay()
    {
        if (healthText != null)
        {
            if (playerHealth != null)
            {
                float percent = (playerHealth.CurrentHealth / playerHealth.maxHealth) * 100f;
                healthText.text = $"Health: {Mathf.CeilToInt(percent)}%";
            }
            else
            {
                healthText.text = "Health: --%";
                // Try finding it again if we missed it start (e.g. player spawned late)
                if (Random.Range(0, 100) < 5) // Occasional retry
                {
                     PlayerHealth ph = FindFirstObjectByType<PlayerHealth>();
                     if (ph != null) playerHealth = ph;
                }
            }
        }
    }
}
