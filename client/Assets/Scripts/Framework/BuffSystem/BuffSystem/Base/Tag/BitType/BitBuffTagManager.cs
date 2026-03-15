using UnityEngine;

namespace NoSLoofah.BuffSystem
{
    /// <summary>
    /// Bit-mask based buff-tag rule evaluator.
    /// </summary>
    public class BitBuffTagManager : BuffTagManager
    {
        [HideInInspector]
        [SerializeField]
        private BitBuffTagData tagData;

        public override void Init(BuffTagData data)
        {
            tagData = data as BitBuffTagData;
        }

        public override bool IsTagRemoveOther(BuffTag tag, BuffTag other)
        {
            if (tag == 0) return false;
            if (tag < 0) throw new System.Exception("Negative buff tags are not supported.");

            int index = BitBuffTagData.GetIndex(tag);
            return ((int)tagData.RemovedTags[index] & (int)other) > 0;
        }

        public override bool IsTagCanAddWhenHaveOther(BuffTag tag, BuffTag other)
        {
            if (tag == 0) return false;
            if (tag < 0) throw new System.Exception("Negative buff tags are not supported.");

            int index = BitBuffTagData.GetIndex(tag);
            return ((int)tagData.BlockTags[index] & (int)other) > 0;
        }
    }
}
