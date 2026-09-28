using UnityEngine;
using UnityEngine.EventSystems;

public class HoverCursor : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Tooltip("Cursor shown on hover, textures are set in Assets/Resources/CursorLibrary")]
    public CursorType cursor = CursorType.Pointer;

    private bool hovering;
    private bool pressed;

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = true;
        CursorLibrary.Set(cursor);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
        // Keep the hover cursor while dragging (e.g. a scrollbar handle) outside the element
        if (!pressed)
        {
            CursorLibrary.Reset();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
        if (!hovering)
        {
            CursorLibrary.Reset();
        }
    }

    // Clicking a button that hides its own panel never sends OnPointerExit, so reset here
    public void OnDisable()
    {
        if (hovering || pressed)
        {
            CursorLibrary.Reset();
        }
        hovering = false;
        pressed = false;
    }
}
