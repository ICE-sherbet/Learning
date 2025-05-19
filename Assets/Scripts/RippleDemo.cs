using UnityEngine;

/// <summary>
/// Demo script: Spawn ripples on mouse click against a ground collider,
/// adjust foot IK using SampleHeight, and apply buoyancy force to rigidbodies.
/// </summary>
public class RippleDemo : MonoBehaviour
{
    [Header("References")]
    public WaveManager waveManager;         // Assign your WaveManager instance
    public Camera     mainCamera;          // Scene camera
    public LayerMask  groundLayerMask;     // Layers considered "ground"

    [Header("Foot IK Setup")]
    public Transform leftFoot;
    public Transform rightFoot;
    public float     baseFootY = 0.1f;      // Base Y offset above wave height

    [Header("Buoyancy Setup")]
    public Rigidbody buoyantRigidbody;
    public float     buoyancyStrength = 10f;
    public float     damping            = 1f;

    void Update()
    {
        // 1) Ripple spawn on left mouse click
        if (Input.GetMouseButtonDown(0) && mainCamera != null)
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 100f, groundLayerMask))
            {
                waveManager.AddWave(hit.point);
            }
        }
    }
}
