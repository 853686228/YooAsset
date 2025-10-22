# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

YooAsset is a Unity3D asset management system that helps development teams quickly deploy and deliver games. It's designed to handle various asset loading scenarios including incremental downloading, flexible packaging strategies, and multi-platform support.

## Key Commands

### Building Asset Bundles
```bash
# Unity Editor menu: YooAsset -> AssetBundle Builder
# Access via: Window -> YooAsset -> AssetBundle Builder
```

### Testing and Development
```bash
# Unity Test Runner: Window -> General -> Test Runner
# Run unit tests from the Test Sample package
```

### Asset Analysis
```bash
# Asset Reporter: Window -> YooAsset -> Asset Art Reporter
# Asset Scanner: Window -> YooAsset -> Asset Art Scanner
# Asset Bundle Debugger: Window -> YooAsset -> Asset Bundle Debugger
```

## Architecture Overview

### Core Systems

1. **Runtime System** (`Assets/YooAsset/Runtime/`)
   - `YooAssets.cs`: Main entry point and API surface
   - `ResourcePackage/`: Package-based resource management
   - `ResourceManager/`: Asset loading and lifecycle management
   - `DownloadSystem/`: Multi-threaded downloading with resume support
   - `FileSystem/`: File abstraction layer with platform-specific implementations
   - `OperationSystem/`: Asynchronous operation handling
   - `Services/`: Core services (logging, encryption, etc.)

2. **Editor System** (`Assets/YooAsset/Editor/`)
   - `AssetBundleBuilder/`: Multi-pipeline build system supporting:
     - Builtin Build Pipeline
     - Scriptable Build Pipeline (SBP)
     - Editor Simulate Pipeline
     - Raw File Pipeline
   - `AssetBundleCollector/`: Asset collection and tagging system
   - `AssetBundleReporter/`: Build analysis and reporting
   - `AssetArtScanner/`: Asset analysis and optimization tools

### Key Design Patterns

- **Package-based Architecture**: Resources are organized into packages with independent management
- **Pipeline System**: Extensible build pipelines for different scenarios
- **Play Mode System**: Multiple runtime modes (Editor, Offline, Online, WebGL)
- **Reference Counting**: Safe asset unloading with leak detection
- **Async Operations**: Coroutine, Task, and delegate-based async patterns

### Important Components

- **Manifest System**: Asset dependency mapping and version management
- **Download System**: Breakpoint resume, file verification, auto-repair
- **Cache System**: Intelligent asset caching with configurable strategies
- **Encryption**: Built-in encryption services with extensibility

## Development Notes

### Asset Bundle Building
- Use the AssetBundle Builder window for interactive builds
- Supports distributed building and modular construction
- Custom build pipelines can be created by implementing `IBuildPipeline`
- Build results include manifests, catalogs, and detailed reports

### Play Modes
- **Editor Mode**: Simulates runtime without building asset bundles
- **Offline Mode**: Uses locally built bundles for development
- **Online Mode**: Downloads from remote servers with update support
- **WebGL Mode**: Specialized handling for WebGL platform constraints

### Sample Projects
The repository includes comprehensive samples:
- **Space Shooter**: Complete runtime example with update/download
- **Mini Game**: Extension file system for WeChat/TikTok platforms
- **Extension Sample**: Custom patchers, importers, and comparers
- **UniTask Sample**: Async/await integration patterns
- **Test Sample**: Unit test cases and validation

### Configuration Files
- `Packages/manifest.json`: Unity package dependencies
- `Assets/YooAsset/package.json`: Package metadata and samples
- Build settings are stored in Unity's project settings

### Platform Support
Supports all major Unity platforms including mobile, desktop, web, and mini-game platforms (WeChat, TikTok).

### Performance Considerations
- Multi-threaded downloading with configurable concurrency
- Memory-efficient asset loading with reference counting
- Optimized dependency analysis to prevent circular references
- Configurable compression and caching strategies

## Code Style Guidelines

- Follow C# naming conventions (PascalCase for public members, camelCase for private)
- Use async/await patterns for asynchronous operations
- Implement proper error handling and logging
- Use the built-in diagnostic system for performance analysis
- Follow Unity's ScriptableObject pattern for configuration data