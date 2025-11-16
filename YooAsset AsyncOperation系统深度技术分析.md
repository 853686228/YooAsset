# YooAsset AsyncOperation系统深度技术分析

## 一、系统概述

### 1.1 设计理念

YooAsset的AsyncOperation系统采用了**分层异步架构**设计，通过OperationSystem作为中央调度器，统一管理所有异步操作。该系统的核心目标是：

- **统一调度管理**：所有异步操作通过统一的调度器进行管理
- **性能优化**：通过优先级调度和时间片控制保证帧率稳定
- **资源安全**：通过引用计数和弱引用机制防止内存泄漏
- **开发友好**：提供多种异步编程模式和完整的调试支持

### 1.2 系统架构图

```
┌─────────────────────────────────────────────────────────┐
│                    用户接口层                              │
│              (Handle & Operation)                       │
├─────────────────────────────────────────────────────────┤
│                GameAsyncOperation                        │
│              (简化扩展基类)                              │
├─────────────────────────────────────────────────────────┤
│                AsyncOperationBase                        │
│              (抽象基类 + 调度接口)                        │
├─────────────────────────────────────────────────────────┤
│                  OperationSystem                         │
│              (中央调度器 + 优先级管理)                     │
├─────────────────────────────────────────────────────────┤
│          ProviderOperation     │     各种具体操作         │
│        (资源提供者操作)        │   (LoadBundle、Scene等)   │
└─────────────────────────────────────────────────────────┘
```

### 1.3 核心组件关系

```
OperationSystem (调度器)
    ├── AsyncOperationBase (抽象基类)
    │   ├── GameAsyncOperation (游戏操作基类)
    │   │   ├── PatchOperation (补丁操作)
    │   │   ├── DownloadOperation (下载操作)
    │   │   └── CustomOperation (自定义操作)
    │   └── ProviderOperation (资源提供者操作)
    │       ├── AssetProvider (资源提供者)
    │       ├── SceneProvider (场景提供者)
    │       └── BundleFileProvider (Bundle文件提供者)
    └── Handle系统 (资源句柄)
        ├── AssetHandle (资源句柄)
        ├── SceneHandle (场景句柄)
        ├── SubAssetsHandle (子资源句柄)
        └── RawFileHandle (原生文件句柄)
```

## 二、OperationSystem调度核心

### 2.1 系统架构

```csharp
internal static class OperationSystem
{
    // 操作管理
    private static readonly List<AsyncOperationBase> _operations = new List<AsyncOperationBase>();
    private static readonly List<AsyncOperationBase> _newList = new List<AsyncOperationBase>();

    // 时间控制
    private static readonly Stopwatch _watch = new Stopwatch();
    private static long _frameTime = 0;
    public static long MaxTimeSlice { get; set; } = long.MaxValue;

    // 统计信息
    public static int OperationCount => _operations.Count;
    public static bool IsBusy => _watch.ElapsedMilliseconds - _frameTime >= MaxTimeSlice;
}
```

### 2.2 调度机制

#### 调度主循环
```csharp
public static void Update()
{
    // 1. 清理已完成操作
    for (int i = _operations.Count - 1; i >= 0; i--)
    {
        var operation = _operations[i];
        if (operation.IsDone)
        {
            _operations.RemoveAt(i);
            continue;
        }
    }

    // 2. 添加新操作
    if (_newList.Count > 0)
    {
        _operations.AddRange(_newList);
        _newList.Clear();
    }

    // 3. 优先级排序（如果有新操作或高优先级操作）
    if (_operations.Count > 0 && _operations[0].Priority > 0)
    {
        _operations.Sort();
    }

    // 4. 执行操作更新
    _watch.Restart();
    _frameTime = _watch.ElapsedMilliseconds;

    for (int i = 0; i < _operations.Count; i++)
    {
        var operation = _operations[i];
        operation.UpdateOperation();

        // 5. 时间片检查
        if (IsBusy)
            break;
    }
}
```

#### 关键特性分析

| 特性 | 实现机制 | 作用 |
|------|----------|------|
| **批量处理** | 在时间片内处理多个操作 | 提高吞吐量 |
| **优先级排序** | 高优先级数字排在前面 | 保证重要操作优先 |
| **时间片保护** | Stopwatch精确计时 | 防止单帧卡顿 |
| **懒加载清理** | 逆序清理已完成操作 | 高效的内存管理 |

