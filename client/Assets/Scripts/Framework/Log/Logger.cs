using System;
using System.IO;
using UnityEngine;

namespace YOTO
{


    public class Logger 
    {
        public enum LogLevel
        {
            Level1 = 1, // 全部写入
            Level2 = 2, // 不写 Debug
            Level3 = 3  // 只写 Error
        }

        public static LogLevel CurrentLevel = LogLevel.Level1;

        private static string logDirectory;
        private static string logFilePath;
        
        public static  void OnDestroy()
        {
            Application.logMessageReceived -= OnUnityLog;
        }

        public static void Init()
        {
            Application.logMessageReceived += OnUnityLog;
            logDirectory = Path.Combine(Environment.CurrentDirectory, "Logs");

            if (!Directory.Exists(logDirectory))
                Directory.CreateDirectory(logDirectory);

            logFilePath = Path.Combine(logDirectory, "player.log");
        }

        /// <summary>
        /// 捕获所有 Unity 的 log
        /// </summary>
        private static void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            // 根据等级过滤
            if (CurrentLevel == LogLevel.Level2 && type == LogType.Log)
                return;

            if (CurrentLevel == LogLevel.Level3 && type != LogType.Error)
                return;

            string tag = type.ToString().ToUpper();

            string full = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{tag}] {condition}";
            File.AppendAllText(logFilePath, full + Environment.NewLine);
        }

        // ------------------
        // 手动调用的接口
        // ------------------
        public static void Log(string msg)
        {
            Debug.Log(msg);
        }

        public static void LogWarning(string msg)
        {
            Debug.LogWarning(msg);
        }

        public static void LogError(string msg)
        {
            Debug.LogError(msg);
        }
    }


}
