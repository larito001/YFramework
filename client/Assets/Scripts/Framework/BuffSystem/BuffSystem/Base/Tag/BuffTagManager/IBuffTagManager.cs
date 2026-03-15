using UnityEngine;

namespace NoSLoofah.BuffSystem
{
    /// <summary>
    /// Resolves relationship rules between buff tags.
    /// </summary>
    public interface IBuffTagManager
    {
        void Init(BuffTagData data);

        /// <summary>
        /// Returns true when applying <paramref name="tag"/> should remove
        /// an existing buff with <paramref name="other"/>.
        /// </summary>
        bool IsTagRemoveOther(BuffTag tag, BuffTag other);

        /// <summary>
        /// Returns true when applying <paramref name="tag"/> is blocked by
        /// an existing buff with <paramref name="other"/>.
        /// </summary>
        bool IsTagCanAddWhenHaveOther(BuffTag tag, BuffTag other);
    }
}
