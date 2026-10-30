#if CMPSETUP_COMPLETE
using AvocadoShark;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public bool dragOnSurfaces = true;
    [SerializeField] private Image image;
    [SerializeField] private TextMeshProUGUI itemName;
    [SerializeField] private TextMeshProUGUI itemQuantity;

    private InventoryUIHandler _inventoryUIHandler;
    private Inventory _inventory;
    private RectTransform _mDraggingIcon;
    private RectTransform _mDraggingPlane;
    private Canvas _canvas;
    private Camera _mainCamera;
    private InventoryItem _inventoryItem;
    private readonly Vector3 _offset = new Vector3(0, 0.83f, 0);

    private void Awake()
    {
        _mainCamera = Camera.main;
    }

    public void SetValue(
        (Canvas canvas, InventoryUIHandler inventoryUIHandler, Inventory inventory, InventoryItem inventoryItem, int
            quantity) value)
    {
        _inventoryUIHandler = value.inventoryUIHandler; 
        _canvas = value.canvas;
        _inventory = value.inventory;
        _inventoryItem = value.inventoryItem;
        image.sprite = value.inventoryItem.icon;
        itemName.text = value.inventoryItem.name;
        itemQuantity.text = value.quantity.ToString();
    }

    /// <summary>True while any inventory item is being dragged (UI rebuilds are deferred).</summary>
    public static bool IsDragInProgress { get; private set; }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_canvas == null)
            return;
        IsDragInProgress = true;
        var draggingIcon = new GameObject("Draggable Object");

        draggingIcon.transform.SetParent(_canvas.transform, false);
        draggingIcon.transform.SetAsLastSibling();

        var img = draggingIcon.AddComponent<Image>();

        img.sprite = image.sprite;
        img.raycastTarget = false;

        _mDraggingIcon = draggingIcon.transform as RectTransform;

        if (dragOnSurfaces)
            _mDraggingPlane = transform as RectTransform;
        else
            _mDraggingPlane = _canvas.transform as RectTransform;

        SetDraggedPosition(eventData);
    }

    public void OnDrag(PointerEventData data)
    {
        if (_mDraggingIcon != null)
            SetDraggedPosition(data);
    }

    private void SetDraggedPosition(PointerEventData data)
    {
        _mDraggingIcon.position = data.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_mDraggingIcon != null)
            Destroy(_mDraggingIcon.gameObject);
        _mDraggingIcon = null;
        IsDragInProgress = false;

        if (!EventSystem.current.IsPointerOverGameObject())
            SpawnItem(eventData.position);

        if (_inventoryUIHandler != null)
            _inventoryUIHandler.NotifyDragEnded();
    }

    private void OnDisable()
    {
        // Returned to the pool mid-drag (async inventory rebuild): clean up the icon.
        if (_mDraggingIcon != null)
        {
            Destroy(_mDraggingIcon.gameObject);
            _mDraggingIcon = null;
            IsDragInProgress = false;
        }
    }
    private void SpawnItem(Vector3 position)
    {
        //OLD RAYCAST SOLUTION
        // var ray = _mainCamera.ScreenPointToRay(position);
        // if (!Physics.Raycast(ray, out var hit))
        //     return;
        //
        // var isPointFarOver = hit.distance > 10f;
        // var spawnPosition = isPointFarOver ? ray.GetPoint(10f) : hit.point;
        
        if (!FusionConnection.Instance.TryGetLocalPlayerComponent(out Transform localPlayerTransform))
        {
            _inventoryUIHandler.ShowItemDropErrorMessage();
            return;
        }

        foreach (var direction in DirectionOffsets)
        {
            var spawnPosition = localPlayerTransform.position + direction * 2f;
            spawnPosition += RandomPointInsideCircle(1f);
            spawnPosition.y += 0.83f;

            if (!CanSpawn(localPlayerTransform.position, spawnPosition))
                continue;
            // Inventory removal happens via the spawner's ConfirmDropRPC once the master
            // client actually spawned the pickup. TrySpawnNetworkPickUp tracks the request
            // as pending so the same item cannot be dropped twice during the round-trip.
            if (!_inventory.TrySpawnNetworkPickUp(_inventoryItem.id, spawnPosition))
                _inventoryUIHandler.ShowItemDropErrorMessage();
            return;
        }

        // show error if all directions fail
        _inventoryUIHandler.ShowItemDropErrorMessage();
        Debug.LogWarning("Failed to find a valid spawn position in any direction.");
    }

    private bool CanSpawn(Vector3 start, Vector3 end)
    {
        if (Physics.CheckSphere(end, 0.5f))
        {
            Debug.LogWarning("Object already exists at the end position.");
            return false;
        }

        if (!Physics.Raycast(start, end - start, out var hit, Vector3.Distance(start, end)))
            return true;
        
        Debug.LogWarning("Object detected between start and end positions: " + hit.collider.gameObject.name);
        return false;
    }
    private static Vector3 RandomPointInsideCircle(float radius)
    {
        var vector2 = Random.insideUnitCircle.normalized * Random.Range(0f, radius);
        return new Vector3(vector2.x, 0, vector2.y);
    }
    private static readonly Vector3[] DirectionOffsets =
    {
        Vector3.forward,
        (Vector3.forward + Vector3.right).normalized,
        Vector3.right,
        (Vector3.back + Vector3.right).normalized,
        Vector3.back,
        (Vector3.back + Vector3.left).normalized,
        Vector3.left,
        (Vector3.forward + Vector3.left).normalized
    };

}
#endif