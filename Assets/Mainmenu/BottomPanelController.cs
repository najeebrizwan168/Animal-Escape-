using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class BottomPanelController : MonoBehaviour
{
    [Header("All Navigation Buttons")]
    [SerializeField] private List<NavButtonSlot> buttonSlots = new List<NavButtonSlot>();
    [SerializeField] private int defaultSelectedIndex = 2; // Default to 'Home' (Index 2)

    private NavButtonSlot currentlySelectedButton;

    private void Start()
    {
        // 1. Automatically grab all NavButtonSlots inside this panel if not assigned manually
        if (buttonSlots.Count == 0)
        {
            buttonSlots.AddRange(GetComponentsInChildren<NavButtonSlot>());
        }

        // 2. Initialize the buttons and setup listeners dynamically
        for (int i = 0; i < buttonSlots.Count; i++)
        {
            int index = i; // Local copy for the closure
            Button btnComp = buttonSlots[i].GetComponent<Button>();
            
            if (btnComp != null)
            {
                btnComp.onClick.AddListener(() => OnButtonSlotClicked(index));
            }

            // Set default visual state
            buttonSlots[i].SetUnselected();
        }

        // 3. Trigger default selection state (e.g., Home Page)
        if (buttonSlots.Count > defaultSelectedIndex)
        {
            OnButtonSlotClicked(defaultSelectedIndex);
        }
    }

    private void OnButtonSlotClicked(int index)
    {
        NavButtonSlot clickedButton = buttonSlots[index];

        // If the user clicked the button that's already active, do nothing
        if (clickedButton == currentlySelectedButton) return;

        // 1. Revert changes on the previous button
        if (currentlySelectedButton != null)
        {
            currentlySelectedButton.SetUnselected();
        }

        // 2. Apply active changes to the newly pressed button
        clickedButton.SetSelected();
        currentlySelectedButton = clickedButton;

        // 3. Log selection details precisely as requested
        Debug.Log($"[Nav Panel Update]: Button Selection changed! Active Index: {index} | Button Name: {clickedButton.gameObject.name}");
    }
}