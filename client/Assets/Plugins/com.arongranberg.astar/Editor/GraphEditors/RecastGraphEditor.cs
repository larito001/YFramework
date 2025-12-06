using UnityEngine;
using UnityEditor;
using Pathfinding.Graphs.Navmesh;
using UnityEditorInternal;

namespace Pathfinding {
	/// <summary>Editor for the RecastGraph.</summary>
	[CustomGraphEditor(typeof(RecastGraph), "Recast Graph")]
	public class RecastGraphEditor : GraphEditor {
		public static bool tagMaskFoldout;
		public static bool meshesUnreadableAtRuntimeFoldout;
		ReorderableList tagMaskList;
		ReorderableList perLayerModificationsList;
		public enum UseTiles {
			UseTiles = 0,
			DontUseTiles = 1
		}

		static readonly GUIContent[] DimensionModeLabels = new [] {
			new GUIContent("2D"),
			new GUIContent("3D"),
		};

		static Rect SliceColumn (ref Rect rect, float width, float spacing = 0) {
			return GUIUtilityx.SliceColumn(ref rect, width, spacing);
		}

		static void DrawIndentedList (ReorderableList list) {
			GUILayout.BeginHorizontal();
			GUILayout.Space(EditorGUI.IndentedRect(default).xMin);
			list.DoLayoutList();
			GUILayout.Space(3);
			GUILayout.EndHorizontal();
		}

		static void DrawColliderDetail (RecastGraph.CollectionSettings settings) {
			const float LowestApproximationError = 0.5f;
			settings.colliderRasterizeDetail = EditorGUILayout.Slider(new GUIContent("圆形碰撞器细节", "控制生成的球形和胶囊网格的细节。" +
				"更高的值可能略微提高导航网格质量，较低的值可提高图扫描性能。"), Mathf.Round(10*settings.colliderRasterizeDetail)*0.1f, 0, 1.0f / LowestApproximationError);
		}

		void DrawCollectionSettings (RecastGraph.CollectionSettings settings, RecastGraph.DimensionMode dimensionMode) {
			settings.collectionMode = (RecastGraph.CollectionSettings.FilterMode)EditorGUILayout.EnumPopup("过滤方式", settings.collectionMode);

			if (settings.collectionMode == RecastGraph.CollectionSettings.FilterMode.Layers) {
				settings.layerMask = EditorGUILayoutx.LayerMaskField("Layer Mask", settings.layerMask);
			} else {
				DrawIndentedList(tagMaskList);
			}

			if (dimensionMode == RecastGraph.DimensionMode.Dimension3D) {
				settings.rasterizeTerrain = EditorGUILayout.Toggle(new GUIContent("绘制 Terrains", "是否应包含栅格化terrains"), settings.rasterizeTerrain);
				if (settings.rasterizeTerrain) {
					EditorGUI.indentLevel++;
					settings.rasterizeTrees = EditorGUILayout.Toggle(new GUIContent("绘制 Trees", "在 terrain 上栅格化树木碰撞体。 " +
						"若树木预制体含有碰撞体，该碰撞体将被栅格化处理；" +
						"若无，则将使用简易盒式碰撞体，脚本会尝试根据树木比例进行调整 " +
						"但效果可能欠佳，因此建议优先使用附加碰撞体。 " ), settings.rasterizeTrees);
					settings.terrainHeightmapDownsamplingFactor = EditorGUILayout.IntField(new GUIContent("高度图降采样（Heightmap Downsampling）", "地形高度图的降采样比率。数值越低效果越好，但扫描速度越慢。"), settings.terrainHeightmapDownsamplingFactor);
					settings.terrainHeightmapDownsamplingFactor = Mathf.Max(1, settings.terrainHeightmapDownsamplingFactor);
					EditorGUI.indentLevel--;
				}

				settings.rasterizeMeshes = EditorGUILayout.Toggle(new GUIContent("根据网格（mesh）绘制", "是否应对mesh进行栅格化处理并用于构建导航网格"), settings.rasterizeMeshes);
				settings.rasterizeColliders = EditorGUILayout.Toggle(new GUIContent("根据碰撞体（collider）绘制", "是否应对collider进行栅格化处理并用于构建导航网格"), settings.rasterizeColliders);
			} else {
				// Colliders are always rasterized in 2D mode
				EditorGUI.BeginDisabledGroup(true);
				EditorGUILayout.Toggle(new GUIContent("根据碰撞体（collider）绘制", "是否应对collider进行栅格化处理并用于构建导航网格，。在2D模式下，此选项将始终启用。"), true);
				EditorGUI.EndDisabledGroup();
			}

			if (settings.rasterizeMeshes && settings.rasterizeColliders && dimensionMode == RecastGraph.DimensionMode.Dimension3D) {
				EditorGUILayout.HelpBox("当前同时栅格化网格（mesh）与碰撞体（collider）。若碰撞体与网格形态相近，此操作可能导致重复计算。可通过RecastNavmeshModifier组件强制包含特定对象，" +
				                        "该设置将无视上述参数配置始终生效。", MessageType.Info);
			}
		}

