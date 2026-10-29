# ADOFAI DX

本README并未经过严谨的人工核验, 本项目由AI辅助完成, 请注意鉴别

This README has not been rigorously manually verified; please be mindful when evaluating its contents.
Parts of this project were developed with assistance from AI coding tools.



ADOFAI DX 是一个面向 Steam 版《A Dance of Fire and Ice》的 UnityModManager 模组。它为该游戏添加了类似《maimai DX》中绝赞音符的机制。


## 功能

- 极高的配置自由度
- 10国语言支持
- 完美判定绝赞音符会在结算时为X精准度提供奖励精准度, 最高1%


## 构建

使用 Visual Studio 打开 `ADOFAI_DX/ADOFAI_DX.slnx`，或运行：

```powershell
dotnet build ADOFAI_DX/ADOFAI_DX/ADOFAI_DX.csproj -c Release
```

游戏程序集路径由 `ADOFAI_DX.csproj` 中的绝对 `HintPath` 指定。

## 兼容性说明

DX 标记使用原版 `EditorComment` 事件保存，不修改游戏事件枚举。未安装本模组的玩家可以正常加载和游玩带有 DX 音符标记的谱面。

未进行大量模组兼容性测试
