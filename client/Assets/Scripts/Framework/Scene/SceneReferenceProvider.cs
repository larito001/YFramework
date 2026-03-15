using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SceneReferenceProvider : MonoBehaviour
{
    [Serializable]
    public struct Entry
    {
        public string key;
        public Transform target;
    }

    [SerializeField] private List<Entry> references = new List<Entry>();

    public IReadOnlyList<Entry> References => references;
}
