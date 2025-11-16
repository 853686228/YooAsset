# YooAsset Runtime系统整体框架分析

## 一、系统架构总览

YooAsset Runtime采用分层架构设计，共分为7个核心模块：

```
┌─────────────────────────────────────────────────────────┐
│                    用户API层                               │
│              (YooAssets静态类)                          │
├─────────────────────────────────────────────────────────┤
│                  资源包管理层                              │
│              (ResourcePackage)                         │
├─────────────────────────────────────────────────────────┤
│          资源管理层          │      异步操作层             │
│     (ResourceManager)     │   (OperationSystem)        │
├─────────────────────────────────────────────────────────┤
│  文件系统层  │  下载系统层  │  服务接口层  │  诊断系统层     │
│(FileSystem) │(DownloadSystem)│(Services) │(Diagnostic) │
└─────────────────────────────────────────────────────────┘
```

### 1.1 目录结构分析

```
Runtime/
├── YooAssets.cs                    # 主入口点和全局管理器
├── YooAssetsDriver.cs              # 驱动器，负责生命周期管理
├── InitializeParameters.cs        # 初始化参数和运行模式定义
├── YooAsset.asmdef                 # 程序集定义文件
├── Utility/                        # 工具类模块
├── Settings/                       # 配置管理模块
├── Services/                       # 服务接口模块
├── ResourcePackage/                # 资源包裹核心模块
├── ResourceManager/                # 资源管理器模块
├── OperationSystem/               # 异步操作模块
├── FileSystem/                     # 文件系统模块
├── DownloadSystem/                 # 下载系统模块
├── DiagnosticSystem/              # 诊断系统模块
└── PackageInvokeBuilder/           # 包调用构建器模块
```

### 1.2 核心模块职责

| 模块 | 职责 | 核心组件 |
|------|------|----------|
| 用户API层 | 提供统一的资源管理入口 | YooAssets静态类 |
| 资源包管理层 | 管理独立的资源包实例 | ResourcePackage |
| 资源管理层 | 实际的资源加载和管理 | ResourceManager, Provider |
| 异步操作层 | 统一的异步操作调度 | OperationSystem |
| 文件系统层 | 抽象的文件操作接口 | IFileSystem实现类 |
| 下载系统层 | 文件下载和更新管理 | DownloadSystem |
| 服务接口层 | 系统扩展和服务定义 | 各种Service接口 |

## 二、核心入口点 - YooAssets类

### 2.1 设计模式

**YooAssets** 采用静态类 + 单例模式设计，作为整个框架的核心入口点。

```csharp
public static partial class YooAssets
{
    private static bool _isInitialize = false;
    private static GameObject _driver = null;
    private static readonly List<ResourcePackage> _packages = new List<ResourcePackage>();
}
```

### 2.2 核心职责

#### 1. 系统生命周期管理

| 方法 | 功能 | 实现要点 |
|------|------|----------|
| `Initialize(ILogger)` | 初始化系统 | 创建驱动器GameObject，设置日志系统 |
| `Destroy()` | 销毁系统 | 清理所有包，卸载AssetBundle |
| `Update()` | 每帧更新 | 驱动OperationSystem更新 |

```csharp
// 初始化流程
public static void Initialize(ILogger logger = null)
{
    if (_isInitialize) return;

    YooLogger.Logger = logger;
    _isInitialize = true;

    // 创建驱动器GameObject
    _driver = new UnityEngine.GameObject($"[{nameof(YooAssets)}]");
    _driver.AddComponent<YooAssetsDriver>();
    UnityEngine.Object.DontDestroyOnLoad(_driver);

    OperationSystem.Initialize();
}
```

#### 2. 资源包管理

| 方法 | 功能 | 异常处理 |
|------|------|----------|
| `CreatePackage(string)` | 创建新的资源包 | 重复包名检测 |
| `GetPackage(string)` | 获取资源包 | 包存在性验证 |
| `RemovePackage(string)` | 移除资源包 | 初始化状态检查 |
| `ContainsPackage(string)` | 检查包是否存在 | 参数验证 |

#### 3. 系统配置

