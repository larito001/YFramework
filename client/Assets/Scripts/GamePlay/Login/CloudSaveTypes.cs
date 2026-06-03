using System;

namespace YOTO
{
    /// <summary>
    /// 云归档信息(渠道无关视图)。一个归档对应一个本地存档槽(归档名 = <c>slot_{slotId}</c>)。
    /// 由 <see cref="ICloudSaveService.GetList"/> 返回,下载时把 <see cref="uuid"/>+<see cref="fileId"/> 传回 SDK。
    /// </summary>
    public class CloudArchiveInfo
    {
        public string uuid;            // 云归档唯一 id(更新/删除/下载用)
        public string fileId;          // 归档文件 id(下载归档数据用)
        public string name;            // 归档名(slot_{slotId})
        public string summary;         // 归档描述(展示用)
        public int slotId;             // 解析自 name 的本地存档槽 id(下载还原时激活此槽)
        public long savedUnix;         // 云端最后保存时间(Unix 秒,展示用)
        public long playtimeSeconds;   // 记录的游玩时长(秒)

        // —— 主流"版本号对账"用(解析自归档 extra 字段,见 CloudSaveMeta)——
        public long version;           // 云端存档代数(对账核心:与本地 version/lastSyncedVersion 比较定新旧/冲突)
        public string desc;            // 上传时的进度快照文本(如"金币 1200",冲突弹窗展示用;旧归档为空)
    }

    /// <summary>
    /// 写入云归档 <c>extra</c> 字段的对账元数据(JSON)。主流云存档(Steam/Google Play 存档)不信设备时钟,
    /// 用单调递增的 <see cref="version"/> 判新旧/冲突;<see cref="desc"/>/<see cref="playtimeSeconds"/> 仅供冲突弹窗展示。
    /// 旧版本上传的归档无此字段(extra 为空),解析失败时 version 视为 0 → 走"云端优先"迁移路径。
    /// </summary>
    [Serializable]
    public class CloudSaveMeta
    {
        public long version;          // 存档代数(对账核心)
        public string desc;           // 进度快照文本(展示用)
        public long playtimeSeconds;  // 游玩时长秒(展示用)
    }
}
