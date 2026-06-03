using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if TAPTAP_CLOUDSAVE
using System.Threading.Tasks;
using TapSDK.CloudSave;
#endif

namespace YOTO
{
    /// <summary>
    /// TapTap 云存档接入(实现 <see cref="ICloudSaveService"/>)。文档:
    /// https://developer.taptap.cn/docs/sdk/tap-cloudsave/features/ 与 .../guide/。
    ///
    /// 依赖:登录(<see cref="ILoginService"/>,云存档需先登录)+ 本地存档(<see cref="StoreMgr"/>)。
    /// 一个云归档 = 一个本地存档槽:上传打包当前激活槽的进度文件,下载还原到对应槽。
    ///
    /// **编译开关 `TAPTAP_CLOUDSAVE`**:所有 SDK 调用都在宏内——未导入 SDK / 未开宏时项目照常编译:
    ///   · Editor 走"模拟成功(本地打包但不真上传)"便于联调上层流程;
    ///   · 真机(未开宏)走"未接入"回退。
    ///
    /// 字段名已对照 SDK 4.10.3 实际类型(<c>ArchiveData</c> 平铺 Uuid/FileId/Name/Summary/Playtime/ModifiedTime;
    /// <c>ArchiveMetadata(name, summary, extra, playtime)</c>)。
    /// </summary>
    public class TapTapCloudSaveService : ICloudSaveService, IGameService
    {
        private StoreMgr store;
        private ILoginService login;

        public void Init(GameContext ctx)
        {
            store = ctx.Get<StoreMgr>();
            ctx.TryGet<ILoginService>(out login);
#if TAPTAP_CLOUDSAVE
            // 注册云存档全局回调(如 300001 需登录 / 300002 初始化失败),便于统一处理与日志。
            TapTapCloudSave.RegisterCloudSaveCallback(new Callback());
#endif
        }

        public void Shutdown()
        {
            store = null;
            login = null;
        }

        public bool IsAvailable
        {
            get
            {
#if TAPTAP_CLOUDSAVE
                return login != null && login.IsLoggedIn;
#else
                return false;
#endif
            }
        }

        // ---------------- 上传 ----------------

        public void Upload(CloudSaveMeta meta, Action<bool, string> onComplete)
        {
#if TAPTAP_CLOUDSAVE
            if (login == null || !login.IsLoggedIn) { onComplete?.Invoke(false, "未登录,无法云存档"); return; }
            int slot = store != null ? store.ActiveSlot : 0;
            if (slot <= 0) { onComplete?.Invoke(false, "无激活存档槽,无法上传"); return; }
            // 先把内存进度落盘,再打包上传,确保上传的是最新进度。
            store.SaveAll(() => UploadAsync(slot, meta ?? new CloudSaveMeta(), onComplete));
#elif UNITY_EDITOR
            int slot = store != null ? store.ActiveSlot : 0;
            if (slot > 0) { CloudArchivePacker.PackSlot(slot, out var n); Debug.Log($"[CloudSave] (编辑器模拟) 打包槽 {slot} 共 {n} 个文件,模拟上传成功(未接入 SDK)。"); }
            onComplete?.Invoke(slot > 0, slot > 0 ? null : "无激活存档槽");
#else
            onComplete?.Invoke(false, "云存档未接入");
#endif
        }

        // ---------------- 列表 ----------------

        public void GetList(Action<bool, List<CloudArchiveInfo>> onComplete)
        {
#if TAPTAP_CLOUDSAVE
            if (login == null || !login.IsLoggedIn) { onComplete?.Invoke(false, null); return; }
            GetListAsync(onComplete);
#elif UNITY_EDITOR
            Debug.Log("[CloudSave] (编辑器模拟) 云归档列表为空(未接入 SDK)。");
            onComplete?.Invoke(true, new List<CloudArchiveInfo>());
#else
            onComplete?.Invoke(false, null);
#endif
        }

        // ---------------- 下载还原 ----------------

        public void Download(CloudArchiveInfo archive, Action<bool, string> onComplete)
        {
            if (archive == null) { onComplete?.Invoke(false, "归档为空"); return; }
#if TAPTAP_CLOUDSAVE
            if (login == null || !login.IsLoggedIn) { onComplete?.Invoke(false, "未登录"); return; }
            DownloadAsync(archive, onComplete);
#elif UNITY_EDITOR
            Debug.Log("[CloudSave] (编辑器模拟) 下载还原(未接入 SDK,无实际数据)。");
            onComplete?.Invoke(false, "云存档未接入(编辑器模拟无数据)");
#else
            onComplete?.Invoke(false, "云存档未接入");
#endif
        }

        // ---------------- 删除 ----------------

        public void Delete(CloudArchiveInfo archive, Action<bool, string> onComplete)
        {
            if (archive == null || string.IsNullOrEmpty(archive.uuid)) { onComplete?.Invoke(false, "归档为空"); return; }
#if TAPTAP_CLOUDSAVE
            if (login == null || !login.IsLoggedIn) { onComplete?.Invoke(false, "未登录"); return; }
            DeleteAsync(archive.uuid, onComplete);
#else
            onComplete?.Invoke(false, "云存档未接入");
#endif
        }

#if TAPTAP_CLOUDSAVE
        // async void:把 SDK 的 Task API 收进内部,对外仍是 fire-and-forget + 回调(不泄漏 Task)。

