# YooAsset Bundle卸载优化方案

## 核心改进思路

将当前粗暴的`AssetBundle.Unload(true)`改为更精细的`AssetBundle.Unload(false)`，配合智能的Asset对象生命周期管理。

## 当前问题分析

### 现有实现
```csharp
// AssetBundleResult.cs - 当前实现
public override void UnloadBundleFile()
{
    if (_assetBundle != null)
    {
        _assetBundle.Unload(true); // 问题：同时释放Bundle镜像和Asset对象
    }
    // ...
}
```

### 存在的风险
1. **对象悬空风险**：如果Asset对象仍在被使用，强制卸载会导致引用失效
2. **重复加载开销**：相同资源可能需要重新从Bundle加载
3. **性能波动**：频繁的加载/卸载导致性能不稳定

## 优化方案设计

### 1. 修改Bundle卸载策略
```csharp
// 优化后的实现
public override void UnloadBundleFile()
{
    if (_assetBundle != null)
    {
        _assetBundle.Unload(false); // 改进：只释放Bundle镜像，保留Asset对象
    }
    // ...
}
```

### 2. 增强Provider销毁逻辑
```csharp
// ProviderOperation.DestroyProvider() 增强
public void DestroyProvider()
{
    IsDestroyed = true;

    // 标记Bundle可卸载，但不立即卸载Asset对象
    if (BundleResultObject != null)
    {
        BundleResultObject.UnloadBundleFile(); // 现在使用Unload(false)
        BundleResultObject = null;
    }

    // 减少BundleLoader引用
    foreach (var bundleLoader in _bundleLoaders)
    {
        bundleLoader.Release();
    }

    // 可选：立即触发一次轻量级清理
    if (_resManager.AggressiveAssetCleanup)
    {
        _resManager.TriggerAssetCleanup(false); // 非强制清理
    }
}
```

### 3. 智能Asset清理策略
```csharp
// 新增：智能Asset清理管理器
public class SmartAssetCleanupManager
{
    private readonly ResourceManager _resourceManager;
    private float _cleanupInterval = 30f; // 30秒检查一次
    private float _lastCleanupTime;
    private int _assetReleaseThreshold = 100; // Asset对象数量阈值

    public void Update()
    {
        if (Time.time - _lastCleanupTime > _cleanupInterval)
        {
            CheckAndCleanupAssets();
            _lastCleanupTime = Time.time;
        }
    }

    private void CheckAndCleanupAssets()
    {
        // 检查内存压力
        if (ShouldTriggerCleanup())
        {
            // 渐进式清理，避免卡顿
            _resourceManager.UnloadUnusedAssetsAsync(loopCount: 3);
        }
    }

    private bool ShouldTriggerCleanup()
    {
        // 综合考虑多个因素：
        // 1. Asset对象数量
        // 2. 内存使用情况
        // 3. 帧率性能
        return GetAssetCount() > _assetReleaseThreshold ||
               GetMemoryPressure() > 0.8f;
    }
}
```

### 4. 配置化的清理策略
```csharp
// 新增：Bundle卸载配置
public class BundleUnloadConfig
{
    public bool UseUnloadFalse = true;           // 是否使用Unload(false)
    public bool AggressiveAssetCleanup = false;  // 是否积极清理Asset对象
    public float CleanupInterval = 30f;          // 清理检查间隔
    public int CleanupLoopCount = 5;             // 每次清理的循环次数
    public long MemoryPressureThreshold = 500 * 1024 * 1024; // 500MB阈值
}
```

## 实现步骤

### Phase 1: 核心修改
1. 修改`AssetBundleResult.UnloadBundleFile()`使用`Unload(false)`
2. 更新相关测试用例
3. 验证基础功能正常

### Phase 2: 智能清理
1. 实现`SmartAssetCleanupManager`
2. 添加配置系统
3. 集成到ResourceManager

### Phase 3: 性能优化
1. 添加内存监控
2. 实现自适应清理策略
3. 性能测试和调优

## 预期收益

### 内存安全性
- ✅ 消除对象悬空风险
- ✅ 更可预测的内存使用模式
- ✅ 减少Crash率

### 性能表现
- ✅ 减少重复资源加载
- ✅ 更平滑的帧率表现
- ✅ 降低GC压力

### 开发体验
- ✅ 更少的资源管理相关Bug
- ✅ 更灵活的配置选项
- ✅ 更好的调试支持

## 风险控制

### 内存增长监控
```csharp
// 添加内存监控
public class MemoryMonitor
{
    public void TrackMemoryUsage()
    {
        long totalMemory = GC.GetTotalMemory(false);
        if (totalMemory > _memoryWarningThreshold)
        {
            // 触发强制清理或警告
            TriggerForceCleanup();
        }
    }
}
```

### 回退机制
```csharp
// 提供回退选项
if (_useUnloadFalse && GetMemoryPressure() > _criticalThreshold)
{
    // 在内存压力下回退到原有策略
    _assetBundle.Unload(true);
}
```

## 总结

这个改进方案在保持YooAsset现有架构优势的基础上，显著提升了内存管理的安全性和性能表现。通过配置化的方式，可以让开发者根据具体项目需求选择最适合的清理策略。

建议先在小规模项目中验证，确认收益后再推广到大型项目。