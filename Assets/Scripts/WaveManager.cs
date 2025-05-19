using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages ripple waves by dispatching a ComputeShader to generate a height field,
/// and exposes wave data to GPU via a structured buffer.
/// </summary>
public class WaveManager : MonoBehaviour
{
    [Header("Grid Settings")]
    [Tooltip("Origin of the grid in world space (X,Z)")]
    public Vector2 gridOrigin = Vector2.zero;
    [Tooltip("Size of the grid in world units (X,Z)")]
    public Vector2 gridSize = new Vector2(10f, 10f);
    [Tooltip("Resolution of the height field texture (cells X)")]
    public int resolutionX = 32;
    [Tooltip("Resolution of the height field texture (cells Z)")]
    public int resolutionZ = 32;

    [Header("Compute Shader Settings")]
    public ComputeShader rippleCS;
    [Tooltip("Maximum number of simultaneous waves")]
    public int maxWaves = 16;

    [Header("Default Wave Parameters")]
    public float speed = 4f;           // Cells per second
    public float wavelength = 4f;      // Cells
    public float attenuation = 0.5f;   // Exponential per cell
    public float fadeTime = 1f;        // Seconds fade in/out
    public float amplitude = 1f;       // World units
    public float _HeightScale = 1f;       // World units

    // Internal buffers
    ComputeBuffer waveBuffer;
    RenderTexture heightField;
    int kernelHandle;

    // Active wave parameter list
    List<WaveParam> waves = new List<WaveParam>();

    // Data struct matching the ComputeShader WaveParam
    struct WaveParam
    {
        public Vector2 centerCell;   // In cell space
        public float startTime;
        public float speed;
        public float wavelength;
        public float attenuation;
        public float fadeDuration;
        public float amplitude;
    }

    void Awake()
    {
        // Create height field texture
        heightField = new RenderTexture(resolutionX, resolutionZ, 0, RenderTextureFormat.RFloat)
        {
            enableRandomWrite = true,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        heightField.Create();
        Shader.SetGlobalTexture("_HeightField", heightField);

        // Create wave param buffer
        int stride = sizeof(float) * 8;
        waveBuffer = new ComputeBuffer(maxWaves, stride);

        // Get kernel and bind buffer & texture
        kernelHandle = rippleCS.FindKernel("CSMain");
        rippleCS.SetBuffer(kernelHandle, "_WaveParams", waveBuffer);
        rippleCS.SetTexture(kernelHandle, "Result", heightField);

        // Set grid properties globally
        Shader.SetGlobalVector("_GridOrigin", new Vector4(gridOrigin.x, 0, gridOrigin.y, 0));
        Shader.SetGlobalVector("_GridSize",   new Vector4(gridSize.x,   0, gridSize.y,   0));
    }

    void OnDestroy()
    {
        waveBuffer.Release();
        heightField.Release();
    }

    void Update()
    {
        float time = Time.time;

        // Remove expired waves
        waves.RemoveAll(w => time - w.startTime > (w.fadeDuration + Mathf.Max(resolutionX, resolutionZ) / w.speed + w.fadeDuration));

        // Prepare wave array for GPU
        int count = Mathf.Min(waves.Count, maxWaves);
        WaveParam[] arr = new WaveParam[count];
        waves.CopyTo(0, arr, 0, count);

        // Upload to ComputeShader
        waveBuffer.SetData(arr);
        rippleCS.SetInt("_WaveCount", count);
        rippleCS.SetInts("_Resolution", resolutionX, resolutionZ);
        rippleCS.SetFloat("_Time", time);

        // Dispatch
        int gx = Mathf.CeilToInt(resolutionX / 8f);
        int gz = Mathf.CeilToInt(resolutionZ / 8f);
        rippleCS.Dispatch(kernelHandle, gx, gz, 1);
    }

    /// <summary>
    /// Add a new ripple wave at world position (X,Z).
    /// </summary>
    public void AddWave(Vector3 worldPosition)
    {
        // Convert world to cell space
        float cellX = (worldPosition.x - gridOrigin.x) / gridSize.x * resolutionX;
        float cellZ = (worldPosition.z - gridOrigin.y) / gridSize.y * resolutionZ;

        WaveParam wp = new WaveParam
        {
            centerCell  = new Vector2(cellX, cellZ),
            startTime   = Time.time,
            speed       = speed,
            wavelength  = wavelength,
            attenuation = attenuation,
            fadeDuration= fadeTime,
            amplitude   = amplitude
        };
        waves.Add(wp);
    }

    /// <summary>
    /// Sample the current height at world X,Z by summing all active waves (for CPU physics).
    /// </summary>
    public float SampleHeight(float worldX, float worldZ)
    {
        float h = 0f;
        float time = Time.time;
        // Convert to cell
        float cellX = (worldX - gridOrigin.x) / gridSize.x * resolutionX;
        float cellZ = (worldZ - gridOrigin.y) / gridSize.y * resolutionZ;

        foreach (var w in waves)
        {
            float dist = Mathf.Abs(cellX - w.centerCell.x) + Mathf.Abs(cellZ - w.centerCell.y);
            float t    = (time - w.startTime) * w.speed;
            if (t < 0f) continue;

            float phase = (dist - t) / w.wavelength * Mathf.PI * 2f;
            float raw   = Mathf.Sin(phase);
            float atten = Mathf.Exp(-w.attenuation * dist);
            float fi    = Mathf.Clamp01(t / w.fadeDuration);
            float fo    = Mathf.Clamp01((t - dist) / w.fadeDuration);
            float fade  = Mathf.Min(fi, fo);

            h += raw * atten * fade * w.amplitude;
        }
        // Scale back to world units
        return h * _HeightScale; // optional multiply if needed
    }
}
