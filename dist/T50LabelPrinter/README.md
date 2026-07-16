# 硕方t50pro打印上位机

这是一个单窗体、低依赖的 Windows 标签编辑和打印程序。界面使用 .NET Framework 4.8 WinForms（标准 Win32 控件），USB 通讯和打印使用用户提供的 `Supvan.T50PRO.SDK.dll`。

## 已实现功能

- 枚举 USB 连接的 T50 Pro、查询打印机状态和错误信息。
- 在底部 SDK 状态栏完整显示 `State`、`PrintDes`、`ErrorMsg`、已打印页数和总页数。
- 自定义标签宽高；标签宽度在模型和界面中均强制限制为 `5–50 mm`。
- 支持间隙纸、中间黑标、黑标卡纸、打印方向、速度、0–9 档浓度、份数和逐份打印。
- 画布中添加多个文字、PDF417 或 Data Matrix 对象；支持平滑拖动、右下角缩放点和精确的 X/Y/宽/高数值编辑。
- 文字对象支持在画布中双击快速编辑；`Enter` 提交、`Shift+Enter` 换行、`Esc` 取消。
- 文字默认提供“思源黑体 / Noto Sans SC”和“思源宋体 / Noto Serif SC”，同时可选择本机安装的其他字体。
- PDF417 与 Data Matrix 的内容均为“3 位英文字母头 + 字符串”；自动时间模式使用 `yyyyMMddHHmmss`，精确到秒，也可关闭自动时间并输入自定义字符串。
- 条码可统一选择打印或不打印；每个条码可独立选择是否附印数位码，并可输入自定义纯数字内容。
- 两种条码均可自由调整大小，打印和预览使用同一套 ZXing 渲染结果。
- 支持垂直、水平、十字居中辅助线；辅助线可仅用于预览，也可选择印到异形标签上。
- 保存/打开 `.t50label` 模板，导出 203 dpi 打印预览 PNG。

## 直接运行

Release 成品位于：

```text
dist\T50LabelPrinter\T50LabelPrinter.exe
```

运行环境：Windows 10/11、.NET Framework 4.8。请保持 EXE 与以下三个 DLL 在同一目录：

```text
Supvan.T50PRO.SDK.dll
zxing.dll
SevenZip.dll
```

连接打印机后打开电源，单击“刷新设备”，选择 USB 设备路径并查询状态。只有 SDK 返回“就绪”时程序才会发送打印任务。个别 SDK 版本会在打印机已正常出纸时让 `DoPrint` 返回 `false`；程序不会据此直接误报失败，而会继续读取设备状态、打印描述与页数，以设备最终状态为准。

## 字体说明

本机已安装 `Noto Sans SC` 和 `Noto Serif SC`，它们分别是思源黑体和思源宋体的 Noto 发行名称。程序会优先使用这两个字体；换到其他电脑时，请先安装思源黑体/思源宋体或 Noto Sans SC/Noto Serif SC。若所选字体不存在，程序会退回到可用的中文无衬线字体。

## 编译

使用 Visual Studio 2022/2026 或 Developer PowerShell：

```powershell
msbuild T50LabelPrinter.sln /t:Rebuild /p:Configuration=Release
```

项目通过相对路径引用原始 SDK：

```text
SUPVAN.T50PRO.DLL\SUPVAN.T50PRO.DLL\PackageSDK\Supvan.T50PRO.SDK
```

Release 编译后会自动把可执行文件、三个依赖 DLL 和本说明复制到 `dist\T50LabelPrinter`。

## 设备限制

SDK 定义打印头为 8 点/mm、`MaxDotValue = 384`，本项目沿用官方示例的 50 mm 纸宽和该点数设置。USB 调用全部在后台线程执行，避免阻塞界面。

已完成 Release 编译、PDF417/Data Matrix 预览生成、条码开关、附加数位码、双击编辑、模板兼容与界面排版检查。打印方向、浓度和异形标签辅助线的最终效果仍建议按实际耗材各打样一张确认。