- **自定义日志系统**：支持传入自定义ILogger实现
- **下载系统参数**：可自定义UnityWebRequest创建委托
- **异步系统参数**：可配置最大时间片（防止单帧卡顿）

### 2.3 YooAssetsDriver驱动器

`YooAssetsDriver` 是一个内部MonoBehaviour组件，负责系统的生命周期管理：

```csharp
internal class YooAssetsDriver : MonoBehaviour
{
    void Update()
    {
        DebugCheckDuplicateDriver(); // 检测重复驱动器
        YooAssets.Update();          // 驱动系统更新
    }

    // 编辑器下的清理逻辑
    void OnApplicationQuit()
    {
        YooAssets.ClearAllPackageOperation();
    }
}
```

**关键特性：**
- 自动检测重复驱动器实例
- 编辑器下确保IO操作正确终止
- 通过`DontDestroyOnLoad`保证场景切换时不被销毁

## 三、运行模式系统

### 3.1 支持的运行模式

YooAsset支持5种运行模式，对应不同的开发阶段和部署场景：

```csharp
public enum EPlayMode
{
    EditorSimulateMode,  // 编辑器模拟模式
    OfflinePlayMode,     // 离线运行模式
    HostPlayMode,        // 联机运行模式
    WebPlayMode,         // WebGL运行模式
    CustomPlayMode,      // 自定义运行模式
}
```

### 3.2 各模式特点和应用场景

| 运行模式 | 初始化参数类 | 适用场景 | 特点 |
|----------|--------------|----------|------|
| **EditorSimulateMode** | `EditorSimulateModeParameters` | 开发阶段 | 无需打包，直接加载Editor资源，快速迭代 |
| **OfflinePlayMode** | `OfflinePlayModeParameters` | 离线环境 | 使用本地已打包资源，无网络依赖 |
| **HostPlayMode** | `HostPlayModeParameters` | 联机环境 | 支持从远程服务器下载更新，完整热更新 |
| **WebPlayMode** | `WebPlayModeParameters` | WebGL平台 | 针对WebGL优化，特殊的文件加载策略 |
| **CustomPlayMode** | `CustomPlayModeParameters` | 特殊需求 | 自定义文件系统组合，最大灵活性 |

### 3.3 初始化参数结构

```csharp
public abstract class InitializeParameters
{
    // 同时加载Bundle文件的最大并发数
    public int BundleLoadingMaxConcurrency = int.MaxValue;

    // WebGL平台强制同步加载资源对象
    public bool WebGLForceSyncLoadAsset = false;

    // 实验性功能：弱引用资源句柄
    internal bool UseWeakReferenceHandle = false;
}
```

## 四、ResourcePackage资源包管理系统

### 4.1 核心设计理念

`ResourcePackage` 是资源管理的核心单元，每个包独立管理自己的资源和生命周期：

```csharp
public class ResourcePackage
{
    private readonly string PackageName;                    // 包名
    private ResourceManager _resourceManager;              // 资源管理器
    private IBundleQuery _bundleQuery;                     // 包查询接口
    private IPlayMode _playModeImpl;                       // 运行模式实现
    private EOperationStatus _initializeStatus;            // 初始化状态
}
```

### 4.2 核心组件关系

```
ResourcePackage
    ├── ResourceManager     // 资源加载和管理
    ├── IBundleQuery       // 包信息查询接口
    └── IPlayMode          // 运行模式实现
        ├── EditorSimulateModeImpl
        ├── OfflinePlayModeImpl
        ├── HostPlayModeImpl
        ├── WebPlayModeImpl
        └── CustomPlayModeImpl
```

### 4.3 主要功能接口

#### 1. 生命周期管理

| 方法 | 功能 | 状态管理 |
|------|------|----------|
| `InitializeAsync()` | 异步初始化包 | None → Succeed/Failed |
| `DestroyAsync()` | 异步销毁包 | 清理所有异步操作和资源 |

#### 2. 资源加载接口

| 资源类型 | 加载方法 | 返回类型 |
|----------|----------|----------|
| 场景 | `LoadSceneAsync()` | `SceneOperationHandle` |
| 游戏对象 | `InstantiateAsync()` | `InstantiateOperationHandle` |
| 原生资源 | `LoadAssetAsync()` | `AssetOperationHandle` |
| 子资源 | `LoadSubAssetsAsync()` | `SubAssetsOperationHandle` |

