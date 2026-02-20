using UnityEngine;

public class EcholocationMarkerAnimation : MonoBehaviour
{
    public Sprite[] sprites;
    public float frameRate = 12f;

    private SpriteRenderer spriteRenderer;
    private float timer;
    private int currentFrame;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (sprites == null || sprites.Length == 0 || spriteRenderer == null) return;

        timer += Time.deltaTime;
        float timePerFrame = 1f / frameRate;

        if (timer >= timePerFrame)
        {
            timer -= timePerFrame;
            currentFrame = (currentFrame + 1) % sprites.Length;
            spriteRenderer.sprite = sprites[currentFrame];
        }
    }
}