### 2.3 操作管理接口

```csharp
// 启动异步操作
internal static void StartOperation(string packageName, AsyncOperationBase operation)
{
    if (operation == null)
        throw new Exception("Should never get here !");

    operation.PackageName = packageName;
    operation.InternalStart();

    lock (_newList)
    {
        _newList.Add(operation);
    }
}

// 清理包相关操作
internal static void ClearPackageOperation(string packageName)
{
    foreach (var operation in _operations)
    {
        if (operation.PackageName == packageName)
            operation.InternalAbort();
    }
    _newList.Clear();
}

// 销毁所有操作
internal static void DestroyAll()
{
    foreach (var operation in _operations)
        operation.InternalAbort();

    _operations.Clear();
    _newList.Clear();
}
```

## 三、AsyncOperationBase抽象基类

### 3.1 核心属性设计

```csharp
public abstract class AsyncOperationBase : IEnumerator, IComparable<AsyncOperationBase>
{
    // 状态管理
    public EOperationStatus Status { get; protected set; }
    public bool IsFinish { get; private set; }
    public bool IsDone => Status == EOperationStatus.Succeed || Status == EOperationStatus.Failed;

    // 进度信息
    public float Progress { get; protected set; }
    public string Error { get; protected set; }

    // 调度相关
    public uint Priority { get; set; }
    public string PackageName { get; internal set; }

    // 异步支持
    public event Action<AsyncOperationBase> Completed;
    public Task Task { get; }

    // 子操作管理
    internal readonly List<AsyncOperationBase> Childs = new List<AsyncOperationBase>();

    // 时间统计
    private readonly DateTime _startTime = DateTime.Now;
    public string BeginTime => _startTime.ToString("HH:mm:ss.fff");
    public long ProcessTime => (long)(DateTime.Now - _startTime).TotalMilliseconds;
}
```

### 3.2 状态机设计

#### 状态枚举定义
```csharp
public enum EOperationStatus
{
    None,       // 初始状态
    Processing, // 处理中
    Succeed,    // 成功完成
    Failed      // 失败完成
}
```

#### 状态转换规则
```
    [创建]
        ↓
     None (初始)
        ↓
   Processing (开始)
    ↙     ↘
Failed  Succeed
  ↓        ↓
[结束]   [结束]
```

### 3.3 核心生命周期方法

```csharp
// 内部生命周期控制
internal virtual void InternalStart() { }
internal virtual void InternalUpdate() { }
internal virtual void InternalAbort() { }
internal virtual void InternalWaitForAsyncComplete() { }

// 操作更新入口
internal void UpdateOperation()
{
    if (IsDone == false)
    {
        try
        {
            InternalUpdate(); // 执行具体更新逻辑
        }
        catch (Exception e)
        {
            Status = EOperationStatus.Failed;
            Error = e.ToString();
            YooLogger.Error(e);
        }
    }

    // 检查完成状态
    if (IsDone && IsFinish == false)
    {
        IsFinish = true;
        Progress = 1f;

        // 触发完成回调
        _callback?.Invoke(this);

        // 完成Task
        if (_taskCompletionSource != null)
            _taskCompletionSource.TrySetResult(null);
    }
}
```

### 3.4 异步回调机制

#### 多种回调支持

**1. 事件回调模式**
```csharp
private Action<AsyncOperationBase> _callback;

public event Action<AsyncOperationBase> Completed
{
    add
    {
        if (IsDone)
        {
            // 如果操作已完成，立即执行回调
            value.Invoke(this);
        }
        else
        {
            // 否则添加到回调列表
            _callback += value;
        }
    }
    remove { _callback -= value; }
}
```

**2. Task-based异步模式**
```csharp
private TaskCompletionSource<object> _taskCompletionSource;

public Task Task
{
    get
    {
        if (_taskCompletionSource == null)
        {
            _taskCompletionSource = new TaskCompletionSource<object>();
            if (IsDone)
                _taskCompletionSource.SetResult(null);
        }
        return _taskCompletionSource.Task;
    }
}
```

**3. 协程模式**
```csharp
// 实现IEnumerator接口以支持协程
bool IEnumerator.MoveNext() => !IsDone;
object IEnumerator.Current => null;
void IEnumerator.Reset() { }
```

