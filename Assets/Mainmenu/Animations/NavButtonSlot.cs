using UnityEngine;
using UnityEngine.UI;

public class NavButtonSlot : MonoBehaviour
{
    [Header("UI References")]
    public Image backgroundImage;
    public Image iconImage;
    public GameObject textLabelObject; // The Text component wrapper

    [Header("Visual Config")]
    public Sprite selectedBackground; // The gold background asset

    // Call this to turn off the background image and hide the text label
    public void SetUnselected()
    {
        // 1. Disable the background image component entirely
        if (backgroundImage != null)
        {
            backgroundImage.sprite = null;
            backgroundImage.enabled = false; 
        }

        // 2. Hide the text label
        if (textLabelObject != null) 
        {
            textLabelObject.SetActive(false);
        }
    }

    // Call this to enable the background image with the gold asset and show the text label
    public void SetSelected()
    {
        // 1. Enable the background image component and assign the gold sprite
        if (backgroundImage != null && selectedBackground != null) 
        {
            backgroundImage.enabled = true;
            backgroundImage.sprite = selectedBackground;
        }

        // 2. Show the text label
        if (textLabelObject != null) 
        {
            textLabelObject.SetActive(true);
        }
    }
}