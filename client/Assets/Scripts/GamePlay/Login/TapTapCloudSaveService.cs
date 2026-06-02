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
    /// ⚠ **SDK 字段名待确认**:下方 <see cref="Convert"/> 等处用到的 ArchiveData / ArchiveMetadata 属性名
    /// (Uuid / FileId / Name / Summary 等)按文档推断,导入 SDK 后请对照实际类型校正(已用注释标出)。
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

        public void Upload(Action<bool, string> onComplete)
        {
#if TAPTAP_CLOUDSAVE
            if (login == null || !login.IsLoggedIn) { onComplete?.Invoke(false, "未登录,无法云存档"); return; }
            int slot = store != null ? store.ActiveSlot : 0;
            if (slot <= 0) { onComplete?.Invoke(false, "无激活存档槽,无法上传"); return; }
            // 先把内存进度落盘,再打包上传,确保上传的是最新进度。
            store.SaveAll(() => UploadAsync(slot, onComplete));
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

        private async void UploadAsync(int slot, Action<bool, string> cb)
        {
            try
            {
                string path = CloudArchivePacker.PackSlot(slot, out int fileCount);
                if (fileCount == 0) { cb?.Invoke(false, "该存档槽暂无进度可上传"); return; }

                string name = CloudArchivePacker.ArchiveName(slot);
                // ArchiveMetadata(归档名[英文数字下划线连字符], 描述[非空], 附加信息, 游玩时长秒)
                var meta = new ArchiveMetadata(name, $"存档槽 {slot}", string.Empty, 0);

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

        // ⚠⚠ 以下取值按文档推断,导入 SDK 后按实际 ArchiveData / ArchiveMetadata 字段名校正 ⚠⚠
        private static string GetUuid(ArchiveData a) => a.Uuid;            // 归档唯一 id
        private static string GetName(ArchiveData a) => a.Metadata?.ArchiveName; // 归档名
        private static string GetFileId(ArchiveData a) => a.FileId;        // 归档文件 id(下载用)

        private static CloudArchiveInfo Convert(ArchiveData a)
        {
            string name = GetName(a);
            return new CloudArchiveInfo
            {
                uuid = GetUuid(a),
                fileId = GetFileId(a),
                name = name,
                summary = a.Metadata?.ArchiveSummary,
                slotId = CloudArchivePacker.ParseSlotId(name),
                savedUnix = 0,        // 如 SDK 有更新时间字段,在此填入(展示/对比新旧用)
                playtimeSeconds = 0,  // 同上,可取 a.Metadata?.ArchivePlaytime
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
