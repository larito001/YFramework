using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Pathfinding.Graphs.Util;
using Pathfinding.Util;

namespace Pathfinding
{
    [CustomEditor(typeof(AstarPath))]
    public class AstarPathEditor : Editor
    {
        /// <summary>可用所有图形编辑器的列表（例如 GridGraphEditor）</summary>
        static Dictionary<string, CustomGraphEditorAttribute> graphEditorTypes =
            new Dictionary<string, CustomGraphEditorAttribute>();

        /// <summary>
        /// 保存每个图形的节点计数，避免每帧计算。
        /// 仅用于可视化目的
        /// </summary>
        static Dictionary<NavGraph, (float, int, int)> graphNodeCounts;

        /// <summary>所有图形编辑器的列表。可能比 script.data.graphs.Length 大</summary>
        GraphEditor[] graphEditors;

        System.Type[] graphTypes => AstarData.graphTypes;

        static int lastUndoGroup = -1000;
        private string fileName = "GraphCache";

        /// <summary>用于确保处理撤销时的正确行为</summary>
        static uint ignoredChecksum;

        const string scriptsFolder = "Assets/AstarPathfindingProject";

        #region SectionFlags

        static bool showSettings, showCustomAreaColors, showTagNames;

        FadeArea settingsArea,
            colorSettingsArea,
            editorSettingsArea,
            aboutArea,
            optimizationSettingsArea,
            serializationSettingsArea;

        FadeArea tagsArea, graphsArea, addGraphsArea, alwaysVisibleArea;

        /// <summary>其“名称”字段获得焦点的图形编辑器</summary>
        GraphEditor graphNameFocused;

        #endregion

        /// <summary>正在检查的 AstarPath 实例</summary>
        public AstarPath script { get; private set; }

        public bool isPrefab { get; private set; }

        #region Styles

        static bool stylesLoaded;
        public static GUISkin astarSkin { get; private set; }

        static GUIStyle level0AreaStyle, level0LabelStyle;
        static GUIStyle level1AreaStyle, level1LabelStyle;

        static GUIStyle graphDeleteButtonStyle,
            graphInfoButtonStyle,
            graphGizmoButtonStyle,
            graphEditNameButtonStyle,
            graphDuplicateButtonStyle;

        public static GUIStyle helpBox { get; private set; }
        public static GUIStyle thinHelpBox { get; private set; }

        #endregion

        /// <summary>保存脚本文件中找到的预处理器定义，用于优化。</summary>
        List<OptimizationHandler.DefineDefinition> defines;

        /// <summary>启用编辑器功能。加载图形，读取设置并进行设置</summary>
        public void OnEnable()
        {
            script = target as AstarPath;
            isPrefab = PrefabUtility.IsPartOfPrefabAsset(script);

            // 确保所有引用都已设置，以避免 NullReferenceExceptions
            script.colorSettings.PushToStatic();

            if (!isPrefab) HideToolsWhileActive();

            Undo.undoRedoPerformed += OnUndoRedoPerformed;

            FindGraphTypes();
            GetAstarEditorSettings();
            LoadStyles();

            // 仅在不播放时加载图形，或者在极端情况下，当 data.graphs 为 null 时
            if ((!Application.isPlaying && (script.data.graphs == null || script.data.graphs.Length == 0)) ||
                script.data.graphs == null)
            {
                DeserializeGraphs();
            }

            CreateFadeAreas();
        }

        /// <summary>
        /// 隐藏 AstarPath 对象的位置/旋转/缩放工具。相反，OnSceneGUI 将为每个图形绘制位置工具。
        ///
        /// 我们不能依赖 Inspector 的 OnEnable/OnDisable 事件，因为它们与 Inspector 的生命周期绑定，
        /// 而 Inspector 不一定跟随选中的对象。特别是在有多个 Inspector 窗口或 Inspector 窗口被锁定的情况下。
        /// </summary>
        void HideToolsWhileActive()
        {
            EditorApplication.CallbackFunction toolsCheck = null;
            var activelyHidden = true;
            Tools.hidden = true;

            AssemblyReloadEvents.AssemblyReloadCallback onAssemblyReload = () =>
            {
                if (activelyHidden)
                {
                    Tools.hidden = false;
                    activelyHidden = false;
                }
            };
            // 确保在 Unity 重新加载脚本时工具变为可见。
            // 避免其卡在隐藏状态。
            AssemblyReloadEvents.beforeAssemblyReload += onAssemblyReload;
            toolsCheck = () =>
            {
                // 如果 Inspector 被禁用，这将触发
                if (script == null)
                {
                    EditorApplication.update -= toolsCheck;
                    AssemblyReloadEvents.beforeAssemblyReload -= onAssemblyReload;
                    if (activelyHidden)
                    {
                        Tools.hidden = false;
                        activelyHidden = false;
                    }

                    return;
                }

                if (Selection.activeGameObject == script.gameObject)
                {
                    Tools.hidden = true;
                    activelyHidden = true;
                }
                else if (activelyHidden)
                {
                    Tools.hidden = false;
                    activelyHidden = false;
                }
            };
            EditorApplication.update += toolsCheck;
        }

        void CreateFadeAreas()
        {
            if (settingsArea == null)
            {
                aboutArea = new FadeArea(false, this, level0AreaStyle, level0LabelStyle);
                optimizationSettingsArea = new FadeArea(false, this, level0AreaStyle, level0LabelStyle);
                graphsArea = new FadeArea(script.showGraphs, this, level0AreaStyle, level0LabelStyle);
                serializationSettingsArea = new FadeArea(false, this, level0AreaStyle, level0LabelStyle);
                settingsArea = new FadeArea(showSettings, this, level0AreaStyle, level0LabelStyle);

                addGraphsArea = new FadeArea(false, this, level1AreaStyle, level1LabelStyle);
                colorSettingsArea = new FadeArea(false, this, level1AreaStyle, level1LabelStyle);
                editorSettingsArea = new FadeArea(false, this, level1AreaStyle, level1LabelStyle);
                alwaysVisibleArea = new FadeArea(true, this, level1AreaStyle, level1LabelStyle);
                tagsArea = new FadeArea(showTagNames, this, level1AreaStyle, level1LabelStyle);
            }
        }

        /// <summary>清理编辑器功能</summary>
        public void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            SetAstarEditorSettings();
            script = null;
        }

        /// <summary>从 EditorPrefs 读取设置</summary>
        void GetAstarEditorSettings()
        {
            FadeArea.fancyEffects = EditorPrefs.GetBool("EditorGUILayoutx.fancyEffects", true);
        }

        void SetAstarEditorSettings()
        {
            EditorPrefs.SetBool("EditorGUILayoutx.fancyEffects", FadeArea.fancyEffects);
        }

        void RepaintSceneView()
        {
            if (!Application.isPlaying || EditorApplication.isPaused) SceneView.RepaintAll();
        }

        /// <summary>告诉 Unity 我们希望使用整个 Inspector 宽度</summary>
        public override bool UseDefaultMargins()
        {
            return false;
        }