#### 3. 下载管理接口

| 方法 | 功能 | 特点 |
|------|------|------|
| `UpdatePackageAsync()` | 更新资源包 | 支持版本对比和增量下载 |
| `DownloadPackageAsync()` | 下载整个包 | 批量下载资源文件 |

#### 4. 信息查询接口

| 方法 | 功能 | 返回信息 |
|------|------|----------|
| `GetAssetInfo()` | 获取资源信息 | 资源路径、依赖关系、标签等 |
| `GetBundleInfo()` | 获取包信息 | 包大小、依赖包、哈希值等 |

## 五、各层系统详细分析

### 5.1 ResourceManager - 资源管理层

#### 5.1.1 Provider模式

ResourceManager采用Provider模式，不同资源类型有专门的Provider：

```csharp
// Provider类型对应关系
AssetOperationHandle      ↔ AssetProvider
SceneOperationHandle      ↔ SceneProvider
SubAssetsOperationHandle  ↔ SubAssetsProvider
InstantiateOperationHandle ↔ InstantiateProvider
```

#### 5.1.2 Handle系统

Handle是资源操作的统一句柄，提供：

- **异步操作管理**：进度、状态、完成回调
- **资源对象访问**：通过`GetAssetObject()`等方法
- **生命周期管理**：自动引用计数和资源释放
- **错误处理**：详细的错误信息和重试机制

#### 5.1.3 引用计数系统

```csharp
// 资源引用计数流程
资源加载 → 引用计数+1 → 返回Handle
Handle释放 → 引用计数-1 → 计数为0时卸载资源
```

### 5.2 OperationSystem - 异步操作层

#### 5.2.1 核心特性

| 特性 | 说明 | 实现机制 |
|------|------|----------|
| **优先级调度** | 支持操作优先级排序 | 基于优先级队列的调度器 |
| **时间片控制** | 防止单帧卡顿 | 每帧最大执行时间限制 |
| **批量处理** | 优化大量操作性能 | 时间片内批量执行操作 |
| **生命周期管理** | 完整的状态转换 | None → Processing → Succeed/Failed |

#### 5.2.2 异步操作类型

```csharp
public abstract class AsyncOperationBase
{
    public EOperationStatus Status;     // 操作状态
    public EOperationProgress Progress; // 操作进度
    public string Error;                // 错误信息
    public abstract void Update();      // 每帧更新逻辑
}
```

### 5.3 FileSystem - 文件系统层

#### 5.3.1 统一接口设计

```csharp
public interface IFileSystem
{
    bool Exists(string fileName);           // 文件是否存在
    FileStream OpenRead(string fileName);   // 打开文件流
    void WriteFile(string fileName, byte[] data); // 写入文件
    void DeleteFile(string fileName);       // 删除文件
    long GetFileSize(string fileName);      // 获取文件大小
}
```

#### 5.3.2 文件系统实现类型

| 文件系统类型 | 作用场景 | 实现类 |
|--------------|----------|--------|
| **内置文件系统** | 读取打包到应用的资源 | `DefaultBuildinFileSystem` |
| **缓存文件系统** | 本地缓存管理 | `DefaultCacheFileSystem` |
| **远程文件系统** | 从服务器下载资源 | `DefaultWebRemoteFileSystem` |
| **Web服务器文件系统** | WebGL特殊处理 | `DefaultWebServerFileSystem` |
| **编辑器文件系统** | 编辑器模式资源访问 | `DefaultEditorFileSystem` |
| **解压文件系统** | 资源包解压处理 | `DefaultUnpackFileSystem` |

#### 5.3.3 文件系统组合策略

不同运行模式使用不同的文件系统组合：

```
EditorSimulateMode: EditorFileSystem
OfflinePlayMode:    BuildinFileSystem
HostPlayMode:       CacheFileSystem + BuildinFileSystem
WebPlayMode:        WebServerFileSystem + WebRemoteFileSystem
```

### 5.4 DownloadSystem - 下载系统层

