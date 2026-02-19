using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace NoSLoofah.BuffSystem
{
    /// <summary>
    /// BuffTag�������Ľӿڣ������ж�Tag֮��Ļ����ϵ
    /// </summary>
    public interface IBuffTagManager
    {
        public void Init(BuffTagData data);
        /// <summary>
        /// ���Ӵ���tag��Buffʱ���Ƿ���Ƴ�TagΪother������Buff
        /// </summary>
        /// <param name="tag">������Buff��tag</param>
        /// <param name="other">����Buff��Tag</param>
        /// <returns></returns>
        public bool IsTagRemoveOther(BuffTag tag, BuffTag other);
        /// <summary>
        /// ���Ӵ���tag��Buffʱ���Ƿ�ᱻTagΪother������Buff����
        /// </summary>
        /// <param name="tag">������Buff��tag</param>
        /// <param name="other">����Buff��Tag</param>
        /// <returns></returns>
        public bool IsTagCanAddWhenHaveOther(BuffTag tag, BuffTag other);
    }
}