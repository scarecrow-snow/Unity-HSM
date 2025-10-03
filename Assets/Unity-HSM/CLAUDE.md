# Unity-HSM プロジェクト

Unity用階層化状態機械（Hierarchical State Machine）システム  
必ず日本語で回答すること

## プロジェクト概要

Unity-HSMは、Unityでキャラクター制御や複雑な状態管理を効率的に行うための階層化状態機械システムです。プレイヤーの移動、ジャンプ、アイドル状態などを構造化された状態遷移で管理し、各状態に非同期アクティビティを付与できる柔軟なアーキテクチャを提供します。

## 主な特徴

- **階層化状態管理**: 状態を階層的に構造化し、複雑な状態遷移を効率的に管理
- **非同期アクティビティシステム**: 各状態にUniTaskベースの非同期処理を付与可能
- **ゼロアロケーション状態遷移**: TempCollectionPoolによる完全なGCフリー状態遷移
- **Physics統合**: Rigidbodyとの連携による物理ベースのキャラクター制御
- **Unityエディタ統合**: CustomEditorとEditorWindowによる視覚的な状態機械デバッグツール
- **柔軟なコンポーネント設計**: IStateMachineProviderインターフェースで既存クラスに統合可能

## アーキテクチャ

### Core システム

#### State (Core/State.cs)
基本状態クラス。すべての状態はこのクラスを継承します。

```csharp
public abstract class State {
    public readonly StateMachine Machine;
    public readonly State Parent;
    public State ActiveChild;

    protected virtual State GetInitialState() => null;
    protected virtual State GetTransition() => null;
    protected virtual void OnEnter() { }
    protected virtual void OnExit() { }
    protected virtual void OnUpdate(float deltaTime) { }
}
```

**主要メソッド:**
- `GetInitialState()`: この状態に入った時の初期子状態を返す
- `GetTransition()`: 状態遷移先を返す（nullなら現在状態を維持）
- `OnEnter/OnExit/OnUpdate`: 状態のライフサイクルフック

#### StateMachine (Core/StateMachine.cs)
状態機械の実行エンジン。状態の開始、更新、遷移を管理します。

```csharp
public class StateMachine {
    public readonly State Root;
    public readonly TransitionSequencer Sequencer;

    public void Tick(float deltaTime) // メインループ
    public void ChangeState(State from, State to) // 状態遷移実行
}
```

#### Activity (Core/Activity.cs)
状態に付随する非同期アクティビティの基底クラス。

```csharp
public abstract class Activity : IActivity {
    public ActivityMode Mode { get; protected set; }
    public virtual async UniTask ActivateAsync(CancellationToken ct)
    public virtual async UniTask DeactivateAsync(CancellationToken ct)
}
```

**ActivityMode:**
- `Inactive`: 非アクティブ
- `Activating`: アクティベーション中
- `Active`: アクティブ
- `Deactivating`: ディアクティベーション中

#### ActivityExecutor (Core/ActivityExecutor.cs)
アクティビティの実行を管理する再利用可能なエンジン。TransitionSequencerから抽出され、外部からも利用可能です。

```csharp
public class ActivityExecutor {
    public bool IsExecuting { get; }

    // 並列実行（ゼロアロケーション）
    public void ExecuteActivitiesParallel(IReadOnlyList<IActivity> activities, bool isDeactivate, CancellationToken ct = default)

    // 順次実行（ゼロアロケーション）
    public async UniTask ExecuteActivitiesSequentialAsync(IReadOnlyList<IActivity> activities, bool isDeactivate, CancellationToken ct = default)

    // PhaseStepパターンでの実行（TransitionSequencer用）
    public void ExecutePhaseSteps(List<PhaseStep> steps, CancellationToken ct = default)

    // タスク完了確認とキャンセル
    public bool AreTasksComplete()
    public void Cancel()
}
```

