using System;
using System.Collections.Generic;

namespace YOTO
{
    /// <summary>
    /// 云存档服务(渠道无关公开契约)。当前实现为 TapTap 云存档(<see cref="TapTapCloudSaveService"/>),
    /// 依赖登录(<see cref="ILoginService"/>)与本地存档(<see cref="StoreMgr"/>)。
    /// 一个云归档对应一个本地存档槽(<see cref="StoreMgr.ActiveSlot"/>)。
    ///
    /// **风格**:操作 fire-and-forget(不暴露 Task),结果走回调;回调首参 = 是否成功,次参 = 失败原因/数据。
    /// 未接入 SDK 或未登录时 <see cref="IsAvailable"/>=false,各操作回失败(Editor 走模拟便于联调)。
    /// </summary>
    public interface ICloudSaveService
    {
        /// <summary>云存档当前是否可用(SDK 已接入 + 已登录)。</summary>
        bool IsAvailable { get; }

        /// <summary>把当前激活存档槽的进度打包上传(已存在则更新,否则新建)。先 SaveAll 落盘再上传。</summary>
        void Upload(Action<bool, string> onComplete);

        /// <summary>拉取云端归档列表。成功回 (true, 列表);失败回 (false, null)。</summary>
        void GetList(Action<bool, List<CloudArchiveInfo>> onComplete);

        /// <summary>下载某云归档并还原到本地:写回文件 → 激活该槽 → LoadAll 还原内存。成功回 (true, null)。</summary>
        void Download(CloudArchiveInfo archive, Action<bool, string> onComplete);

        /// <summary>删除某云归档(仅删云端,不动本地)。</summary>
        void Delete(CloudArchiveInfo archive, Action<bool, string> onComplete);
    }
}
