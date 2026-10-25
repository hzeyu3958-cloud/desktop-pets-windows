# 桌宠 Windows 版

一群「接话型」桌宠的 Windows 移植。

点一下头像，他说一句；拖动换位置；右键调音量、换大小、挑句子。置顶悬浮、不占任务栏、不抢键盘焦点、不联网、不需要安装任何运行库（系统自带的 .NET Framework 4 就够）。

## 里头有谁

| 桌宠 | 会说什么 | 原版（macOS） |
| --- | --- | --- |
| 太对了 | 28 句「太对了」 | [SunPet](https://github.com/Ganzhe2028/SunPet) |
| 峰哥 | 11 句「这是个好事」 | [FengGe-Desktop-Pet](https://github.com/Ganzhe2028/FengGe-Desktop-Pet) |

原版基于 Swift + AppKit，仅支持 macOS 14+ / Apple Silicon，作者明说「Windows 版还没有」。这是对原作行为逻辑的 1:1 移植：28 句一轮内不重复、连点换句不叠播、说话时摇头、字幕气泡、记住位置和音量——全都在。

## 玩

1. 下载本仓库：绿色的 `Code` 按钮 → `Download ZIP`，解压。
2. 进入 `pets/` 里任意角色的文件夹（比如 `pets/太对了/`）。
3. 双击 `太对了.exe`。没了。

> 想加进「开机自启」：`Win + R` 输入 `shell:startup` 回车，把 exe 的快捷方式扔进去。

左键点一下随机说一句。按住拖动换位置（自动记住）。右键：挑一句固定播 / 音量 / 大小 / 移回右下角 / 退出。多只可同时开，互不干扰。

## 自己编译（可选）

不信任仓库里的 exe？双击根目录的 `build.cmd`，它用系统自带的 C# 编译器（`csc.exe`，Windows 10/11 都有）重新编译 `src/Program.cs`，产出 `pets/*/` 下两个 exe。十几秒，无任何下载。

## 加一个新角色

程序是通用的：一份代码读 `pet.json` 和 `Assets/`。想把某个角色的音频和脸换成你想要的：

1. 复制 `pets/峰哥/` 整个文件夹，改成新名字，比如 `pets/新角色/`。
2. 换 `Assets/face.png`（任意正方形 PNG，最好透明背景）。
3. 换 `Assets/Clips/` 里的音频（wav 或 m4a）。
4. 改 `Assets/clips.json`，一条一句：`{"id":1,"label":"台词","file":"01.wav","duration":1.5}`。
5. 改 `pet.json` 里的 `name` 和 `header`。
6. 把 `峰哥.exe` 复制过来改名（exe 是同一份通用代码），或者跑一下 `build.cmd`。

## 工程说明

- `src/Program.cs` —— 全部逻辑，约 450 行 C#（WPF，只依赖系统自带 .NET Framework 4.x）。一份代码支持任意角色。
- `build.cmd` —— 一键重编所有角色。
- `pets/<角色>/Assets/clips.json` —— 句子清单（编号/台词/文件/时长）。
- `pets/<角色>/pet.json` —— 角色名（显示在菜单和托盘提示里，同时用于多开互斥）。

设置存在 `%AppData%\DesktopPets\<角色名>\settings.ini`。卸载：退出进程、删文件夹、顺手删掉那个设置目录。

## 已知差异（和 macOS 原版比）

- 「摇一摇」功能没有移植（读的是 Mac 专有的机身加速度计）。
- 三只之间的「捧哏联动」没有移植（原版走 macOS 的 DistributedNotificationCenter）。
- 其余交互行为一致。

## 致谢与声明

- 原作：[Ganzhe2028](https://github.com/Ganzhe2028) 的 [SunPet](https://github.com/Ganzhe2028/SunPet)、[FengGe-Desktop-Pet](https://github.com/Ganzhe2028/FengGe-Desktop-Pet)、[ShaolanPet](https://github.com/Ganzhe2028/ShaolanPet)，行为逻辑、素材切分、菜单设计全部来自原作，本仓库仅做 Windows 平台移植。
- 代码 MIT，见 `LICENSE`。
- **音频和头像素材版权归原权利人所有，不在 MIT 范围内**，仓库仅作学习交流和玩梗，不商用。权利人若觉得不妥，开 issue，秒删。
