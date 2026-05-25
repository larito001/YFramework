using UnityEngine;

public class ScreenMonitor : MonoBehaviour
{
    public delegate void ScreenResizeDelegate(int width, int height);
    public event ScreenResizeDelegate OnScreenResize;

    private int mLastWidth;
    private int mLastHeight;

    private void ResetScreen()
    {
        mLastWidth = Screen.width;
        mLastHeight = Screen.height;
    }

    private void Awake()
    {
        ResetScreen();
    }

    private void Update()
    {
        if (mLastWidth != Screen.width || mLastHeight != Screen.height)
        {
            OnScreenResize?.Invoke(Screen.width, Screen.height);
            ResetScreen();
        }
    }
}
