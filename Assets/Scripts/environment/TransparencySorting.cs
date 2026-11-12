using UnityEngine;

public class TransparencySorting : MonoBehaviour
{
    void Awake()
    {
        Camera cam = Camera.main;

        cam.transparencySortMode = TransparencySortMode.CustomAxis;
        cam.transparencySortAxis = new Vector3(0, 1, 0);
    }
}