using UnityEngine;

/// <summary>挂在物体上让其绕指定轴匀速自转(UI 模型预览的转台用)。</summary>
public class AutoRotate : MonoBehaviour
{
    public float degPerSecond = 30f;
    public Vector3 axis = Vector3.up;

    private void Update()
    {
        transform.Rotate(axis, degPerSecond * Time.deltaTime, Space.Self);
    }
}
