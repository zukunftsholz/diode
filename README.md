<div align="center">
  <img src="assets/logo.png" alt="diode" width="96" />
  <h1>diode</h1>
  <p><b>One click and every monitor goes dark. One more and they're all bright again.</b></p>
  <p>
    <img src="https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6" alt="platform" />
    <img src="https://img.shields.io/badge/runtime-.NET%20Framework%204.8-512BD4" alt="runtime" />
    <img src="https://img.shields.io/badge/size-390%20KB-2ea44f" alt="size" />
    <img src="https://img.shields.io/badge/license-MIT-green" alt="license" />
  </p>
</div>

> [!TIP]
> **你的《世界计划》本地化知识库，为同人作者和考据党而生：[SekaiSync](https://github.com/omoinoki/sekaisync)**  
> **Your own *Project SEKAI* localization knowledge base, built for fan creators and lore enthusiasts: [SekaiSync](https://github.com/omoinoki/sekaisync)**
>
> 即将面向 DeepSeek Harness 插件生态针对性适配，欢迎各位豆腐人 Star 和共建。  
> Targeted adaptation for the DeepSeek Harness plugin ecosystem is coming soon. Fellow Tofus are welcome to star and contribute!


diode is a 390 KB Windows tray app that does one thing. Left-click the icon and every monitor plugged into the machine drops to minimum brightness. Left-click again and they all snap back to maximum. No sliders, no per-monitor panels, nothing adaptive.

diode 是一个 390 KB 的单一功能 Windows 托盘小程序：左键点一下图标，本机所有显示器一起压到最暗；再点一下，全部弹回最亮。没有滑块、逐屏面板，也没有任何自适应的东西。

## Why "diode"

A diode passes current one way and blocks it the other. Two states, no dial in between. Same deal here: darkest or brightest. It won't do 37% for you.

If you read Chinese internet slang, there's a second layer. 二极管 is what you call someone who sees everything in black and white with no shades of grey — "你这人真是个二极管". Naming this diode is a straight admission: yes, it's a diode, it has no nuance, and it isn't pretending to have any. After a decade of "smart adaptive brightness" that keeps guessing wrong, a tool that owns up to having exactly two states is a relief.

二极管正向导通、反向截止，两个状态，中间没有旋钮。因此"二极管"一词同时常被用来调侃一个人看问题非黑即白、毫无灰度。这个软件也一样：要么最暗，要么最亮——想要 37% 也没用，把它叫 “diode”其实就是在昭告天下：“对，我就是个二极管，没有也不打算装出灰度。”

在"自适应亮度"猜错了人类心思这么多年之后，多一个老老实实承认自己只有两个状态的工具，也许能让大家松口气。

## What it's actually for

Brightness tools usually get sold as eye-comfort accessories. This one isn't. It's for machines that have to keep working while nobody is looking at them.

Say an AI agent is driving your desktop, or a long job is running overnight. Locking the screen isn't available to you: it swaps you to the Winlogon desktop, drops DDC/CI handles, and can leave the whole run stuck on a UAC prompt nobody will click at 3 a.m. Leaving the monitors lit all night is the other kind of unbearable.

diode takes the third option. Picture off, session on. Brightness drops to the floor while the desktop, the handles and the login session stay exactly where they were. The agent carries on. The room stays dark.

亮度控制工具往往标榜护眼功能。但它偏不——它为无人值守却必须持续工作的设备而生。

当 AI 智能体操纵桌面或执行一项通宵运行的长程任务时，锁屏无异于自杀——它会把你切到 Winlogon、丢掉 DDC/CI 句柄，还可能让整个任务卡在一个凌晨三点没人点的 UAC 弹窗上。

当然，让显示器亮一整宿，对你的钱包和生物钟是另一种折磨。因此 diode 提供了一个新解法：画面熄掉，会话留着。亮度压到底，桌面、句柄、登录会话原封不动。智能体继续跑，房间光照可控。

## It won't lose a monitor

Most of these tools speak exactly one protocol. Any monitor that doesn't answer on it simply disappears from the UI, which is how you end up with a utility that controls three of your four monitors.

diode asks every monitor the same three questions, in order, on every single click:

1. **DDC/CI** (`dxva2`): real backlight control. The good path.
2. **WMI** (`root\WMI`): the ACPI interface laptop panels expose.
3. **Gamma**: rewrites the GPU's output curve when nothing else answers.

Enumeration runs per click, so plugging in a projector halfway through a meeting doesn't need a restart.

主流的第三方显示器亮度控制工具大多钻单一协议的牛角尖，应答不上来的显示器，直接从界面里蒸发——于是你手里的工具反而阻止你便捷地将所有显示器调暗。

diode 对每一台显示器、每一次点击，都按同样的顺序进行轮询：

1. **DDC/CI**（`dxva2`）——真正改背光，最好的一条路。
2. **WMI**（`root\WMI`）——笔记本内置屏暴露的 ACPI 接口。
3. **Gamma**——前面都不通时，改写显卡输出曲线。

每次点击都重新枚举一遍，开会开到一半插上投影仪也不用重启。

## Install

Download `diode.exe`, drop it in any folder you can write to, double-click. That's the install.

It needs .NET Framework 4.8, which ships inside Windows 10 (1903+) and Windows 11. You already have it. No UAC prompt, no runtime download. Nothing to keep beside it, either.

> **One caveat.** Keep it out of `Program Files`. The app writes `config.ini` next to itself on first run, and in a read-only directory that write fails silently. It still runs on defaults, and it won't remember anything you change.

下载 `diode.exe`，丢进任意一个有写权限（程序首次运行时会在同文件夹创建 `config.ini`，如果目录只读且未提权，将无法创建持久化配置文件，一定程度上影响使用体验，因此不建议扔进 `Program Files` 了事）的文件夹，双击即可使用——绿色免安装。

当然它需要 .NET Framework 4.8，不过它已经内置在 Win10（1903 起）和 Win11 里——我们假设你本来就有这套依赖。在这个前提下，它不弹 UAC，不下载运行时，也没有要跟着它一起放的 DLL。

## Usage

Left-click doesn't flip a flag blindly. It reads the current brightness first and picks a direction from there, so it stays correct even when something else moved your monitors in the meantime.

| Do this | Get this |
| --- | --- |
| Left-click the tray icon | toggle between darkest and brightest |
| Right-click the tray icon | menu: darkest, brightest, run at startup, edit config, diagnostics, quit |
| `Ctrl+Alt+B` | toggle from anywhere |

The tray icon and menu are Per-Monitor V2 aware, so they render sharply at 125% / 150% / 200%, and stay correct on mixed setups where one monitor runs 100% and another 150%.

按下左键并不会盲目地拨弄开关，它将读取当前亮度，再据此决定操作——所以就算中途有别的东西动过你的显示器，它也踩得准。

| 操作 | 结果 |
| --- | --- |
| 左键点托盘图标 | 在最暗和最亮之间切换 |
| 右键点托盘图标 | 菜单：最暗、最亮、开机自启、编辑配置、诊断信息、退出 |
| `Ctrl+Alt+B` | 任意界面切换 |

托盘图标和菜单声明了 Per-Monitor V2 感知，125% / 150% / 200% 缩放下都清晰；一台 100%、另一台 150% 的混搭环境也各自正确。

## Configuration

First launch writes a `config.ini` next to the exe. Edit it, save, done. No restart.

| Key | Default | Meaning |
| --- | --- | --- |
| `Dim` | `0` | brightness for "darkest", 0–100. If 0 is too pitch black, 10–20 is the sane range. |
| `Bright` | `100` | brightness for "brightest" |
| `HotKey` | `B` | the key after `Ctrl+Alt+`; blank disables the hotkey |
| `GammaMin` | `0.10` | how far the Gamma fallback dims, 0.02–1.0. Lower is darker. |

首次启动会在 exe 旁边写一份 `config.ini`。改完保存即生效，不用重启。

| 键 | 默认值 | 含义 |
| --- | --- | --- |
| `Dim` | `0` | "最暗"对应的亮度，0–100。嫌 0 太黑，10–20 是合理区间。 |
| `Bright` | `100` | "最亮"对应的亮度 |
| `HotKey` | `B` | `Ctrl+Alt+` 之后那个键；留空则禁用热键 |
| `GammaMin` | `0.10` | Gamma 兜底能压到多暗，0.02–1.0，越小越暗 |

## Command line

```console
diode.exe --diag        # write a diagnostics report to the Desktop
diode.exe --set 40      # set every monitor to 40%
diode.exe --version     # print the version
```

`--diag` is the one to run when a monitor won't respond. It reports which of the three paths each monitor actually took.

```console
diode.exe --diag        # 在桌面生成诊断报告
diode.exe --set 40      # 把所有显示器设为 40%
diode.exe --version     # 打印版本号
```

某台显示器不听话的时候，就跑 `--diag`。它会报告每台显示器各执行哪个选项。

## Known limitations

- **Gamma isn't a power switch.** It dims the picture, not the backlight. On a monitor with no DDC/CI, "darkest" still glows a little. Fine for an empty office; less fine if someone is trying to sleep in the room.
- **DDC/CI is at the mercy of your hardware.** Plenty of KVMs, cheap HDMI adapters and docks eat the DDC channel. When that happens diode quietly falls back to WMI or Gamma.

- **Gamma 不是电源开关。** 它压暗的是画面，不是背光。不支持 DDC/CI 的显示器，"最暗"仍然会透出一点光。空无一人的办公室无所谓，若在他人卧榻之侧就不太妙。
- **DDC/CI 得看硬件脸色。** 不少 KVM、廉价 HDMI 转接头和扩展坞会吞掉 DDC 通道，这时 diode 会自动降级到 WMI 或 Gamma。

## Building

Targets .NET Framework 4.8, so the binary stays small and the runtime is already on the machine.

```console
dotnet build build48/diode.csproj -c Release
```

Or double-click `build.bat`. Either way you get one `diode.exe` with the DPI manifest and icon baked in.

目标框架是 .NET Framework 4.8——产物小，而且运行时机器上本来就有。

```console
dotnet build build48/diode.csproj -c Release
```

或者直接双击 `build.bat`。两种方式的产物都是单独一个 `diode.exe`，DPI manifest 和图标都已内嵌。

## License

MIT. Use it however you like. Just don't blame the diode.