        private async void UploadAsync(int slot, CloudSaveMeta saveMeta, Action<bool, string> cb)
        {
            try
            {
                string path = CloudArchivePacker.PackSlot(slot, out int fileCount);
                if (fileCount == 0) { cb?.Invoke(false, "该存档槽暂无进度可上传"); return; }

                string name = CloudArchivePacker.ArchiveName(slot);
                // 把对账元数据(版本号 + 展示快照)序列化进 extra;playtime 单独占 SDK 的 playtime 字段。
                string extra = JsonUtility.ToJson(saveMeta);
                // ArchiveMetadata(归档名[英文数字下划线连字符], 描述[非空], 附加信息(extra), 游玩时长秒)
                var meta = new ArchiveMetadata(name, $"存档槽 {slot}", extra, (int)saveMeta.playtimeSeconds);

                // 已有同名归档则更新,否则新建(避免重复创建多份)。
                var existing = await FindArchiveByName(name);
                if (existing != null)
                    await TapTapCloudSave.UpdateArchive(GetUuid(existing), meta, path, null); // ⚠ 确认 UpdateArchive 签名/Uuid 取法
                else
                    await TapTapCloudSave.CreateArchive(meta, path, null);                    // 封面传 null(可选,≤512KB)

                cb?.Invoke(true, null);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CloudSave] 上传失败: {e}");
                cb?.Invoke(false, e.Message);
            }
        }

        private async void GetListAsync(Action<bool, List<CloudArchiveInfo>> cb)
        {
            try
            {
                List<ArchiveData> list = await TapTapCloudSave.GetArchiveList();
                var result = new List<CloudArchiveInfo>();
                if (list != null)
                    foreach (var a in list) result.Add(Convert(a));
                cb?.Invoke(true, result);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CloudSave] 获取列表失败: {e}");
                cb?.Invoke(false, null);
            }
        }

        private async void DownloadAsync(CloudArchiveInfo archive, Action<bool, string> cb)
        {
            try
            {
                byte[] data = await TapTapCloudSave.GetArchiveData(archive.uuid, archive.fileId);
                int n = CloudArchivePacker.UnpackBytes(data);
                if (n == 0) { cb?.Invoke(false, "归档数据为空"); return; }

                int slot = archive.slotId > 0 ? archive.slotId : CloudArchivePacker.ParseSlotId(archive.name);
                if (slot > 0) store.SetActiveSlot(slot); // 切到该槽再读,使写回的 slot{id}_ 文件被读回内存
                store.LoadAll(() => cb?.Invoke(true, null));
            }
            catch (Exception e)
            {
                Debug.LogError($"[CloudSave] 下载还原失败: {e}");
                cb?.Invoke(false, e.Message);
            }
        }

        private async void DeleteAsync(string uuid, Action<bool, string> cb)
        {
            try
            {
                await TapTapCloudSave.DeleteArchive(uuid);
                cb?.Invoke(true, null);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CloudSave] 删除失败: {e}");
                cb?.Invoke(false, e.Message);
            }
        }

        private async Task<ArchiveData> FindArchiveByName(string name)
        {
            var list = await TapTapCloudSave.GetArchiveList();
            if (list != null)
                foreach (var a in list)
                    if (GetName(a) == name) return a;
            return null;
        }

        // 字段名已对照 SDK 4.10.3 的 ArchiveData(Runtime/Public/ArchiveData.cs):name/summary/playtime/file_id 等均平铺在 ArchiveData 上。
        private static string GetUuid(ArchiveData a) => a.Uuid;     // 归档唯一 id(更新时用)
        private static string GetName(ArchiveData a) => a.Name;     // 归档名(按 slot_{id} 匹配本地槽)

        private static CloudArchiveInfo Convert(ArchiveData a)
        {
            // 解析 extra 里的对账元数据(版本号 + 展示快照)。旧归档 extra 为空/非法 → version 视为 0(走云端优先迁移)。
            CloudSaveMeta sm = null;
            if (!string.IsNullOrEmpty(a.Extra))
            {
                try { sm = JsonUtility.FromJson<CloudSaveMeta>(a.Extra); }
                catch (Exception e) { Debug.LogWarning($"[CloudSave] 归档 extra 解析失败,按 version=0 处理:{e.Message}"); }
            }
            return new CloudArchiveInfo
            {
                uuid = a.Uuid,
                fileId = a.FileId,
                name = a.Name,
                summary = a.Summary,
                slotId = CloudArchivePacker.ParseSlotId(a.Name),
                savedUnix = a.ModifiedTime,    // 云端最后修改时间(展示用)
                playtimeSeconds = sm != null ? sm.playtimeSeconds : a.Playtime,
                version = sm != null ? sm.version : 0,
                desc = sm != null ? sm.desc : null,
            };
        }

        /// <summary>云存档全局回调:统一日志(300001 需登录 / 300002 初始化失败 等)。</summary>
        private sealed class Callback : ITapCloudSaveCallback
        {
            public void OnResult(int resultCode)
            {
                Debug.Log($"[CloudSave] 回调 resultCode={resultCode}");
            }
        }
#endif
    }
}
