# v1.7.0 验证记录

2026-10-09（Asia/Shanghai）。

- Release 编译通过。
- 42 项断言覆盖独立/行内日期、空行可打印、连续多行合并、带文字/时间/完成标记时禁止拆分、恢复原始行数、八个手柄等比缩放、自由缩放及边界、图片实际渲染、图像深拷贝、v2 模板往返及原子覆盖、v1 模板兼容、右键保留多选，以及大字体下现有/新增行的间距。
- `tests/run.ps1` 可复现上述回归，并生成打印位图与离屏控件渲染。
- `tests/run-update-integration.ps1` 使用正式 EasyUpdate 服务验证旧版本识别更新、下载 ZIP、大小/SHA256/包内版本校验；错误校验不会覆盖已有文件，取消会清理临时文件。
- 正式服务已发布应用 `com.zlight.t50labelprinter` 的 `1.7.0 / 10700`，旧版本返回有更新，当前版本返回无更新；ZIP MIME、206 Range、完整下载校验通过。
- `scripts/build-release.ps1` 生成运行 ZIP 及 SHA256SUMS；ZIP 根目录包含版本元数据。

限制：Windows 界面自动化返回 `GetCursorPos failed / access denied`，未完成真实鼠标操作验证；离屏控件渲染不等同于屏幕截图。未进行实体打印机出纸验证。
