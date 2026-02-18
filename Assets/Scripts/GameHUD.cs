using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class GameHUD : MonoBehaviour
{
    public static GameHUD Instance { get; private set; }

    [Header("Configuration")]
    public Vector2 coinPosition = new Vector2(-20, -20);
    // Health position will be used for the heart container
    public Vector2 healthPosition = new Vector2(-20, -50);
    public int fontSize = 24;
    public Color textColor = Color.black;

    [Header("Scaling Settings")]
    public Vector2 referenceResolution = new Vector2(1920, 1080);
    [Range(0, 1)] public float matchWidthOrHeight = 0.5f; // 0=Width, 1=Height
    [Range(0.5f, 3.0f)] public float uiScale = 1.0f; // Multiplier for overall UI size

    [Header("Heart Assets")]
    public Sprite fullHeartSprite;
    public Sprite halfHeartSprite;
    public Sprite emptyHeartSprite;
    public Vector2 heartSize = new Vector2(32, 32);

    private Text coinText;
    private Text bombText;
    private int coinCount = 0;
    private PlayerHealth playerHealth;
    
    private List<HeartDisplay> hearts = new List<HeartDisplay>();
    private int lastHealth = -1; // Track for animations

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

    private Text winText; 
    
    private void OnDestroy()
    {
         Room.OnRoomCleared -= CheckWinCondition;
    }
    

    private Room currentRoom;

    private void Start()
    {
         Room.OnRoomCleared += CheckWinCondition;
         Room.OnRoomEntered += OnRoomEntered;

        // Find player health
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
        }
        else
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
        }

        UpdateCoinDisplay();
        
        if (winText != null) winText.gameObject.SetActive(false);
    }
    
    private void OnDisable()
    {
         Room.OnRoomCleared -= CheckWinCondition;
         Room.OnRoomEntered -= OnRoomEntered;
    }
    
    private void OnRoomEntered(Room room)
    {
        currentRoom = room;
    }

    private void CheckWinCondition(Room room)
    {
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
        Time.timeScale = 0; 
    }

    private void Update()
    {
        UpdateHealthDisplay();
        UpdateBombDisplay();
        UpdateMarkers();
        UpdateCanvasScaler();
    }
    
    private void UpdateCanvasScaler()
    {
        if (currentScaler != null)
        {
            // Inverse scaling: Smaller reference = Bigger UI
            // Avoid divide by zero
            float scale = Mathf.Max(uiScale, 0.1f);
            currentScaler.referenceResolution = referenceResolution / scale;
            currentScaler.matchWidthOrHeight = matchWidthOrHeight;
        }
    }

    // --- Marker System ---
    private class Marker
    {
        public GameObject obj;
        public Transform target;
        public Vector3 offset;
        public float timer;
        public float duration;
    }
    
    private List<Marker> activeMarkers = new List<Marker>();
    private GameObject hudCanvasObj;
    private GameObject markerContainer;

    public void ShowEnemyMarker(Transform target, Vector3 offset, float duration)
    {
        Marker existing = activeMarkers.Find(m => m.target == target);
        if (existing != null)
        {
            existing.timer = 0; 
            existing.duration = duration;
            return;
        }

        if (markerContainer == null)
        {
             markerContainer = new GameObject("MarkerContainer");
        }
        
        GameObject markerObj = new GameObject("EnemyMarker_World");
        markerObj.transform.SetParent(markerContainer.transform);
        
        SpriteRenderer sr = markerObj.AddComponent<SpriteRenderer>();
        sr.color = Color.red;
        // Set sorting layer to Object as requested
        sr.sortingLayerName = "Object";
        sr.sortingOrder = 100; // High order to sit on top of enemies
        
        Sprite circle = Resources.Load<Sprite>("Sprites/Circle");
        if (circle == null)
        {
            int size = 64; 
            Texture2D tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Bilinear; 
            
            Color[] colors = new Color[size*size];
            Vector2 center = new Vector2(size/2f, size/2f);
            float radius = (size/2f) - 1; 
            
            for(int y=0; y<size; y++)
            {
                for(int x=0; x<size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x,y), center);
                    float alpha = 1f - Mathf.Clamp01(dist - radius + 0.5f);
                    
                    if (dist <= radius) colors[y*size + x] = new Color(1, 1, 1, alpha); 
                    else colors[y*size + x] = Color.clear;
                }
            }
            tex.SetPixels(colors);
            tex.Apply();
            circle = Sprite.Create(tex, new Rect(0,0,size,size), new Vector2(0.5f, 0.5f));
        }
        sr.sprite = circle;

        // Position initially
        if (target != null)
        {
            markerObj.transform.position = target.position + offset;
        }
        
        Marker m = new Marker();
        m.obj = markerObj;
        m.target = target;
        m.offset = offset;
        m.duration = duration;
        m.timer = 0;
        
        activeMarkers.Add(m);
    }

    private void UpdateMarkers()
    {
        for (int i = activeMarkers.Count - 1; i >= 0; i--)
        {
            Marker m = activeMarkers[i];
            
            if (m.target == null)
            {
                if (m.obj != null) Destroy(m.obj);
                activeMarkers.RemoveAt(i);
                continue;
            }
            
            m.timer += Time.deltaTime;
            if (m.timer >= m.duration)
            {
                if (m.obj != null) Destroy(m.obj);
                activeMarkers.RemoveAt(i);
                continue;
            }
            
            // Sync Position
            if (m.obj != null)
            {
                m.obj.transform.position = m.target.position + m.offset;
            }
        }
    }

    private CanvasScaler currentScaler;

    private void CreateUI()
    {
        GameObject canvasObj = new GameObject("GameHUDCanvas");
        hudCanvasObj = canvasObj; 
        
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 101; 
        
        currentScaler = canvasObj.AddComponent<CanvasScaler>();
        currentScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        currentScaler.referenceResolution = referenceResolution;
        currentScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        currentScaler.matchWidthOrHeight = matchWidthOrHeight;

        canvasObj.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasObj);

        // Coin Text
        GameObject coinObj = new GameObject("CoinText");
        coinObj.transform.SetParent(canvasObj.transform, false);
        coinText = coinObj.AddComponent<Text>();
        coinText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (coinText.font == null) coinText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        coinText.fontSize = fontSize;
        coinText.color = textColor;
        coinText.alignment = TextAnchor.UpperRight;
        coinText.horizontalOverflow = HorizontalWrapMode.Overflow;
        coinText.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform coinRect = coinObj.GetComponent<RectTransform>();
        coinRect.anchorMin = Vector2.one; 
        coinRect.anchorMax = Vector2.one;
        coinRect.pivot = Vector2.one;
        coinRect.anchoredPosition = coinPosition;

        // --- HEART CONTAINER ---
        GameObject healthObj = new GameObject("HealthContainer");
        healthObj.transform.SetParent(canvasObj.transform, false);
        
        HorizontalLayoutGroup layout = healthObj.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 5;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childAlignment = TextAnchor.UpperLeft; // Changed from UpperRight
        // layout.reverseArrangement = true; // Was causing Left-to-Right loss. Default is Left-to-Right layout.

        RectTransform healthRect = healthObj.GetComponent<RectTransform>();
        healthRect.anchorMin = new Vector2(0, 1); // Top Left
        healthRect.anchorMax = new Vector2(0, 1);
        healthRect.pivot = new Vector2(0, 1);
        healthRect.anchoredPosition = new Vector2(20, -20); // Top Left with padding
        float width = (heartSize.x * 3) + (layout.spacing * 2);
        healthRect.sizeDelta = new Vector2(width, heartSize.y);

        // Instantiate 3 hearts
        hearts.Clear();
        for (int i = 0; i < 3; i++)
        {
            GameObject heartGo = new GameObject($"Heart_{i}");
            heartGo.transform.SetParent(healthObj.transform, false);
            
            Image img = heartGo.AddComponent<Image>();
            img.preserveAspect = true;
            
            RectTransform hr = heartGo.GetComponent<RectTransform>();
            hr.sizeDelta = heartSize;

            // Effect child
            GameObject effectGo = new GameObject("Effect");
            effectGo.transform.SetParent(heartGo.transform, false);
            Image effImg = effectGo.AddComponent<Image>();
            effImg.preserveAspect = true;
            effectGo.SetActive(false);
            
            RectTransform effRect = effectGo.GetComponent<RectTransform>();
            effRect.anchorMin = Vector2.zero;
            effRect.anchorMax = Vector2.one;
            effRect.offsetMin = Vector2.zero;
            effRect.offsetMax = Vector2.zero;

            HeartDisplay hd = heartGo.AddComponent<HeartDisplay>();
            hd.heartImage = img;
            hd.effectImage = effImg;
            // Pass references (they might be null initially, user must assign in inspector)
            hd.fullHeart = fullHeartSprite;
            hd.halfHeart = halfHeartSprite;
            hd.emptyHeart = emptyHeartSprite;
            
            hearts.Add(hd);
        }

        // Bomb Text
        GameObject bombObj = new GameObject("BombText");
        bombObj.transform.SetParent(canvasObj.transform, false);
        bombText = bombObj.AddComponent<Text>();
        bombText.font = coinText.font;
        bombText.fontSize = fontSize - 4; 
        bombText.color = textColor;
        bombText.alignment = TextAnchor.UpperRight;
        bombText.horizontalOverflow = HorizontalWrapMode.Overflow;
        bombText.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform bombRect = bombObj.GetComponent<RectTransform>();
        bombRect.anchorMin = Vector2.one; 
        bombRect.anchorMax = Vector2.one;
        bombRect.pivot = Vector2.one;
        bombRect.anchoredPosition = new Vector2(coinPosition.x, coinPosition.y - 65); // Adjusted for hearts
        

        
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
        winRect.anchorMin = new Vector2(0.5f, 0.5f); 
        winRect.anchorMax = new Vector2(0.5f, 0.5f);
        winRect.pivot = new Vector2(0.5f, 0.5f);
        winRect.anchoredPosition = Vector2.zero;
        
        winObj.SetActive(false); 
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
        if (playerHealth == null)
        {
             // Retry finding player
             if (Random.Range(0, 100) < 5) 
             {
                  PlayerHealth ph = FindFirstObjectByType<PlayerHealth>();
                  if (ph != null) playerHealth = ph;
             }
             return;
        }

        int current = Mathf.Clamp(playerHealth.CurrentHealth, 0, playerHealth.maxHealth);
        
        // Sync with local field sprites just in case they were assigned late
        foreach(var h in hearts)
        {
            if (h.fullHeart == null) h.fullHeart = fullHeartSprite;
            if (h.halfHeart == null) h.halfHeart = halfHeartSprite;
            if (h.emptyHeart == null) h.emptyHeart = emptyHeartSprite;
        }

        // Logic to determine state of each heart
        // 1 Heart = 2 health points. Max 6 health = 3 hearts.
        // Heart 0: Health 1-2
        // Heart 1: Health 3-4
        // Heart 2: Health 5-6

        // Detect if logic needs running
        if (current == lastHealth) return;

        // Helper to get status of interaction for heart i
        HeartDisplay.HeartStatus GetStatus(int heartIndex, int health)
        {
            int thresholdLow = heartIndex * 2;
            int thresholdHigh = (heartIndex + 1) * 2;
            
            if (health >= thresholdHigh) return HeartDisplay.HeartStatus.Full;
            if (health <= thresholdLow) return HeartDisplay.HeartStatus.Empty;
            return HeartDisplay.HeartStatus.Half;
        }

        for (int i = 0; i < 3; i++)
        {
            HeartDisplay hd = hearts[i];
            HeartDisplay.HeartStatus newStatus = GetStatus(i, current);
            
            // If we have a previous state, check for animation
            if (lastHealth != -1)
            {
                HeartDisplay.HeartStatus oldStatus = GetStatus(i, lastHealth);
                if (oldStatus != newStatus)
                {
                    if (current < lastHealth)
                    {
                        // Damage taken
                        hd.AnimateLoss(oldStatus, newStatus);
                    }
                    else
                    {
                        // Healed
                        hd.SetHeart(newStatus);
                    }
                }
                else
                {
                     // Ensure visual is correct anyway (e.g. init)
                     hd.SetHeart(newStatus); 
                }
            }
            else
            {
                // First initialization
                hd.SetHeart(newStatus);
            }
        }

        lastHealth = current;
    }
    
    private void UpdateBombDisplay()
    {
        if (bombText != null)
        {
            if (BombManager.Instance != null)
            {
                bombText.text = $"Bombs: {BombManager.Instance.CurrentBombs}";
            }
            else
            {
                bombText.text = "Bombs: --";
            }
        }
    }
}
