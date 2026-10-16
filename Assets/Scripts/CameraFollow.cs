using UnityEngine;

// Keeps the camera centred on the player without showing past the edge of the world.
// It moves after the player has, so it is never a frame behind.
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Rect bounds;
    // How much higher up the screen than the target the camera should look, so it stays on the player as they are
    // drawn. In the top-down view the player is drawn above where they stand when they are high up.
    public System.Func<float> lift;
    // How hard the view shakes right now, in blocks; 0 for not at all.
    public static float Shake;

    Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (target == null) return;
        float currentLift = lift != null ? lift() : 0f;

        float halfHeight = cam.orthographicSize, halfWidth = halfHeight * cam.aspect;
        float x = Mathf.Clamp(target.position.x, bounds.xMin + halfWidth, bounds.xMax - halfWidth);
        float y = Mathf.Clamp(target.position.y + currentLift, bounds.yMin + halfHeight, bounds.yMax - halfHeight);
        // A shake, as when the ice boss breaks out of the ice.
        Vector2 shake = Shake > 0f ? Random.insideUnitCircle * Shake : Vector2.zero;
        transform.position = new Vector3(x + shake.x, y + shake.y, -10f);
    }
}