#### 优先级比较接口
```csharp
// 实现IComparable接口支持优先级排序
public int CompareTo(AsyncOperationBase other)
{
    if (ReferenceEquals(this, other)) return 0;
    if (ReferenceEquals(null, other)) return 1;
    return other.Priority.CompareTo(this.Priority);
}
```

## 四、GameAsyncOperation扩展基类

### 4.1 简化设计理念

`GameAsyncOperation` 是为简化异步操作开发而设计的基类，它封装了复杂的生命周期管理，开发者只需关注业务逻辑实现。

### 4.2 核心实现

```csharp
public abstract class GameAsyncOperation : AsyncOperationBase
{
    internal override void InternalStart()
    {
        Status = EOperationStatus.Processing;
        OnStart();
    }

    internal override void InternalUpdate()
    {
        if (Status == EOperationStatus.Processing)
        {
            OnUpdate();
        }
    }

    internal override void InternalAbort()
    {
        OnAbort();
    }

    internal override void InternalWaitForAsyncComplete()
    {
        if (Status == EOperationStatus.Processing)
        {
            OnWaitForAsyncComplete();
        }
    }

    // 开发者需要实现的抽象方法
    protected abstract void OnStart();
    protected abstract void OnUpdate();
    protected abstract void OnAbort();
    protected virtual void OnWaitForAsyncComplete() { }
}
```

### 4.3 实际应用示例

#### 补丁下载操作实现
```csharp
public class PatchOperation : GameAsyncOperation
{
    private enum ESteps
    {
        None,
        Update,
        Done,
    }

    private ESteps _steps = ESteps.None;
    private readonly FsmManager _machine = new FsmManager();

    // 结果数据
    public int TotalDownloadCount { get; private set; }
    public long TotalDownloadBytes { get; private set; }

    protected override void OnStart()
    {
        _steps = ESteps.Update;
        _machine.Run<FsmInitializePackage>();

        // 监听状态机完成
        _machine.OnMachineComplete += OnMachineComplete;
    }

    protected override void OnUpdate()
    {
        if (_steps == ESteps.Update)
        {
            _machine.Update();
            Progress = _machine.Progress;
        }
    }

    protected override void OnAbort()
    {
        _machine.DestroyAllFsm();
    }

    private void OnMachineComplete()
    {
        if (_machine.CurrentFsm is FsmDownloadFiles downloadFsm)
        {
            TotalDownloadCount = downloadFsm.TotalDownloadCount;
            TotalDownloadBytes = downloadFsm.TotalDownloadBytes;
        }

        if (_machine.HasError)
        {
            Status = EOperationStatus.Failed;
            Error = _machine.CurrentError;
        }
        else
        {
            Status = EOperationStatus.Succeed;
        }

        _steps = ESteps.Done;
    }
}
```

#### 自定义异步操作示例
```csharp
public class CustomDataLoadOperation : GameAsyncOperation
{
    private enum ESteps
    {
        None,
        LoadFile,
        ParseData,
        Done
    }

    private ESteps _steps = ESteps.None;
    private string _filePath;
    private byte[] _fileData;
    private CustomData _resultData;

    public CustomData Result => _resultData;

    public CustomDataLoadOperation(string filePath)
    {
        _filePath = filePath;
    }

    protected override void OnStart()
    {
        _steps = ESteps.LoadFile;
        Progress = 0f;
    }

    protected override void OnUpdate()
    {
        switch (_steps)
        {
            case ESteps.LoadFile:
                // 模拟文件加载
                _fileData = File.ReadAllBytes(_filePath);
                Progress = 0.5f;
                _steps = ESteps.ParseData;
                break;

            case ESteps.ParseData:
                // 模拟数据解析
                _resultData = JsonUtility.FromJson<CustomData>(
                    Encoding.UTF8.GetString(_fileData));
                Progress = 1f;
                Status = EOperationStatus.Succeed;
                _steps = ESteps.Done;
                break;
        }
    }

    protected override void OnAbort()
    {
        // 清理资源
        _fileData = null;
        _resultData = null;
    }
}
```

## 五、ProviderOperation资源操作体系

### 5.1 架构设计

`ProviderOperation` 是资源加载操作的核心基类，负责管理具体的资源加载逻辑。

