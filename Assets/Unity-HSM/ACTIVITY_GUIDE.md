# Unity-HSM Activity使用ガイド

## 目次

1. [Activityとは](#activityとは)
2. [基本的な使い方](#基本的な使い方)
3. [組み込みActivity](#組み込みactivity)
4. [カスタムActivityの作成](#カスタムactivityの作成)
5. [SequentialActivityGroup](#sequentialactivitygroup)
6. [ActivityExecutorの直接利用](#activityexecutorの直接利用)
7. [ベストプラクティス](#ベストプラクティス)

## Activityとは

Activityは、状態（State）の入退場時に実行される非同期処理を表すコンポーネントです。UniTaskベースで実装され、以下のような用途に使用できます：

- アニメーション制御
- エフェクトの再生・停止
- サウンドの再生
- UI表示の切り替え
- リソースの読み込み・解放
- 遅延処理

### ActivityMode

すべてのActivityは以下の4つの状態を持ちます：

| Mode | 説明 |
|------|------|
| `Inactive` | 非アクティブ（初期状態） |
| `Activating` | アクティベーション中 |
| `Active` | アクティブ |
| `Deactivating` | ディアクティベーション中 |

## 基本的な使い方

### Stateへの追加

```csharp
public class MyState : State
{
    public MyState(StateMachine m, State parent, Context ctx)
        : base(m, parent)
    {
        // コンストラクタでActivityを追加
        Add(new ColorPhaseActivity(renderer, Color.green, Color.yellow));
        Add(new DelayActivationActivity(0.5f));
    }
}
```

### 実行タイミング

- **状態入場時（OnEnter）**: すべてのActivityの`ActivateAsync()`が呼ばれる
- **状態退場時（OnExit）**: すべてのActivityの`DeactivateAsync()`が呼ばれる

TransitionSequencerは以下の順序で処理を行います：

1. **Exit Phase**: 退出する状態のActivityを**並列**にDeactivate
2. **State Change**: 状態遷移を実行
3. **Enter Phase**: 入場する状態のActivityを**並列**にActivate

## 組み込みActivity

### 1. ColorPhaseActivity

レンダラーの色を変更するActivity。

```csharp
public class ColorPhaseActivity : Activity
{
    public Color enterColor = Color.red;
    public Color exitColor = Color.yellow;

    public ColorPhaseActivity(Renderer r, Color enter, Color exit)
    {
        this.renderer = r;
        this.enterColor = enter;
        this.exitColor = exit;
    }
}
```

**使用例:**
```csharp
// 状態に入ったら緑、出たら黄色
Add(new ColorPhaseActivity(renderer, Color.green, Color.yellow));
```

**特徴:**
- マテリアルインスタンスをキャッシュして再利用
- 同期処理（UniTask.CompletedTaskを返す）

### 2. DelayActivationActivity

指定時間の遅延を行うActivity。

```csharp
public class DelayActivationActivity : Activity
{
    float delay;

    public DelayActivationActivity(float delay)
    {
        this.delay = delay;
    }
}
```

**使用例:**
```csharp
// 0.5秒待機してからActiveになる
Add(new DelayActivationActivity(0.5f));
```

**特徴:**
- UniTask.Delayを使用した非同期待機
- CancellationTokenによるキャンセル対応

### 3. SequentialActivityGroup

複数のActivityを順次実行するグループ化Activity。

```csharp
var group = new SequentialActivityGroup();
group.AddActivity(new DelayActivationActivity(0.2f));
group.AddActivity(new ColorPhaseActivity(renderer, Color.red, Color.blue));
group.AddActivity(new DelayActivationActivity(0.3f));

Add(group);
```

**特徴:**
- **順次実行**: Activateは登録順、Deactivateは逆順（LIFO）
- **ネスト可能**: SequentialActivityGroup内に別のSequentialActivityGroupを追加可能
- **エディタ可視化**: StateMachineViewerで内部Activityが階層表示される

## カスタムActivityの作成

### 基本テンプレート

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using HSM;

public class MyCustomActivity : Activity
{
    // 必要なフィールド
    private SomeComponent component;

    public MyCustomActivity(SomeComponent comp)
    {
        this.component = comp;
    }

    public override async UniTask ActivateAsync(CancellationToken ct)
    {
        // 既にアクティブな場合は何もしない
        if (Mode != ActivityMode.Inactive) return;

        Mode = ActivityMode.Activating;

        // アクティベーション処理
        component.Enable();
        await UniTask.Delay(100, cancellationToken: ct);

        Mode = ActivityMode.Active;
    }

    public override async UniTask DeactivateAsync(CancellationToken ct)
    {
        // 非アクティブな場合は何もしない
        if (Mode != ActivityMode.Active) return;

        Mode = ActivityMode.Deactivating;

        // ディアクティベーション処理
        await UniTask.Delay(100, cancellationToken: ct);
        component.Disable();

        Mode = ActivityMode.Inactive;
    }
}
```

### 実装のポイント

1. **Modeチェック**: 最初に適切な状態かチェック
2. **Mode更新**: 処理の前後でModeを適切に更新
3. **CancellationToken対応**: 長時間処理はctを渡してキャンセル可能に
4. **同期処理の場合**: `await UniTask.CompletedTask;` または処理後即座にMode変更

### 実用例1: アニメーション制御

```csharp
public class AnimationActivity : Activity
{
    private Animator animator;
    private string stateName;

    public AnimationActivity(Animator anim, string state)
    {
        animator = anim;
        stateName = state;
    }

    public override async UniTask ActivateAsync(CancellationToken ct)
    {
        if (Mode != ActivityMode.Inactive) return;

        Mode = ActivityMode.Activating;

        animator.Play(stateName);

        // アニメーション遷移完了まで待機
        await UniTask.WaitUntil(() =>
            animator.GetCurrentAnimatorStateInfo(0).IsName(stateName),
            cancellationToken: ct);

        Mode = ActivityMode.Active;
    }

    public override async UniTask DeactivateAsync(CancellationToken ct)
    {
        if (Mode != ActivityMode.Active) return;

        Mode = ActivityMode.Deactivating;

        // フェードアウトなど
        await UniTask.CompletedTask;

        Mode = ActivityMode.Inactive;
    }
}
```

### 実用例2: パーティクルエフェクト

```csharp
public class ParticleActivity : Activity
{
    private ParticleSystem ps;

    public ParticleActivity(ParticleSystem particle)
    {
        ps = particle;
    }

    public override async UniTask ActivateAsync(CancellationToken ct)
    {
        if (Mode != ActivityMode.Inactive) return;

        Mode = ActivityMode.Activating;

        ps.Play();

        Mode = ActivityMode.Active;
        await UniTask.CompletedTask;
    }

    public override async UniTask DeactivateAsync(CancellationToken ct)
    {
        if (Mode != ActivityMode.Active) return;

        Mode = ActivityMode.Deactivating;

        ps.Stop();

        // パーティクルが完全に消えるまで待機（オプション）
        await UniTask.WaitUntil(() => !ps.IsAlive(), cancellationToken: ct);

        Mode = ActivityMode.Inactive;
    }
}
```

### 実用例3: サウンド再生

```csharp
public class AudioActivity : Activity
{
    private AudioSource audioSource;
    private AudioClip enterClip;
    private AudioClip exitClip;

    public AudioActivity(AudioSource source, AudioClip enter, AudioClip exit = null)
    {
        audioSource = source;
        enterClip = enter;
        exitClip = exit;
    }

    public override async UniTask ActivateAsync(CancellationToken ct)
    {
        if (Mode != ActivityMode.Inactive) return;

        Mode = ActivityMode.Activating;

        if (enterClip != null)
        {
            audioSource.PlayOneShot(enterClip);
        }

        Mode = ActivityMode.Active;
        await UniTask.CompletedTask;
    }

    public override async UniTask DeactivateAsync(CancellationToken ct)
    {
        if (Mode != ActivityMode.Active) return;

        Mode = ActivityMode.Deactivating;

        if (exitClip != null)
        {
            audioSource.PlayOneShot(exitClip);
        }

        Mode = ActivityMode.Inactive;
        await UniTask.CompletedTask;
    }
}
```

## SequentialActivityGroup

### 基本的な使い方

```csharp
var sequence = new SequentialActivityGroup();
sequence.AddActivity(new DelayActivationActivity(0.1f));
sequence.AddActivity(new ColorPhaseActivity(renderer, Color.red, Color.blue));
sequence.AddActivity(new AudioActivity(audioSource, sfx));
sequence.AddActivity(new DelayActivationActivity(0.2f));

Add(sequence);
```

### ネストした使用例

```csharp
// メインシーケンス
var mainSequence = new SequentialActivityGroup();

// サブシーケンス1: エフェクト準備
var prepareSequence = new SequentialActivityGroup();
prepareSequence.AddActivity(new ParticleActivity(chargeEffect));
prepareSequence.AddActivity(new DelayActivationActivity(0.5f));

// サブシーケンス2: 実行
var executeSequence = new SequentialActivityGroup();
executeSequence.AddActivity(new AudioActivity(audioSource, executeSound));
executeSequence.AddActivity(new AnimationActivity(animator, "Execute"));
executeSequence.AddActivity(new ParticleActivity(hitEffect));

// メインに追加
mainSequence.AddActivity(prepareSequence);
mainSequence.AddActivity(executeSequence);

Add(mainSequence);
```

### ActivateとDeactivateの実行順序

**Activate時（登録順）:**
```
1. Activity A
2. Activity B
3. Activity C
```

**Deactivate時（逆順 - LIFO）:**
```
1. Activity C
2. Activity B
3. Activity A
```

これにより、リソースの適切なクリーンアップが保証されます。

## ActivityExecutorの直接利用

状態遷移以外でActivityを実行したい場合、ActivityExecutorを直接使用できます。

### 並列実行

```csharp
var executor = new ActivityExecutor();
var activities = new List<IActivity>
{
    new ColorPhaseActivity(renderer1, Color.red, Color.blue),
    new AudioActivity(audioSource, sfx),
    new ParticleActivity(particles)
};

// 並列実行開始
executor.ExecuteActivitiesParallel(activities, isDeactivate: false, ct);

// 完了待機
while (executor.IsExecuting)
{
    await UniTask.Yield();
}
```

### 順次実行

```csharp
var executor = new ActivityExecutor();
var activities = new List<IActivity>
{
    new DelayActivationActivity(0.2f),
    new ColorPhaseActivity(renderer, Color.red, Color.blue),
    new DelayActivationActivity(0.3f)
};

// 順次実行（完了まで待機）
await executor.ExecuteActivitiesSequentialAsync(activities, isDeactivate: false, ct);
```

### 実用例: カットシーン

```csharp
public class CutsceneController : MonoBehaviour
{
    ActivityExecutor executor = new ActivityExecutor();

    public async UniTask PlayCutscene(CancellationToken ct)
    {
        var sequence = new List<IActivity>
        {
            new DelayActivationActivity(1f),
            new AudioActivity(bgm, openingMusic),
            new AnimationActivity(camera, "CameraZoom"),
            new DelayActivationActivity(2f),
            new DialogActivity(dialogUI, "Welcome..."),
            new DelayActivationActivity(1f)
        };

        await executor.ExecuteActivitiesSequentialAsync(sequence, false, ct);
    }
}
```

## ベストプラクティス

### 1. Activityは再利用可能に設計する

**Good:**
```csharp
public class FadeActivity : Activity
{
    private CanvasGroup canvasGroup;
    private float duration;

    public FadeActivity(CanvasGroup cg, float dur)
    {
        canvasGroup = cg;
        duration = dur;
    }
}

// 使用
Add(new FadeActivity(uiCanvasGroup, 0.5f));
```

**Bad:**
```csharp
public class FadeActivity : Activity
{
    // ハードコードされた値
    private const float Duration = 0.5f;
}
```

### 2. 長時間処理は必ずCancellationTokenを渡す

**Good:**
```csharp
public override async UniTask ActivateAsync(CancellationToken ct)
{
    Mode = ActivityMode.Activating;

    // ctを渡す
    await UniTask.Delay(TimeSpan.FromSeconds(5), cancellationToken: ct);

    Mode = ActivityMode.Active;
}
```

**Bad:**
```csharp
public override async UniTask ActivateAsync(CancellationToken ct)
{
    Mode = ActivityMode.Activating;

    // ctを渡さない - キャンセルできない
    await UniTask.Delay(TimeSpan.FromSeconds(5));

    Mode = ActivityMode.Active;
}
```

### 3. Mode状態を適切にチェックする

```csharp
public override async UniTask ActivateAsync(CancellationToken ct)
{
    // 重要: 既にアクティブまたはアクティベート中なら何もしない
    if (Mode != ActivityMode.Inactive) return;

    Mode = ActivityMode.Activating;
    // 処理...
    Mode = ActivityMode.Active;
}
```

### 4. リソースの管理

```csharp
public class ResourceActivity : Activity
{
    private GameObject loadedObject;

    public override async UniTask ActivateAsync(CancellationToken ct)
    {
        if (Mode != ActivityMode.Inactive) return;

        Mode = ActivityMode.Activating;

        // リソース読み込み
        loadedObject = await LoadResourceAsync(ct);

        Mode = ActivityMode.Active;
    }

    public override async UniTask DeactivateAsync(CancellationToken ct)
    {
        if (Mode != ActivityMode.Active) return;

        Mode = ActivityMode.Deactivating;

        // リソース解放
        if (loadedObject != null)
        {
            Object.Destroy(loadedObject);
            loadedObject = null;
        }

        Mode = ActivityMode.Inactive;
        await UniTask.CompletedTask;
    }
}
```

### 5. エラーハンドリング

```csharp
public override async UniTask ActivateAsync(CancellationToken ct)
{
    if (Mode != ActivityMode.Inactive) return;

    Mode = ActivityMode.Activating;

    try
    {
        await SomeRiskyOperationAsync(ct);
        Mode = ActivityMode.Active;
    }
    catch (OperationCanceledException)
    {
        // キャンセルは正常系として扱う
        Mode = ActivityMode.Inactive;
        throw;
    }
    catch (Exception ex)
    {
        Debug.LogError($"Activation failed: {ex}");
        Mode = ActivityMode.Inactive;
        throw;
    }
}
```

## まとめ

- **Activity**は状態に付随する非同期処理を表現する強力なツール
- **並列/順次実行**を自由に組み合わせられる柔軟性
- **SequentialActivityGroup**で複雑なシーケンスを構造化
- **ActivityExecutor**で状態遷移以外の場面でも利用可能
- **StateMachineViewer**でリアルタイムデバッグ

適切に設計されたActivityは、ゲームロジックを読みやすく保守しやすいコードに保つための重要な要素です。
