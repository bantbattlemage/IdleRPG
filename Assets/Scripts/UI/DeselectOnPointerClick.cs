using UnityEngine;
using UnityEngine.EventSystems;

// Attach once (for example to the EventSystem GameObject) to clear UI selection when the user clicks/taps.
// This prevents a button from remaining "selected" after a pointer click so keyboard/Submit won't re-activate it.
public class DeselectOnPointerClick : MonoBehaviour
{
    void Update()
    {
        if (EventSystem.current == null) return;

        // Pointer click or touch begin -> clear current selection
        if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