		public override void OnEnable () {
			base.OnEnable();
			var graph = target as RecastGraph;
			tagMaskList = new ReorderableList(graph.collectionSettings.tagMask, typeof(string), true, true, true, true) {
				drawElementCallback = (Rect rect, int index, bool active, bool isFocused) => {
					graph.collectionSettings.tagMask[index] = EditorGUI.TagField(rect, graph.collectionSettings.tagMask[index]);
				},
				drawHeaderCallback = (Rect rect) => {
					GUI.Label(rect, "Tag mask");
				},
				elementHeight = EditorGUIUtility.singleLineHeight,
				onAddCallback = (ReorderableList list) => {
					graph.collectionSettings.tagMask.Add("Untagged");
				}
			};

			perLayerModificationsList = new ReorderableList(graph.perLayerModifications, typeof(RecastGraph.PerLayerModification), true, true, true, true) {
				drawElementCallback = (Rect rect, int index, bool active, bool isFocused) => {
					var element = graph.perLayerModifications[index];
					var w = rect.width;
					var spacing = EditorGUIUtility.standardVerticalSpacing;
					element.layer = EditorGUI.LayerField(SliceColumn(ref rect, w * 0.3f, spacing), element.layer);

					if (element.mode == RecastNavmeshModifier.Mode.WalkableSurfaceWithTag) {
						element.mode = (RecastNavmeshModifier.Mode)EditorGUI.EnumPopup(SliceColumn(ref rect, w * 0.4f, spacing), element.mode);
						element.surfaceID = Util.EditorGUILayoutHelper.TagField(rect, GUIContent.none, element.surfaceID, AstarPathEditor.EditTags);
						element.surfaceID = Mathf.Clamp(element.surfaceID, 0, GraphNode.MaxTagIndex);
					} else if (element.mode == RecastNavmeshModifier.Mode.WalkableSurfaceWithSeam) {
						element.mode = (RecastNavmeshModifier.Mode)EditorGUI.EnumPopup(SliceColumn(ref rect, w * 0.4f, spacing), element.mode);
						string helpTooltip =  "此网格上的所有表面都将可行走，并在该网格表面与其他网格（具有不同表面 ID）的表面之间创建缝隙";
						GUI.Label(SliceColumn(ref rect, 70, spacing), new GUIContent("Surface ID", helpTooltip));
						element.surfaceID = Mathf.Max(0, EditorGUI.IntField(rect, new GUIContent("", helpTooltip), element.surfaceID));
					} else {
						element.mode = (RecastNavmeshModifier.Mode)EditorGUI.EnumPopup(rect, element.mode);
					}

					graph.perLayerModifications[index] = element;
				},
				drawHeaderCallback = (Rect rect) => {
					GUI.Label(rect, "每层（Layer）修改");
				},
				elementHeight = EditorGUIUtility.singleLineHeight,
				onAddCallback = (ReorderableList list) => {
					// Find the first layer that is not already modified
					var availableLayers = graph.collectionSettings.layerMask;
					foreach (var mod in graph.perLayerModifications) {
						availableLayers &= ~(1 << mod.layer);
					}
					var newMod = RecastGraph.PerLayerModification.Default;
					for (int i = 0; i < 32; i++) {
						if ((availableLayers & (1 << i)) != 0) {
							newMod.layer = i;
							break;
						}
					}
					graph.perLayerModifications.Add(newMod);
				}
			};
		}

