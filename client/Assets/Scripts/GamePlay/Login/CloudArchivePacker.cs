using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace YOTO
{
    /// <summary>
    /// 云归档打包/解包(纯本地 IO,不依赖任何 SDK)。把 <see cref="StoreMgr"/> 某个存档槽的进度文件
    /// (<c>persistentDataPath/slot{id}_*.json</c>)打成一个归档文件上传;下载后再写回这些文件。
    ///
    /// 归档文件是一个 JSON(文件名 → 内容 的列表),单文件须 ≤10MB(TapTap 云存档限制)。
    /// 归档名约定 <c>slot_{slotId}</c>,便于从云端列表反查对应本地槽。
    /// </summary>
    public static class CloudArchivePacker
    {
        /// <summary>云归档名 ↔ 本地槽 id 的约定前缀。</summary>
        private const string NamePrefix = "slot_";

        /// <summary>本地某槽进度文件的文件名前缀(与 <see cref="StoreMgr"/> 的 SlotKey 格式一致)。</summary>
        private static string FilePrefix(int slotId) => $"slot{slotId}_";

        /// <summary>云归档名(英文/数字/下划线/连字符)。</summary>
        public static string ArchiveName(int slotId) => NamePrefix + slotId;

        /// <summary>从云归档名解析本地槽 id;不匹配返回 0。</summary>
        public static int ParseSlotId(string archiveName)
        {
            if (string.IsNullOrEmpty(archiveName) || !archiveName.StartsWith(NamePrefix)) return 0;
            return int.TryParse(archiveName.Substring(NamePrefix.Length), out var id) ? id : 0;
        }

        /// <summary>
        /// 打包某槽的全部进度文件为一个归档文件,返回归档文件路径(写在 persistentDataPath 下)。
        /// <paramref name="fileCount"/> 回传打包的文件数(0 表示该槽尚无任何进度文件)。
        /// </summary>
        public static string PackSlot(int slotId, out int fileCount)
        {
            fileCount = 0;
            string dir = Application.persistentDataPath;
            var blob = new ArchiveBlob { slotId = slotId };

            string prefix = FilePrefix(slotId);
            foreach (var file in Directory.GetFiles(dir, prefix + "*.json"))
            {
                blob.files.Add(new ArchiveBlob.Entry
                {
                    name = Path.GetFileName(file),
                    content = File.ReadAllText(file, Encoding.UTF8),
                });
            }
            fileCount = blob.files.Count;

            string outPath = Path.Combine(dir, $"__cloud_pack_slot{slotId}.dat");
            File.WriteAllText(outPath, JsonUtility.ToJson(blob), Encoding.UTF8);
            return outPath;
        }

        /// <summary>把归档文件解包写回 persistentDataPath(覆盖同名文件)。返回写回的文件数。</summary>
        public static int Unpack(string archiveFilePath)
        {
            if (string.IsNullOrEmpty(archiveFilePath) || !File.Exists(archiveFilePath)) return 0;
            var blob = JsonUtility.FromJson<ArchiveBlob>(File.ReadAllText(archiveFilePath, Encoding.UTF8));
            return UnpackBlob(blob);
        }

        /// <summary>把归档字节(SDK 下载得到的 byte[])解包写回。返回写回的文件数。</summary>
        public static int UnpackBytes(byte[] data)
        {
            if (data == null || data.Length == 0) return 0;
            var blob = JsonUtility.FromJson<ArchiveBlob>(Encoding.UTF8.GetString(data));
            return UnpackBlob(blob);
        }

        private static int UnpackBlob(ArchiveBlob blob)
        {
            if (blob?.files == null) return 0;
            string dir = Application.persistentDataPath;
            int n = 0;
            foreach (var e in blob.files)
            {
                if (string.IsNullOrEmpty(e.name)) continue;
                // 只允许写回纯文件名(防归档内构造路径穿越);忽略带目录分隔符的条目。
                if (e.name.IndexOf('/') >= 0 || e.name.IndexOf('\\') >= 0) continue;
                File.WriteAllText(Path.Combine(dir, e.name), e.content ?? string.Empty, Encoding.UTF8);
                n++;
            }
            return n;
        }

        [Serializable]
        private class ArchiveBlob
        {
            public int slotId;
            public List<Entry> files = new List<Entry>();

            [Serializable]
            public class Entry
            {
                public string name;    // 文件名(含 slot{id}_ 前缀与 .json 后缀)
                public string content; // 文件文本内容
            }
        }
    }
}
