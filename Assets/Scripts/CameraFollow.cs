using UnityEngine;
using UnityEngine.Tilemaps;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;

    private Camera cameraComponent;
    private Bounds mapBounds;
    private bool hasMapBounds;

    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();
        FindMapBounds();
    }

    public void SetTarget(Transform t)
    {
        target = t;
        Debug.Log($"[Camera] Now following: {t.name}");
    }

    private void LateUpdate()
    {
        if (target == null || cameraComponent == null || !hasMapBounds)
            return;

        float halfHeight = mapBounds.size.x / (2f * cameraComponent.aspect);
        cameraComponent.orthographicSize = halfHeight;

        float roomHeight = halfHeight * 2f;
        float roomCenterY = mapBounds.min.y + halfHeight +
                            Mathf.Floor((target.position.y - mapBounds.min.y) / roomHeight) * roomHeight;
        float minY = mapBounds.min.y + halfHeight;
        float maxY = Mathf.Max(minY, mapBounds.max.y - halfHeight);
        roomCenterY = Mathf.Clamp(roomCenterY, minY, maxY);

        transform.position = new Vector3(mapBounds.center.x, roomCenterY, transform.position.z);
    }

    private void FindMapBounds()
    {
        CompositeCollider2D[] compositeColliders =
            FindObjectsByType<CompositeCollider2D>(FindObjectsSortMode.None);
        for (int i = 0; i < compositeColliders.Length; i++)
            AddMapBounds(compositeColliders[i].bounds);

        if (hasMapBounds)
            return;

        TilemapCollider2D[] colliders =
            FindObjectsByType<TilemapCollider2D>(FindObjectsSortMode.None);
        for (int i = 0; i < colliders.Length; i++)
            AddMapBounds(colliders[i].bounds);

        if (!hasMapBounds)
            Debug.LogError("[Camera] No usable tilemap collider found for camera bounds.", this);
    }

    private void AddMapBounds(Bounds bounds)
    {
        if (bounds.size.x <= 0f || bounds.size.y <= 0f)
            return;

        if (!hasMapBounds)
        {
            mapBounds = bounds;
            hasMapBounds = true;
        }
        else
        {
            mapBounds.Encapsulate(bounds);
        }
    }
}
