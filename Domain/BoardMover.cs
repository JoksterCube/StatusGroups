using JoksterCube.StatusGroups.Settings;
using UnityEngine;
using UnityEngine.EventSystems;

namespace JoksterCube.StatusGroups.Domain;

// Lets the player drag the whole status board while the menu (Esc) is open, like ServerPlayerList's panel.
// Sits on the board's root, so a press on any icon, name or the backdrop behind them moves it all.
// The spot is kept in the Position setting when the button is let go; a right-click puts the board back.
internal sealed class BoardMover : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IPointerClickHandler
{
    // The backdrop shown while the board can be moved; its corners are what is kept on screen.
    internal RectTransform Handle = null!;
    internal bool Dragging { get; private set; }

    private RectTransform _board = null!;
    private Vector2 _grab;
    private Vector2 _start;

    private void Awake() => _board = (RectTransform)transform;

    internal static bool CanMove => Menu.IsVisible();

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !CanMove) return;
        if (!ToParent(eventData.position, out _grab)) return;
        _start = _board.anchoredPosition;
        Dragging = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!Dragging || eventData.button != PointerEventData.InputButton.Left) return;
        if (!ToParent(eventData.position, out Vector2 point)) return;
        _board.anchoredPosition = KeepOnScreen(_start + point - _grab);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) Finish();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right || !CanMove) return;
        _board.anchoredPosition = Vector2.zero;
        PluginConfig.Position.Value = Vector2.zero;
    }

    // Also when the menu closes mid-drag: the board stays where it was let go.
    internal void Finish()
    {
        if (!Dragging) return;
        Dragging = false;
        Vector2 position = _board.anchoredPosition;
        if (PluginConfig.Position.Value != position) PluginConfig.Position.Value = position;
    }

    private bool ToParent(Vector2 screen, out Vector2 point) =>
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_board.parent, screen, Camera(), out point);

    private Camera? Camera()
    {
        Canvas? canvas = GetComponentInParent<Canvas>();
        if (!canvas) return null;
        canvas = canvas!.rootCanvas;
        return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }

    private static readonly Vector3[] Corners = new Vector3[4];

    // Shifts the wanted spot so the backdrop stays fully on screen (or as much of it as fits).
    private Vector2 KeepOnScreen(Vector2 wanted)
    {
        if (!Handle || !Handle.gameObject.activeInHierarchy) return wanted;
        RectTransform parent = (RectTransform)_board.parent;
        if (!ToParent(Vector2.zero, out Vector2 screenMin) || !ToParent(new Vector2(Screen.width, Screen.height), out Vector2 screenMax))
            return wanted;

        Handle.GetWorldCorners(Corners);
        Vector2 min = parent.InverseTransformPoint(Corners[0]);
        Vector2 max = min;
        for (int i = 1; i < 4; i++)
        {
            Vector2 corner = parent.InverseTransformPoint(Corners[i]);
            min = Vector2.Min(min, corner);
            max = Vector2.Max(max, corner);
        }
        // Where the backdrop would be at the wanted spot.
        Vector2 shift = wanted - _board.anchoredPosition;
        min += shift;
        max += shift;

        Vector2 fix = Vector2.zero;
        if (max.x > screenMax.x) fix.x = screenMax.x - max.x;
        if (min.x + fix.x < screenMin.x) fix.x = screenMin.x - min.x;
        if (min.y < screenMin.y) fix.y = screenMin.y - min.y;
        if (max.y + fix.y > screenMax.y) fix.y = screenMax.y - max.y;
        return wanted + fix;
    }
}