```csharp
internal abstract class ProviderOperation : AsyncOperationBase
{
    // 资源信息
    public AssetInfo MainAssetInfo { get; protected set; }
    public UnityEngine.Object AssetObject { protected set; get; }
    public UnityEngine.Object[] AllAssetObjects { protected set; get; }
    public UnityEngine.Object[] SubAssetObjects { protected set; get; }

    // Bundle管理
    public BundleResult BundleResultObject { protected set; get; }
    protected List<LoadBundleFileOperation> _bundleLoaders;

    // 引用计数
    public int RefCount { get; private set; }

    // Handle管理
    private readonly List<HandleBase> _handles = new List<HandleBase>();
    private readonly LinkedList<WeakReference<HandleBase>> _weakReferences
        = new LinkedList<WeakReference<HandleBase>>();
}
```

### 5.2 Handle创建和管理

```csharp
// 创建强引用Handle
public T CreateHandle<T>() where T : HandleBase
{
    RefCount++;
    HandleBase handle = HandleFactory.CreateHandle(this, typeof(T));
    _handles.Add(handle);

    // 如果启用弱引用
    if (_resManager.UseWeakReferenceHandle)
    {
        var weakRef = new WeakReference<HandleBase>(handle);
        _weakReferences.AddLast(weakRef);
    }

    return handle as T;
}

// 释放Handle
public void ReleaseHandle(HandleBase handle)
{
    if (RefCount <= 0)
        throw new Exception("Should never get here !");

    RefCount--;
    _handles.Remove(handle);

    // 清理弱引用
    var node = _weakReferences.First;
    while (node != null)
    {
        var next = node.Next;
        if (node.Value.TryGetTarget(out var target) && ReferenceEquals(target, handle))
        {
            _weakReferences.Remove(node);
            break;
        }
        node = next;
    }
}
```

### 5.3 弱引用机制

```csharp
// 检查是否启用弱引用
public bool UseWeakReferenceHandle { get; set; }

// 清理失效的弱引用
public void ClearInvalidWeakReferences()
{
    var node = _weakReferences.First;
    while (node != null)
    {
        var next = node.Next;
        if (!node.Value.TryGetTarget(out _))
        {
            _weakReferences.Remove(node);
        }
        node = next;
    }
}

// 获取有效Handle数量
public int GetValidHandleCount()
{
    int count = 0;
    var node = _weakReferences.First;
    while (node != null)
    {
        if (node.Value.TryGetTarget(out _))
            count++;
        node = node.Next;
    }
    return count;
}
```

## 六、Handle资源句柄系统

### 6.1 HandleBase基类

```csharp
public abstract class HandleBase : IEnumerator, IDisposable
{
    private readonly AssetInfo _assetInfo;
    internal ProviderOperation Provider { private set; get; }
    private bool _isDisposed = false;

    protected HandleBase(ProviderOperation provider)
    {
        Provider = provider;
        _assetInfo = provider.MainAssetInfo;
    }

    // 状态查询接口
    public virtual EOperationStatus Status => Provider.Status;
    public virtual float Progress => Provider.Progress;
    public virtual bool IsDone => Provider.IsDone;
    public virtual string Error => Provider.Error;

    // 资源信息接口
    public AssetInfo GetAssetInfo() => _assetInfo;
    public virtual DownloadStatus GetDownloadStatus() => Provider.GetDownloadStatus();

    // 异步支持
    public virtual Task Task => Provider.Task;

    // 协程支持
    bool IEnumerator.MoveNext() => !IsDone;
    object IEnumerator.Current => null;
    void IEnumerator.Reset() { }

    // 资源释放
    public virtual void Dispose()
    {
        if (_isDisposed == false)
        {
            _isDisposed = true;
            Provider.ReleaseHandle(this);
        }
    }
}
```

### 6.2 具体Handle实现

#### AssetHandle - 资源加载句柄
```csharp
public sealed class AssetHandle : HandleBase
{
    internal AssetHandle(ProviderOperation provider) : base(provider) { }

    // 资源对象访问
    public UnityEngine.Object AssetObject => Provider.AssetObject;

    // 类型安全访问
    public TAsset GetAssetObject<TAsset>() where TAsset : UnityEngine.Object
    {
        return Provider.AssetObject as TAsset;
    }

    // 实例化支持
    public GameObject InstantiateSync()
    {
        if (IsDone == false)
            throw new Exception("Operation is not completed !");

        if (Status == EOperationStatus.Failed)
            throw new Exception($"Operation failed ! Error : {Error}");

        var gameObject = GetAssetObject<GameObject>();
        if (gameObject == null)
            throw new Exception("Asset object is invalid !");

        return UnityEngine.Object.Instantiate(gameObject);
    }

    public InstantiateOperation InstantiateAsync()
    {
        if (IsDone == false)
            throw new Exception("Operation is not completed !");

        if (Status == EOperationStatus.Failed)
            throw new Exception($"Operation failed ! Error : {Error}");

        var gameObject = GetAssetObject<GameObject>();
        if (gameObject == null)
            throw new Exception("Asset object is invalid !");

        return InstantiateOperation.InstantiateAsync(gameObject, this);
    }
}
```

