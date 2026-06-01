#if UNITY_IOS
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace Dirichlet.Ad.Editor
{
    /// <summary>
    /// iOS build post-processor for Dirichlet Ad SDK.
    /// Handles CocoaPods installation and Xcode project configuration.
    /// </summary>
    public class DirichletAdIOSPostProcessor
    {
        private const string SDKVersion = "4.2.0.2";
        private const string MinIOSVersion = "11.0";

        // 与 Mediation 版本保持一致的 override（仅在解析失败或工程极端定制时使用）
        private const string ENV_FRAMEWORK_TARGET = "DIRICHLET_UNITY_FRAMEWORK_TARGET";

        [PostProcessBuild(100)]
        public static void OnPostprocessBuild(BuildTarget buildTarget, string pathToBuiltProject)
        {
            if (buildTarget != BuildTarget.iOS)
            {
                return;
            }

            Debug.Log("[DirichletAd] Starting iOS post-process...");

            try
            {
                // 1. Generate Podfile dynamically based on resolved framework target
                var frameworkTargetName = ResolveFrameworkTargetName(pathToBuiltProject);
                GeneratePodfile(pathToBuiltProject, frameworkTargetName);

                // 2. Modify Xcode project settings
                ModifyXcodeProject(pathToBuiltProject);

                // 3. Modify Info.plist for required permissions
                ModifyInfoPlist(pathToBuiltProject);

                // 4. Run pod install
                RunPodInstall(pathToBuiltProject);

                Debug.Log("[DirichletAd] iOS post-process completed successfully.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DirichletAd] iOS post-process failed: {ex.Message}");
                Debug.LogError($"[DirichletAd] Stack trace: {ex.StackTrace}");
                throw; // 失败即中断，避免导出工程处于半配置状态
            }
        }

        private static string ResolveFrameworkTargetName(string projectPath)
        {
            var envFrameworkTarget = Environment.GetEnvironmentVariable(ENV_FRAMEWORK_TARGET);
            if (!string.IsNullOrEmpty(envFrameworkTarget))
            {
                Debug.Log($"[DirichletAd] Using framework target from env: {ENV_FRAMEWORK_TARGET}={envFrameworkTarget}");
                return envFrameworkTarget;
            }

            var xcodeProjectName = DetectXcodeProjectName(projectPath);
            var projectFilePath = Path.Combine(projectPath, $"{xcodeProjectName}.xcodeproj/project.pbxproj");

            if (!File.Exists(projectFilePath))
            {
                throw new Exception($"project.pbxproj not found at: {projectFilePath}");
            }

            var pbxProject = new PBXProject();
            pbxProject.ReadFromFile(projectFilePath);

#if UNITY_2019_3_OR_NEWER
            var frameworkGuid = pbxProject.GetUnityFrameworkTargetGuid();
#else
            var frameworkGuid = pbxProject.TargetGuidByName("Unity-iPhone");
#endif

            // 优先尝试常见 target 名称（避免读取/解析大文件的开销）
            var commonTargetNames = new[] { "UnityFramework", "TuanjieFramework", "Unity-iPhone", "Tuanjie-iPhone" };
            foreach (var name in commonTargetNames)
            {
                try
                {
                    var guid = pbxProject.TargetGuidByName(name);
                    if (guid == frameworkGuid)
                    {
                        Debug.Log($"[DirichletAd] Resolved framework target: {name} (GUID: {frameworkGuid})");
                        return name;
                    }
                }
                catch
                {
                    // ignore
                }
            }

            // 兜底：从 pbxproj 中解析 target 名称列表并反查
            var allTargets = GetAllTargetNames(projectFilePath);
            foreach (var name in allTargets)
            {
                try
                {
                    var guid = pbxProject.TargetGuidByName(name);
                    if (guid == frameworkGuid)
                    {
                        Debug.Log($"[DirichletAd] Resolved framework target: {name} (GUID: {frameworkGuid})");
                        return name;
                    }
                }
                catch
                {
                    // ignore
                }
            }

            var targetList = allTargets.Length > 0 ? string.Join(", ", allTargets) : "(none)";
            throw new Exception(
                $"Failed to resolve framework target name from pbxproj.\n" +
                $"  .xcodeproj: {xcodeProjectName}\n" +
                $"  Available targets: {targetList}\n" +
                $"  Framework GUID: {frameworkGuid ?? "(null)"}\n\n" +
                $"解决方案：设置环境变量强制指定 target：\n" +
                $"  export {ENV_FRAMEWORK_TARGET}=YourFrameworkTarget"
            );
        }

        private static string[] GetAllTargetNames(string projectFilePath)
        {
            try
            {
                var content = File.ReadAllText(projectFilePath);
                var names = new System.Collections.Generic.List<string>();

                var targetRegex = new System.Text.RegularExpressions.Regex(
                    @"/\*\s*([^\*]+)\s*\*/\s*=\s*\{\s*isa\s*=\s*PBXNativeTarget"
                );

                var matches = targetRegex.Matches(content);
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        var name = match.Groups[1].Value.Trim();
                        if (!string.IsNullOrEmpty(name) && !names.Contains(name))
                        {
                            names.Add(name);
                        }
                    }
                }

                return names.ToArray();
            }
            catch
            {
                return new string[0];
            }
        }

        private static void GeneratePodfile(string projectPath, string frameworkTargetName)
        {
            var podfileContent = new System.Text.StringBuilder();
            podfileContent.AppendLine("# Generated by Dirichlet Ad Unity Plugin");
            podfileContent.AppendLine("# Post Process 版本：导出时从 pbxproj 解析 framework target（不再做 Ruby 猜测）");
            podfileContent.AppendLine($"# Framework target: {frameworkTargetName}");
            podfileContent.AppendLine();
            podfileContent.AppendLine("source 'https://cdn.cocoapods.org/'");
            podfileContent.AppendLine();
            podfileContent.AppendLine($"platform :ios, '{MinIOSVersion}'");
            podfileContent.AppendLine("use_frameworks!");
            podfileContent.AppendLine();
            podfileContent.AppendLine($"target '{frameworkTargetName}' do");
            podfileContent.AppendLine("  inherit! :search_paths");
            podfileContent.AppendLine($"  pod 'DirichletAdSDK', '{SDKVersion}'");
            podfileContent.AppendLine($"  pod 'DirichletCoreSDK', '{SDKVersion}'");
            podfileContent.AppendLine("end");
            podfileContent.AppendLine();
            podfileContent.AppendLine("post_install do |installer|");
            podfileContent.AppendLine("  installer.pods_project.targets.each do |target|");
            podfileContent.AppendLine("    target.build_configurations.each do |config|");
            podfileContent.AppendLine($"      config.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] = '{MinIOSVersion}'");
            podfileContent.AppendLine("      config.build_settings['ENABLE_BITCODE'] = 'NO'");
            podfileContent.AppendLine("    end");
            podfileContent.AppendLine("  end");
            podfileContent.AppendLine("end");

            var podfilePath = Path.Combine(projectPath, "Podfile");
            File.WriteAllText(podfilePath, podfileContent.ToString());
            Debug.Log($"[DirichletAd] Generated Podfile at {podfilePath}");
            Debug.Log($"[DirichletAd] Target allocation: {frameworkTargetName}: DirichletAdSDK + DirichletCoreSDK");
        }

        /// <summary>
        /// 检测 Xcode 项目名称，兼容 Unity/Tuanjie/自定义项目
        /// 动态搜索目录下的 .xcodeproj 文件
        /// </summary>
        private static string DetectXcodeProjectName(string projectPath)
        {
            var xcodeprojDirs = Directory.GetDirectories(projectPath, "*.xcodeproj");

            if (xcodeprojDirs.Length == 0)
            {
                throw new Exception(
                    $"No .xcodeproj found in: {projectPath}\n" +
                    "请确认 Unity 导出路径正确，且导出已完成。"
                );
            }

            var xcodeprojPath = xcodeprojDirs[0];
            var xcodeprojName = Path.GetFileNameWithoutExtension(xcodeprojPath);

            Debug.Log($"[DirichletAd] Detected Xcode project: {xcodeprojName}");
            return xcodeprojName;
        }

        private static void ModifyXcodeProject(string projectPath)
        {
            var xcodeProjectName = DetectXcodeProjectName(projectPath);
            var projectFilePath = Path.Combine(projectPath, $"{xcodeProjectName}.xcodeproj/project.pbxproj");
            var pbxProject = new PBXProject();
            pbxProject.ReadFromFile(projectFilePath);

#if UNITY_2019_3_OR_NEWER
            var frameworkTargetGuid = pbxProject.GetUnityFrameworkTargetGuid();
            var appTargetGuid = pbxProject.GetUnityMainTargetGuid();
#else
            var frameworkTargetGuid = pbxProject.TargetGuidByName("Unity-iPhone");
            var appTargetGuid = frameworkTargetGuid;
#endif

            Debug.Log("[DirichletAd] Modifying Xcode project:");
            Debug.Log($"  Framework target GUID: {frameworkTargetGuid}");
            Debug.Log($"  App target GUID: {appTargetGuid}");

            // NOTE: System frameworks (AdSupport, AVFoundation, WebKit, etc.)
            // should be declared in podspecs and will be automatically linked by CocoaPods.
            // No need to manually add them here.

            // Build settings (CocoaPods 不会保证覆盖这些设置)
            pbxProject.SetBuildProperty(frameworkTargetGuid, "ENABLE_BITCODE", "NO");
            pbxProject.SetBuildProperty(frameworkTargetGuid, "CLANG_ENABLE_MODULES", "YES");
            pbxProject.SetBuildProperty(appTargetGuid, "ENABLE_BITCODE", "NO");
            pbxProject.SetBuildProperty(appTargetGuid, "CLANG_ENABLE_MODULES", "YES");

            // Swift 标准库嵌入应由宿主 App target 负责
            pbxProject.SetBuildProperty(appTargetGuid, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");

            // 运行时动态库搜索路径（保持与 Mediation PostProcessor 一致）
            pbxProject.SetBuildProperty(appTargetGuid, "LD_RUNPATH_SEARCH_PATHS", "$(inherited) @executable_path/Frameworks");
            pbxProject.SetBuildProperty(frameworkTargetGuid, "LD_RUNPATH_SEARCH_PATHS", "$(inherited) @executable_path/Frameworks @loader_path/Frameworks");

            pbxProject.WriteToFile(projectFilePath);
            Debug.Log("[DirichletAd] Modified Xcode project settings");
        }

        private static void ModifyInfoPlist(string projectPath)
        {
            // Note: The following Info.plist keys should be configured by the developer manually
            // to avoid potential App Store review issues:
            // - NSAppTransportSecurity: Configure based on your app's network requirements
            // - NSUserTrackingUsageDescription: Required for iOS 14+ IDFA access, use your custom description
            //
            // Reference: https://ssp.dirichlet.cn/docs/dirichlet-sdk/sdk-guide-ios/

            Debug.Log("[DirichletAd] Info.plist modification skipped - please configure manually if needed");
        }

        private static void RunPodInstall(string projectPath)
        {
            var podfilePath = Path.Combine(projectPath, "Podfile");
            if (!File.Exists(podfilePath))
            {
                Debug.LogWarning("[DirichletAd] Podfile not found, skipping pod install");
                return;
            }

            var podBinaryPath = FindPodExecutable();
            if (string.IsNullOrEmpty(podBinaryPath))
            {
                Debug.LogWarning("[DirichletAd] 未能自动定位 pod 可执行文件，请设置 POD_BINARY 环境变量或手动在导出的 iOS 工程目录执行 'pod install'。");
                return;
            }

            Debug.Log($"[DirichletAd] Running 'pod install' with '{podBinaryPath}'...");
            Debug.Log("[DirichletAd] Note: This may take a few minutes on first build.");

            try
            {
                // 优先不做 repo update（更快、更稳定）；如遇到“找不到 podspec”再退化为 --repo-update
                var exitCode = RunPodCommand(podBinaryPath, "install", projectPath, out var output, out var error);

                if (exitCode != 0 && ShouldRetryWithRepoUpdate(output, error))
                {
                    Debug.LogWarning("[DirichletAd] pod install failed (missing specs suspected). Retrying with --repo-update...");
                    exitCode = RunPodCommand(podBinaryPath, "install --repo-update", projectPath, out output, out error);
                }

                if (exitCode == 0)
                {
                    Debug.Log("[DirichletAd] pod install completed successfully");
                    if (!string.IsNullOrEmpty(output) && output.Length < 2000)
                    {
                        Debug.Log($"Output:\n{output}");
                    }
                    return;
                }

                Debug.LogError($"[DirichletAd] pod install failed with exit code {exitCode}");
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError($"Error:\n{error}");
                }
                if (!string.IsNullOrEmpty(output))
                {
                    Debug.LogError($"Output:\n{output}");
                }

                if (IsCocoaPodsCdnHttp2Error(output, error))
                {
                    Debug.LogWarning(
                        "[DirichletAd] Detected CocoaPods CDN HTTP/2 error (e.g. 'Error in the HTTP2 framing layer'). " +
                        "This is usually a network/proxy/SSL issue. Try rerun without repo update, switch network, upgrade CocoaPods, " +
                        "or run 'pod install' manually."
                    );
                }

                throw new Exception($"pod install failed with exit code {exitCode}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DirichletAd] Failed to run pod install: {ex.Message}");
                Debug.LogWarning("[DirichletAd] Please run 'pod install' manually in the Xcode project directory:");
                Debug.LogWarning($"  cd {projectPath}");
                Debug.LogWarning("  pod install");
                throw;
            }
        }

        private static int RunPodCommand(string podPath, string arguments, string projectPath, out string output, out string error)
        {
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = podPath,
                Arguments = arguments,
                WorkingDirectory = projectPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            processInfo.EnvironmentVariables["LANG"] = "en_US.UTF-8";
            processInfo.EnvironmentVariables["LC_ALL"] = "en_US.UTF-8";

            using (var process = System.Diagnostics.Process.Start(processInfo))
            {
                output = process.StandardOutput.ReadToEnd();
                error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                return process.ExitCode;
            }
        }

        private static bool ShouldRetryWithRepoUpdate(string output, string error)
        {
            var combined = (output ?? string.Empty) + "\n" + (error ?? string.Empty);
            return combined.Contains("Unable to find a specification for")
                   || combined.Contains("None of your spec sources contain a spec satisfying")
                   || combined.Contains("spec satisfying")
                   || combined.Contains("No podspec found for")
                   || combined.Contains("could not find compatible versions for pod");
        }

        private static bool IsCocoaPodsCdnHttp2Error(string output, string error)
        {
            var combined = (output ?? string.Empty) + "\n" + (error ?? string.Empty);
            return combined.Contains("CDN: trunk Repo update failed")
                   || combined.Contains("Error in the HTTP2 framing layer")
                   || combined.Contains("URL couldn't be downloaded: https://cdn.cocoapods.org/");
        }

        private static string FindPodExecutable()
        {
            // 1. POD_BINARY 环境变量优先，方便 CI 或接入方手动配置
            var envValue = Environment.GetEnvironmentVariable("POD_BINARY");
            if (!string.IsNullOrEmpty(envValue))
            {
                var expanded = envValue.Trim();
                if (File.Exists(expanded))
                {
                    Debug.Log($"[DirichletAd] Found pod via POD_BINARY: {expanded}");
                    return expanded;
                }

                Debug.LogWarning($"[DirichletAd] POD_BINARY points to non-existing path: {expanded}");
            }

            // 2. 常见的 Homebrew / 系统路径以及用户级安装目录
            var possiblePaths = new[]
            {
                "/opt/homebrew/bin/pod",
                "/usr/local/bin/pod",
                "/usr/bin/pod",
                Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? "", ".rbenv/shims/pod"),
                Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? "", ".rvm/wrappers/default/pod")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    Debug.Log($"[DirichletAd] Found pod at: {path}");
                    return path;
                }
            }

            // 3. Try using 'which pod'
            try
            {
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/usr/bin/env",
                    Arguments = "which pod",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var process = System.Diagnostics.Process.Start(processInfo))
                {
                    var output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit();

                    if (process.ExitCode == 0 && !string.IsNullOrEmpty(output) && File.Exists(output))
                    {
                        Debug.Log($"[DirichletAd] Found pod via 'which': {output}");
                        return output;
                    }
                }
            }
            catch
            {
                // Ignore
            }

            return null;
        }
    }
}
#endif

