using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public float smoothSpeed = 5f;
    public Vector3 offset = new Vector3(0, 2, -10);
    // private Transform target;
    [SerializeField] private Transform target;

    public void SetTarget(Transform t)
    {
        target = t;
        Debug.Log($"[Camera] Now following: {t.name}");
    }

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 desired = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
    }
}