#### SceneHandle - 场景加载句柄
```csharp
public class SceneHandle : HandleBase
{
    internal SceneHandle(ProviderOperation provider) : base(provider) { }

    // 场景信息
    public string SceneName => GetAssetInfo().AssetPath;
    public UnityEngine.SceneManagement.Scene SceneObject { get; internal set; }

    // 场景控制
    public bool ActivateScene()
    {
        if (IsDone == false)
            return false;

        var scene = Provider.BundleResultObject.SceneObject;
        if (scene.IsValid() == false)
            return false;

        SceneObject = scene;
        return true;
    }

    public bool UnSuspend()
    {
        if (IsDone == false || Status == EOperationStatus.Failed)
            return false;

        return Provider.BundleResultObject.UnSuspend();
    }

    // 场景卸载
    public UnloadSceneOperation UnloadAsync()
    {
        if (IsDone == false)
            throw new Exception("Operation is not completed !");

        if (Status == EOperationStatus.Failed)
            throw new Exception($"Operation failed ! Error : {Error}");

        return new UnloadSceneOperation(this);
    }
}
```

#### SubAssetsHandle - 子资源句柄
```csharp
public sealed class SubAssetsHandle : HandleBase
{
    internal SubAssetsHandle(ProviderOperation provider) : base(provider) { }

    // 子资源访问
    public IReadOnlyList<UnityEngine.Object> SubAssetObjects => Provider.SubAssetObjects;

    // 按名称获取子资源
    public TObject GetSubAssetObject<TObject>(string assetName) where TObject : UnityEngine.Object
    {
        if (IsDone == false || Status == EOperationStatus.Failed)
            return null;

        foreach (var assetObject in Provider.SubAssetObjects)
        {
            if (assetObject is TObject typedObject && assetObject.name == assetName)
                return typedObject;
        }
        return null;
    }

    // 按类型获取子资源
    public TObject[] GetSubAssetObjects<TObject>() where TObject : UnityEngine.Object
    {
        if (IsDone == false || Status == EOperationStatus.Failed)
            return null;

        var result = new List<TObject>();
        foreach (var assetObject in Provider.SubAssetObjects)
        {
            if (assetObject is TObject typedObject)
                result.Add(typedObject);
        }
        return result.ToArray();
    }
}
```

#### RawFileHandle - 原生文件句柄
```csharp
public class RawFileHandle : HandleBase
{
    internal RawFileHandle(ProviderOperation provider) : base(provider) { }

    // 文件数据访问
    public byte[] GetRawFileData()
    {
        if (IsDone == false || Status == EOperationStatus.Failed)
            return null;

        return Provider.BundleResultObject.GetRawFileData();
    }

    public string GetRawFileText()
    {
        var fileData = GetRawFileData();
        if (fileData == null)
            return null;

        return Encoding.UTF8.GetString(fileData);
    }

    public string GetRawFilePath()
    {
        if (IsDone == false || Status == EOperationStatus.Failed)
            return null;

        return Provider.BundleResultObject.FileLoadPath;
    }
}
```

### 6.3 HandleFactory工厂模式

```csharp
internal static class HandleFactory
{
    private static readonly Dictionary<Type, Func<ProviderOperation, HandleBase>> _handleFactory;

    static HandleFactory()
    {
        _handleFactory = new Dictionary<Type, Func<ProviderOperation, HandleBase>>();

        // 注册Handle创建函数
        _handleFactory.Add(typeof(AssetHandle), operation => new AssetHandle(operation));
        _handleFactory.Add(typeof(SceneHandle), operation => new SceneHandle(operation));
        _handleFactory.Add(typeof(SubAssetsHandle), operation => new SubAssetsHandle(operation));
        _handleFactory.Add(typeof(AllAssetsHandle), operation => new AllAssetsHandle(operation));
        _handleFactory.Add(typeof(RawFileHandle), operation => new RawFileHandle(operation));
    }

    public static HandleBase CreateHandle(ProviderOperation operation, Type type)
    {
        if (_handleFactory.TryGetValue(type, out var creator))
        {
            return creator(operation);
        }
        throw new Exception($"Not found handle creator : {type.FullName}");
    }
}
```

