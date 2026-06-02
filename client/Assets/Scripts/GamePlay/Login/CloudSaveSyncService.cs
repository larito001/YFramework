using UnityEngine;

namespace YOTO
{
    /// <summary>
    /// 云存档自动同步(B 方案)。把本地存档与 TapTap 云自动打通,业务侧无需手动调上传/下载:
    ///   · 登录成功后(<see cref="ILoginService.LoggedIn"/>):拉云端列表,若当前存档槽的云归档比本地新,自动下载还原;
    ///   · 任意"进度存档"落盘后(<see cref="StoreMgr.ProgressSaved"/>):防抖合并后自动上传当前槽。
    ///
    /// **防抖**:TapTap 云存档创建/更新限 60 次/分钟,且一个存档点常短时间多次写盘(货币+背包+任务…),
    /// 故把 <see cref="UploadDebounceSeconds"/> 秒内的多次写盘合并成一次上传——既"存了就同步",又不触限。
    /// 想更即时就调小该常量(但别小到让一连串写盘各发一次请求)。
    ///
    /// **防自触发死循环**:上传内部会先 SaveAll(取最新进度),那又会触发 ProgressSaved;用 <see cref="uploading"/>
    /// 标记在上传期间忽略这些回声,避免"上传→存档→再上传"无限循环。
    /// </summary>
    public class CloudSaveSyncService : IGameService, ITickable
    {
        private const float UploadDebounceSeconds = 3f; // 写盘后合并窗口(秒)

        private ILoginService login;
        private ICloudSaveService cloud;
        private StoreMgr store;

        private bool dirty;     // 有待上传的本地变更
        private bool uploading; // 正在上传/还原(期间忽略 ProgressSaved 回声)
        private float timer;    // 防抖倒计时

        public void Init(GameContext ctx)
        {
            ctx.TryGet<ILoginService>(out login);
            ctx.TryGet<ICloudSaveService>(out cloud);
            store = ctx.Get<StoreMgr>();

            if (login != null) login.LoggedIn += OnLoggedIn;
            if (store != null) store.ProgressSaved += OnProgressSaved;
        }

        public void Shutdown()
        {
            if (login != null) login.LoggedIn -= OnLoggedIn;
            if (store != null) store.ProgressSaved -= OnProgressSaved;
            login = null;
            cloud = null;
            store = null;
        }

        // ---------------- 登录后:云端较新则下载 ----------------

        private void OnLoggedIn(LoginAccount account)
        {
            if (cloud == null || !cloud.IsAvailable || store == null) return;
            int slot = store.ActiveSlot;
            if (slot <= 0) return;

            cloud.GetList((ok, list) =>
            {
                if (!ok || list == null) return;

                CloudArchiveInfo match = null;
                for (int i = 0; i < list.Count; i++)
                    if (list[i].slotId == slot) { match = list[i]; break; }
                if (match == null) return; // 云端没有当前槽的存档,什么都不做(保留本地)

                long localUnix = LocalSlotTime(slot);
                long cloudUnix = NormalizeUnix(match.savedUnix);
                if (cloudUnix <= localUnix)
                {
                    Debug.Log($"[CloudSaveSync] 本地不旧于云端(local={localUnix} cloud={cloudUnix}),保留本地。");
                    return;
                }

                Debug.Log($"[CloudSaveSync] 云端更新(cloud={cloudUnix} > local={localUnix}),自动下载还原。");
                uploading = true; // 还原期间屏蔽上传
                cloud.Download(match, (dok, err) =>
                {
                    uploading = false;
                    dirty = false; // 刚还原的就是云端版本,无需立刻回传
                    if (!dok) Debug.LogWarning($"[CloudSaveSync] 云存档下载失败:{err}");
                });
            });
        }

        // ---------------- 进度落盘后:防抖上传 ----------------

        private void OnProgressSaved()
        {
            if (uploading) return; // 上传/还原自身引发的写盘回声,忽略
            dirty = true;
            timer = UploadDebounceSeconds;
        }

        public void Tick(float dt)
        {
            if (!dirty || uploading) return;
            if (cloud == null || !cloud.IsAvailable) { dirty = false; return; } // 未登录/未接入:不上传
            timer -= dt;
            if (timer > 0f) return;

            uploading = true;
            dirty = false;
            cloud.Upload((ok, err) =>
            {
                uploading = false;
                if (!ok) Debug.LogWarning($"[CloudSaveSync] 云存档上传失败:{err}");
            });
        }

        // ---------------- 工具 ----------------

        /// <summary>本地激活槽的最后游玩/写档时间(Unix 秒);取不到为 0。</summary>
        private long LocalSlotTime(int slot)
        {
            var slots = store.Slots;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].id == slot) return slots[i].lastPlayedUnix;
            return 0;
        }

        /// <summary>把可能为毫秒的时间戳归一到秒(&gt;1e12 视为毫秒)。云端 ModifiedTime 单位未知时的兜底。</summary>
        private static long NormalizeUnix(long t) => t > 1000000000000L ? t / 1000L : t;
    }
}