**主要機能:**
- **並列/順次実行**: アクティビティを並列または順次実行
- **ゼロアロケーション**: IReadOnlyList<T>を使用し、内部でのアロケーションを削減
- **実行監視**: IsExecutingプロパティで実行状態を確認
- **キャンセル制御**: CancellationTokenによる柔軟なキャンセル管理
- **再利用可能**: TransitionSequencer内部だけでなく外部からも利用可能

**使用例:**
```csharp
var executor = new ActivityExecutor();
var activities = new List<IActivity> { new SomeActivity(), new OtherActivity() };

// 並列実行
executor.ExecuteActivitiesParallel(activities, isDeactivate: false, ct);
while (executor.IsExecuting) {
    await UniTask.Yield();
}

// 順次実行
await executor.ExecuteActivitiesSequentialAsync(activities, isDeactivate: false, ct);
```

#### TransitionSequencer (Core/TransitionSequencer.cs)
状態遷移の非同期実行を管理。ActivityExecutorを内部で使用します。

**主要機能:**
- LCA（Lowest Common Ancestor）アルゴリズムによる効率的な状態遷移
- 状態退出→状態変更→状態入場の3段階処理
- アクティビティの並列/順次実行切り替え（ActivityExecutorに委譲）
- **ゼロアロケーション最適化**: TempCollectionPoolによるプール化されたコレクション管理

**プール管理システム:**
```csharp
// インスタンスフィールドでプールされたコレクションを管理
List<State> exitChainPool, enterChainPool;
List<PhaseStep> exitStepsPool, enterStepsPool;

// ライフサイクル管理
void BeginTransition(State from, State to) {
    // プールからコレクションを取得
    exitChainPool = TempCollectionPool<List<State>, State>.Get();
    StatesToExit(from, lca, exitChainPool); // 直接書き込み
}

void EndTransition() {
    // プールにコレクションを返却
    TempCollectionPool<List<State>, State>.Release(exitChainPool);
}
```

### プレイヤー状態実装

#### PlayerRoot (States/PlayerRoot.cs)
プレイヤー状態のルート。地上状態と空中状態を管理します。

```csharp
public class PlayerRoot : State {
    public readonly Grounded Grounded;
    public readonly Airborne Airborne;

    protected override State GetInitialState() => Grounded;
    protected override State GetTransition() => ctx.grounded ? null : Airborne;
}
```

#### Grounded (States/Grounded.cs)
地上状態。IdleとMoveの子状態を持ち、ジャンプ処理を行います。

**状態遷移ロジック:**
- ジャンプボタン押下 → Airborne状態へ
- 地面から離れる → Airborne状態へ
- デフォルト初期状態: Idle

#### Idle & Move (States/Idle.cs, States/Move.cs)
地上での静止・移動状態。

**Idle状態:**
- 横移動入力があるとMove状態へ遷移
- 入場時に速度をゼロにリセット

**Move状態:**
- 横移動入力がなくなるとIdle状態へ遷移
- 物理ベースの移動速度計算

#### Airborne (States/Airborne.cs)
空中状態。地面接触でGrounded状態へ戻ります。

### アクティビティ実装

#### ColorPhaseActivity (Activities/ColorPhaseActivity.cs)
状態遷移時にレンダラーの色を変更するアクティビティ。

```csharp
public class ColorPhaseActivity : Activity {
    public Color enterColor = Color.red;
    public Color exitColor = Color.yellow;

    public override UniTask ActivateAsync(CancellationToken ct) {
        mat.color = enterColor;
        return UniTask.CompletedTask;
    }
}
```

**使用例:**
- Grounded状態: 黄色で入場
- Airborne状態: 赤色で入場

#### SequentialActivityGroup (Activities/SequentialActivityGroup.cs)
複数のアクティビティを順次実行するグループ化アクティビティ。

```csharp
public class SequentialActivityGroup : Activity {
    public void AddActivity(IActivity activity)

    public override async UniTask ActivateAsync(CancellationToken ct) {
        // アクティビティを順番に実行
        for (int i = 0; i < activities.Count; i++) {
            await activities[i].ActivateAsync(ct);
        }
    }

    public override async UniTask DeactivateAsync(CancellationToken ct) {
        // 逆順でディアクティベート
        for (int i = activities.Count - 1; i >= 0; i--) {
            await activities[i].DeactivateAsync(ct);
        }
    }
}
```

