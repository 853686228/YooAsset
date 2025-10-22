# YooAsset三种Collector类型详细对比

通过分析YooAsset源码，详细解释`MainAssetCollector`、`StaticAssetCollector`和`DependAssetCollector`的区别。

## 🎯 三种Collector类型定义

```csharp
public enum ECollectorType
{
    /// <summary>
    /// 收集参与打包的主资源对象，并写入到资源清单的资源列表里（可以通过代码加载）。
    /// </summary>
    MainAssetCollector,

    /// <summary>
    /// 收集参与打包的主资源对象，但不写入到资源清单的资源列表里（无法通过代码加载）。
    /// </summary>
    StaticAssetCollector,

    /// <summary>
    /// 收集参与打包的依赖资源对象，但不写入到资源清单的资源列表里（无法通过代码加载）。
    /// 注意：如果依赖资源对象没有被主资源对象引用，则不参与打包构建。
    /// </summary>
    DependAssetCollector,
}
```

---

## 📋 详细对比分析

### 1. **MainAssetCollector**（主资源收集器）

#### 🎯 作用
- **参与打包**：资源会被打包进AssetBundle
- **写入清单**：会出现在资源清单中
- **可代码加载**：可以通过`YooAssets.LoadAssetAsync()`等API动态加载

#### 💡 使用场景
```csharp
// 游戏中需要动态加载的资源
// 场景：关卡场景、UI界面
// 预制体：角色、道具、特效
// 音频：背景音乐、音效
// 配置：游戏参数、关卡数据

// 加载示例
var handle = YooAssets.LoadAssetAsync<GameObject>("Player prefab");
yield return handle;
GameObject player = Instantiate(handle.AssetObject);
```

#### 🔧 特性
- **完整功能**：支持标签、寻址规则等所有功能
- **可搜索**：出现在资源清单中，可通过地址查找
- **可管理**：支持引用计数和生命周期管理

### 2. **StaticAssetCollector**（静态资源收集器）

#### 🎯 作用
- **参与打包**：资源会被打包进AssetBundle
- **不写入清单**：不会出现在资源清单中
- **不可直接加载**：无法通过代码API直接加载

#### 💡 使用场景
```csharp
// 固定场景引用的资源
// 场景中直接拖拽的组件引用
// 静态UI元素
// 场景内的装饰对象

// 场景A的静态资源：
// - 地面纹理（被地面Material引用）
// - 建筑模型（被场景GameObject引用）
// - UI图标（被UI预制体引用）

// 这些资源随着场景A一起打包，但无法单独加载
```

#### 🔧 特性
- **隐式加载**：当加载引用它的主资源时自动加载
- **自动管理**：由Unity的依赖关系自动处理
- **可忽略**：构建时可以通过`IgnoreStaticCollector`标志忽略

### 3. **DependAssetCollector**（依赖资源收集器）

#### 🎯 作用
- **参与打包**：资源会被打包进AssetBundle
- **不写入清单**：不会出现在资源清单中
- **不可直接加载**：无法通过代码API直接加载
- **必须被引用**：如果没有主资源引用则不会打包

#### 💡 使用场景
```csharp
// 通用依赖资源
// 共享材质球（多个模型使用）
// 纹理贴图（多个UI使用）
// 动画片段（多个角色使用）
// 着色器（多个特效使用）

// 例如：
// - "通用木纹贴图"（被多个家具模型使用）
// - "金属材质球"（被多个道具使用）
// - "走路动画"（被多个角色使用）
```

#### 🔧 特性
- **零引用清理**：构建时自动移除未被任何主资源引用的依赖资源
- **共享优化**：多个主资源共享同一个依赖资源，避免重复打包
- **可忽略**：构建时可以通过`IgnoreDependCollector`标志忽略

---

## 📊 三者对比表

| 特性 | MainAssetCollector | StaticAssetCollector | DependAssetCollector |
|------|------------------|-------------------|-------------------|
| **参与打包** | ✅ | ✅ | ✅ |
| **写入清单** | ✅ | ❌ | ❌ |
| **代码可加载** | ✅ | ❌ | ❌ |
| **支持标签** | ✅ | ❌ | ❌ |
| **可搜索** | ✅ | ❌ | ❌ |
| **可被忽略** | ❌ | ✅ | ✅ |
| **引用计数** | ✅ | ❌ | ❌ |
| **生命周期管理** | ✅ | ❌ | ❌ |
| **寻址规则** | ✅ | ✅ | ✅ |
| **打包规则** | ✅ | ✅ | ✅ |
| **过滤规则** | ✅ | ✅ | ✅ |

---

## 🔄 工作流程差异

### MainAssetCollector流程
```
1. 资源收集 → 2. 写入清单 → 3. 可通过API加载 → 4. 支持引用计数管理
```

### StaticAssetCollector流程
```
1. 资源收集 → 2. 打包但不写入清单 → 3. 隐式加载 → 4. 自动释放
```

### DependAssetCollector流程
```
1. 依赖分析 → 2. 检查是否被引用 → 3. 如果有引用则打包 → 4. 隐式加载
```

---

## 🎯 构建时处理差异

