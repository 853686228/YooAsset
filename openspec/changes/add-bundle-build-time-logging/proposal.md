# Change: Add Bundle Build Time Logging

## Why
Developers need visibility into asset bundle build performance to optimize build times and identify bottlenecks. Currently, YooAsset only provides task-level timing without granular bundle-specific timing information, making it difficult to understand which bundles are causing build delays.

## What Changes
- Add bundle-level timing information during the asset bundle build process
- Enhance build logs with detailed timing data for each bundle phase
- Include timing summary in build reports showing slowest bundles
- Add console progress output with real-time timing information
- **BREAKING**: Extends BuildLogger API with new bundle-specific timing methods

## Impact
- Affected specs: build-system
- Affected code:
  - `TaskBuilding_BBP.cs` - Primary bundle timing injection point
  - `BuildLogger.cs` - Enhanced logging capabilities
  - `TaskCreateReport.cs` - Timing information in reports
  - `BuildRunner.cs` - Extended timing hooks