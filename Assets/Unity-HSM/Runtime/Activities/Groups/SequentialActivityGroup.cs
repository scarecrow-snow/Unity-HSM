using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace HSM {
    /// <summary>
    /// 複数のアクティビティを順次実行するグループ
    /// アクティベート時は登録順、非アクティベート時は逆順で処理する
    /// </summary>
    public class SequentialActivityGroup : Activity
    {
        // 実行するアクティビティのリスト
        readonly List<IActivity> activities = new List<IActivity>();

        /// <summary>
        /// 内部のアクティビティリスト（エディタ表示用）
        /// </summary>
        public IReadOnlyList<IActivity> Activities => activities;

        /// <summary>
        /// グループにアクティビティを追加
        /// </summary>
        /// <param name="activity">追加するアクティビティ</param>
        public void AddActivity(IActivity activity)
        {
            if (activity != null) activities.Add(activity);
        }

        /// <summary>
        /// 全アクティビティを順番にアクティベート
        /// </summary>
        /// <param name="ct">キャンセルトークン</param>
        public override async UniTask ActivateAsync(CancellationToken ct)
        {
            // 既にアクティブまたはアクティベート中の場合は何もしない
            if (Mode != ActivityMode.Inactive) return;

            Mode = ActivityMode.Activating;

            try
            {
                // 登録順にアクティビティを起動
                for (int i = 0; i < activities.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    await activities[i].ActivateAsync(ct);
                }

                Mode = ActivityMode.Active;
            }
            catch
            {
                Mode = ActivityMode.Inactive;
                throw;
            }
        }

        /// <summary>
        /// 全アクティビティを逆順に非アクティベート
        /// </summary>
        /// <param name="ct">キャンセルトークン</param>
        public override async UniTask DeactivateAsync(CancellationToken ct)
        {
            // 非アクティブまたは非アクティベート中の場合は何もしない
            if (Mode != ActivityMode.Active) return;

            Mode = ActivityMode.Deactivating;

            try
            {
                // 登録と逆順にアクティビティを終了（LIFO方式）
                for (int i = activities.Count - 1; i >= 0; i--)
                {
                    ct.ThrowIfCancellationRequested();
                    await activities[i].DeactivateAsync(ct);
                }

                Mode = ActivityMode.Inactive;
            }
            catch
            {
                Mode = ActivityMode.Active;
                throw;
            }
        }
        
        public override void Dispose()
        {
            foreach (var activity in activities)
            {
                activity?.Dispose();
            }
            activities.Clear();
        }
    }
}