**主要機能:**
- **順次実行**: 複数のアクティビティを順番に実行
- **逆順ディアクティベート**: スタック構造に基づいた適切なクリーンアップ
- **構造化**: 複数のアクティビティを1つのグループとして管理

## ファイル構造

```
Unity-HSM/
├── Core/                             # 核となるシステムクラス
│   ├── State.cs                      # 基本状態クラス (ViewParameters追加)
│   ├── StateMachine.cs               # 状態機械エンジン
│   ├── Activity.cs                   # アクティビティ基底クラス
│   ├── ActivityExecutor.cs           # アクティビティ実行エンジン
│   ├── TransitionSequencer.cs        # 状態遷移管理
│   └── StateMachineBuilder.cs        # 状態機械構築
├── Editor/                           # Unityエディタ拡張
│   ├── StateMachineEditor.cs         # Inspector拡張
│   └── StateMachineViewer.cs         # EditorWindow - 階層可視化
├── Interfaces/                       # インターフェース
│   └── IStateMachineProvider.cs      # StateMachine提供インターフェース
├── States/                           # 具体的な状態実装
│   ├── PlayerRoot.cs                 # プレイヤールート状態
│   ├── Grounded.cs                   # 地上状態
│   ├── Airborne.cs                   # 空中状態
│   ├── Idle.cs                       # 静止状態
│   └── Move.cs                       # 移動状態
├── Activities/                       # アクティビティ実装
│   ├── ColorPhaseActivity.cs         # 色変更アクティビティ
│   ├── DelayActivationActivity.cs    # 遅延アクティベーション
│   └── SequentialActivityGroup.cs    # 順次実行グループ (Activities公開)
├── Examples/                         # 使用例
│   └── ActivityExecutorExample.cs    # ActivityExecutor使用例
└── PlayerStateDriver.cs              # プレイヤー制御メインクラス
```

## 使用方法

### 基本セットアップ

1. `PlayerStateDriver`をプレイヤーGameObjectにアタッチ
2. 地面判定用の`groundCheck` Transformを設定
3. `groundMask`で地面レイヤーを指定

### 入力処理

現在の入力システム（Input System）:
```csharp
// 移動入力
if (Keyboard.current.aKey.isPressed) x -= 1f;
if (Keyboard.current.dKey.isPressed) x += 1f;
ctx.move.x = x;

// ジャンプ入力
ctx.jumpPressed = Keyboard.current.spaceKey.wasPressedThisFrame;
```

### 物理パラメータ設定

`PlayerContext`で調整可能なパラメータ:
```csharp
public class PlayerContext {
    public float moveSpeed = 6f;    // 移動速度
    public float accel = 40f;       // 加速度
    public float jumpSpeed = 7f;    // ジャンプ速度
}
```

## 新しい状態の追加方法

### 1. 状態クラスの作成

```csharp
public class CustomState : State {
    readonly PlayerContext ctx;

    public CustomState(StateMachine m, State parent, PlayerContext ctx)
        : base(m, parent) {
        this.ctx = ctx;
    }

    protected override State GetTransition() {
        // 遷移条件を記述
        return someCondition ? targetState : null;
    }

    protected override void OnEnter() {
        // 状態入場時の処理
    }

    protected override void OnUpdate(float deltaTime) {
        // 毎フレームの更新処理
    }
}
```

### 2. 親状態での子状態登録

```csharp
public class ParentState : State {
    public readonly CustomState CustomState;

    public ParentState(StateMachine m, State parent, PlayerContext ctx)
        : base(m, parent) {
        CustomState = new CustomState(m, this, ctx);
    }

    protected override State GetInitialState() => CustomState;
}
```

## アクティビティの作成方法

