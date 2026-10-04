# 平台支持说明

## 当前支持状态

| 平台 | 状态 | 说明 |
| --- | --- | --- |
| Windows 10/11 | 支持 | 发行包已在 Windows 上完成编译与自动测试。 |
| macOS（Apple Silicon / Intel） | 尚未支持 | 当前没有可验证、可安全发布的 macOS 二进制文件。 |
| Linux（x64 / ARM64） | 尚未支持 | 当前没有可验证、可安全发布的 Linux 二进制文件。 |

## 为什么不能直接生成 macOS / Linux 包

当前应用基于 .NET Framework 4 和 Windows Forms，并直接使用 Windows 专属能力：

- `user32.dll` / `kernel32.dll` 输入模拟、窗口句柄、全局热键与钩子；
- x86 / x64 Windows DLL 桥接器；
- Qt、Paddle、OpenCV、ONNX Runtime 等 Windows 原生动态库与辅助 EXE；
- Windows 专属的窗口绑定、后台消息、截图和进程控制行为。

因此，把现有 EXE 放入 macOS 或 Linux 压缩包，或者仅通过 Mono/Wine 启动，都不能算作原生适配，也无法保证自动化操作的正确性和安全性。

## 跨平台移植范围

真正的 macOS / Linux 版本至少需要：

1. 将 WinForms 界面迁移到跨平台桌面框架（例如 Avalonia）。
2. 把输入模拟、全局热键、窗口枚举、截图等能力拆分为平台适配层。
3. 为 macOS 实现辅助功能权限处理，为 Linux 分别处理 X11 与 Wayland 的限制。
4. 替换或重新编译全部原生 DLL/EXE 依赖，并按平台、CPU 架构发布。
5. 在真实 Intel/Apple Silicon Mac、Linux X11/Wayland 环境中完成集成与安全测试。

在这些工作完成并经过对应平台验证前，Release 只提供 Windows 离线包和源码。