### 资源清单处理
```csharp
// TaskGetBuildMap.cs:38
if (collectAssetInfo.CollectorType != ECollectorType.MainAssetCollector)
{
    // 只有MainAssetCollector的资源才能保留标签
    if (collectAssetInfo.AssetTags.Count > 0)
    {
        collectAssetInfo.AssetTags.Clear();
        BuildLogger.Warning("Remove asset tags that don't work");
    }
}
```

### 零引用依赖清理
```csharp
// TaskGetBuildMap.cs:151-195
// 1. 获取所有主资源引用的依赖资源集合
// 2. 找出所有零引用的依赖资源集合
// 3. 移除所有零引用的依赖资源
// 4. 生成独立资源报告
```

### 可忽略性处理
```csharp
// AssetBundleCollector.cs:142-154
bool ignoreStaticCollector = command.IsFlagSet(ECollectFlags.IgnoreStaticCollector);
bool ignoreDependCollector = command.IsFlagSet(ECollectFlags.IgnoreDependCollector);

if (ignoreStaticCollector && CollectorType == ECollectorType.StaticAssetCollector)
    return new List<CollectAssetInfo>();

if (ignoreDependCollector && CollectorType == ECollectorType.DependAssetCollector)
    return new List<CollectAssetInfo>();
```

---

## 💡 实际项目配置示例

### 游戏资源配置示例

```xml
<!-- 主场景和UI -->
<Collector CollectPath="Assets/GameRes/Scenes/MainMenu.unity"
          CollectorType="MainAssetCollector"
          Tags="Scene,MainMenu" />

<Collector CollectPath="Assets/GameRes/Prefabs/UI/PlayerPanel.prefab"
          CollectorType="MainAssetCollector"
          Tags="UI,Player" />

<!-- 静态资源 -->
<Collector CollectPath="Assets/GameRes/Materials/SceneMaterials"
          CollectorType="StaticAssetCollector" />

<!-- 共享依赖资源 -->
<Collector CollectPath="Assets/GameRes/Textures/Shared"
          CollectorType="DependAssetCollector" />
```

### 典型文件夹结构
```
Assets/GameRes/
├── Scenes/              # MainAssetCollector (需要动态加载)
├── Prefabs/            # MainAssetCollector (需要动态加载)
├── Audio/              # MainAssetCollector (需要动态加载)
├── UI/                 # MainAssetCollector (需要动态加载)
├── Materials/          # StaticAssetCollector (场景专用)
├── Textures/           # DependAssetCollector (共享纹理)
├── Materials/          # DependAssetCollector (共享材质)
└── Animations/         # DependAssetCollector (共享动画)
```

---

## 💡 最佳实践建议

### 何时使用MainAssetCollector
- ✅ 需要通过代码动态加载的资源
- ✅ 场景、预制体、UI面板等主要游戏对象
- ✅ 需要精细化资源管理的对象
- ✅ 需要异步加载和卸载的资源
- ✅ 需要分阶段加载的大型资源

### 何时使用StaticAssetCollector
- ✅ 场景中固定的引用资源
- ✅ UI预制体的固定依赖
- ✅ 不会被其他地方共享的资源
- ✅ 随主资源一起打包的辅助资源
- ✅ 需要隐式加载的配置资源

### 何时使用DependAssetCollector
- ✅ 多个资源共享的通用资源
- ✅ 纹理、材质、动画等依赖资源
- ✅ 需要避免重复打包的共享对象
- ✅ 希望打包在一起但不需要单独管理的资源
- ✅ 通用的音效、特效素材

---

## ⚠️ 常见错误和注意事项

### 1. 混淆Static和Depend
```csharp
// ❌ 错误：把场景专用资源放在DependAssetCollector
// 场景专用材质应该用StaticAssetCollector
<Collector CollectPath="Assets/GameRes/Scenes/MainMenu/Materials"
          CollectorType="DependAssetCollector" /> // 错误

// ✅ 正确：共享资源用DependAssetCollector
<Collector CollectPath="Assets/GameRes/Materials/Shared"
          CollectorType="DependAssetCollector" /> // 正确
```

### 2. 依赖资源零引用
```csharp
// ⚠️ 警告：DependAssetCollector的资源必须被主资源引用
// 如果配置了DependAssetCollector但没有主资源引用它
// 构建时会警告并自动移除该资源
```

### 3. 资源加载失败
```csharp
// ❌ 错误：尝试加载Static或Depend资源
var handle = YooAssets.LoadAssetAsync<Texture>("SharedTexture"); // 会失败

// ✅ 正确：只有MainAssetCollector的资源才能直接加载
var handle = YooAssets.LoadAssetAsync<GameObject>("PlayerPrefab");
```

---

## 🎉 总结

三种Collector类型的本质区别在于**资源清单的可见性和API的可访问性**：

- **MainAssetCollector**：完全可见、可管理、可加载
- **StaticAssetCollector**：不可见、不可加载、随主资源打包
- **DependAssetCollector**：不可见、不可加载、作为依赖打包

### 关键要点
1. **清单可见性**决定了资源是否可通过API访问
2. **引用关系**决定了依赖资源的打包策略
3. **零引用清理**确保依赖资源的有效性
4. **构建标志**提供了灵活的资源收集控制

合理使用这三种类型可以实现高效的资源管理和优化，是YooAsset资源管理系统的核心设计理念！