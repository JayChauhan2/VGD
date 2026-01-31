using UnityEngine;
using System.Collections.Generic;

public class RoomDecorator : MonoBehaviour
{
    public static RoomDecorator Instance;

    [Header("Obstacle Settings")]
    [SerializeField] private bool enableDecoration = false;
    [SerializeField] private Color obstacleColor = Color.white;
    [SerializeField] private Vector2 obstacleSize = new Vector2(1f, 1f);
    
    private Sprite obstacleSprite;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        GenerateObstacleSprite();
    }

    private void GenerateObstacleSprite()
    {
        // Create a simple 32x32 white texture
        int res = 32;
        Texture2D texture = new Texture2D(res, res);
        Color[] pixels = new Color[res * res];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        texture.SetPixels(pixels);
        texture.Apply();

        obstacleSprite = Sprite.Create(texture, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res); // PPU = res implies 1 unit size
    }

    public enum RoomLayout
    {
        Empty,
        Pillars,
        CenterBlock,
        Cross,
        Corners,
        HorizontalBarriers,
        VerticalBarriers,
        Donut,
        MazeSimple,
        Checkerboard,
        Diamond,
        Scatter,
        NarrowPassageH,
        NarrowPassageV,
        XShape
    }

    public void Decorate(Room room)
    {
        if (!enableDecoration) return;

        // Don't decorate the starting room or critical rooms if needed
        // For now, random layout
        RoomLayout layout = (RoomLayout)Random.Range(0, System.Enum.GetValues(typeof(RoomLayout)).Length);
        
        // Ensure some rooms are empty for breathing room
        if (Random.value < 0.2f) layout = RoomLayout.Empty;

        Debug.Log($"Decorating Room {room.name} with layout: {layout}");

        switch (layout)
        {
            case RoomLayout.Empty: break;
            case RoomLayout.Pillars: LayoutPillars(room); break;
            case RoomLayout.CenterBlock: LayoutCenterBlock(room); break;
            case RoomLayout.Cross: LayoutCross(room); break;
            case RoomLayout.Corners: LayoutCorners(room); break;
            case RoomLayout.HorizontalBarriers: LayoutHorizontalBarriers(room); break;
            case RoomLayout.VerticalBarriers: LayoutVerticalBarriers(room); break;
            case RoomLayout.Donut: LayoutDonut(room); break;
            case RoomLayout.MazeSimple: LayoutMazeSimple(room); break;
            case RoomLayout.Checkerboard: LayoutCheckerboard(room); break;
            case RoomLayout.Diamond: LayoutDiamond(room); break;
            case RoomLayout.Scatter: LayoutScatter(room); break;
            case RoomLayout.NarrowPassageH: LayoutNarrowPassageH(room); break;
            case RoomLayout.NarrowPassageV: LayoutNarrowPassageV(room); break;
            case RoomLayout.XShape: LayoutXShape(room); break;
        }
    }

    private void CreateObstacle(Room room, Vector3 localPos)
    {
        // Safety check: Don't block doors
        // Doors are at (0, +/-6) and (+/-10, 0) roughly (Room Size 20x12)
        // Let's define a "safe zone" radius around door centers
        if (IsNearDoor(localPos, room.roomSize)) return;

        GameObject obj = new GameObject("Obstacle");
        obj.transform.SetParent(room.transform);
        obj.transform.localPosition = localPos;
        obj.tag = "Obstacle"; // Ensure this tag exists or use "Untagged"

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = obstacleSprite;
        sr.color = obstacleColor;

        BoxCollider2D col = obj.AddComponent<BoxCollider2D>();
        col.size = obstacleSize;
        
        // Make it static for performance if it doesn't move
        obj.isStatic = true; 
    }

    private bool IsNearDoor(Vector3 localPos, Vector2 roomSize)
    {
        // Door Locations relative to center
        Vector3 topDoor = new Vector3(0, roomSize.y / 2f, 0);
        Vector3 bottomDoor = new Vector3(0, -roomSize.y / 2f, 0);
        Vector3 leftDoor = new Vector3(-roomSize.x / 2f, 0, 0);
        Vector3 rightDoor = new Vector3(roomSize.x / 2f, 0, 0);

        float safeRadius = 2.5f; // Keep obstacles 2.5 units away from door centers

        if (Vector3.Distance(localPos, topDoor) < safeRadius) return true;
        if (Vector3.Distance(localPos, bottomDoor) < safeRadius) return true;
        if (Vector3.Distance(localPos, leftDoor) < safeRadius) return true;
        if (Vector3.Distance(localPos, rightDoor) < safeRadius) return true;
        
        // Also keep center clear sometimes? No, some layouts use center.
        return false;
    }

    // --- Layout Algorithms ---

    private void LayoutPillars(Room room)
    {
        // 6-8 pillars
        float xSpacing = 5f;
        float ySpacing = 3f;
        
        for (float x = -5; x <= 5; x += xSpacing)
        {
            for (float y = -3; y <= 3; y += ySpacing)
            {
                CreateObstacle(room, new Vector3(x, y, 0));
            }
        }
    }

    private void LayoutCenterBlock(Room room)
    {
        // 3x3 block in center
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                CreateObstacle(room, new Vector3(x, y, 0));
            }
        }
    }

    private void LayoutCross(Room room)
    {
        // Horizontal line
        for (int x = -4; x <= 4; x++)
        {
             if(x == 0) continue; // Leave center open? Or fill it. Let's fill it.
             CreateObstacle(room, new Vector3(x, 0, 0));
        }
        // Vertical line
        for (int y = -3; y <= 3; y++)
        {
            CreateObstacle(room, new Vector3(0, y, 0));
        }
    }

    private void LayoutCorners(Room room)
    {
        // L shapes in corners
        float w = 7f; // Width extent
        float h = 4f; // Height extent
        
        // Top Left
        CreateObstacle(room, new Vector3(-w, h, 0));
        CreateObstacle(room, new Vector3(-w+1, h, 0));
        CreateObstacle(room, new Vector3(-w, h-1, 0));

        // Top Right
        CreateObstacle(room, new Vector3(w, h, 0));
        CreateObstacle(room, new Vector3(w-1, h, 0));
        CreateObstacle(room, new Vector3(w, h-1, 0));

        // Bottom Left
        CreateObstacle(room, new Vector3(-w, -h, 0));
        CreateObstacle(room, new Vector3(-w+1, -h, 0));
        CreateObstacle(room, new Vector3(-w, -h+1, 0));

        // Bottom Right
        CreateObstacle(room, new Vector3(w, -h, 0));
        CreateObstacle(room, new Vector3(w-1, -h, 0));
        CreateObstacle(room, new Vector3(w, -h+1, 0));
    }

    private void LayoutHorizontalBarriers(Room room)
    {
        // Two long horizontal lines
        for (int x = -6; x <= 6; x++)
        {
            CreateObstacle(room, new Vector3(x, 2, 0));
            CreateObstacle(room, new Vector3(x, -2, 0));
        }
    }

    private void LayoutVerticalBarriers(Room room)
    {
        // Two vertical lines
        for (int y = -4; y <= 4; y++)
        {
            CreateObstacle(room, new Vector3(-3, y, 0));
            CreateObstacle(room, new Vector3(3, y, 0));
        }
    }

    private void LayoutDonut(Room room)
    {
        // Ring
        for (int x = -4; x <= 4; x++)
        {
            CreateObstacle(room, new Vector3(x, 3, 0));
            CreateObstacle(room, new Vector3(x, -3, 0));
        }
        for (int y = -2; y <= 2; y++)
        {
            CreateObstacle(room, new Vector3(-4, y, 0));
            CreateObstacle(room, new Vector3(4, y, 0));
        }
    }

    private void LayoutMazeSimple(Room room)
    {
        // Random walker or just random walls
        for (int i = 0; i < 20; i++)
        {
            int x = Random.Range(-8, 9);
            int y = Random.Range(-4, 5);
            CreateObstacle(room, new Vector3(x, y, 0));
        }
    }

    private void LayoutCheckerboard(Room room)
    {
        for (int x = -7; x <= 7; x+=2)
        {
            for (int y = -4; y <= 4; y+=2)
            {
               CreateObstacle(room, new Vector3(x, y, 0));
            }
        }
    }
    
    private void LayoutDiamond(Room room)
    {
        // |x| + |y| <= radius
        for (int x = -5; x <= 5; x++)
        {
            for (int y = -5; y <= 5; y++)
            {
                if (Mathf.Abs(x) + Mathf.Abs(y) == 4)
                {
                    CreateObstacle(room, new Vector3(x, y, 0));
                }
            }
        }
    }

    private void LayoutScatter(Room room)
    {
        int count = Random.Range(10, 25);
        for(int i=0; i<count; i++)
        {
            Vector3 pos = new Vector3(Random.Range(-8f, 8f), Random.Range(-4f, 4f), 0);
            CreateObstacle(room, pos);
        }
    }

    private void LayoutNarrowPassageH(Room room)
    {
        // Fill everything except Y=0
        for (int x = -8; x <= 8; x++)
        {
            for (int y = -4; y <= 4; y++)
            {
                if (y != 0 && Mathf.Abs(y) > 1) // Leave a gap of 3 units
                {
                    CreateObstacle(room, new Vector3(x, y, 0));
                }
            }
        }
    }

    private void LayoutNarrowPassageV(Room room)
    {
         // Fill everything except X=0
        for (int x = -8; x <= 8; x++)
        {
            if (x != 0 && Mathf.Abs(x) > 1)
            {
                for (int y = -4; y <= 4; y++)
                {
                    CreateObstacle(room, new Vector3(x, y, 0));
                }
            }
        }
    }
    
    private void LayoutXShape(Room room)
    {
        // y = x and y = -x
        for (int x = -5; x <= 5; x++)
        {
             CreateObstacle(room, new Vector3(x, x * 0.5f, 0)); // flattened X because room is 20x12 (ratio ~5/3)
             CreateObstacle(room, new Vector3(x, -x * 0.5f, 0));
        }
    }
}
