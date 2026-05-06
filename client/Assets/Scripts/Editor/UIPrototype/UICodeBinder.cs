using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class UICodeBinder
{
    public struct BindResult
    {
        public int bound;
        public int unbound;
    }

    public static BindResult AttachAndBind(string prefabAssetPath, string scriptAssetPath)
    {
        BindResult result = default;

        MonoScript monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptAssetPath);
        if (monoScript == null)
        {
            Debug.LogError("[UICodeBinder] 脚本资源不存在: " + scriptAssetPath);
            return result;
        }

        Type type = monoScript.GetClass();
        if (type == null)
        {
            Debug.LogWarning("[UICodeBinder] 类尚未编译完成: " + scriptAssetPath);
            return result;
        }

        GameObject prefab = PrefabUtility.LoadPrefabContents(prefabAssetPath);
        try
        {
            Component comp = prefab.GetComponent(type);
            if (comp == null)
            {
                comp = prefab.AddComponent(type);
            }

            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            SerializedObject so = new SerializedObject(comp);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (!typeof(Component).IsAssignableFrom(field.FieldType))
                {
                    continue;
                }

                Transform child = FindChildByName(prefab.transform, field.Name);
                if (child == null)
                {
                    Debug.LogWarning("[UICodeBinder] 找不到与字段同名的子节点: " + field.Name);
                    result.unbound++;
                    continue;
                }

                Component target = child.GetComponent(field.FieldType);
                if (target == null)
                {
                    Debug.LogWarning("[UICodeBinder] 子节点 " + field.Name + " 上缺少 " + field.FieldType.Name + " 组件");
                    result.unbound++;
                    continue;
                }

                SerializedProperty prop = so.FindProperty(field.Name);
                if (prop == null)
                {
                    result.unbound++;
                    continue;
                }

                prop.objectReferenceValue = target;
                result.bound++;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(prefab, prefabAssetPath);
            Debug.Log("[UICodeBinder] " + prefabAssetPath + " ← " + Path.GetFileName(scriptAssetPath) + "：绑定 " + result.bound + " 个字段，未绑定 " + result.unbound + " 个");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefab);
        }

        return result;
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        if (root.name == name)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }
}