		public override void OnInspectorGUI (NavGraph target) {
			var graph = target as RecastGraph;

			Header("形状");

			graph.dimensionMode = (RecastGraph.DimensionMode)EditorGUILayout.Popup(new GUIContent("维度", "图是用于2D还是3D世界？"), (int)graph.dimensionMode, DimensionModeLabels);
			if (graph.dimensionMode == RecastGraph.DimensionMode.Dimension2D && Mathf.Abs(Vector3.Dot(Quaternion.Euler(graph.rotation) * Vector3.up, Vector3.forward)) < 0.99999f) {
				EditorGUI.indentLevel++;
				EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
				GUILayout.Label(EditorGUIUtility.IconContent("console.warnicon"), GUILayout.ExpandWidth(false));
				GUILayout.BeginVertical();
				GUILayout.FlexibleSpace();
				GUILayout.BeginHorizontal();
				GUILayout.Label("您的图不在 XY 平面");
				if (GUILayout.Button("对齐")) {
					graph.rotation = new Vector3(-90, 0, 0);
					graph.forcedBoundsCenter = new Vector3(graph.forcedBoundsCenter.x, graph.forcedBoundsCenter.y, -graph.forcedBoundsSize.y * 0.5f);
				}
				GUILayout.EndHorizontal();
				GUILayout.FlexibleSpace();
				GUILayout.EndVertical();
				EditorGUILayout.EndHorizontal();
				EditorGUI.indentLevel--;
			}

			// In 3D mode, we use the graph's center as the pivot point, but in 2D mode, we use the center of the base plane of the graph as the pivot point.
			// This makes sense because in 2D mode, you typically want to set the base plane's center to Z=0, and you don't care much about the height of the graph.
			var pivot = graph.dimensionMode == RecastGraph.DimensionMode.Dimension2D ? new Vector3(0.0f, -0.5f, 0.0f) : Vector3.zero;
			var centerOffset = Quaternion.Euler(graph.rotation) * Vector3.Scale(graph.forcedBoundsSize, pivot);
			var newCenter = EditorGUILayout.Vector3Field("中心center", graph.forcedBoundsCenter + centerOffset);
			var newSize = EditorGUILayout.Vector3Field("大小size", graph.forcedBoundsSize);

			// Make sure the bounding box is not infinitely thin along any axis
			newSize = Vector3.Max(newSize, Vector3.one * 0.001f);

			// Recalculate the center offset with the new size, and then adjust the center so that the pivot point stays the same if the size changes
			centerOffset = Quaternion.Euler(graph.rotation) * Vector3.Scale(newSize, pivot);
			graph.forcedBoundsCenter = RoundVector3(newCenter) - centerOffset;
			graph.forcedBoundsSize = RoundVector3(newSize);

			graph.rotation = RoundVector3(EditorGUILayout.Vector3Field("旋转rotation", graph.rotation));

			long estWidth = Mathf.RoundToInt(Mathf.Ceil(graph.forcedBoundsSize.x / graph.cellSize));
			long estDepth = Mathf.RoundToInt(Mathf.Ceil(graph.forcedBoundsSize.z / graph.cellSize));

			EditorGUI.BeginDisabledGroup(true);
			var estTilesX = (estWidth + graph.editorTileSize - 1) / graph.editorTileSize;
			var estTilesZ = (estDepth + graph.editorTileSize - 1) / graph.editorTileSize;
			var label = estWidth.ToString() + " x " + estDepth.ToString() + " voxels（体素）";
			if (graph.useTiles) {
				label += ", 分为  " + (estTilesX*estTilesZ) + " 块（tiles）";
			}
			EditorGUILayout.LabelField(new GUIContent("（大小）Size", "基于体素大小和边界框"), new GUIContent(label));
			EditorGUI.EndDisabledGroup();

			// Show a warning if the number of voxels is too large
			if (estWidth*estDepth >= 3000*3000) {
				GUIStyle helpBox = GUI.skin.FindStyle("HelpBox") ?? GUI.skin.FindStyle("Box");

				Color preColor = GUI.color;
				if (estWidth*estDepth >= 8192*8192) {
					GUI.color = Color.red;
				} else {
					GUI.color = Color.yellow;
				}

				GUILayout.Label("警告：计算可能需要一些时间", helpBox);
				GUI.color = preColor;
			}

			if (!editor.isPrefab)
			{
		
				var btn = new GUIContent("将边界对齐到场景（自动对齐边界）", "将图的边界精确包含所有匹配掩码的场景网格。");
				if (GUILayout.Button(btn)) {
					graph.SnapBoundsToScene();
					GUI.changed = true;
				}
			}

			Separator();
			Header("输入过滤");

			DrawCollectionSettings(graph.collectionSettings, graph.dimensionMode);

			Separator();
			Header("角色属性");

			graph.characterRadius = EditorGUILayout.FloatField(new GUIContent("角色半径", "角色的半径。建议留有一定余量。\n世界单位。"), graph.characterRadius);
			graph.characterRadius = Mathf.Max(graph.characterRadius, 0);

			if (graph.characterRadius < graph.cellSize * 2) {
				EditorGUILayout.HelpBox("为获得最佳导航网格质量，建议角色半径至少为体素大小的2倍。更小的体素可提供更高质量的导航网格，但扫描图的时间会更长。", MessageType.Warning);
			}

			if (graph.dimensionMode == RecastGraph.DimensionMode.Dimension3D) {
				graph.walkableHeight = EditorGUILayout.DelayedFloatField(new GUIContent("角色高度", "可行走区域到顶棚的最小距离"), graph.walkableHeight);
				graph.walkableHeight = Mathf.Max(graph.walkableHeight, 0);

				graph.walkableClimb = EditorGUILayout.FloatField(new GUIContent("最大台阶（step）高度", "角色可以垂直跨越的最大高度"), graph.walkableClimb);

				// A walkableClimb higher than this can cause issues when generating the navmesh since then it can in some cases
				// Both be valid for a character to walk under an obstacle and climb up on top of it (and that cannot be handled with a navmesh without links)
				if (graph.walkableClimb >= graph.walkableHeight) {
					graph.walkableClimb = graph.walkableHeight;
					EditorGUILayout.HelpBox("最大台阶高度应小于角色高度。已限制为 " + graph.walkableHeight+".", MessageType.Warning);
				} else if (graph.walkableClimb < 0) {
					graph.walkableClimb = 0;
				}

				graph.maxSlope = EditorGUILayout.Slider(new GUIContent("最大坡度（Slop）", "近似最大坡度"), graph.maxSlope, 0F, 90F);
			}

			if (graph.dimensionMode == RecastGraph.DimensionMode.Dimension2D) {
				graph.backgroundTraversability = (RecastGraph.BackgroundTraversability)EditorGUILayout.EnumPopup("背景background可通行性", graph.backgroundTraversability);
			}

			DrawIndentedList(perLayerModificationsList);

			int seenLayers = 0;
			for (int i = 0; i < graph.perLayerModifications.Count; i++) {
				if ((seenLayers & 1 << graph.perLayerModifications[i].layer) != 0) {
					EditorGUILayout.HelpBox("重复图层。每个图层只能被单个规则修改。", MessageType.Error);
					break;
				}
				seenLayers |= 1 << graph.perLayerModifications[i].layer;
			}

			Separator();
			Header("栅格化Rasterization");

			graph.cellSize = EditorGUILayout.FloatField(new GUIContent("体素（voxel）大小", "每个体素的世界单位大小"), graph.cellSize);
			if (graph.cellSize < 0.001F) graph.cellSize = 0.001F;

			graph.useTiles = (UseTiles)EditorGUILayout.EnumPopup("使用瓦片", graph.useTiles ? UseTiles.UseTiles : UseTiles.DontUseTiles) == UseTiles.UseTiles;

			if (graph.useTiles) {
				EditorGUI.indentLevel++;
				graph.editorTileSize = EditorGUILayout.IntField(new GUIContent("瓦片大小 (voxel)", "单个瓦片的体素大小。\n较大的瓦片可加快初次扫描，但在大型场景可能导致内存不足。\n较小瓦片更新更快。\n不同瓦片大小可能影响路径质量。【推荐64-256】"), graph.editorTileSize);
				graph.editorTileSize = Mathf.Max(10, graph.editorTileSize);
				EditorGUI.indentLevel--;
			}

			graph.maxEdgeLength = EditorGUILayout.FloatField(new GUIContent("最大边缘长度", "完成的导航网格中单条边的最大长度，超过该长度将被拆分。较小值可获得更高质量图，但不要太小以避免大量细长三角形。"), graph.maxEdgeLength);
			graph.maxEdgeLength = graph.maxEdgeLength < graph.cellSize ? graph.cellSize : graph.maxEdgeLength;

			// This is actually a float, but to make things easier for the user, we only allow picking integers. Small changes don't matter that much anyway.
			graph.contourMaxError = EditorGUILayout.IntSlider(new GUIContent("边缘简化", "简化导航网格边缘，使其偏离真实值不超过此体素数。"), Mathf.RoundToInt(graph.contourMaxError), 0, 5);
			graph.minRegionSize = EditorGUILayout.FloatField(new GUIContent("最小区域（min region）大小", "小区域将被移除，单位为体素。仅单瓦片内区域会被移除，跨瓦片区域始终保留。如果未使用瓦片，则所有小区域将被移除。"), graph.minRegionSize);

			var effectivelyRasterizingColliders = graph.collectionSettings.rasterizeColliders || (graph.dimensionMode == RecastGraph.DimensionMode.Dimension3D && graph.collectionSettings.rasterizeTerrain && graph.collectionSettings.rasterizeTrees) || graph.dimensionMode == RecastGraph.DimensionMode.Dimension2D;
			if (effectivelyRasterizingColliders) {
				DrawColliderDetail(graph.collectionSettings);
			}

			var countStillUnreadable = 0;
			for (int i = 0; graph.meshesUnreadableAtRuntime != null && i < graph.meshesUnreadableAtRuntime.Count; i++) {
				countStillUnreadable += graph.meshesUnreadableAtRuntime[i].Item2.isReadable ? 0 : 1;
			}
			if (countStillUnreadable > 0) {
				GUILayout.BeginHorizontal();
				GUILayout.Space(EditorGUI.IndentedRect(new Rect(0, 0, 0, 0)).xMin);
				EditorGUILayout.BeginVertical(EditorStyles.helpBox);
				GUILayout.BeginHorizontal();
				GUILayout.BeginVertical();
				GUILayout.FlexibleSpace();
				meshesUnreadableAtRuntimeFoldout = GUILayout.Toggle(meshesUnreadableAtRuntimeFoldout, "", EditorStyles.foldout, GUILayout.Width(10));
				GUILayout.FlexibleSpace();
				GUILayout.EndVertical();

				GUILayout.Label(EditorGUIUtility.IconContent("console.warnicon"), GUILayout.ExpandWidth(false));
				GUILayout.Label(graph.meshesUnreadableAtRuntime.Count + " 个网格在独立构建中将被忽略，因为它们不可读。如果计划在独立构建中扫描图，所有包含的网格必须在导入设置中标记为可读/写。", EditorStyles.wordWrappedMiniLabel);
				// EditorGUI.DrawTextureTransparent() EditorGUIUtility.IconContent("console.warnicon")
				GUILayout.EndHorizontal();

				if (meshesUnreadableAtRuntimeFoldout) {
					EditorGUILayout.Separator();
					for (int i = 0; i < graph.meshesUnreadableAtRuntime.Count; i++) {
						var(source, mesh) = graph.meshesUnreadableAtRuntime[i];
						if (!mesh.isReadable) {
							GUILayout.BeginHorizontal();
							EditorGUI.BeginDisabledGroup(true);
							EditorGUILayout.ObjectField(source, typeof(Mesh), true);
							EditorGUILayout.ObjectField(mesh, typeof(Mesh), false);
							EditorGUI.EndDisabledGroup();
							if (GUILayout.Button("设为可读（Make readable）")) {
								var importer = ModelImporter.GetAtPath(AssetDatabase.GetAssetPath(mesh)) as ModelImporter;
								if (importer != null) {
									importer.isReadable = true;
									importer.SaveAndReimport();
								}
							}
							GUILayout.EndHorizontal();
						}
					}
				}
				EditorGUILayout.EndVertical();
				GUILayout.EndHorizontal();
			}

			Separator();
			Header("Runtime Settings");

			graph.enableNavmeshCutting = EditorGUILayout.Toggle(new GUIContent("受Navmesh剪切影响", "使此图受 NavmeshCut 和 NavmeshAdd 组件影响。详见文档。"), graph.enableNavmeshCutting);

			Separator();
			Header("Debug");
			GUILayout.BeginHorizontal();
			GUILayout.Space(18);
			graph.showMeshSurface = GUILayout.Toggle(graph.showMeshSurface, new GUIContent("显示表面", "切换网格表面Gizmos显示"), EditorStyles.miniButtonLeft);
			graph.showMeshOutline = GUILayout.Toggle(graph.showMeshOutline, new GUIContent("显示轮廓", "切换节点轮廓Gizmos显示"), EditorStyles.miniButtonMid);
			graph.showNodeConnections = GUILayout.Toggle(graph.showNodeConnections, new GUIContent("显示连接", "切换节点连接Gizmos显示"), EditorStyles.miniButtonRight);
			GUILayout.EndHorizontal();


			Separator();
			Header("高级");

			graph.relevantGraphSurfaceMode = (RecastGraph.RelevantGraphSurfaceMode)EditorGUILayout.EnumPopup(new GUIContent("相关图表面模式（Relevant Graph Surface Mode）",
					"要求每个区域内部有 RelevantGraphSurface 组件。\n" +
					"场景中的 RelevantGraphSurface 组件指定该导航网格区域应包含在导航网格中。\n\n" +
					"如果设置为 OnlyForCompletelyInsideTile\n" +
					"仅当区域内部有 RelevantGraphSurface 或紧邻瓦片边界时才包含该区域。可能会包含一些不想包含的小区域，但无需在每个瓦片放置组件。\n\n" +
					"如果设置为 RequireForAll\n" +
					"仅当区域内部有 RelevantGraphSurface 时才包含该区域。即便瓦片间导航网格连续，但瓦片是独立计算的，因此每个区域和瓦片都需组件。"),
				graph.relevantGraphSurfaceMode);

			#pragma warning disable 618
			if (graph.nearestSearchOnlyXZ) {
				graph.nearestSearchOnlyXZ = EditorGUILayout.Toggle(new GUIContent("仅在XZ空间搜索最近节点", "推荐用于单层环境。更快，但在多层环境中可能不准确。"), graph.nearestSearchOnlyXZ);

				EditorGUILayout.HelpBox("节点在XZ空间的全局开关已弃用。请改用 NNConstraint 设置。", MessageType.Warning);
			}
			#pragma warning restore 618

			if (GUILayout.Button("导出为 .obj 文件")) {
				editor.RunTask(() => ExportToFile(graph));
			}
		}