        public override void OnInspectorGUI()
        {
            if (!LoadStyles())
            {
                EditorGUILayout.HelpBox("在文件夹 " + EditorResourceHelper.editorAssets +
                                        "/ 中的 GUISkin 'AstarEditorSkin.guiskin' 未找到或其某些自定义样式不存在。\n" +
                                        "A* Pathfinding Project 编辑器需要此文件。\n\n" +
                                        "如果您正在尝试将 A* 添加到新项目，请不要在 Unity 外部复制文件，" +
                                        "将它们导出为 UnityPackage 并导入到此项目，或从 Asset Store 下载包，" +
                                        "或从 A* Pathfinding Project 网站下载“仅脚本”包。\n\n\n" +
                                        "皮肤加载在 AstarPathEditor.cs --> LoadStyles 方法中完成", MessageType.Error);
                return;
            }

#if ASTAR_ATAVISM
		EditorGUILayout.HelpBox("这是 A* Pathfinding Project 为 Atavism 提供的特殊版本。此版本仅支持扫描 recast 图形并导出它们，但不支持运行时的路径查找。", MessageType.Info);
#endif

            EditorGUI.BeginChangeCheck();

            Undo.RecordObject(script, "A* inspector");

            CheckGraphEditors();

            EditorGUI.indentLevel = 1;

            // 显然这些有时会被 Unity 组件“吃掉”
            // 所以我在这里捕获它们以供以后使用
            EventType storedEventType = Event.current.type;
            string storedEventCommand = Event.current.commandName;

            DrawMainArea();

            GUILayout.Space(5);

            if (isPrefab)
            {
                EditorGUI.BeginDisabledGroup(true);
                GUILayout.Button(new GUIContent("Scan", "无法在预制件上重新计算图形"));
                EditorGUI.EndDisabledGroup();
            }
            else if (GUILayout.Button(new GUIContent("scan", "重新计算所有图形。快捷键 cmd+alt+s ( Windows 上是 ctrl+alt+s )")))
            {
                RunTask(MenuScan);
            }


            // 处理撤销
            SaveGraphsAndUndo(storedEventType, storedEventCommand);


            if (EditorGUI.EndChangeCheck())
            {
                RepaintSceneView();
                EditorUtility.SetDirty(script);
            }
        }

        /// <summary>
        /// 加载 GUISkin 并设置样式。
        /// 参见: EditorResourceHelper.LocateEditorAssets
        /// 返回: 如果找到所有样式则为 True，如果某处出错则为 false
        /// </summary>
        public static bool LoadStyles()
        {
            if (stylesLoaded) return true;

            // 加载失败时的虚拟样式
            var inspectorSkin = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector);

            if (!EditorResourceHelper.LocateEditorAssets())
            {
                return false;
            }

            var skinPath = EditorResourceHelper.editorAssets + "/AstarEditorSkin" +
                           (EditorGUIUtility.isProSkin ? "Dark" : "Light") + ".guiskin";
            astarSkin = AssetDatabase.LoadAssetAtPath(skinPath, typeof(GUISkin)) as GUISkin;

            if (astarSkin != null)
            {
                astarSkin.button = inspectorSkin.button;
            }
            else
            {
                Debug.LogWarning("无法在 '" + skinPath + "' 加载编辑器皮肤");
                return false;
            }

            level0AreaStyle = astarSkin.FindStyle("PixelBox");

            // 如果第一个样式为 null，那么其余的也可能已损坏
            // 可能是由于用户没有复制 meta 文件
            if (level0AreaStyle == null)
            {
                return false;
            }

            level1LabelStyle = astarSkin.FindStyle("BoxHeader");
            level0LabelStyle = astarSkin.FindStyle("TopBoxHeader");

            level1AreaStyle = astarSkin.FindStyle("PixelBox3");
            graphDeleteButtonStyle = astarSkin.FindStyle("PixelButton");
            graphInfoButtonStyle = astarSkin.FindStyle("InfoButton");
            graphGizmoButtonStyle = astarSkin.FindStyle("GizmoButton");
            graphEditNameButtonStyle = astarSkin.FindStyle("EditButton");
            graphDuplicateButtonStyle = astarSkin.FindStyle("DuplicateButton");

            helpBox = inspectorSkin.FindStyle("HelpBox") ?? inspectorSkin.box;
            thinHelpBox = astarSkin.FindStyle("Banner");

