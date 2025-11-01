using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YOTO
{
    public class YOTOFramework : SingletonMono<YOTOFramework>
    {
        private bool isInit = false;
        
        ScreenMonitor screenMonitor = new ScreenMonitor();
        
        public static  TimeMgr timeMgr = new TimeMgr();
        public static  Logger logger = new Logger();
        public static  EventMgr eventMgr = new EventMgr();
        public static  StoreMgr storeMgr = new StoreMgr();
        public static  ResMgr resMgr = new ResMgr();
        public static  CameraMgr cameraMgr = new CameraMgr();
        public static  UIMgr uIMgr = new UIMgr();
        public static  EntityMgr entityMgr = new EntityMgr();
        private static PluginManager PluginMgr = new PluginManager();
        public static SoundMgr soundMgr = new SoundMgr();
        public static TaskManager taskMgr = new TaskManager();
        public void Init()
        {
            if (!isInit)
            {
                isInit = true;
    
                storeMgr.Init();
                PluginMgr.InitPlugins();
                logger.Init();
                resMgr.Init();
                timeMgr.Init();
                // gameInputMgr.Init();
                cameraMgr.Init();
                entityMgr.Init();
                uIMgr.Init();
                soundMgr.Init();
                taskMgr.Init();
            }

            Debug.Log("YTLOG初始化完成");
        }

      

        private void OnEnable()
        {
        }

        private void Start()
        {
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            entityMgr._FixedUpdate(dt);
        }
        private void OnScreenResize(int width, int height)
        {
        YOTOFramework.timeMgr.DelayCallFram(() =>
        {
            OnResizeScreen();
        },2);
        }

        private void OnResizeScreen()
        {
            uIMgr.ResizeScreen();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            timeMgr.Update(dt);
            entityMgr._Update(dt);
            cameraMgr.Update(dt);
            screenMonitor.Update();
        }

        private void OnDestroy()
        {
            Debug.Log("YTLOG销毁完成");
            logger.OnDisable();
            isInit = true;
            
            logger=null;
            resMgr=null;
            timeMgr=null;
            cameraMgr=null;
            entityMgr=null;
            uIMgr=null;
            System.GC.Collect();
            PluginMgr.Unload();
            PluginMgr = null;
            storeMgr=null;
            taskMgr.Unload();
            taskMgr = null;
            Unload();
        }

        private void OnGUI()
        {
            logger.OnGUI();
        }
    }
}