### 1. カスタムアクティビティクラス

```csharp
public class CustomActivity : Activity {
    public override async UniTask ActivateAsync(CancellationToken ct) {
        Mode = ActivityMode.Activating;

        // アクティベーション処理
        await SomeAsyncOperation(ct);

        Mode = ActivityMode.Active;
    }

    public override async UniTask DeactivateAsync(CancellationToken ct) {
        Mode = ActivityMode.Deactivating;

        // ディアクティベーション処理
        await SomeAsyncCleanup(ct);

        Mode = ActivityMode.Inactive;
    }
}
```

### 2. 状態への追加

```csharp
public CustomState(StateMachine m, State parent, PlayerContext ctx)
    : base(m, parent) {
    Add(new CustomActivity());
}
```

## Unityエディタ統合

### IStateMachineProvider

StateMachineをGameObjectにアタッチするには、`IStateMachineProvider`インターフェースを実装します：

```csharp
public interface IStateMachineProvider
{
    StateMachine Machine { get; }
}
```

**実装例:**
```csharp
public class PlayerController : MonoBehaviour, IStateMachineProvider
{
    public StateMachine Machine { get; private set; }

    void Awake()
    {
        var root = new PlayerRoot(null, null, ctx);
        Machine = new StateMachineBuilder(root).Build();
        Machine.Start();
    }

    void Update()
    {
        Machine?.Tick(Time.deltaTime);
    }

    void OnDestroy()
    {
        Machine?.Dispose();
    }
}
```

このインターフェースを実装することで、既存のMonoBehaviourクラスにHSMエディタツールの機能を追加できます。

### StateMachineEditor (Inspector拡張)

IStateMachineProviderを実装したコンポーネントのInspectorに以下が追加されます:
- **"Open HSM Viewer"ボタン**: EditorWindowを開く
- **Runtime Data (Foldout)**: 実行時の状態情報
  - Active Path: 現在のアクティブな状態パス
  - Is Executing Activities: アクティビティ実行中かどうか

### StateMachineViewer (EditorWindow)

**起動方法:**
- メニューバー: `HSM/State Machine Viewer`
- Inspector: `Open HSM Viewer`ボタン

**機能:**
- **階層表示**: State階層をインデント形式で表示
- **Foldout**: 各Stateを折り畳み/展開可能
- **カラーコーディング**:
  - **緑色**: アクティブパス上の状態
  - **グレー**: 非アクティブな状態
- **アクティビティ表示**:
  - 各StateのActivityリストを表示
  - SequentialActivityGroupの内部Activityも階層表示
  - ActivityModeに応じた色分け:
    - Active: 明るい緑
    - Activating: シアン
    - Deactivating: オレンジ
    - Inactive: グレー
- **Content Scale**: スライダーでUI要素のサイズ調整（0.5x-2.0x）
- **自動更新**: 実行中は自動でRepaint

**表示例:**
```
State Machine: Player
  PlayerRoot → Grounded
    [Activity] ColorPhaseActivity (Active)  ← 緑色
    Grounded → Move
      [Activity] SequentialActivityGroup (Active)
        └─ DelayActivity (Activating)  ← シアン色
        └─ SomeActivity (Inactive)  ← グレー色
      Move
        [Activity] MoveActivity (Active)
```

## デバッグとテスト

### 状態遷移の確認

**コンソールログ:**
```
State PlayerRoot > Grounded > Idle
State PlayerRoot > Grounded > Move
State PlayerRoot > Airborne
```

**HSM Viewer:**
実行時にHSM Viewerウィンドウを開くことで、リアルタイムで状態階層とアクティビティの状態を確認できます。

### Gizmosによる地面判定可視化

Scene ViewでプレイヤーGameObjectを選択すると、`groundCheck`の判定範囲が白い球体で表示されます。

### パフォーマンス考慮

