using UnityEngine;

public class CameraFit : MonoBehaviour
{
    public Camera cam;
    public int rows = 8, cols = 8;
    public float cellSize = 1f;
    public float padding = 1f;

    void Start()
    {
        if (!cam) cam = Camera.main;
        float cx = (cols - 1) * cellSize * 0.5f;
        float cy = (rows - 1) * cellSize * 0.5f;
        cam.transform.position = new Vector3(cx, cy, -10f);

        float sizeByHeight = rows * cellSize * 0.5f + padding;
        float sizeByWidth  = (cols * cellSize * 0.5f + padding) / cam.aspect;
        cam.orthographicSize = Mathf.Max(sizeByHeight, sizeByWidth);
    }
}