## 七、时间片控制和性能优化

### 7.1 时间片控制原理

时间片控制是防止异步操作影响帧率的关键机制。

```csharp
// 时间片配置
public static void SetOperationSystemMaxTimeSlice(long milliseconds)
{
    if (milliseconds < 10)
    {
        milliseconds = 10;
        YooLogger.Warning($"MaxTimeSlice minimum value is 10 milliseconds.");
    }
    OperationSystem.MaxTimeSlice = milliseconds;
}

// 繁忙状态检测
public static bool IsBusy
{
    get
    {
        return _watch.ElapsedMilliseconds - _frameTime >= MaxTimeSlice;
    }
}
```

### 7.2 性能优化策略

#### 批量处理优化
```csharp
// 批量操作更新
for (int i = 0; i < _operations.Count; i++)
{
    var operation = _operations[i];
    operation.UpdateOperation();

    // 时间片检查，防止单帧卡顿
    if (IsBusy)
        break;
}
```

#### 懒加载清理
```csharp
// 延迟清理已完成操作，避免频繁的内存分配
for (int i = _operations.Count - 1; i >= 0; i--)
{
    var operation = _operations[i];
    if (operation.IsDone)
    {
        _operations.RemoveAt(i);
        continue;
    }
}
```

#### 优先级优化
```csharp
// 只有在必要时才进行排序
if (_operations.Count > 0 && _operations[0].Priority > 0)
{
    _operations.Sort();
}
```

### 7.3 性能监控

```csharp
// 操作统计信息
public struct DebugOperationInfo
{
    public string OperationName;     // 操作名称
    public string OperationDesc;     // 操作描述
    public uint Priority;            // 优先级
    public float Progress;           // 进度
    public string BeginTime;         // 开始时间
    public long ProcessTime;         // 处理耗时
    public string Status;            // 状态
    public List<DebugOperationInfo> Childs; // 子操作
}

// 获取调试信息
internal static DebugOperationInfo GetDebugOperationInfo(AsyncOperationBase operation)
{
    DebugOperationInfo info = new DebugOperationInfo
    {
        OperationName = operation.GetType().Name,
        Priority = operation.Priority,
        Progress = operation.Progress,
        BeginTime = operation.BeginTime,
        ProcessTime = operation.ProcessTime,
        Status = operation.Status.ToString(),
    };

    // 添加子操作信息
    foreach (var child in operation.Childs)
    {
        if (info.Childs == null)
            info.Childs = new List<DebugOperationInfo>();
        info.Childs.Add(GetDebugOperationInfo(child));
    }

    return info;
}
```

## 八、实际使用示例

### 8.1 基础资源加载

```csharp
public class ResourceLoader : MonoBehaviour
{
    private void Start()
    {
        // 方式1：Task-based异步加载
        LoadAssetAsync();

        // 方式2：事件回调加载
        LoadAssetWithCallback();

        // 方式3：协程加载
        StartCoroutine(LoadAssetCoroutine());
    }

    // Task-based异步加载
    private async void LoadAssetAsync()
    {
        try
        {
            var handle = await YooAssets.LoadAssetAsync<GameObject>("Prefabs/Player");

            if (handle.Status == EOperationStatus.Succeed)
            {
                GameObject player = handle.InstantiateSync();
                player.transform.position = Vector3.zero;
            }
            else
            {
                Debug.LogError($"加载失败: {handle.Error}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"加载异常: {e}");
        }
    }

    // 事件回调加载
    private void LoadAssetWithCallback()
    {
        var handle = YooAssets.LoadAssetAsync<GameObject>("Prefabs/Enemy");

        handle.Completed += (operation) =>
        {
            if (operation.Status == EOperationStatus.Succeed)
            {
                GameObject enemy = operation.InstantiateSync();
                enemy.transform.position = new Vector3(5, 0, 0);
            }
        };
    }

    // 协程加载
    private IEnumerator LoadAssetCoroutine()
    {
        var handle = YooAssets.LoadAssetAsync<GameObject>("Prefabs/UI");

        yield return handle; // 等待操作完成

        if (handle.Status == EOperationStatus.Succeed)
        {
            GameObject ui = handle.InstantiateSync();
            ui.transform.SetParent(transform);
        }
    }
}
```

