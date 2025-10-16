using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityUtils;


namespace HSM
{
    /// <summary>
    /// 状態遷移のシーケンスを管理するクラス
    /// Exit phase（Deactivate）→ ChangeState → Enter phase（Activate）の順で実行
    /// </summary>
    public class TransitionSequencer
    {
        public readonly StateMachine Machine;

        Action nextPhase;                    // フェーズ間の切り替え処理
        (State from, State to)? pending;     // 保留中の遷移リクエスト

        // nextPhaseラムダでのクロージャアロケーションを回避するためのフィールド
        State transitionFrom, transitionTo, transitionLca;
        bool hasEnterPhase;

        // 非同期タスク管理
        int runningTaskCount = 0;
        CancellationTokenSource cts;

        public bool IsExecuting => runningTaskCount > 0;

        public TransitionSequencer(StateMachine machine)
        {
            Machine = machine;

            // クロージャアロケーションを避けるためnextPhaseデリゲートを初期化
            nextPhase = ExecuteEnterPhase;
        }

        /// <summary>
        /// 状態遷移をリクエストする
        /// </summary>
        public void RequestTransition(State from, State to)
        {
            if (to == null || from == to)
            {
                return;
            }

            // 重複した遷移リクエストをチェック
            if (IsSameTransition(from, to))
            {
                return;
            }

            if (runningTaskCount > 0)
            {
                pending = (from, to);
                return;
            }
            BeginTransition(from, to);
        }


        /// <summary>
        /// Exit対象の状態リストを取得: from → LCA手前まで（ボトムアップ順）
        /// </summary>
        static void StatesToExit(State from, State lca, List<State> result)
        {
            result.Clear();
            for (var s = from; s != null && s != lca; s = s.Parent)
            {
                result.Add(s);
            }
        }

        /// <summary>
        /// Enter対象の状態リストを取得: LCA手前 → to まで（トップダウン順）
        /// </summary>
        static void StatesToEnter(State to, State lca, List<State> result)
        {
            result.Clear();
            // 逆順でパスを構築
            for (var s = to; s != null && s != lca; s = s.Parent)
            {
                result.Add(s);
            }
            // トップダウン順に反転
            result.Reverse();
        }

        bool IsSameTransition(State from, State to)
        {
            // 現在遷移中で全く同じfrom->toかチェック
            if (runningTaskCount > 0 && transitionFrom == from && transitionTo == to)
            {
                return true;
            }

            // 保留中の遷移と同じかチェック
            if (pending.HasValue)
            {
                var (pendingFrom, pendingTo) = pending.Value;
                if (pendingFrom == from && pendingTo == to)
                {
                    return true;
                }
            }

            return false;
        }


        void BeginTransition(State from, State to)
        {

            cts?.Cancel();
            cts?.Dispose();
            cts = new CancellationTokenSource();

            // from=nullは初期遷移を意味する（Exitフェーズ不要）
            var lca = from == null ? null : Lca(from, to);

            // Exitフェーズの準備（プールされたコレクションを使用）
            using (var exitChainScope = TempCollectionPool<List<State>, State>.GetScoped())
            using (var exitStepsScope = TempCollectionPool<List<PhaseStep>, PhaseStep>.GetScoped())
            {
                var exitChain = exitChainScope.Collection;
                var exitSteps = exitStepsScope.Collection;

                StatesToExit(from, lca, exitChain);

                GatherPhaseSteps(exitChain, deactivate: true, exitSteps);

                // 1. 旧ブランチのActivityを並列でDeactivate
                ExecutePhaseSteps(exitSteps, cts.Token);
            }

            // クロージャアロケーション回避のため遷移パラメータを保存
            transitionFrom = from;
            transitionTo = to;
            transitionLca = lca;
            hasEnterPhase = true;

            // Exitフェーズが同期的に完了したかチェック
            if (runningTaskCount == 0 && hasEnterPhase)
            {
                nextPhase();
            }
        }