#### ゼロアロケーション最適化
- **完全なGCフリー状態遷移**: TempCollectionPoolにより一時コレクションのアロケーションを完全に削除
- **プールベースのコレクション管理**: List<State>, List<PhaseStep>, HashSet<State>をすべてプール化
- **メソッドシグネチャ最適化**: 戻り値型からvoid型に変更し、結果を呼び出し側のコレクションに直接書き込み

**最適化前後の比較:**
```csharp
// Before: アロケーションが発生
static List<State> StatesToExit(State from, State lca) {
    var list = new List<State>(); // GCアロケーション
    // ...
    return list;
}

// After: ゼロアロケーション
static void StatesToExit(State from, State lca, List<State> result) {
    result.Clear(); // プールされたコレクションを再利用
    // ...
}
```

#### その他の最適化
- `ColorPhaseActivity`はマテリアルインスタンスをキャッシュ
- 状態遷移時のLCAアルゴリズムにより無駄な処理を削減
- UniTaskによる効率的な非同期処理
- **メモリプレッシャー軽減**: 頻繁な状態遷移でもGCが発生しない
- **フレームレート安定化**: アロケーションによるスパイクを完全に除去

## 依存関係

- **Unity 2022.3以降**
- **UniTask**: 非同期処理フレームワーク
- **Input System**: 新しい入力システム
- **UnityUtils**: カスタムユーティリティ（`GetOrAdd<T>`拡張メソッド、`TempCollectionPool<TCollection, TElement>`等）

## 開発履歴

### 2025年9月: 基本システム実装
- 基本的な階層化状態機械システム実装
- コア機能: State, StateMachine, Activity
- プレイヤー制御: 地上/空中状態、移動/静止状態
- 非同期アクティビティシステム
- 視覚的デバッグ機能

### 2025年9月: ゼロアロケーション最適化
- **TempCollectionPool統合**: 完全なGCフリー状態遷移を実現
- **メソッドシグネチャ最適化**: 戻り値からvoid型への変更、結果を直接書き込み
- **プール管理システム**: BeginTransition/EndTransition/ReleasePooledCollectionsライフサイクル
- **4つのプールされたコレクション**: exitChainPool, enterChainPool, exitStepsPool, enterStepsPool
- **Lcaメソッド最適化**: HashSet<State>のプール化
- **パフォーマンス向上**: 状態遷移時のアロケーションを完全に除去

### 2025年9月: ActivityExecutorリファクタリング
- **ActivityExecutorクラス**: TransitionSequencerからアクティビティ実行ロジックを抽出
- **再利用可能な実行エンジン**: 状態遷移以外の場面でもアクティビティ実行可能に
- **SequentialActivityGroup**: 複数アクティビティの順次実行をサポート
- **外部利用サンプル**: ActivityExecutorExampleによる使用例を追加
- **アーキテクチャ改善**: 関心の分離により保守性とテスタビリティが向上

### 2025年10月: Unityエディタ統合
- **IStateMachineProvider**: インターフェースベースの柔軟なコンポーネント設計
- **StateMachineEditor**: CustomEditorによるInspector拡張（IStateMachineProviderを実装した全MonoBehaviourに対応）
- **StateMachineViewer**: EditorWindowによる階層可視化ツール
- **State.ViewParameters**: エディタ用のFoldout状態管理（#if UNITY_EDITORで囲まれている）
- **SequentialActivityGroup.Activities**: 内部アクティビティの公開プロパティ追加
- **リアルタイムデバッグ**: 実行時の状態とアクティビティを視覚的に確認可能

## 今後の拡張予定
- [ ] より多様なアクティビティタイプ
- [x] **エディタ拡張による可視化ツール** (完了: StateMachineViewerによる階層可視化実現)
- [x] **パフォーマンス最適化** (完了: TempCollectionPoolによるゼロアロケーション実現)
- [ ] 状態遷移パフォーマンステスト自動化
- [ ] より大規模な状態機械での最適化検証
- [ ] グラフベースのエディタ（GraphView利用）

---

このシステムにより、複雑なキャラクター制御を構造化された方法で実装でき、拡張性と保守性を両立した開発が可能になります。