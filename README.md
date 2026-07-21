# YogaDNS 中文本地化工具

面向 Windows 的 [YogaDNS](https://www.yogadns.com/) 资源级简体中文本地化工具。它能够从不同版本的 `YogaDNS.exe` 中提取可翻译文本、复用内置或自定义翻译，并生成独立的汉化副本。

> 本项目是社区本地化工具，与 YogaDNS 官方无隶属或合作关系。使用前请确认你有权修改目标程序，并遵守 YogaDNS 和相关第三方软件的许可条款。

## 功能特点

- 提取 PE 文件中的 `MENU`、`DIALOG` 和 `STRINGTABLE` 文本资源
- 提供已整理的 DNS 语境简体中文翻译
- 支持导出、导入和保存 JSON 翻译文件
- 可在表格中直接编辑译文，并按原文、译文或上下文搜索
- 可筛选尚未翻译的条目，便于适配新版 YogaDNS
- 翻译可按资源信息及英文原文匹配，资源 ID 变化后仍可复用
- 写入简体中文资源（语言 ID `2052`），同时保留原英文资源
- 始终生成新的 EXE，不覆盖原始文件

## 系统要求

### 直接使用

- Windows 10 或 Windows 11（x64）
- 待处理的官方 `YogaDNS.exe`
- [Resource Hacker](https://www.angusj.com/resourcehacker/) 的 `ResourceHacker.exe`

若下载的是自包含发布包，则无需另外安装 .NET 运行时。

### 从源码构建

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10 或 Windows 11
- Resource Hacker

## 使用方法

1. 启动 `YogaDNS汉化工具.exe`。
2. 点击“选择 EXE”选择目标 `YogaDNS.exe`，也可以将 EXE 直接拖入窗口。
3. 选择翻译来源：
   - 点击“内置翻译”载入项目自带的简体中文翻译。
   - 点击“导入翻译”载入已有 JSON 翻译文件。
   - 点击“导出模板”从当前 YogaDNS 版本提取完整翻译模板。
4. 在“译文（可编辑）”列中检查或修改翻译。可使用搜索框和“只看未翻译”筛选项。
5. 如需保留修改后的翻译，点击“保存”导出 JSON 文件。
6. 点击“生成汉化 EXE”，选择一个尚不存在的新文件名。
7. 使用生成的副本运行 YogaDNS；请妥善保留原始 EXE。

工具只处理资源文本，不会进行全局二进制字符串替换。

## 翻译文件

翻译文件采用 JSON 格式，顶层包含格式版本、翻译名称、来源文件信息及翻译项列表。单条翻译示例：

```json
{
  "ResourceType": "Menu",
  "ResourceId": 128,
  "ControlId": 0,
  "Original": "DNS Servers",
  "Translation": "DNS 服务器",
  "Context": "主菜单",
  "Status": "已翻译"
}
```

字段说明：

| 字段 | 说明 |
| --- | --- |
| `ResourceType` | 资源类型：`Menu`、`Dialog` 或 `String` |
| `ResourceId` | PE 资源 ID |
| `ControlId` | 对话框控件或菜单命令 ID；不适用时为 `0` |
| `Original` | 英文原文 |
| `Translation` | 简体中文译文；留空表示尚未翻译 |
| `Context` | 文本所在资源的上下文信息 |
| `Status` | 翻译或匹配状态 |

不同 YogaDNS 版本的资源 ID 可能发生变化。工具优先使用资源类型、资源 ID、控件 ID 和原文进行精确匹配，也可根据同类型英文原文复用译文。无法匹配的新条目会保留为空，供人工补充。

翻译包含 `%1`、`%s` 等占位符时，应在译文中完整保留，否则运行时显示可能异常。

## 构建与测试

克隆仓库后，将从 Resource Hacker 官方获取的 `ResourceHacker.exe` 放到：

```text
tools\ResourceHacker\ResourceHacker.exe
```

然后在仓库根目录执行：

```powershell
dotnet restore .\YogaDnsLocalizer.slnx
dotnet test .\YogaDnsLocalizer.slnx -c Release
dotnet build .\YogaDnsLocalizer.slnx -c Release
```

发布 Windows x64 自包含单文件版本：

```powershell
dotnet publish .\src\YogaDnsLocalizer.App\YogaDnsLocalizer.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -o .\publish\YogaDNS汉化工具-win-x64
```

项目的 MSBuild 目标会在构建和发布时将 `ResourceHacker.exe` 复制到输出目录的 `tools` 子目录。因此构建前必须准备该文件，但它不应提交到本仓库或随公开发行包再分发，除非你已获得相应许可。

## 项目结构

```text
YogaDNS-Chinese/
├─ src/
│  ├─ YogaDnsLocalizer.App/       # WPF 桌面界面与交互逻辑
│  └─ YogaDnsLocalizer.Core/      # 资源提取、解析、匹配与写入逻辑
├─ tests/
│  └─ YogaDnsLocalizer.Tests/     # xUnit 单元测试
├─ tools/
│  └─ ResourceHacker/             # 本地 Resource Hacker 放置目录
├─ YogaDnsLocalizer.slnx
└─ README.md
```

## 工作原理

1. 调用 Resource Hacker 提取英文（语言 ID `1033`）菜单、对话框和字符串表资源。
2. 将 RC 资源解析为可编辑的翻译目录，并记录来源文件 SHA-256。
3. 将翻译应用回 RC 文本；对话框字体会调整为 `Microsoft YaHei UI`。
4. 编译生成简体中文资源，并以语言 ID `2052` 写入新的 EXE。
5. 保留目标文件中的英文资源及其他未处理资源。

## 重要说明

- 修改 PE 资源会使 YogaDNS 原厂 Authenticode 数字签名失效，这是预期行为。
- Windows SmartScreen 或安全软件可能对修改后的、未签名 EXE 发出警告。
- 请仅处理可信来源的 YogaDNS 安装文件，并始终保留原始文件备份。
- 输出路径不能指向已经存在的文件，工具不会覆盖输入或既有输出。
- 本仓库不提供 YogaDNS 程序本体。

## 第三方组件

本项目通过命令行调用 Angus Johnson 的 Resource Hacker。Resource Hacker 不是本项目的一部分，其许可条款限制未经许可的公开再分发。请从[官方网站](https://www.angusj.com/resourcehacker/)自行获取，并遵守其许可要求。

YogaDNS 名称及相关商标归其各自权利人所有。