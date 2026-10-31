using UnityEngine;

/// <summary>
/// Two-layer cloud parallax. Each background layer follows a fraction of the
/// camera's movement (classic parallax) and additionally sways with a very
/// slow sinusoidal drift, so the effect reads even when the camera is still.
/// The cloud textures are single non-repeating images (Wrap = Clamp), so the
/// drift is a gentle sway instead of a scroll — texture edges never show.
/// </summary>
public class ParallaxLayers : MonoBehaviour
{
    [System.Serializable]
    public class Layer
    {
        public Transform target;

        [Range(0f, 1f)]
        [Tooltip("Fraction of the camera movement applied to this layer.")]
        public float depthFactor = 0.2f;

        [Tooltip("Speed of the slow automatic drift (radians/sec).")]
        public float driftSpeed = 0.05f;

        [Tooltip("Amplitude of the slow automatic drift (world units).")]
        public float driftAmount = 1.0f;

        [HideInInspector]
        public Vector3 basePosition;
    }

    public Layer[] layers = new Layer[0];

    private Camera cam;
    private Vector3 camStart;

    private void Start()
    {
        cam = Camera.main;
        if (cam != null)
        {
            camStart = cam.transform.position;
        }

        foreach (var layer in layers)
        {
            if (layer != null && layer.target != null)
            {
                layer.basePosition = layer.target.position;
            }
        }
    }

    private void LateUpdate()
    {
        if (cam == null)
        {
            return;
        }

        Vector3 delta = cam.transform.position - camStart;
        foreach (var layer in layers)
        {
            if (layer == null || layer.target == null)
            {
                continue;
            }

            float swayX = Mathf.Sin(Time.time * layer.driftSpeed) * layer.driftAmount;
            float swayY = Mathf.Cos(Time.time * layer.driftSpeed * 0.7f) * layer.driftAmount * 0.35f;
            layer.target.position = layer.basePosition + new Vector3(
                -delta.x * layer.depthFactor + swayX,
                -delta.y * layer.depthFactor + swayY,
                0f);
        }
    }
}
