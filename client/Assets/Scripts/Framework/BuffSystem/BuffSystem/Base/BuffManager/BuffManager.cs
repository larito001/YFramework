using NoSLoofah.BuffSystem.Dependence;
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace NoSLoofah.BuffSystem.Manager
{
    /// <summary>
    /// Buff������������ģʽ��
    /// ����ͨ����Ż�ȡBuff����
    /// </summary>
    public class BuffManager : MonoSingleton<BuffManager>, IBuffManager
    {
        //public static readonly string SO_PATH = "Assets/NoSLoofah_BuffSystem/BuffSystem/Data/BuffData";    //����Data��·��
        [HideInInspector][SerializeField] private BuffCollection collection;
        private IBuffTagManager tagManager;
        public bool IsWorking => collection != null;

        public IBuffTagManager TagManager => tagManager;
        public void SetData(BuffCollection buffCollection)
        {
            this.collection = buffCollection;
        }

        protected override void Awake()
        {
            base.Awake();
            if (collection == null) Debug.LogError("BuffCollection���ݶ�ʧ");
        }
        public IBuff GetBuff(int id)
        {
            if (id < 0 || id >= collection.Size)
            {
                throw new System.Exception("ʹ�÷Ƿ���Buff id��" + id + " (��ǰBuff����Ϊ" + collection.Size + ")");
            }
            if (collection.buffList[id] == null) throw new System.Exception("���õ�BuffΪnull��id��" + id);
            return collection.buffList[id].Clone();
        }

        public void RegisterBuffTagManager(IBuffTagManager mgr)
        {
            tagManager = mgr;
        }
    }
}