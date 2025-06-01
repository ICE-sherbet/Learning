using System;
using UnityEngine;

public class Stage : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
    }
    
    private void OnValidate()
    {
        Debug.Log(this.hideFlags);
        this.hideFlags |= HideFlags.DontSaveInBuild;
        Debug.Log(this.hideFlags);
    }
    
    private void Reset()
    {
        Debug.Log(this.hideFlags);
        this.hideFlags |= HideFlags.DontSaveInBuild;
        Debug.Log(this.hideFlags);
    }
}