		static readonly Vector3[] handlePoints = new [] { new Vector3(-1, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 0, -1), new Vector3(0, 0, 1), new Vector3(0, 1, 0), new Vector3(0, -1, 0) };

		public override void OnSceneGUI (NavGraph target) {
			var graph = target as RecastGraph;

			Handles.matrix = Matrix4x4.identity;
			Handles.color = AstarColor.BoundsHandles;
			Handles.CapFunction cap = Handles.CylinderHandleCap;

			var center = graph.forcedBoundsCenter;
			Matrix4x4 matrix = Matrix4x4.TRS(center, Quaternion.Euler(graph.rotation), graph.forcedBoundsSize * 0.5f);

			if (Tools.current == Tool.Scale) {
				const float HandleScale = 0.1f;

				Vector3 mn = Vector3.zero;
				Vector3 mx = Vector3.zero;
				EditorGUI.BeginChangeCheck();
				for (int i = 0; i < handlePoints.Length; i++) {
					var ps = matrix.MultiplyPoint3x4(handlePoints[i]);
					Vector3 p = matrix.inverse.MultiplyPoint3x4(Handles.Slider(ps, ps - center, HandleScale*HandleUtility.GetHandleSize(ps), cap, 0));

					if (i == 0) {
						mn = mx = p;
					} else {
						mn = Vector3.Min(mn, p);
						mx = Vector3.Max(mx, p);
					}
				}

				if (EditorGUI.EndChangeCheck()) {
					graph.forcedBoundsCenter = matrix.MultiplyPoint3x4((mn + mx) * 0.5f);
					graph.forcedBoundsSize = Vector3.Scale(graph.forcedBoundsSize, (mx - mn) * 0.5f);
				}
			} else if (Tools.current == Tool.Move) {
				EditorGUI.BeginChangeCheck();
				center = Handles.PositionHandle(center, Tools.pivotRotation == PivotRotation.Global ? Quaternion.identity : Quaternion.Euler(graph.rotation));

				if (EditorGUI.EndChangeCheck() && Tools.viewTool != ViewTool.Orbit) {
					graph.forcedBoundsCenter = center;
				}
			} else if (Tools.current == Tool.Rotate) {
				EditorGUI.BeginChangeCheck();
				var rot = Handles.RotationHandle(Quaternion.Euler(graph.rotation), graph.forcedBoundsCenter);

				if (EditorGUI.EndChangeCheck() && Tools.viewTool != ViewTool.Orbit) {
					graph.rotation = rot.eulerAngles;
				}
			}
		}

