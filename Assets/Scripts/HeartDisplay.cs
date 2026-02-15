using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class HeartDisplay : MonoBehaviour
{
    public enum HeartStatus { Empty, Half, Full }

    [Header("References")]
    public Image heartImage;
    public Image effectImage; // Used for blinking/fading out lost parts
    
    [Header("Sprites")]
    public Sprite fullHeart;
    public Sprite halfHeart;
    public Sprite emptyHeart;

    private HeartStatus currentStatus;

    public void SetHeart(HeartStatus status)
    {
        currentStatus = status;
        UpdateImage();
    }

    private void UpdateImage()
    {
        if (heartImage == null) return;
        
        // Ensure main image is always Simple
        heartImage.type = Image.Type.Simple;
        heartImage.preserveAspect = true;
        
        switch (currentStatus)
        {
            case HeartStatus.Full:
                heartImage.sprite = fullHeart;
                heartImage.color = Color.white;
                break;
            case HeartStatus.Half:
                heartImage.sprite = halfHeart;
                heartImage.color = Color.white;
                break;
            case HeartStatus.Empty:
                heartImage.sprite = emptyHeart;
                heartImage.color = Color.white; 
                break;
        }
    }

    public void AnimateLoss(HeartStatus from, HeartStatus to)
    {
        // 1. Set the main image to the NEW state immediately (background)
        SetHeart(to);

        // 2. Configure the effect image to overlay the "lost part" and blink
        if (effectImage != null)
        {
            effectImage.gameObject.SetActive(true);
            effectImage.color = Color.white;
            effectImage.rectTransform.localScale = Vector3.one;
            effectImage.preserveAspect = true; 

            // Determine what the "lost part" looks like
            if (from == HeartStatus.Full && to == HeartStatus.Half)
            {
                // Lost the Right Half.
                // We use the Full Heart masked to show only the Right Half.
                effectImage.sprite = fullHeart;
                effectImage.type = Image.Type.Filled;
                effectImage.fillMethod = Image.FillMethod.Horizontal;
                // Unity 2021+ uses different enum names sometimes, but usually OriginHorizontal.Right = 1
                effectImage.fillOrigin = (int)Image.OriginHorizontal.Right; 
                effectImage.fillAmount = 0.5f; // Show right 50%
            }
            else if (from == HeartStatus.Half && to == HeartStatus.Empty)
            {
                // Lost the Left Half.
                // Just blink the Half Heart (Left side).
                effectImage.sprite = halfHeart;
                effectImage.type = Image.Type.Simple;
            }
            else if (from == HeartStatus.Full && to == HeartStatus.Empty)
            {
                // Lost everything. Blink Full Heart.
                effectImage.sprite = fullHeart;
                effectImage.type = Image.Type.Simple;
            }

            // 3. Start the blink/fade coroutine
            StopAllCoroutines();
            StartCoroutine(BlinkAndFadeRoutine());
        }
    }

    private IEnumerator BlinkAndFadeRoutine()
    {
        float duration = 1.0f; 
        float timer = 0f;
        
        // Blink speed
        float blinkInterval = 0.1f;
        
        while (timer < duration)
        {
            timer += Time.deltaTime;
            
            // Toggle visibility
            float remainder = timer % (blinkInterval * 2);
            bool visible = remainder < blinkInterval;
            
            Color c = effectImage.color;
            c.a = visible ? 1f : 0f;
            effectImage.color = c;

            yield return null;
        }

        // Cleanup
        effectImage.gameObject.SetActive(false);
    }
}