### 8.2 场景管理

```csharp
public class SceneManager : MonoBehaviour
{
    public async void LoadGameplayScene()
    {
        try
        {
            // 1. 异步加载场景
            var sceneHandle = await YooAssets.LoadSceneAsync(
                "Gameplay",
                UnityEngine.SceneManagement.LoadSceneMode.Additive);

            if (sceneHandle.Status == EOperationStatus.Succeed)
            {
                // 2. 激活场景
                if (sceneHandle.ActivateScene())
                {
                    Debug.Log("场景加载并激活成功");

                    // 3. 场景加载完成后的处理
                    OnGameplaySceneLoaded(sceneHandle.SceneObject);
                }
                else
                {
                    Debug.LogError("场景激活失败");
                }
            }
            else
            {
                Debug.LogError($"场景加载失败: {sceneHandle.Error}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"场景加载异常: {e}");
        }
    }

    private async void UnloadGameplayScene()
    {
        var sceneHandle = YooAssets.GetSceneHandle("Gameplay");
        if (sceneHandle != null)
        {
            var unloadOp = await sceneHandle.UnloadAsync();

            if (unloadOp.Status == EOperationStatus.Succeed)
            {
                Debug.Log("场景卸载成功");
            }
            else
            {
                Debug.LogError($"场景卸载失败: {unloadOp.Error}");
            }
        }
    }

    private void OnGameplaySceneLoaded(UnityEngine.SceneManagement.Scene scene)
    {
        // 场景加载完成后的初始化逻辑
        var rootObjects = scene.GetRootGameObjects();
        foreach (var obj in rootObjects)
        {
            if (obj.TryGetComponent<GameplayManager>(out var manager))
            {
                manager.InitializeGameplay();
                break;
            }
        }
    }
}
```

### 8.3 自定义异步操作

```csharp
public class CustomAsyncOperationExample : MonoBehaviour
{
    public async void ExecuteCustomOperation()
    {
        var operation = new CustomDataLoadOperation("Data/config.json");

        // 可以通过多种方式等待操作完成
        await operation; // Task-based

        if (operation.Status == EOperationStatus.Succeed)
        {
            CustomData config = operation.Result;
            Debug.Log($"配置加载成功: {config}");
        }
        else
        {
            Debug.LogError($"配置加载失败: {operation.Error}");
        }
    }
}

// 自定义配置数据类
[Serializable]
public class CustomData
{
    public string gameVersion;
    public int maxPlayers;
    public string[] availableMaps;
}
```

### 8.4 批量资源加载

```csharp
public class BatchResourceLoader : MonoBehaviour
{
    public async void LoadMultipleAssets()
    {
        var tasks = new List<Task<AssetHandle>>();

        // 批量启动加载任务
        string[] assetPaths = {
            "Prefabs/Player",
            "Prefabs/Enemy",
            "Prefabs/UI/HealthBar",
            "Textures/grass",
            "Audio/background_music"
        };

        foreach (var path in assetPaths)
        {
            tasks.Add(YooAssets.LoadAssetAsync<UnityEngine.Object>(path));
        }

        // 等待所有任务完成
        var results = await Task.WhenAll(tasks);

        // 处理结果
        foreach (var handle in results)
        {
            if (handle.Status == EOperationStatus.Succeed)
            {
                Debug.Log($"加载成功: {handle.GetAssetInfo().AssetPath}");
            }
            else
            {
                Debug.LogError($"加载失败: {handle.GetAssetInfo().AssetPath}, 错误: {handle.Error}");
            }
        }
    }
}
```

## 九、最佳实践和注意事项

### 9.1 性能优化建议

#### 1. 合理设置时间片
```csharp
// 根据项目需求调整时间片
// 移动设备建议较小的值
YooAssets.SetOperationSystemMaxTimeSlice(16); // 约60fps

// PC设备可以使用较大值
YooAssets.SetOperationSystemMaxTimeSlice(33); // 约30fps
```

