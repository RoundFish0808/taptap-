# 逆塔防 · 路线构筑 Demo

Unity 2022.3.62f3 的 2D 玩法原型。角色自动行走与战斗，玩家通过多格道路卡和功能地块卡构筑地图。

## 在 Unity 中运行

1. 克隆仓库，在 Unity Hub 中添加仓库根目录。
2. 使用 Unity **2022.3.62f3** 打开，等待 Package Manager 完成依赖解析。
3. 打开 `Assets/Scenes/ReverseDefenseDemo.unity`，点击 Play。
4. 也可以通过菜单「演示 → 逆塔防 → 打开并运行」进入。

UI 全部使用 `UnityEngine.UI.Text` 显示中文，默认使用 Windows 微软雅黑。其他平台可在场景中的 `DemoController.ChineseFont` 指定支持中文的 Font。

## 玩法规则

- 角色先完成原主道路的一圈，再按**上、下、左、右**顺序依次行走拼接闭环，全部走完后再从主道路开始。
- 拖放卡牌或点击卡牌后点击网格，R 键旋转，右键取消选择，空格免费暂停。
- 施工时游戏暂停，可以分多张卡拼路；「完成施工」统一检查闭环。失败退回本批全部卡牌，并恢复原地图。
- 自动战斗掉落道路卡或地块卡，资源与恢复地块按圈触发。
- 击败三位首领即获胜，可以继续当前地图直到失败，或重新开始一局。

详细演示步骤和原型配置见 [操作说明](README-Demo.md)。

## 构建与验证

- Windows 构建：Unity 菜单「演示 → 逆塔防 → 构建 Windows 演示」。输出到 `Builds/ReverseDefenseDemo/`。
- 有 .NET 10 SDK 时运行 `dotnet run --project Tools/CoreChecks/CoreChecks.csproj`，执行 77 项检查，包括包配置重复键验证和核心玩法行为。
- 构建后的可执行文件支持 `-demo-smoke -demo-output <目录>`，自动检查中文字体、拖放、提交、回滚，以及先主道路后拼接闭环的实际运行行为。

仓库保存 `Assets`、`Packages`、`ProjectSettings` 与源码检查工具；缓存、临时文件和构建产物由 `.gitignore` 排除。
