using System;
using UnityEngine;

namespace YOTO
{
    /// <summary>
    /// 云存档自动同步(主流"版本号对账"方案,对标 Steam Cloud / Google Play 存档)。
    /// 把本地存档与 TapTap 云自动打通,业务侧无需手动调上传/下载。
    ///
    /// **不信设备时钟**:用每槽单调递增的 <see cref="SaveSlotInfo.version"/>(进度落盘 +1)判新旧,
    /// 配合 <see cref="SaveSlotInfo.lastSyncedVersion"/>(上次与云端同步到的代数)做四态对账:
    ///   · 两端都=已同步代数 → 一致,不动;
    ///   · 云端>已同步、本地=已同步 → 下载;
    ///   · 本地>已同步、云端=已同步 → 上传;
    ///   · 两端都>已同步(各玩各的)→ **冲突**,弹 <see cref="ConfirmPanel"/> 让玩家选「用云端/保留本地」;
    ///   · 本槽从未同步(lastSyncedVersion=0:全新安装/重装/首次升级迁移)→ 云端权威,直接下载。
    /// 这从结构上根治了"重装后本地种子时间戳压过云端→不还原+反向覆盖"的旧 bug(不再比时间戳)。
    ///
    /// **上传防抖**:TapTap 创建/更新限 60 次/分钟,一个存档点常短时间多次写盘,故把
    /// <see cref="UploadDebounceSeconds"/> 秒内的多次写盘合并成一次上传。
    /// **防自触发死循环 / 防对账期误传**:上传/还原/对账期间用 <see cref="uploading"/> 标记屏蔽 ProgressSaved 回声与防抖上传。
    /// </summary>
    public class CloudSaveSyncService : IGameService, ITickable
    {
        private const float UploadDebounceSeconds = 3f; // 写盘后合并窗口(秒)

        private ILoginService login;
        private ICloudSaveService cloud;
        private StoreMgr store;
        private UIMgr ui;

        /// <summary>进度快照文本提供器(展示用):冲突弹窗/上传 extra 里描述本地进度,如"金币 1200"。
        /// 由组装层(<see cref="GameBootstrapper"/>)注入以保持本服务与游戏系统解耦;为空则回退为"存档"。</summary>
        public Func<string> DescribeLocalProgress;

        private bool dirty;         // 有待上传的本地变更
        private bool uploading;     // 正在上传/还原/对账(期间屏蔽 ProgressSaved 回声与防抖上传)
        private bool suppressUpload; // 冲突但无法弹窗时的兜底:本会话不自动上传,避免误覆盖云端
        private bool localFreshThisBoot; // 本次启动是否新建了存档槽(全新安装/重装)。迁移期用于区分"本地是种子"还是"本地有旧存档"
        private float timer;        // 防抖倒计时

        /// <summary>由启动流程在"本地无任何存档、本次新建了种子槽"(全新安装/重装/清数据)时调用。
        /// 影响首次同步(lastSyncedVersion=0)的处置:种子槽→云端权威直接下载;否则(老玩家升级迁移)→弹窗让玩家选,不静默覆盖。</summary>
        public void MarkLocalFreshThisBoot() => localFreshThisBoot = true;

        public void Init(GameContext ctx)
        {
            ctx.TryGet<ILoginService>(out login);
            ctx.TryGet<ICloudSaveService>(out cloud);
            ctx.TryGet<UIMgr>(out ui);
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
            ui = null;
            DescribeLocalProgress = null;
        }

        // ---------------- 登录后:版本号四态对账 ----------------

        private void OnLoggedIn(LoginAccount account)
        {
            if (cloud == null || !cloud.IsAvailable || store == null) return;
            int slot = store.ActiveSlot;
            if (slot <= 0) return;

            // 对账期间先挡住自动上传:拉列表是异步、冲突还要等玩家点弹窗,期间防抖一旦到点会把本地抢传上去
            // 覆盖云端(旧 bug 的数据丢失链)。下载/上传/判定一致后再释放。
            uploading = true;

            // 取出并复位"本次启动新建种子槽"标记(一次性):仅首次同步(synced=0)时用于区分全新安装 vs 升级迁移。
            bool localFresh = localFreshThisBoot;
            localFreshThisBoot = false;

            cloud.GetList((ok, list) =>
            {
                if (!ok || list == null) { uploading = false; return; }

                CloudArchiveInfo match = null;
                for (int i = 0; i < list.Count; i++)
                    if (list[i].slotId == slot) { match = list[i]; break; }

                if (match == null)
                {
                    // 云端无该槽归档:全新玩家/本地独有。释放守卫,本地后续照常上传(会新建归档)。
                    uploading = false;
                    return;
                }

                long vCloud = match.version;
                long vLocal = store.GetSlotVersion(slot);
                long vSynced = store.GetSlotSyncedVersion(slot);

                if (vSynced == 0)
                {
                    // 本槽从未与云端同步(lastSyncedVersion=0)。区分两种情况避免误覆盖:
                    if (localFresh)
                    {
                        // 全新安装/重装:本地只是本次新建的种子 → 云端权威,直接下载还原。
                        Debug.Log($"[CloudSaveSync] 全新安装/重装,云端有归档→下载还原。cloud_v={vCloud}");
                        DoDownload(slot, match);
                    }
                    else
                    {
                        // 老玩家升级迁移:本地是旧版攒下的真存档(无版本号),云端也有 → 无法判新旧,交玩家选。
                        Debug.Log($"[CloudSaveSync] 升级迁移:本地有旧存档且云端有归档,弹窗由玩家选择。cloud_v={vCloud}");
                        ShowConflict(slot, match);
                    }
                    return;
                }
                if (vCloud == vSynced && vLocal == vSynced)
                {
                    Debug.Log("[CloudSaveSync] 本地与云端一致,无需同步。");
                    uploading = false;
                    return;
                }
                if (vCloud > vSynced && vLocal == vSynced)
                {
                    Debug.Log($"[CloudSaveSync] 云端更新(cloud_v={vCloud}>synced={vSynced}),下载还原。");
                    DoDownload(slot, match);
                    return;
                }
                if (vLocal > vSynced && vCloud == vSynced)
                {
                    Debug.Log($"[CloudSaveSync] 本地更新(local_v={vLocal}>synced={vSynced}),上传。");
                    DoUpload(slot);
                    return;
                }

                // 两端都比上次同步代数高(各设备独立推进)→ 真冲突,让玩家选(uploading 维持 true 等待选择)。
                Debug.Log($"[CloudSaveSync] 存档冲突 cloud_v={vCloud} local_v={vLocal} synced={vSynced},弹窗由玩家选择。");
                ShowConflict(slot, match);
            });
        }

