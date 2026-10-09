using UnityEngine;

// Keeps the camera centred on the player without showing past the edge of the world.
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Rect bounds;

    Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (target == null) return;
        float halfHeight = cam.orthographicSize, halfWidth = halfHeight * cam.aspect;
        float x = Mathf.Clamp(target.position.x, bounds.xMin + halfWidth, bounds.xMax - halfWidth);
        float y = Mathf.Clamp(target.position.y, bounds.yMin + halfHeight, bounds.yMax - halfHeight);
        transform.position = new Vector3(x, y, -10f);
    }
}
