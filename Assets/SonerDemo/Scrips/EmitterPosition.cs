using UnityEngine;

namespace SonerDemo.Scrips
{
    public class EmitterPosition : MonoBehaviour
    {
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                FindObjectsByType<VoxelWaveController>(FindObjectsSortMode.None)[0].Emit(transform.position);
            }
        }
    }
}