# ForgeTax 修改器（ForgeCheat）

给单机游戏 **ForgeTax v26.10.12**（Unity 6 / Mono 后端）做的运行时修改器。
不依赖 BepInEx/MelonLoader，纯离线：一个自写 DLL + 两处 IL 补丁。

## 安装

```
install.bat
```

或者手动：

```
dotnet build src\ForgeCheat\ForgeCheat.csproj -c Release
dotnet build src\Patcher\Patcher.csproj -c Release
dotnet src\Patcher\bin\Release\net8.0\ForgeTaxPatcher.dll "E:\ForgeTax v26.10.12"
```

游戏装在别处就把路径当参数传进去。安装脚本可重复执行（每次都会先从
`Assembly-CSharp.dll.orig` 还原再重新打补丁），**游戏更新后重跑一次即可**。

卸载：`uninstall.bat`（还原原始程序集、删除 mod 与配置）。

> 本仓库**只包含修改器自身的源码**，不包含、也不再分发任何游戏文件或反编译产物。
> 构建时需要你本机已安装游戏：`ForgeCheat.csproj` 通过 `GameDir` 属性引用游戏
> 程序集（默认 `E:\ForgeTax v26.10.12`），装在别处就这样构建：
>
> ```
> dotnet build src\ForgeCheat\ForgeCheat.csproj -c Release -p:GameDir="D:\你的\ForgeTax"
> ```

## 按键

面板默认显示在左上角（深度 `-1500`，画在游戏 HUD 之上）。

| 键 | 功能 |
|---|---|
| `F1` | 必定大成功 —— 每次敲击都是大成功，等级 +2，且永不破碎 |
| `F2` | 无限金币 —— 金币自动补到 10 亿 |
| `F3` | 税赋归零 —— 每轮配额压到 1，随便交一把剑就达标 |
| `F4` | 锻造加速（默认**关闭**，见下方"卡顿"一节） |
| `F5` | 当前剑价值 ×100（单次，作用于铁砧上或手持的剑） |
| `F6` | 当前剑等级 +5（单次，上限 99） |
| `F7` | 一键满配 —— 8 件遗物 + 全消耗品 ×99 |
| `F9` | 消耗品无限（护符/回溯常驻 99） |
| `F8` | 显示/隐藏面板 |
| `F11` | 鼠标接管 —— 释放鼠标以便点击面板按钮，再按一次恢复视角 |
| `F10` | 全部关闭（应急用，同时退出鼠标接管） |

### 鼠标与视角控制

默认**不碰鼠标**，所以不会干扰游戏。全部功能都能用快捷键完成。

面板上的按钮需要鼠标才能点，而游戏在游玩时会锁定鼠标，所以要用鼠标操作面板时按
`F11` 进入"鼠标接管"：这一步做的正是游戏自己打开语言菜单时做的事
（`player.ReleaseCursor()` + `player.enabled = false`，见 `ForgeLanguageMenu`），
再按 `F11` 会把控制器和鼠标原样还回去。**只有在这两种状态之间切换，绝不在每帧抢鼠标。**

> 早先的版本在面板打开时每帧强制解锁鼠标，而游戏在你点击时又会重新捕获，两边互相
> 抢，结果就是游戏提示"点击恢复视角控制"却怎么点都恢复不了。这个问题已经修掉了。

设置写在游戏根目录 `ForgeCheat.cfg`，改完重启游戏生效。`Diagnostics=True` 时每
10 秒往 `Player.log` 打一行帧率、面板/鼠标状态和开关状态，方便排查。

`F7` 发放的 8 件遗物是为刷价值挑的复利组合：
`coal` `token` `blackstone` `thorn` `idol` `bell` `knot` `incense`。

## 关于"游戏变得非常卡"

**原因**：`F4 锻造加速`。它把游戏的敲击频率提到 8~12 次/秒，而游戏每次敲击都要
跑一整套重活：

- `ForgeImpactFeedback.Burst` —— 大成功一次**单独发射 100 个粒子**（普通成功 48 个），
  是 100 次独立的 `ParticleSystem.Emit()` 调用；
- `GPT6ForgeRoom.RefreshDisplays()` —— 遍历场景里所有剑重算价值，再用 LINQ 排序
  重建铁砧上的概率显示；
- `ForgeCodex.Discover` / `ForgeAchievements.ObserveRun` / 音效 / 飘字。

正常节奏下这些开销无所谓（约 1.5 次/秒），被放大 8 倍后就成了瓶颈。

**已处理**：

- `FastForge` 默认改为**关闭**，`ForgeSpeed` 默认从 8 降到 2，上限锁到 10；
- 面板加了实时 FPS 显示，开 `F4` 能立刻看到掉帧；
- 加了 `F10` 一键全关。

实测：起始界面在全部作弊开启时稳定 57–60 FPS（游戏自身锁 60），所以修改器本身
不拖帧；掉帧只发生在高速锻造时。真正的"去肝"主力是 `F1` + `F3` + `F5`/`F6`
—— 它们不增加每秒的计算量，而是直接把进度推到位。

## 实现

游戏是 Mono 后端，`Assembly-CSharp.dll` 未加密，所以直接静态注入：

1. **`ForgeFramePacing.Boot`** 头部插入 `call ForgeCheat.Bootstrap::Boot()`
   —— 该方法本来就被 `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` 调用，
   借它启动 mod，不需要改 Unity 的启动清单。
2. **`ForgeChances.Roll`** 方法体改写为委托 `ForgeCheat.Hooks::Roll(ref ForgeChances, int)`
   —— 原逻辑在托管代码里逐行复刻，开关关闭时行为与原版完全一致。
3. **`ForgeReworkRules.Speed`** 方法体改写为委托 `ForgeCheat.Hooks::Speed(ForgeCampaign, bool)`。

其余作弊（金币/配额/遗物/价值/等级）不碰 IL，直接在 mod 的 `Update` 里操作活的
游戏对象，私有字段用反射读写（`GPT6ForgeRoom.nextStrike`、`enhancementBalance`）。

启动时 `Bootstrap.SelfTest()` 会真的调用一次改写后的 `Roll`/`Speed` 并把结果写进
`Player.log`，补丁失效会立刻看到 `FAIL` 而不是静默失灵。

目录：

```
src/ForgeCheat/     修改器本体（netstandard2.1，引用游戏 DLL）
src/Patcher/        Cecil 注入器（net8.0）
decompiled/         ILSpy 反编译出来的游戏源码，分析用
economy_dump.json   从 resources 里抽出的经济配置（90 遗物/7 消耗品/8 名工）
```

## 注意

- 只影响本地单机存档，但**会触发 Steam 成就**，介意就别用。
- 改动会跟着游戏自动存档一起保存。想回到干净存档，先 `F10` 全关再玩。
- 游戏更新后需要重跑 `install.bat`；如果 `Roll`/`Speed` 的签名变了，注入器会报错
  而不是写出坏程序集。
