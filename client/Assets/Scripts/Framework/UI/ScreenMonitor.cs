
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

    public class ScreenMonitor:IGameService, ITickable
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

        public void Init(GameContext ctx)
        {
            ResetScreen();
        }

        public void Shutdown()
        {
        }

        public void Tick(float dt)
        {
            if (mLastWidth != Screen.width || mLastHeight != Screen.height)
            {
                if (OnScreenResize != null)
                    OnScreenResize(Screen.width, Screen.height);
                ResetScreen();
            }
        }
    }
