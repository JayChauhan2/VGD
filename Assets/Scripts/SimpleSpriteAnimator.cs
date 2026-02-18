using UnityEngine;
using UnityEngine.UI;

public class SimpleSpriteAnimator : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("The sprites to cycle through for the animation.")]
    public Sprite[] animationFrames;
    
    [Tooltip("Frames per second.")]
    public float frameRate = 12f;

    private Image targetImage;
    private float timer;
    private int currentFrame;

    private void Awake()
    {
        targetImage = GetComponent<Image>();
    }

    private void Update()
    {
        if (targetImage == null || animationFrames == null || animationFrames.Length == 0) return;

        timer += Time.deltaTime;
        float timePerFrame = 1f / frameRate;

        if (timer >= timePerFrame)
        {
            timer -= timePerFrame;
            currentFrame = (currentFrame + 1) % animationFrames.Length;
            targetImage.sprite = animationFrames[currentFrame];
        }
    }
    
    public void SetFrameRate(float rate)
    {
        frameRate = rate;
    }
    
    public void SetTargetImage(Image img)
    {
        targetImage = img;
    }
}
