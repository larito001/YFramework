using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// 菜单：Tools/Network/Generate Proto
/// 调 client/Proto/gen_net_proto.bat 把 Proto/Net/*.proto 编译到 Assets/Scripts/GamePlay/Network/Messages/，
/// 完成后 AssetDatabase.Refresh() 让 Unity 重新导入生成的 .cs。Windows Editor 限定。
public static class NetProtoGenerator
{
    private const string BatRelativePath = "Proto/gen_net_proto.bat";
    private const string MenuPath = "Tools/Network/Generate Proto";

    [MenuItem(MenuPath, false, 100)]
    public static void Generate()
    {
        if (Application.platform != RuntimePlatform.WindowsEditor)
        {
            Debug.LogError("[NetProto] 仅 Windows Editor 支持（bat 脚本）");
            return;
        }

        var projectRoot = Directory.GetParent(Application.dataPath).FullName;
        var batPath = Path.Combine(projectRoot, BatRelativePath);
        if (!File.Exists(batPath))
        {
            Debug.LogError($"[NetProto] 找不到生成脚本: {batPath}");
            return;
        }

        // UseShellExecute=false 不能直接启动 .bat，必须经 cmd.exe。
        // /c 后整个命令用一对外层引号包住，内部 bat 路径再用一对引号，避免空格路径被截断。
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"\"{batPath}\"\"",
            WorkingDirectory = Path.GetDirectoryName(batPath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        Debug.Log($"[NetProto] 运行 {batPath}");

        string stdout, stderr;
        int exit;
        try
        {
            using var proc = Process.Start(psi);
            if (proc == null)
            {
                Debug.LogError("[NetProto] Process.Start 返回 null");
                return;
            }
            stdout = proc.StandardOutput.ReadToEnd();
            stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            exit = proc.ExitCode;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[NetProto] 启动失败: {e}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(stdout)) Debug.Log("[NetProto] stdout:\n" + stdout.TrimEnd());
        if (!string.IsNullOrWhiteSpace(stderr)) Debug.LogWarning("[NetProto] stderr:\n" + stderr.TrimEnd());

        if (exit != 0)
        {
            Debug.LogError($"[NetProto] 生成失败，exit={exit}");
            return;
        }

        AssetDatabase.Refresh();
        Debug.Log("[NetProto] 完成，AssetDatabase 已刷新");
    }
}