        void ExecuteEnterPhase()
        {
            // 重要: 二重呼び出しを防ぐため、最初にフラグをクリア
            hasEnterPhase = false;

            // 2. ChangeStateで状態ツリーの構造を更新
            Machine.ChangeState(transitionFrom, transitionTo);

            // 3. ChangeStateで入った最終的なリーフステートを計算
            // Machine.ChangeStateによって、transitionToから子ステートへの遷移が完了している
            // 現在のアクティブリーフを取得（Machine.Rootから辿る）
            State finalLeaf = Machine.Root.Leaf();

            // 4. 新ブランチのActivityをActivate
            using (var enterChainScope = TempCollectionPool<List<State>, State>.GetScoped())
            using (var enterStepsScope = TempCollectionPool<List<PhaseStep>, PhaseStep>.GetScoped())
            {
                var enterChain = enterChainScope.Collection;
                var enterSteps = enterStepsScope.Collection;

                StatesToEnter(finalLeaf, transitionLca, enterChain);

                GatherPhaseSteps(enterChain, deactivate: false, enterSteps);

                // Enterフェーズを並列実行
                ExecutePhaseSteps(enterSteps, cts.Token);
            }

            // Enterフェーズが同期的に完了したかチェック
            if (runningTaskCount == 0)
            {
                EndTransition();
            }
        }

        void EndTransition()
        {
            ClearTasks();

            if (pending.HasValue)
            {
                (State from, State to) p = pending.Value;
                pending = null;
                BeginTransition(p.from, p.to);
            }
        }

        public void Tick(float deltaTime)
        {
            if (runningTaskCount > 0)
            {
                return; // 遷移中は通常のUpdateを実行しない
            }
            Machine.InternalTick(deltaTime);
        }

        /// <summary>
        /// 状態チェーンからフェーズステップを収集
        /// </summary>
        void GatherPhaseSteps(List<State> chain, bool deactivate, List<PhaseStep> result)
        {
            result.Clear();
            for (int i = 0; i < chain.Count; i++)
            {
                var st = chain[i];
                var acts = st.Activities;
                for (int j = 0; j < acts.Count; j++)
                {
                    var a = acts[j];

                    // Exit phase: Activeなものだけを収集してDeactivate
                    // Enter phase: Inactiveなものだけを収集してActivate
                    // これにより、すでに正しい状態にあるActivityは重複実行されない
                    bool include = deactivate ? (a.Mode == ActivityMode.Active)
                        : (a.Mode == ActivityMode.Inactive);
                    if (!include) continue;

                    result.Add(new PhaseStep(a, deactivate));
                }
            }
        }

        /// <summary>
        /// フェーズステップを並列実行
        /// </summary>
        void ExecutePhaseSteps(List<PhaseStep> steps, CancellationToken ct)
        {
            ClearTasks();

            for (int i = 0; i < steps.Count; i++)
            {
                runningTaskCount++;
                ExecutePhaseStepSafe(steps[i], ct).Forget();
            }
        }

        async Cysharp.Threading.Tasks.UniTaskVoid ExecutePhaseStepSafe(PhaseStep step, CancellationToken ct)
        {
            try
            {
                await step.Execute(ct);
            }
            catch (System.OperationCanceledException)
            {
                // キャンセルは想定内なので抑制
            }
            catch (System.Exception e)
            {
                // その他の例外の場合はエラーとして出力
                Debug.LogError(e);
                throw;
            }
            finally
            {
                runningTaskCount--;

                // すべてのタスクが完了したら次のフェーズへ
                if (runningTaskCount == 0)
                {
                    if (hasEnterPhase)
                    {
                        nextPhase();
                    }
                    else
                    {
                        EndTransition();
                    }
                }
            }
        }

        void ClearTasks()
        {
            runningTaskCount = 0;
        }

        /// <summary>
        /// 2つの状態の最小共通祖先（LCA）を計算
        /// </summary>
        public static State Lca(State a, State b)
        {
            // aの全ての親をセットに格納
            using (var scope = TempCollectionPool<HashSet<State>, State>.GetScoped())
            {
                var ap = scope.Collection;
                for (var s = a; s != null; s = s.Parent) ap.Add(s);

                // bの親でaの親でもある最初のものを見つける
                for (var s = b; s != null; s = s.Parent)
                    if (ap.Contains(s))
                        return s;

                // 共通祖先が見つからない場合はnullを返す
                return null;
            }
        }
    }
}