        // ---------------- 进度落盘后:防抖上传 ----------------

        private void OnProgressSaved()
        {
            if (uploading) return; // 上传/还原/对账自身引发的写盘回声,忽略
            dirty = true;
            timer = UploadDebounceSeconds;
        }

        public void Tick(float dt)
        {
            if (!dirty || uploading || suppressUpload) return;
            if (cloud == null || !cloud.IsAvailable || store == null) { dirty = false; return; } // 未登录/未接入:不上传
            timer -= dt;
            if (timer > 0f) return;

            int slot = store.ActiveSlot;
            if (slot <= 0) { dirty = false; return; }
            DoUpload(slot);
        }

        // ---------------- 上传 / 下载(统一收口,负责对账记账) ----------------

        private void DoUpload(int slot)
        {
            uploading = true;
            long v = store.GetSlotVersion(slot); // 记下本次上传的代数,成功后据此推进 lastSyncedVersion
            cloud.Upload(BuildMeta(v), (ok, err) =>
            {
                if (ok) store.MarkSlotSynced(slot, v);
                else Debug.LogWarning($"[CloudSaveSync] 云存档上传失败:{err}");
                uploading = false;
                dirty = false;
            });
        }

        private void DoDownload(int slot, CloudArchiveInfo match)
        {
            uploading = true; // 还原期间屏蔽上传
            cloud.Download(match, (ok, err) =>
            {
                // 还原成功:本地数据已变成云端那份,version 与 lastSyncedVersion 都对齐到云端代数。
                if (ok) store.SetSlotSyncState(slot, match.version, match.version);
                else Debug.LogWarning($"[CloudSaveSync] 云存档下载失败:{err}");
                uploading = false;
                dirty = false;
            });
        }

        // ---------------- 冲突:弹窗让玩家选 ----------------

        private void ShowConflict(int slot, CloudArchiveInfo match)
        {
            if (ui == null)
            {
                // 无 UI 兜底:不自动覆盖任一端,本会话暂停自动上传,保留云端那份待下次处理。
                Debug.LogWarning("[CloudSaveSync] 冲突但无 UIMgr,保留现状、本会话不自动上传。");
                suppressUpload = true;
                uploading = false;
                return;
            }

            string cloudLine = $"云端：{Describe(match.desc)}（{RelTime(match.savedUnix)}）";
            string localLine = $"本地：{Describe(DescribeLocalProgress?.Invoke())}（{RelTime(store.GetSlotLastPlayed(slot))}）";
            // UIMgr 只有单泛型 Show<TPage>(object param);双泛型 Show<,> 是面板基类内的便捷包装,这里用前者。
            ui.Show<ConfirmPanel>(new ConfirmParam
            {
                title = "存档冲突",
                message = $"{cloudLine}\n{localLine}\n\n两台设备的进度不同,使用哪一个?",
                confirmText = "用云端",
                cancelText = "保留本地",
                onConfirm = () => DoDownload(slot, match), // 用云端 → 下载覆盖本地
                onCancel = () => DoUpload(slot),           // 保留本地 → 上传覆盖云端
            });
            // uploading 维持 true:弹窗等待期间不自动上传;玩家选择后 DoDownload/DoUpload 复位。
        }

        // ---------------- 工具 ----------------

        private CloudSaveMeta BuildMeta(long version) => new CloudSaveMeta
        {
            version = version,
            desc = DescribeLocalProgress?.Invoke(),
            playtimeSeconds = 0, // 接入真实游玩时长后填这里(冲突弹窗/云端展示用)
        };

        private static string Describe(string s) => string.IsNullOrEmpty(s) ? "存档" : s;

        /// <summary>Unix 秒 → "刚刚/x分钟前/x小时前/x天前"。冲突弹窗展示两端存档时间用。</summary>
        private static string RelTime(long unix)
        {
            if (unix <= 0) return "时间未知";
            long d = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - unix;
            if (d < 0) d = 0;
            if (d < 60) return "刚刚";
            if (d < 3600) return $"{d / 60} 分钟前";
            if (d < 86400) return $"{d / 3600} 小时前";
            return $"{d / 86400} 天前";
        }
    }
}
