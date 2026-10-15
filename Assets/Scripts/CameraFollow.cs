using UnityEngine;

// Keeps the camera centred on the player without showing past the edge of the world.
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    // How fast the camera moves up or down to a new height, in tiles per second, and the change in height past
    // which it jumps straight there instead (such as when the view switches).
    const float LiftSpeed = 5f;
    const float SnapLift = 2.5f;

    public Transform target;
    public Rect bounds;
    // How much higher up the screen than the target the camera should look. In the top-down view the player is drawn
    // above where they stand when they are on high ground.
    public System.Func<float> lift;
    // How hard the view shakes right now, in blocks; 0 for not at all.
    public static float Shake;

    Camera cam;
    float currentLift;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (target == null) return;
        float wanted = lift != null ? lift() : 0f;
        currentLift = Mathf.Abs(wanted - currentLift) > SnapLift ? wanted : Mathf.MoveTowards(currentLift, wanted, LiftSpeed * Time.deltaTime);

        float halfHeight = cam.orthographicSize, halfWidth = halfHeight * cam.aspect;
        float x = Mathf.Clamp(target.position.x, bounds.xMin + halfWidth, bounds.xMax - halfWidth);
        float y = Mathf.Clamp(target.position.y + currentLift, bounds.yMin + halfHeight, bounds.yMax - halfHeight);
        // A shake, as when the ice boss breaks out of the ice.
        Vector2 shake = Shake > 0f ? Random.insideUnitCircle * Shake : Vector2.zero;
        transform.position = new Vector3(x + shake.x, y + shake.y, -10f);
    }
}
