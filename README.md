# ADOFAI DX

本README并未经过严谨的人工核验, 本项目大多数由AI辅助完成, 请注意鉴别
This README has not been rigorously manually verified; please be mindful when evaluating its contents.
Parts of this project were developed with assistance from AI coding tools.



ADOFAI DX 是一个面向 Steam 版《A Dance of Fire and Ice》的 UnityModManager 模组。它在原版判定完成后追加更严格的 DX 判定，并把 DX 结果并入 X 准度显示。

安装包不修改 UnityModManager 的 `Params.xml`，也不会覆盖其他模组设置。


## 功能

- 编辑器中的 DX 音符标记与独立 DX 按钮
- 游玩中的暖金橙红音符渐变、真实轮廓边缘光和均匀外发光
- DX 音符漂浮动画，判定后平滑固定
- 编辑器显示 DX 文字，游玩模式隐藏文字并保留音符视觉效果
- DX 音符 RGB 配色、轻微颜色浮动和默认颜色重置
- DX 颜色可自适应原音符 RGB，并支持颜色与漂浮幅度倍率
- DX 与 Break/Miss 特效共用可点击可见光谱调色盘
- 功能总开关关闭时自动隐藏对应下级设置
- DX 音符总开关，关闭后无需重启即可停用全部 DX 功能
- 可配置组合快捷键
- 原版判定保持不变，DX 判定独立计算
- DX 判定支持普通、中旋、HOLD、HOLD 释放、多押、自动音符和多星球音符
- HOLD 链与自动衔接不会因沿用旧角度而被误判为 Miss
- 多押只在最终落点计算一次，同一拍可记录多个不同 DX 音符
- DX Perfect 奖励并入 X 准度，并显示 `(+x.xx%)`
- DX Perfect 判定始终为当前原版 Perfect 判定的 1/3
- DX 视觉跟随原音符透明度与显示/隐藏事件
- 初次加载时自动校正轮廓，避免临时五边形光晕
- 不规则音符使用三角网格真实外边界生成发光
- 音符网格更新后自动重建 DX 视觉
- 页面切换黑屏开始时立即隐藏全部 DX 叠层
- 切页隐藏使用游戏加载事件，不影响正常 DX 功能
- 初次加载等待音符几何稳定后再显示边线和光晕
- 发光直接使用音符网格的多层放大剪影
- Mesh 与 Sprite 音符均支持 DX 表面和发光
- 实时 DX-Accuracy UI，可调整像素位置和大小
- 实时 DX-Accuracy 显示本模组贡献的 0%–1% 准度奖励
- 实时 UI 小标跟随轨道主色，并根据排列切换左右位置
- 结算保持原版 X 准度格式，并在百分号后显示模组奖励
- 内置 Chime / Click / Off 音效与统一音量
- 独立自定义 Break/Miss 音效
- 自定义 Break 特效颜色、文字、大小和图片
- 10 语言设置界面：English、한국어、Español、Polski、Français、Tiếng Việt、中文（简体）、Русский、日本語、Deutsch

## 支持的音频

- MP3
- WAV（PCM）
- OGG Vorbis（不支持 OGG-FLAC、OGG-Opus）
- AIFF / AIF

自定义音频最长 30 秒，加载超过 30 秒也会失败。

## 支持的特效图片

- PNG
- JPG / JPEG

单张图片最大 10 MB。

## 构建

使用 Visual Studio 打开 `ADOFAI_DX/ADOFAI_DX.slnx`，或运行：

```powershell
dotnet build ADOFAI_DX/ADOFAI_DX/ADOFAI_DX.csproj -c Release
```

游戏程序集路径由 `ADOFAI_DX.csproj` 中的绝对 `HintPath` 指定。

## 兼容性说明

DX 标记使用原版 `EditorComment` 事件保存，不修改游戏事件枚举。未安装本模组的玩家可以正常加载和游玩带有 DX 标记的谱面。
未进行大量模组兼容性测试