#### 5.4.1 核心功能

| 功能 | 实现机制 | 特点 |
|------|----------|------|
| **多线程下载** | UnityWebRequest并发请求 | 可配置并发数量 |
| **断点续传** | HTTP Range请求支持 | 网络中断后可继续 |
| **文件验证** | CRC32校验和验证 | 确保文件完整性 |
| **进度监控** | 实时下载进度回调 | 支持暂停和恢复 |

#### 5.4.2 下载流程

```
开始下载 → 检查本地文件 → 计算下载范围 → 分片下载 → 文件合并 → 完整性验证 → 完成
```

### 5.5 Services - 服务接口层

#### 5.5.1 服务接口类型

```csharp
// 加密解密服务
public interface IEncryptionServices { byte[] Encrypt(byte[] data); }
public interface IDecryptionServices { byte[] Decrypt(byte[] data); }

// 远程服务
public interface IRemoteServices
{
    bool RequestDownloadURL(string fileURL, ref string fileURL);
    bool RequestReportFail(string fileURL);
}

// 清单处理服务
public interface IManifestProcessServices
{
    byte[] Process(byte[] fileData, string fileCRC);
}

// 本地文件复制服务
public interface ICopyLocalFileServices
{
    void CopyLocalFile(string sourcePath, string destPath);
}
```

#### 5.5.2 服务扩展机制

YooAsset通过服务接口实现高度可扩展性：

- **资源加密**：通过IEncryptionServices实现自定义加密算法
- **远程定制**：通过IRemoteServices实现CDN、鉴权等定制需求
- **清单处理**：通过IManifestProcessServices实现清单文件的自定义处理

## 六、数据流和工作流程

### 6.1 典型资源加载流程

```
用户请求：YooAssets.LoadAssetAsync("Prefabs/Player")
    ↓
1. 获取资源包：GetPackage("DefaultPackage")
    ↓
2. 创建资源句柄：resourceManager.LoadAssetAsync()
    ↓
3. 启动异步操作：OperationSystem.StartOperation()
    ↓
4. 文件系统操作：FileSystem.LoadBundle()
    ↓
    ├─ 缓存命中：直接加载
    └─ 缓存未命中：DownloadSystem下载 → CacheFileSystem缓存
    ↓
5. 资源加载：AssetBundle.LoadAsset()
    ↓
6. 返回句柄：返回AssetOperationHandle给用户
```

### 6.2 异步操作调度流程

```
每帧Update()调用
    ↓
OperationSystem.Update()
    ↓
检查时间片剩余时间
    ↓
处理优先级队列中的操作
    ↓
调用每个操作的Update()方法
    ↓
时间片耗尽则暂停处理
```

### 6.3 文件系统查询流程

```
查询文件：FileSystem.Exists("bundle_file")
    ↓
按优先级查询文件系统列表
    ↓
1. CacheFileSystem → 命中则返回
2. BuildinFileSystem → 命中则返回
3. WebRemoteFileSystem → 下载并缓存
    ↓
所有文件系统都未命中 → 文件不存在
```

## 七、关键设计模式

### 7.1 单例模式
- **应用位置**：YooAssets静态类
- **实现方式**：静态成员 + 私有构造
- **优势**：全局唯一访问点，统一管理

### 7.2 工厂模式
- **应用位置**：HandleFactory, ProviderFactory
- **实现方式**：根据资源类型创建对应的Handle或Provider
- **优势**：解耦对象创建和使用

### 7.3 策略模式
- **应用位置**：不同的运行模式(IPlayMode)和文件系统(IFileSystem)
- **实现方式**：接口定义，多种策略实现
- **优势**：运行时可切换，易于扩展

### 7.4 观察者模式
- **应用位置**：异步操作完成回调机制
- **实现方式**：事件委托 + 回调方法
- **优势**：松耦合的异步通知

### 7.5 代理模式
- **应用位置**：文件系统接口抽象
- **实现方式**：接口代理具体实现
- **优势**：统一接口，隐藏实现细节

### 7.6 模板方法模式
- **应用位置**：AsyncOperationBase抽象基类
- **实现方式**：定义操作流程模板，子类实现具体逻辑
- **优势**：代码复用，统一操作流程