#### 2. 优先级管理
```csharp
// 关键资源设置高优先级
var criticalHandle = YooAssets.LoadAssetAsync<GameObject>("Prefabs/MainMenu");
// 假设可以设置优先级（需要扩展）
// criticalHandle.Priority = 100;

// 普通资源使用默认优先级
var normalHandle = YooAssets.LoadAssetAsync<GameObject>("Prefabs/Decorations");
```

#### 3. 及时释放资源
```csharp
public class ResourceManager : MonoBehaviour
{
    private List<HandleBase> _activeHandles = new List<HandleBase>();

    private async void LoadUI()
    {
        var handle = await YooAssets.LoadAssetAsync<GameObject>("UI/MainMenu");
        _activeHandles.Add(handle);

        // 使用UI...
    }

    private void OnDestroy()
    {
        // 及时释放所有Handle
        foreach (var handle in _activeHandles)
        {
            handle?.Dispose();
        }
        _activeHandles.Clear();
    }
}
```

### 9.2 错误处理策略

#### 1. 网络异常处理
```csharp
public async void LoadWithRetry(string assetPath, int maxRetries = 3)
{
    int retryCount = 0;

    while (retryCount < maxRetries)
    {
        try
        {
            var handle = await YooAssets.LoadAssetAsync<UnityEngine.Object>(assetPath);

            if (handle.Status == EOperationStatus.Succeed)
            {
                return; // 成功，退出重试循环
            }
            else
            {
                Debug.LogWarning($"加载失败，第{retryCount + 1}次重试: {handle.Error}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"加载异常，第{retryCount + 1}次重试: {e}");
        }

        retryCount++;
        if (retryCount < maxRetries)
        {
            await Task.Delay(1000 * retryCount); // 递增延迟
        }
    }

    Debug.LogError($"加载最终失败，已达到最大重试次数: {assetPath}");
}
```

#### 2. 内存监控
```csharp
public class MemoryMonitor : MonoBehaviour
{
    public float maxMemoryMB = 500f;

    private void Update()
    {
        float currentMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemory(false) / (1024f * 1024f);

        if (currentMemory > maxMemoryMB)
        {
            Debug.LogWarning($"内存使用过高: {currentMemory:F2}MB");

            // 触发垃圾回收
            System.GC.Collect();

            // 可以在这里实现资源卸载逻辑
            YooAssets.UnloadUnusedAssets();
        }
    }
}
```

### 9.3 常见问题和解决方案

| 问题 | 原因 | 解决方案 |
|------|------|----------|
| **资源加载失败** | 网络问题、文件损坏、路径错误 | 实现重试机制、检查清单文件、验证路径 |
| **内存占用过高** | 资源未及时释放、循环引用 | 及时释放Handle、定期垃圾回收、监控内存使用 |
| **加载性能差** | 优先级设置不当、时间片配置问题 | 调整优先级、优化时间片设置、预加载关键资源 |
| **异步操作死锁** | 同步等待异步操作、循环依赖 | 避免同步等待、检查依赖关系、使用超时机制 |

## 十、总结

YooAsset的AsyncOperation系统是一个设计精良、功能完善的异步操作管理框架，具有以下核心优势：

### 技术优势

1. **统一调度管理**
   - 中央调度器统一管理所有异步操作
   - 优先级调度确保重要操作优先执行
   - 时间片控制保证帧率稳定

2. **高性能设计**
   - 批量处理提高操作吞吐量
   - 懒加载清理优化内存使用
   - 引用计数和弱引用机制防止内存泄漏

3. **开发友好**
   - 多种异步编程模式支持
   - 简化的基类设计便于扩展
   - 完整的调试信息和性能监控

4. **扩展性强**
   - 模块化设计支持自定义扩展
   - 标准化接口便于集成
   - 工厂模式支持类型扩展

### 应用价值

- **大型游戏项目**：处理复杂的资源管理和场景切换
- **多平台开发**：适配不同平台的性能需求
- **团队协作**：标准化的异步操作接口
- **性能优化**：精细的资源加载和内存管理

### 学习价值

这个AsyncOperation系统是现代异步编程的优秀实践案例，展示了：

- **分层架构设计**：清晰的职责分离和接口定义
- **设计模式应用**：工厂模式、观察者模式、策略模式的实际应用
- **性能优化技巧**：时间片控制、批量处理、内存管理的最佳实践
- **异步编程模式**：Task-based、事件回调、协程等多种模式的统一实现

通过深入理解这个系统的设计思想和实现机制，可以为其他项目的异步操作管理提供宝贵的参考和借鉴。