		/// <summary>Exports the INavmesh graph to a .obj file</summary>
		public static void ExportToFile (NavmeshBase target) {
			if (target == null) return;

			NavmeshTile[] tiles = target.GetTiles();

			if (tiles == null) {
				if (EditorUtility.DisplayDialog("导出前扫描图？", "图中没有网格数据。是否要扫描？", "确定", "取消")) {
					AstarPathEditor.MenuScan();
					tiles = target.GetTiles();
					if (tiles == null) return;
				} else {
					return;
				}
			}

			string path = EditorUtility.SaveFilePanel("导出 .obj", "", "navmesh.obj", "obj");
			if (path == "") return;

			//Generate .obj
			var sb = new System.Text.StringBuilder();

			string name = System.IO.Path.GetFileNameWithoutExtension(path);

			sb.Append("g ").Append(name).AppendLine();

			//Vertices start from 1
			int vCount = 1;

			//Define single texture coordinate to zero
			sb.Append("vt 0 0\n");

			for (int t = 0; t < tiles.Length; t++) {
				NavmeshTile tile = tiles[t];

				if (tile == null) continue;

				var vertices = tile.verts;

				//Write vertices
				for (int i = 0; i < vertices.Length; i++) {
					var v = (Vector3)vertices[i];
					sb.Append(string.Format("v {0} {1} {2}\n", -v.x, v.y, v.z));
				}

				//Write triangles
				TriangleMeshNode[] nodes = tile.nodes;
				for (int i = 0; i < nodes.Length; i++) {
					TriangleMeshNode node = nodes[i];
					if (node == null) {
						Debug.LogError("节点为空或非 TriangleMeshNode，严重错误。图类型 " + target.GetType().Name);
						return;
					}
					if (node.GetVertexArrayIndex(0) < 0 || node.GetVertexArrayIndex(0) >= vertices.Length) throw new System.Exception("ERR");

					sb.Append(string.Format("f {0}/1 {1}/1 {2}/1\n", (node.GetVertexArrayIndex(0) + vCount), (node.GetVertexArrayIndex(1) + vCount), (node.GetVertexArrayIndex(2) + vCount)));
				}

				vCount += vertices.Length;
			}

			string obj = sb.ToString();

			using (var sw = new System.IO.StreamWriter(path)) {
				sw.Write(obj);
			}
		}
	}
}