## 八、系统特色功能

### 8.1 多运行模式支持
- **开发阶段**：EditorSimulateMode快速迭代
- **测试阶段**：OfflinePlayMode离线测试
- **生产阶段**：HostPlayMode在线更新
- **特殊平台**：WebPlayMode专门优化

### 8.2 异步操作管理
- **优先级调度**：重要资源优先加载
- **时间片控制**：防止主线程卡顿
- **批量处理**：提高大量操作效率
- **状态管理**：完整的生命周期跟踪

### 8.3 资源管理优化
- **智能缓存**：多级文件系统缓存
- **引用计数**：自动资源释放
- **依赖分析**：避免重复加载
- **内存管理**：防止内存泄漏

### 8.4 扩展性设计
- **服务接口**：自定义加密、远程服务
- **文件系统**：自定义存储策略
- **运行模式**：自定义业务逻辑
- **异步操作**：自定义操作类型

## 九、学习建议和最佳实践

### 9.1 学习路径建议

#### 第一阶段：基础概念掌握
1. **理解YooAssets基础API** - 如何初始化、创建包、加载资源
2. **掌握ResourcePackage概念** - 理解包管理和独立生命周期
3. **了解不同运行模式** - 各模式的适用场景和区别

#### 第二阶段：核心机制理解
4. **学习ResourceManager** - Provider模式、Handle系统、引用计数
5. **研究OperationSystem** - 异步操作调度、优先级管理、时间片控制
6. **理解FileSystem** - 抽象接口、多种实现、组合策略

#### 第三阶段：高级特性掌握
7. **深入DownloadSystem** - 断点续传、多线程下载、文件验证
8. **研究Services** - 服务接口扩展、自定义实现
9. **掌握DiagnosticSystem** - 性能监控、调试分析

#### 第四阶段：实战应用
10. **完整项目实践** - 从资源打包到运行更新的完整流程
11. **性能优化** - 针对具体项目的优化策略
12. **扩展开发** - 自定义功能开发

### 9.2 最佳实践建议

#### 1. 包管理策略
```
建议：按功能模块划分包
- 核心系统包：Base Package
- UI资源包：UI Package
- 场景资源包：Scene Package
- 音频资源包：Audio Package
```

#### 2. 运行模式选择
```
开发期：EditorSimulateMode (快速迭代)
测试期：OfflinePlayMode (验证打包结果)
生产期：HostPlayMode (支持热更新)
Web平台：WebPlayMode (针对Web优化)
```

#### 3. 资源加载优化
```
- 使用预加载减少运行时等待
- 合理设置优先级保证关键资源优先加载
- 及时释放不再使用的资源Handle
- 利用依赖包避免资源重复
```

#### 4. 错误处理
```
- 始终检查异步操作的完成状态
- 实现重试机制处理网络异常
- 提供降级策略应对加载失败
- 记录详细日志便于问题排查
```

### 9.3 常见问题和解决方案

| 问题 | 原因 | 解决方案 |
|------|------|----------|
| 资源加载失败 | 文件路径错误或依赖缺失 | 检查清单文件，验证资源路径 |
| 内存占用过高 | 资源未及时释放 | 及时释放Handle，监控引用计数 |
| 下载速度慢 | 并发数限制或网络问题 | 调整并发参数，使用CDN |
| 包初始化失败 | 清单文件损坏或网络异常 | 实现重试机制，提供离线包 |

## 十、总结

YooAsset Runtime系统是一个设计精良、功能完善的Unity资源管理框架。它通过分层架构实现了：

1. **清晰的职责分离** - 每层专注特定功能，降低系统复杂度
2. **良好的扩展性** - 通过接口和抽象支持各种定制需求
3. **高效的资源管理** - 多级缓存、引用计数、异步调度等优化
4. **完整的生命周期** - 从资源打包到运行更新的完整解决方案

该架构设计体现了现代软件工程的最佳实践，是学习和构建资源管理系统的优秀参考。通过深入理解其设计思想和实现机制，可以更好地使用和扩展这个框架，满足各种复杂项目的资源管理需求。