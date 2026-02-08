using UnityEngine;

public class BombManager : MonoBehaviour
{
    public static BombManager Instance { get; private set; }
    
    [Header("Bomb Settings")]
    public int startingBombs = 5;
    public GameObject bombPrefab; // Will be created procedurally if null
    
    private int currentBombs;
    public int CurrentBombs => currentBombs;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        currentBombs = startingBombs;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("BombManager");
            Instance = go.AddComponent<BombManager>();
            DontDestroyOnLoad(go);
        }
    }

    public bool PlaceBomb(Vector3 position)
    {
        if (currentBombs <= 0)
        {
            Debug.Log("BombManager: No bombs remaining!");
            return false;
        }
        
        // Create bomb
        GameObject bombObj;
        if (bombPrefab != null)
        {
            bombObj = Instantiate(bombPrefab, position, Quaternion.identity);
        }
        else
        {
            // Create procedural bomb
            bombObj = new GameObject("FlashBomb");
            bombObj.transform.position = position;
            bombObj.AddComponent<FlashBomb>();
        }
        
        currentBombs--;
        Debug.Log($"BombManager: Placed bomb. Remaining: {currentBombs}");
        return true;
    }

    public void AddBombs(int amount)
    {
        currentBombs += amount;
        Debug.Log($"BombManager: Added {amount} bombs. Total: {currentBombs}");
    }
}
