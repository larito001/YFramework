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
        public long savedUnix;         // 云端最后保存时间(Unix 秒,展示/对比新旧用)
        public long playtimeSeconds;   // 记录的游玩时长(秒)
    }
}