            stylesLoaded = true;
            return true;
        }

        /// <summary>在 Inspector 中绘制主区域</summary>
        void DrawMainArea()
        {
            CheckGraphEditors();

            graphsArea.Begin();
            graphsArea.Header("图形", ref script.showGraphs);

            if (graphsArea.BeginFade())
            {
                bool anyNonNull = false;
                for (int i = 0; i < script.graphs.Length; i++)
                {
                    if (script.graphs[i] != null && script.graphs[i].showInInspector)
                    {
                        anyNonNull = true;
                        DrawGraph(graphEditors[i]);
                    }
                }

                // Draw the Add Graph button
                addGraphsArea.Begin();
                addGraphsArea.open |= !anyNonNull;
                addGraphsArea.Header("添加新图形");

                if (addGraphsArea.BeginFade())
                {
                    script.data.FindGraphTypes();
                    for (int i = 0; i < graphTypes.Length; i++)
                    {
                        if (graphEditorTypes.ContainsKey(graphTypes[i].Name))
                        {
                            if (GUILayout.Button(graphEditorTypes[graphTypes[i].Name].displayName))
                            {
                                addGraphsArea.open = false;
                                AddGraph(graphTypes[i]);
                            }
                        }
                        else if (!graphTypes[i].Name.Contains("Base") && graphTypes[i] != typeof(LinkGraph))
                        {
                            EditorGUI.BeginDisabledGroup(true);
                            GUILayout.Label(graphTypes[i].Name + " (未找到编辑器)", "Button");
                            EditorGUI.EndDisabledGroup();
                        }
                    }
                }

                addGraphsArea.End();
            }

            graphsArea.End();

            DrawSettings();
            DrawSerializationSettings();
            DrawOptimizationSettings();
            DrawAboutArea();

            bool showNavGraphs = EditorGUILayout.Toggle("显示图形", script.showNavGraphs);
            if (script.showNavGraphs != showNavGraphs)
            {
                script.showNavGraphs = showNavGraphs;
                RepaintSceneView();
            }
        }

        /// <summary>绘制优化设置。</summary>
        void DrawOptimizationSettings()
        {
            optimizationSettingsArea.Begin();
            optimizationSettingsArea.Header("优化");

            if (optimizationSettingsArea.BeginFade())
            {
                defines = defines ?? OptimizationHandler.FindDefines();

                EditorGUILayout.HelpBox("使用 C# 预处理器指令，可以通过禁用项目中不使用的功能来提高性能和减少内存使用。\n" +
                                        "对这些设置的任何更改都需要重新编译脚本", MessageType.Info);
                foreach (var define in defines)
                {
                    EditorGUILayout.Separator();

                    var label = new GUIContent(ObjectNames.NicifyVariableName(define.name), define.description);
                    define.enabled = EditorGUILayout.Toggle(label, define.enabled);
                    EditorGUILayout.HelpBox(define.description, MessageType.None);

                    if (!define.consistent)
                    {
                        GUIUtilityx.PushTint(Color.red);
                        EditorGUILayout.HelpBox("此定义在所有构建目标中不一致，有些已启用，有些已禁用。按“应用”将它们更改为相同的值", MessageType.Error);
                        GUIUtilityx.PopTint();
                    }
                }

                EditorGUILayout.Separator();
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("应用", GUILayout.Width(150)))
                {
                    RunTask(() =>
                    {
                        if (EditorUtility.DisplayDialog("应用优化", "应用优化需要（如果有任何更改）重新编译脚本。Inspector 也必须重新加载。是否继续？", "确定",
                                "取消"))
                        {
                            OptimizationHandler.ApplyDefines(defines);
                            AssetDatabase.Refresh();
                            defines = null;
                        }
                    });
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            optimizationSettingsArea.End();
        }

        /// <summary>
        /// 返回所有字段完全定义的版本。
        /// 这是因为默认情况下 new Version(3,0,0) > new Version(3,0)。
        /// 这不是期望的行为，因此我们确保此处所有字段都已定义
        /// </summary>
        public static System.Version FullyDefinedVersion(System.Version v)
        {
            return new System.Version(Mathf.Max(v.Major, 0), Mathf.Max(v.Minor, 0), Mathf.Max(v.Build, 0),
                Mathf.Max(v.Revision, 0));
        }

        void DrawAboutArea()
        {
            aboutArea.Begin();

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("关于", level0LabelStyle))
            {
                aboutArea.open = !aboutArea.open;
                GUI.changed = true;
            }

#if !ASTAR_ATAVISM
            System.Version newVersion = AstarUpdateChecker.latestVersion;
            bool beta = false;

            // 检查最新的发布版本或最新的测试版本是否比此版本新
            if (FullyDefinedVersion(AstarUpdateChecker.latestVersion) > FullyDefinedVersion(AstarPath.Version) ||
                FullyDefinedVersion(AstarUpdateChecker.latestBetaVersion) > FullyDefinedVersion(AstarPath.Version))
            {
                if (FullyDefinedVersion(AstarUpdateChecker.latestVersion) <= FullyDefinedVersion(AstarPath.Version))
                {
                    newVersion = AstarUpdateChecker.latestBetaVersion;
                    beta = true;
                }
            }

            // 检查最新版本是否比此版本新
            if (FullyDefinedVersion(newVersion) > FullyDefinedVersion(AstarPath.Version)
               )
            {
                GUIUtilityx.PushTint(Color.green);
                if (GUILayout.Button((beta ? "Beta" : "New") + " 版本可用! " + newVersion, thinHelpBox))
                {
                    Application.OpenURL(AstarUpdateChecker.GetURL("download"));
                }

                GUIUtilityx.PopTint();
                GUILayout.Space(20);
            }
#endif

            GUILayout.EndHorizontal();

            if (aboutArea.BeginFade())
            {
                GUILayout.Label("A* Pathfinding Project 由 Aron Granberg 制作\n您当前的版本是 " + AstarPath.Version);

#if !ASTAR_ATAVISM
                if (FullyDefinedVersion(newVersion) > FullyDefinedVersion(AstarPath.Version))
                {
                    EditorGUILayout.HelpBox("A new " + (beta ? "beta " : "") +
                                            "version of the A* Pathfinding Project is available, the new version is " +
                                            newVersion, MessageType.Info);

                    if (GUILayout.Button("What's new?"))
                    {
                        Application.OpenURL(AstarUpdateChecker.GetURL(beta ? "beta_changelog" : "changelog"));
                    }

                    if (GUILayout.Button("Click here to find out more"))
                    {
                        Application.OpenURL(AstarUpdateChecker.GetURL("findoutmore"));
                    }

                    GUIUtilityx.PushTint(new Color(0.3F, 0.9F, 0.3F));

                    if (GUILayout.Button("Download new version"))
                    {
                        Application.OpenURL(AstarUpdateChecker.GetURL("download"));
                    }

                    GUIUtilityx.PopTint();
                }
#endif

                if (GUILayout.Button(new GUIContent("文档", "打开 A* Pathfinding Project 的文档")))
                {
                    Application.OpenURL(AstarUpdateChecker.GetURL("documentation"));
                }

                if (GUILayout.Button(new GUIContent("项目主页", "打开 A* Pathfinding Project 的主页")))
                {
                    Application.OpenURL(AstarUpdateChecker.GetURL("homepage"));
                }
            }

            aboutArea.End();
        }

        void DrawGraphHeader(GraphEditor graphEditor)
        {
            var graph = graphEditor.target;

            // 图形 guid，仅用于获取唯一值
            string graphGUIDString = graph.guid.ToString();

            GUILayout.BeginHorizontal();

            if (graphNameFocused == graphEditor)
            {
                GUI.SetNextControlName(graphGUIDString);
                graph.name = GUILayout.TextField(graph.name ?? "", level1LabelStyle, GUILayout.ExpandWidth(true),
                    GUILayout.ExpandHeight(false));

                // 当名称字段失去焦点或用户按下 Return 或 Escape 时，将其标记为取消选中
                if ((Event.current.type == EventType.Repaint && GUI.GetNameOfFocusedControl() != graphGUIDString) ||
                    (Event.current.type == EventType.KeyUp && (Event.current.keyCode == KeyCode.Return ||
                                                               Event.current.keyCode == KeyCode.Escape)))
                {
                    if (Event.current.type == EventType.KeyUp) Event.current.Use();
                    graphNameFocused = null;
                }
            }
            else
            {
                // 如果图形名称文本字段未聚焦且图形名称为空，则填充它
                if (graph.name == null || graph.name == "")
                    graph.name = graphEditorTypes[graph.GetType().Name].displayName;

                if (GUILayout.Button(graph.name, level1LabelStyle))
                {
                    graphEditor.fadeArea.open = graph.open = !graph.open;
                    if (!graph.open)
                    {
                        graph.infoScreenOpen = false;
                    }

                    RepaintSceneView();
                }
            }

            // OnInspectorGUI 方法通过检查 EndChangeCheck 确保在切换 Gizmos 时重绘场景视图
            graph.drawGizmos = GUILayout.Toggle(graph.drawGizmos, new GUIContent("绘制 Gizmos", "绘制 Gizmos"),
                graphGizmoButtonStyle);

            if (GUILayout.Button(new GUIContent("", "编辑名称"), graphEditNameButtonStyle))
            {
                graphNameFocused = graphEditor;
                GUI.FocusControl(graphGUIDString);
            }

            if (GUILayout.Toggle(graph.infoScreenOpen, new GUIContent("信息", "信息"), graphInfoButtonStyle))
            {
                if (!graph.infoScreenOpen)
                {
                    graphEditor.infoFadeArea.open = graph.infoScreenOpen = true;
                    graphEditor.fadeArea.open = graph.open = true;
                }
            }
            else
            {
                graphEditor.infoFadeArea.open = graph.infoScreenOpen = false;
            }

            if (GUILayout.Button(new GUIContent("复制", "复制"), graphDuplicateButtonStyle))
            {
                DuplidateGraph(graph);
            }

            if (GUILayout.Button(new GUIContent("删除", "删除"), graphDeleteButtonStyle))
            {
                RemoveGraph(graph);
            }

            GUILayout.EndHorizontal();
        }

        void DrawGraphInfoArea(GraphEditor graphEditor)
        {
            graphEditor.infoFadeArea.Begin();

            if (graphEditor.infoFadeArea.BeginFade())
            {
                int total = 0;
                int numWalkable = 0;

                // 计算图形中的节点数
                (float, int, int) pair;
                graphNodeCounts = graphNodeCounts ?? new Dictionary<NavGraph, (float, int, int)>();

                if (!graphNodeCounts.TryGetValue(graphEditor.target, out pair) ||
                    (Time.realtimeSinceStartup - pair.Item1) > 2)
                {
                    graphEditor.target.GetNodes(node =>
                    {
                        // 防止用户实现的图形出错
                        if (node != null)
                        {
                            total++;
                            if (node.Walkable) numWalkable++;
                        }
                    });
                    pair = (Time.realtimeSinceStartup, total, numWalkable);
                    graphNodeCounts[graphEditor.target] = pair;
                }

                total = pair.Item2;
                numWalkable = pair.Item3;

                EditorGUI.indentLevel++;

                EditorGUILayout.LabelField("节点", total.ToString());
                EditorGUILayout.LabelField("可行走", numWalkable.ToString());
                EditorGUILayout.LabelField("不可行走", (total - numWalkable).ToString());
                if (!graphEditor.target.isScanned)
                    EditorGUILayout.HelpBox("Graph未扫描", MessageType.Info);

                EditorGUI.indentLevel--;
            }

            graphEditor.infoFadeArea.End();
        }

        /// <summary>使用给定的图形编辑器绘制给定图形的 Inspector</summary>
        void DrawGraph(GraphEditor graphEditor)
        {
            graphEditor.fadeArea.Begin();
            DrawGraphHeader(graphEditor);

            if (graphEditor.fadeArea.BeginFade())
            {
                DrawGraphInfoArea(graphEditor);
                graphEditor.OnInspectorGUI(graphEditor.target);
                graphEditor.OnBaseInspectorGUI(graphEditor.target);
            }

            graphEditor.fadeArea.End();
        }

        public void OnSceneGUI()
        {
            script = target as AstarPath;

            DrawSceneGUISettings();

            // OnSceneGUI 可能从 EditorUtility.DisplayProgressBar 调用
            // 它在图形在编辑器中扫描时重复调用。但是，在图形
            // 正在扫描时运行 OnSceneGUI 方法是一个坏主意，因为它可能干扰
            // 扫描，特别是可能会解除路径队列的阻塞。
            // 如果 AstarPath 对象不是活动对象，也不要这样做，因为序列化在某些方面使用单例。
            if (script.isScanning)
            {
                return;
            }

            script.colorSettings.PushToStatic();
            EditorGUI.BeginChangeCheck();

            if (!LoadStyles()) return;

            // 某些 GUI 控件可能会将其更改为 Used，所以我们需要在这里获取它
            EventType et = Event.current.type;

            CheckGraphEditors();
            for (int i = 0; i < script.graphs.Length; i++)
            {
                NavGraph graph = script.graphs[i];
                if (graph != null && graphEditors[i] != null)
                {
                    graphEditors[i].OnSceneGUI(graph);
                }
            }

            SaveGraphsAndUndo(et);

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(target);
            }
        }

        void DrawSceneGUISettings()
        {
            var darkSkin = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Scene);

            Handles.BeginGUI();
            float width = 180;
            float height = 76;
            float margin = 10;

            var origWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 144;

            GUILayout.BeginArea(
                new Rect(Camera.current.pixelWidth / EditorGUIUtility.pixelsPerPoint - width,
                    Camera.current.pixelHeight / EditorGUIUtility.pixelsPerPoint - height, width - margin,
                    height - margin), "Graph 显示", astarSkin.FindStyle("SceneBoxDark"));
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("显示 Graphs", darkSkin.toggle, astarSkin.FindStyle("ScenePrefixLabel"));
            script.showNavGraphs = EditorGUILayout.Toggle(script.showNavGraphs, darkSkin.toggle);
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Scan", darkSkin.button))
            {
                RunTask(MenuScan);
            }

            // Invisible button to capture clicks. This prevents a click inside the box from causing some other GameObject to be selected.
            GUI.Button(new Rect(0, 0, width - margin, height - margin), "", GUIStyle.none);
            GUILayout.EndArea();

            EditorGUIUtility.labelWidth = origWidth;
            Handles.EndGUI();
        }


        TextAsset SaveGraphData(byte[] bytes, TextAsset target = null)
        {
            string projectPath = System.IO.Path.GetDirectoryName(Application.dataPath) + "/";

            string path;

            if (target != null)
            {
                path = AssetDatabase.GetAssetPath(target);
            }
            else
            {
                // 由于不在一个程序集，所以只能写死
                path = "Assets/ResourcesAssets/Config/Astar/" + fileName + ".bytes";
                // int i = 0;
                // do {
                // 	path = "Assets/GraphCaches/GraphCache" + (i == 0 ? "" : i.ToString()) + ".bytes";
                // 	i++;
                // } while (System.IO.File.Exists(projectPath+path));
            }

            string fullPath = projectPath + path;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath));
            var fileInfo = new System.IO.FileInfo(fullPath);
            // Make sure we can write to the file
            if (fileInfo.Exists && fileInfo.IsReadOnly)
                fileInfo.IsReadOnly = false;
            System.IO.File.WriteAllBytes(fullPath, bytes);

            AssetDatabase.Refresh();
            return AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        }

        void DrawSerializationSettings()
        {
            serializationSettingsArea.Begin();
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("保存 并 加载", level0LabelStyle))
            {
                serializationSettingsArea.open = !serializationSettingsArea.open;
            }

            if (script.data.cacheStartup && script.data.file_cachedStartup != null)
            {
                GUIUtilityx.PushTint(Color.yellow);
                GUILayout.Label("启动时应用此Cache", thinHelpBox);
                GUILayout.Space(20);
                GUIUtilityx.PopTint();
            }

            GUILayout.EndHorizontal();

            // This displays the serialization settings
            if (serializationSettingsArea.BeginFade())
            {
                script.data.cacheStartup = EditorGUILayout.Toggle(
                    new GUIContent("启动时应用当前Cache", "如果启用，将缓存图形，以便在启动时无需扫描它们"),
                    script.data.cacheStartup);

                //设置路径
                if (!script.data.file_cachedStartup)
                {
                    GUILayout.Label("保存名称");
                    fileName = EditorGUILayout.TextField(fileName);
                }

                script.data.file_cachedStartup =
                    EditorGUILayout.ObjectField(script.data.file_cachedStartup, typeof(TextAsset), false) as TextAsset;

                if (script.data.cacheStartup && script.data.file_cachedStartup == null)
                {
                    EditorGUILayout.HelpBox("未生成Cache", MessageType.Error);
                }

                if (script.data.cacheStartup && script.data.file_cachedStartup != null)
                {
                    EditorGUILayout.HelpBox(
                        "游戏启动时，所有Graph设置将被Cache中的设置替换",
                        MessageType.Info);
                }

                GUILayout.BeginHorizontal();

                if (GUILayout.Button("生成Cache"))
                {
                    RunTask(() =>
                    {
                        var serializationSettings = new Pathfinding.Serialization.SerializeSettings();

                        if (isPrefab)
                        {
                            if (!EditorUtility.DisplayDialog("只能保存设置",
                                    "当 AstarPath 对象是预制件时，只能保存Graph设置。将预制件实例化到场景中才能同时保存节点数据。", "保存设置", "取消"))
                            {
                                return;
                            }
                        }
                        else
                        {
                            serializationSettings.nodes = true;

                            if (EditorUtility.DisplayDialog("生成缓存前扫描？", "是否要在保存缓存前扫描Graph？\n" +
                                                                        "如果Graph尚未扫描，则缓存可能不包含节点数据，那么Graph将不得不在启动时扫描。",
                                    "Scan", " Dont scan"))
                            {
                                MenuScan();
                            }
                        }

                        // 保存图形
                        var bytes = script.data.SerializeGraphs(serializationSettings);

                        // 将其存储在文件中
                        script.data.file_cachedStartup = SaveGraphData(bytes, script.data.file_cachedStartup);
                        script.data.cacheStartup = true;
                    });
                }

                if (GUILayout.Button("从Cache加载"))
                {
                    RunTask(() =>
                    {
                        if (EditorUtility.DisplayDialog("确定要从缓存加载吗？", "确定要从缓存加载图形吗？这将替换您当前的图形？", "是", "取消"))
                        {
                            script.data.LoadFromCache();
                        }
                    });
                }

                GUILayout.EndHorizontal();

                GUILayout.Space(5);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("保存到自定义文件"))
                {
                    RunTask(() =>
                    {
                        string path = EditorUtility.SaveFilePanel("保存Graphs", "", "graph.bytes", "bytes");

                        if (path != "")
                        {
                            var serializationSettings = Pathfinding.Serialization.SerializeSettings.Settings;
                            if (isPrefab)
                            {
                                if (!EditorUtility.DisplayDialog("只能保存设置",
                                        "当 AstarPath 对象是预制件时，只能保存图形设置。将预制件实例化到场景中才能同时保存节点数据。", "保存设置", "取消"))
                                {
                                    return;
                                }
                            }
                            else
                            {
                                if (EditorUtility.DisplayDialog("包含节点数据？", "是否要在保存文件中包含节点数据？" +
                                                                           "如果包含节点数据，图形可以完全恢复，而无需先扫描它。", "包含节点数据",
                                        "仅设置"))
                                {
                                    serializationSettings.nodes = true;
                                }
                            }

                            if (serializationSettings.nodes && EditorUtility.DisplayDialog("保存前扫描？", "是否要在保存前扫描图形？" +
                                    "\n不扫描可能导致节点数据从文件中省略，如果图形尚未扫描。", "scan", "Dont scan"))
                            {
                                MenuScan();
                            }

                            uint checksum;
                            var bytes = SerializeGraphs(serializationSettings, out checksum);
                            Pathfinding.Serialization.AstarSerializer.SaveToFile(path, bytes);

                            EditorUtility.DisplayDialog("保存完成", "图形数据保存完成。", "确定");
                        }
                    });
                }

                if (GUILayout.Button("从文件加载"))
                {
                    RunTask(() =>
                    {
                        string path = EditorUtility.OpenFilePanel("加载Graphs", "", "");

                        if (path != "")
                        {
                            try
                            {
                                byte[] bytes = Pathfinding.Serialization.AstarSerializer.LoadFromFile(path);
                                DeserializeGraphs(bytes);
                            }
                            catch (System.Exception e)
                            {
                                Debug.LogError("无法从文件加载：at :" + path + "'\n" + e);
                            }
                        }
                    });
                }

                GUILayout.EndHorizontal();
            }

            serializationSettingsArea.End();
        }

        public void RunTask(System.Action action)
        {
            EditorApplication.CallbackFunction wrapper = null;
            wrapper = () =>
            {
                EditorApplication.update -= wrapper;
                // 仅当自计划任务以来编辑器未被禁用时才运行回调
                if (script != null) action();
            };
            EditorApplication.update += wrapper;
        }

        void DrawSettings()
        {
            settingsArea.Begin();
            settingsArea.Header("设置", ref showSettings);

            if (settingsArea.BeginFade())
            {
                DrawPathfindingSettings();
                DrawDebugSettings();
                DrawColorSettings();
                DrawTagSettings();
                DrawEditorSettings();
            }

            settingsArea.End();
        }

        void DrawPathfindingSettings()
        {
            alwaysVisibleArea.Begin();
            alwaysVisibleArea.HeaderLabel("寻路");
            alwaysVisibleArea.BeginFade();

#if !ASTAR_ATAVISM
            EditorGUI.BeginDisabledGroup(Application.isPlaying);

            script.threadCount = (ThreadCount)EditorGUILayout.EnumPopup(new GUIContent("线程数", "运行路径查找的线程数（如果有）。更多线程 " +
                "可以在多核系统上提高性能。\n" +
                "使用 None 进行调试或如果您不使用太多路径查找。\n " +
                "查看更多信息请参阅文档"), script.threadCount);

            EditorGUI.EndDisabledGroup();

            int threads = AstarPath.CalculateThreadCount(script.threadCount);
            if (threads > 0)
                EditorGUILayout.HelpBox(
                    "Using " + threads + " thread(s)" + (script.threadCount < 0 ? " on your machine" : ""),
                    MessageType.None);
            else
                EditorGUILayout.HelpBox(
                    "Using a single coroutine (no threads)" + (script.threadCount < 0 ? " on your machine" : ""),
                    MessageType.None);
            if (threads > SystemInfo.processorCount)
                EditorGUILayout.HelpBox(
                    "Using more threads than there are CPU cores may not have a positive effect on performance",
                    MessageType.Warning);

            if (script.threadCount == ThreadCount.None)
            {
                script.maxFrameTime = EditorGUILayout.FloatField(
                    new GUIContent("Max Frame Time",
                        "Max number of milliseconds to use for path calculation per frame"), script.maxFrameTime);
            }
            else
            {
                script.maxFrameTime = 10;
            }

            script.maxNearestNodeDistance = EditorGUILayout.FloatField(new GUIContent("Max Nearest Node Distance",
                    "Normally, if the nearest node to e.g the start point of a path was not walkable" +
                    " a search will be done for the nearest node which is walkble. This is the maximum distance (world units) which it will search"),
                script.maxNearestNodeDistance);

            script.heuristic = (Heuristic)EditorGUILayout.EnumPopup("（启发式）Heuristic", script.heuristic);

            if (script.heuristic == Heuristic.Manhattan || script.heuristic == Heuristic.Euclidean ||
                script.heuristic == Heuristic.DiagonalManhattan)
            {
                EditorGUI.indentLevel++;
                script.heuristicScale = EditorGUILayout.FloatField("（启发式缩放）Heuristic Scale", script.heuristicScale);
                script.heuristicScale = Mathf.Clamp01(script.heuristicScale);
                EditorGUI.indentLevel--;
            }

            GUILayout.Label(new GUIContent("高级"), EditorStyles.boldLabel);

            DrawHeuristicOptimizationSettings();

            script.batchGraphUpdates = EditorGUILayout.Toggle(
                new GUIContent("Batch Graph Updates",
                    "Limit graph updates to only run every x seconds. Can have positive impact on performance if many graph updates are done"),
                script.batchGraphUpdates);

            if (script.batchGraphUpdates)
            {
                EditorGUI.indentLevel++;
                script.graphUpdateBatchingInterval = EditorGUILayout.FloatField(
                    new GUIContent("Update Interval (s)",
                        "Minimum number of seconds between each batch of graph updates"),
                    script.graphUpdateBatchingInterval);
                EditorGUI.indentLevel--;
            }

            // 仅当场景中实际存在 navmesh/recast 图形时显示
            // 以帮助减少其他用户的混乱。
            if (script.data.FindGraphWhichInheritsFrom(typeof(NavmeshBase)) != null)
            {
                script.navmeshUpdates.updateInterval = EditorGUILayout.FloatField(
                    new GUIContent("Navmesh 切割更新间隔 (秒)", "检查 navmesh 切割是否更改的频率。"),
                    script.navmeshUpdates.updateInterval);
            }
#endif
            script.scanOnStartup = EditorGUILayout.Toggle(
                new GUIContent("唤醒时扫描", "在 Awake 时扫描所有图形。如果为 false，您必须自己调用 AstarPath.active.Scan ()。如果您想用代码更改图形，这很有用。"),
                script.scanOnStartup);

            alwaysVisibleArea.End();
        }

        readonly string[] heuristicOptimizationOptions = new[]
        {
            "无",
            "随机 (低质量)",
            "RandomSpreadOut (高质量)",
            "自定义"
        };

        void DrawHeuristicOptimizationSettings()
        {
            script.euclideanEmbedding.mode = (HeuristicOptimizationMode)EditorGUILayout.Popup(
                new GUIContent("（启发式优化）Heuristic Optimization"), (int)script.euclideanEmbedding.mode,
                heuristicOptimizationOptions);

            EditorGUI.indentLevel++;
            if (script.euclideanEmbedding.mode == HeuristicOptimizationMode.Random)
            {
                script.euclideanEmbedding.spreadOutCount = EditorGUILayout.IntField(new GUIContent("Count",
                        "Number of optimization points, higher numbers give better heuristics and could make it faster, " +
                        "but too many could make the overhead too great and slow it down. Try to find the optimal value for your map. Recommended value < 100"),
                    script.euclideanEmbedding.spreadOutCount);
            }
            else if (script.euclideanEmbedding.mode == HeuristicOptimizationMode.Custom)
            {
                script.euclideanEmbedding.pivotPointRoot = EditorGUILayout.ObjectField(new GUIContent(
                        "Pivot point root",
                        "All children of this transform are going to be used as pivot points. " +
                        "Recommended count < 100"), script.euclideanEmbedding.pivotPointRoot, typeof(Transform),
                    true) as Transform;
                if (script.euclideanEmbedding.pivotPointRoot == null)
                {
                    EditorGUILayout.HelpBox("Please assign an object", MessageType.Error);
                }
            }
            else if (script.euclideanEmbedding.mode == HeuristicOptimizationMode.RandomSpreadOut)
            {
                script.euclideanEmbedding.pivotPointRoot = EditorGUILayout.ObjectField(new GUIContent(
                        "Pivot point root",
                        "All children of this transform are going to be used as pivot points. " +
                        "They will seed the calculation of more pivot points. " +
                        "Recommended count < 100"), script.euclideanEmbedding.pivotPointRoot, typeof(Transform),
                    true) as Transform;

                if (script.euclideanEmbedding.pivotPointRoot == null)
                {
                    EditorGUILayout.HelpBox("No root is assigned. A random node will be choosen as the seed.",
                        MessageType.Info);
                }

                script.euclideanEmbedding.spreadOutCount = EditorGUILayout.IntField(new GUIContent("Count",
                        "Number of optimization points, higher numbers give better heuristics and could make it faster, " +
                        "but too many could make the overhead too great and slow it down. Try to find the optimal value for your map. Recommended value < 100"),
                    script.euclideanEmbedding.spreadOutCount);
            }

            if (script.euclideanEmbedding.mode != HeuristicOptimizationMode.None)
            {
                EditorGUILayout.HelpBox(
                    "Heuristic optimization assumes the graph remains static. No graph updates, dynamic obstacles or similar should be applied to the graph " +
                    "when using heuristic optimization.", MessageType.Info);
            }

            EditorGUI.indentLevel--;
        }

        /// <summary>Opens the A* Inspector and shows the section for editing tags</summary>
        public static void EditTags()
        {
            AstarPath astar = UnityCompatibility.FindAnyObjectByType<AstarPath>();

            if (astar != null)
            {
                showTagNames = true;
                showSettings = true;
                Selection.activeGameObject = astar.gameObject;
            }
            else
            {
                Debug.LogWarning("场景中没有 AstarPath 组件");
            }
        }

        void DrawTagSettings()
        {
            tagsArea.Begin();
            tagsArea.Header("Tag Names", ref showTagNames);

            if (tagsArea.BeginFade())
            {
                string[] tagNames = script.GetTagNames();

                for (int i = 0; i < tagNames.Length; i++)
                {
                    tagNames[i] =
                        EditorGUILayout.TextField(new GUIContent("Tag " + i, "Name for tag " + i), tagNames[i]);
                    if (tagNames[i] == "") tagNames[i] = "" + i;
                }
            }

            tagsArea.End();
        }

        void DrawEditorSettings()
        {
            editorSettingsArea.Begin();
            editorSettingsArea.Header("编辑器");

            if (editorSettingsArea.BeginFade())
            {
                FadeArea.fancyEffects = EditorGUILayout.Toggle("平滑过渡", FadeArea.fancyEffects);
            }

            editorSettingsArea.End();
        }

        static void DrawColorSlider(ref float left, ref float right, bool editable)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUILayout.BeginVertical();

            GUILayout.Box("", astarSkin.GetStyle("ColorInterpolationBox"));
            GUILayout.BeginHorizontal();
            if (editable)
            {
                left = EditorGUILayout.IntField((int)left);
            }
            else
            {
                GUILayout.Label(left.ToString("0"));
            }

            GUILayout.FlexibleSpace();
            if (editable)
            {
                right = EditorGUILayout.IntField((int)right);
            }
            else
            {
                GUILayout.Label(right.ToString("0"));
            }

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.Space(4);
            GUILayout.EndHorizontal();
        }

        void DrawDebugSettings()
        {
            alwaysVisibleArea.Begin();
            alwaysVisibleArea.HeaderLabel("Debug");
            alwaysVisibleArea.BeginFade();

            script.logPathResults = (PathLog)EditorGUILayout.EnumPopup("路径日志记录", script.logPathResults);
            script.debugMode = (GraphDebugMode)EditorGUILayout.EnumPopup("Graph Coloring", script.debugMode);

            if (script.debugMode == GraphDebugMode.SolidColor)
            {
                EditorGUI.BeginChangeCheck();
                script.colorSettings._SolidColor = EditorGUILayout.ColorField(
                    new GUIContent("Color", "当“图形着色”='纯色'时用于图形的颜色"),
                    script.colorSettings._SolidColor);
                if (EditorGUI.EndChangeCheck())
                {
                    script.colorSettings.PushToStatic();
                }
            }

            if (script.debugMode == GraphDebugMode.G || script.debugMode == GraphDebugMode.H ||
                script.debugMode == GraphDebugMode.F || script.debugMode == GraphDebugMode.Penalty)
            {
                script.manualDebugFloorRoof = !EditorGUILayout.Toggle("自动限制", !script.manualDebugFloorRoof);
                DrawColorSlider(ref script.debugFloor, ref script.debugRoof, script.manualDebugFloorRoof);
            }

            script.showSearchTree = EditorGUILayout.Toggle("显示搜索树", script.showSearchTree);
            if (script.showSearchTree)
            {
                EditorGUILayout.HelpBox(
                    "“显示搜索树”已启用，在游戏运行时，您可能会在图形渲染中看到渲染故障" +
                    "。这没什么可担心的，仅仅是因为路径计算与 Gizmos 渲染同时进行。您可以暂停游戏以查看准确的渲染。", MessageType.Info);
            }

            script.showUnwalkableNodes = EditorGUILayout.Toggle("显示不可行走节点", script.showUnwalkableNodes);

            if (script.showUnwalkableNodes)
            {
                EditorGUI.indentLevel++;
                script.unwalkableNodeDebugSize = EditorGUILayout.FloatField("Size", script.unwalkableNodeDebugSize);
                EditorGUI.indentLevel--;
            }

            alwaysVisibleArea.End();
        }

        void DrawColorSettings()
        {
            colorSettingsArea.Begin();
            colorSettingsArea.Header("Colors");

            if (colorSettingsArea.BeginFade())
            {
                // Make sure the object is not null
                AstarColor colors = script.colorSettings = script.colorSettings ?? new AstarColor();

                colors._SolidColor = EditorGUILayout.ColorField(
                    new GUIContent("Solid Color", "当“图形着色”='纯色'时用于图形的颜色"),
                    colors._SolidColor);
                colors._UnwalkableNode = EditorGUILayout.ColorField("不可行走节点", colors._UnwalkableNode);
                colors._BoundsHandles = EditorGUILayout.ColorField("（边界）Bounds Handles", colors._BoundsHandles);

                colors._ConnectionLowLerp =
                    EditorGUILayout.ColorField("Connection Gradient（渐变） (low)", colors._ConnectionLowLerp);
                colors._ConnectionHighLerp =
                    EditorGUILayout.ColorField("Connection Gradient（渐变） (high)", colors._ConnectionHighLerp);

                colors._MeshEdgeColor = EditorGUILayout.ColorField("Mesh Edge", colors._MeshEdgeColor);

                if (EditorResourceHelper.GizmoSurfaceMaterial != null && EditorResourceHelper.GizmoLineMaterial != null)
                {
                    EditorGUI.BeginChangeCheck();
                    var col1 = EditorResourceHelper.GizmoSurfaceMaterial.color;
                    col1.a = EditorGUILayout.Slider("Navmesh 表面不透明度", col1.a, 0, 1);

                    var col2 = EditorResourceHelper.GizmoLineMaterial.color;
                    col2.a = EditorGUILayout.Slider("Navmesh Outline 不透明度", col2.a, 0, 1);

                    var fade = EditorResourceHelper.GizmoSurfaceMaterial.GetColor("_FadeColor");
                    fade.a = EditorGUILayout.Slider("Opacity Behind Objects", fade.a, 0, 1);

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObjects(
                            new[] { EditorResourceHelper.GizmoSurfaceMaterial, EditorResourceHelper.GizmoLineMaterial },
                            "Change navmesh transparency（透明度）");
                        EditorResourceHelper.GizmoSurfaceMaterial.color = col1;
                        EditorResourceHelper.GizmoLineMaterial.color = col2;
                        EditorResourceHelper.GizmoSurfaceMaterial.SetColor("_FadeColor", fade);
                        EditorResourceHelper.GizmoLineMaterial.SetColor("_FadeColor", fade * new Color(1, 1, 1, 0.7f));
                    }
                }

                colors._AreaColors = colors._AreaColors ?? new Color[0];

                // Custom Area Colors
                showCustomAreaColors = EditorGUILayout.Foldout(showCustomAreaColors, "自定义区域颜色");
                if (showCustomAreaColors)
                {
                    EditorGUI.indentLevel += 2;

                    for (int i = 0; i < colors._AreaColors.Length; i++)
                    {
                        GUILayout.BeginHorizontal();
                        colors._AreaColors[i] =
                            EditorGUILayout.ColorField("Area " + i + (i == 0 ? " (not used usually)" : ""),
                                colors._AreaColors[i]);
                        if (GUILayout.Button(new GUIContent("", "Reset to the default color"),
                                astarSkin.FindStyle("SmallReset"), GUILayout.Width(20)))
                        {
                            colors._AreaColors[i] = AstarMath.IntToColor(i, 1F);
                        }

                        GUILayout.EndHorizontal();
                    }

                    GUILayout.BeginHorizontal();
                    EditorGUI.BeginDisabledGroup(colors._AreaColors.Length > 255);

                    if (GUILayout.Button("Add New"))
                    {
                        Memory.Realloc(ref colors._AreaColors, colors._AreaColors.Length + 1);
                        colors._AreaColors[colors._AreaColors.Length - 1] =
                            AstarMath.IntToColor(colors._AreaColors.Length - 1, 1F);
                    }

                    EditorGUI.EndDisabledGroup();
                    EditorGUI.BeginDisabledGroup(colors._AreaColors.Length == 0);

                    if (GUILayout.Button("Remove last") && colors._AreaColors.Length > 0)
                    {
                        colors._AreaColors = Memory.ShrinkArray(colors._AreaColors, colors._AreaColors.Length - 1);
                    }

                    EditorGUI.EndDisabledGroup();
                    GUILayout.EndHorizontal();

                    EditorGUI.indentLevel -= 2;
                }

                if (GUI.changed)
                {
                    colors.PushToStatic();
                }
            }

            colorSettingsArea.End();
        }

        /// <summary>确保每个图形都有一个图形编辑器</summary>
        void CheckGraphEditors()
        {
            var data = script.data;
            data.graphs = data.graphs ?? new NavGraph[0];
            // 确保 graphEditors.Length >= data.graphs.Length
            Memory.Realloc(ref graphEditors, data.graphs.Length);

            for (int i = 0; i < script.graphs.Length; i++)
            {
                var graph = script.graphs[i];

                if (graph != null && graph.guid == new Pathfinding.Util.Guid())
                {
                    graph.guid = Pathfinding.Util.Guid.NewGuid();
                }

                if (graph == null || !graph.showInInspector)
                {
                    graphEditors[i] = null;
                    continue;
                }

                if (graphEditors[i] == null || graphEditors[i].target != graph)
                {
                    graphEditors[i] = CreateGraphEditor(graph);
                }
            }
        }

        void RemoveGraph(NavGraph graph)
        {
            script.data.RemoveGraph(graph);
            CheckGraphEditors();
            GUI.changed = true;
            Repaint();
        }

        void DuplidateGraph(NavGraph graph)
        {
            script.data.DuplicateGraph(graph);
            CheckGraphEditors();
            GUI.changed = true;
            Repaint();
        }

        void AddGraph(System.Type type)
        {
            script.data.AddGraph(type);
            CheckGraphEditors();
            GUI.changed = true;
        }

        /// <summary>为图形创建 GraphEditor</summary>
        GraphEditor CreateGraphEditor(NavGraph graph)
        {
            var graphType = graph.GetType().Name;
            GraphEditor result;

            if (graphEditorTypes.TryGetValue(graphType, out var graphEditorTypeAttr))
            {
                var graphEditorType = graphEditorTypeAttr.editorType;
                result = System.Activator.CreateInstance(graphEditorType) as GraphEditor;

                // 反序列化编辑器设置
                var editorData = (graph as IGraphInternals).SerializedEditorSettings;
                if (editorData != null)
                    Pathfinding.Serialization.TinyJsonDeserializer.Deserialize(editorData, graphEditorType, result,
                        script.gameObject);
            }
            else
            {
                Debug.LogError("找不到图形类型 '" + graphType + "' 的编辑器。有 " + graphEditorTypes.Count + " 个可用的图形编辑器");
                result = new GraphEditor();
                graphEditorTypes[graphType] = new CustomGraphEditorAttribute(graph.GetType(), graphType)
                {
                    editorType = typeof(GraphEditor)
                };
            }

            result.editor = this;
            result.fadeArea = new FadeArea(graph.open, this, level1AreaStyle, level1LabelStyle);
            result.infoFadeArea = new FadeArea(graph.infoScreenOpen, this, null, null);
            result.target = graph;

            result.OnEnable();
            return result;
        }

        void HandleUndo()
        {
            // 用户尝试撤销某些操作，应用它
            DeserializeGraphs();
        }

        void SerializeIfDataChanged()
        {
            byte[] bytes = SerializeGraphs(out var checksum);

            uint byteHash = Checksum.GetChecksum(bytes);
            uint dataHash = Checksum.GetChecksum(script.data.GetData());
            // 检查数据是否与先前数据不同，使用校验和
            bool isDifferent = checksum != ignoredChecksum && dataHash != byteHash;

            /// <summary>当执行撤销或重做操作时调用</summary>
            if (isDifferent)
            {
                Undo.RegisterCompleteObjectUndo(script, "A* Graph设置");
                Undo.IncrementCurrentGroup();
                // Assign the new data
                script.data.SetData(bytes);
                EditorUtility.SetDirty(script);
            }
        }

        /// <summary>当执行撤销或重做操作时调用</summary>
        void OnUndoRedoPerformed()
        {
            if (!this) return;

            byte[] bytes = SerializeGraphs(out var checksum);

            // 检查数据是否与先前数据不同，使用校验和
            bool isDifferent = Checksum.GetChecksum(script.data.GetData()) != Checksum.GetChecksum(bytes);

            if (isDifferent)
            {
                HandleUndo();
            }

            CheckGraphEditors();
            // 反序列化图形不一定产生与从数据加载相同的哈希
            // 这（可能）是因为编辑器设置并非一直保存
            // 所以我们明确忽略新的哈希
            SerializeGraphs(out checksum);
            ignoredChecksum = checksum;
        }

        public void SaveGraphsAndUndo(EventType et = EventType.Used, string eventCommand = "")
        {
            // 序列化图形的设置

            // 不要在编辑器中处理撤销事件，我们不想重置图形
            // 如果图形正在更新，也不要这样做，因为序列化图形
            // 可能会干扰（特别是可能会解除路径队列的阻塞）。
            // 如果 AstarPath 对象不是活动对象，也不要这样做，因为序列化在某些方面使用单例
            if (Application.isPlaying || script.isScanning || script.IsAnyWorkItemInProgress)
            {
                return;
            }

            if ((Undo.GetCurrentGroup() != lastUndoGroup || et == EventType.MouseUp) &&
                eventCommand != "UndoRedoPerformed")
            {
                SerializeIfDataChanged();

                lastUndoGroup = Undo.GetCurrentGroup();
            }

            if (Event.current == null || script.data.GetData() == null)
            {
                SerializeIfDataChanged();
            }
        }

        public byte[] SerializeGraphs(out uint checksum)
        {
            return SerializeGraphs(Pathfinding.Serialization.SerializeSettings.Settings, out checksum);
        }

        public byte[] SerializeGraphs(Pathfinding.Serialization.SerializeSettings settings, out uint checksum)
        {
            CheckGraphEditors();
            // 序列化所有图形编辑器
            var output = new System.Text.StringBuilder();
            for (int i = 0; i < graphEditors.Length; i++)
            {
                if (graphEditors[i] == null) continue;
                output.Length = 0;
                Pathfinding.Serialization.TinyJsonSerializer.Serialize(graphEditors[i], output);
                (graphEditors[i].target as IGraphInternals).SerializedEditorSettings = output.ToString();
            }

            // 序列化所有图形（包括序列化的编辑器数据）
            return script.data.SerializeGraphs(settings, out checksum);
        }

        void DeserializeGraphs()
        {
            // 用户清除了数据字段。撤销此操作。
            if (script.data.GetData() == null) script.data.SetData(new byte[0]);
            DeserializeGraphs(script.data.GetData());
        }

        void DeserializeGraphs(byte[] bytes)
        {
            try
            {
                if (bytes == null || bytes.Length == 0)
                {
                    script.data.graphs = new NavGraph[0];
                }
                else
                {
                    script.data.DeserializeGraphs(bytes);
                }

                // Make sure every graph has a graph editor
                CheckGraphEditors();
            }
            catch (System.Exception e)
            {
                Debug.LogError("反序列化graph失败");
                Debug.LogException(e);
                script.data.SetData(null);
            }
        }

        [MenuItem("Edit/Pathfinding/Scan All Graphs %&s")]
        public static void MenuScan()
        {
            if (AstarPath.active == null)
            {
                AstarPath.FindAstarPath();
                if (AstarPath.active == null)
                {
                    return;
                }
            }

            try
            {
                var lastMessageTime = Time.realtimeSinceStartup;
                foreach (var p in AstarPath.active.ScanAsync())
                {
                    // 显示进度条相当慢，所以不要做得太频繁
                    if (Time.realtimeSinceStartup - lastMessageTime > 0.2f)
                    {
                        // 显示扫描的进度条
                        UnityEditor.EditorUtility.DisplayProgressBar("Scanning", p.ToString(), p.progress);
                        lastMessageTime = Time.realtimeSinceStartup;
                    }
                }

                // 除了场景视图外，还要重绘游戏视图。
                // 如果用户只打开了游戏视图，刷新它以便他们可以看到图形是很好的。
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
            catch (System.Exception e)
            {
                Debug.LogError("生成图形时出错：\n" + e + "\n\n如果您认为这是一个错误，请在 forum.arongranberg.com 上联系我（发布一个新主题）\n");
                EditorUtility.DisplayDialog("Error Generating Graphs",
                    "生成图形错误", "生成图形时出错，请检查控制台以获取更多信息", "确定");
                throw e;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>在当前程序集中搜索 GraphEditor 和 NavGraph 类型</summary>
        void FindGraphTypes()
        {
            if (graphEditorTypes.Count > 0) return;

            graphEditorTypes = new Dictionary<string, CustomGraphEditorAttribute>();

            var editorTypes = AssemblySearcher.FindTypesInheritingFrom<GraphEditor>();
            foreach (var type in editorTypes)
            {
                // 循环遍历 CustomGraphEditorAttribute 属性的属性
                foreach (var attribute in type.GetCustomAttributes(false))
                {
                    if (attribute is CustomGraphEditorAttribute cge && !System.Type.Equals(cge.graphType, null))
                    {
                        cge.editorType = type;
                        graphEditorTypes.Add(cge.graphType.Name, cge);
                    }
                }
            }

            // 确保图形类型（不是图形编辑器类型）也是最新的
            script.data.FindGraphTypes();
        }
    }
}