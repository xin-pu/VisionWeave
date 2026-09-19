# VisionWeave

VisionWeave 是一款基于 .NET 10、WPF UI、Nodify 与 OpenCvSharp 构建的可视化图像处理工作台。它通过类型化节点图编排采集、预处理、分析、检测与结果输出流程。

Aries 仅用于发现历史使用场景和设计问题。VisionWeave 不承诺兼容 Aries 的源码、节点类型、执行语义、文件格式、布局行为或隐含副作用。

## Repository structure

- `src/`：产品代码，按 Contracts、Domain、Application、OpenCV、Persistence、PluginSdk 与 WPF App 分层。
- `tests/`：领域、应用、架构与集成测试。
- `docs/design/`：项目设计。
- `docs/adr/`：架构决策记录。
- `docs/ledger/`：项目风险、改进和标准偏差记录。

## Settings and startup

```powershell
dotnet run --project src/VisionWeave.App
```

`src/VisionWeave.App/appsettings.json` 随可执行文件发布，给出执行并发、预览像素上限与自动保存间隔的默认值；同目录下可选的 `appsettings.user.json` 在本机覆盖这些值，未写出的键沿用其选项类型声明的默认值。启动时会逐节校验设置，无法执行的取值以 `VW-CONFIG-001` 记录并弹出提示后终止启动，而不是换成别的值继续运行。

## Local verification

```powershell
dotnet restore VisionWeave.slnx
dotnet build VisionWeave.slnx --no-restore
dotnet test VisionWeave.slnx --no-build
dotnet format VisionWeave.slnx --verify-no-changes --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-ProjectDocuments.ps1
```

详细架构仍处于 Proposed 状态，见 [VisionWeave detailed design](docs/design/visionweave-detailed-design.md)。
