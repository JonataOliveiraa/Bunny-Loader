# 更新日志

## 尚未发布版本

- 新增模组坐骑（`ModMount`）和示例矿车。
- 新增模组标题菜单（`ModMenu`），带有专属音乐（*Mysterious Mystery*）；可在「设置 > 菜单主题」中选择。
- 新增模组生物群系：名称、居民幸福度、图鉴、地图背景和自然生成。
- 新增 `ModCommand`、`ModHair`、`ModCloud`、`ModEmoteBubble`、`ModAchievement` 和钓鱼内容。
- 新增示例传送柱（`ModPylon`），可由示例生物群系中的居民出售。
- 新增图格实体（`ModTileEntity`）：收集雨水的木桶。
- 新增示例树木（`ModTree`），包含树苗和树叶，可种在示例方块上。
- 为???之眼新增专属首领血条（`ModBossBar`）。
- 新增拾取物品效果（`OnPickup`、`GrabRange`）、示例短剑（`ShouldUpdatePosition`）、带有 `GrappleRange`、`NumGrappleHooks` 和速度设置的钩爪，以及防御提示中的剩余时间（`ModifyBuffText`）。
- 新增屏幕效果：示例终极棱镜（移植自 `ExampleLastPrism`，光束命中处会产生画面波纹）、示例裂隙法杖（使用专属 `.frag` 着色器，包含扭曲和现有效果），以及生命值较低时出现的红色屏幕边缘（`ExampleLowLifePlayer`）。
- 新增适用于所有图格、增益、首领血条和配方的全局内容：摇晃树木时的掉落物（`ShakeTrees`）、示例方块上的向日葵（`SunflowerChanges`）、所有增益提示中的剩余时间和摇晃的篝火图标（`ExampleGlobalBuff`）、克苏鲁之眼血条（`ExampleGlobalBossBar`），以及 `ExampleGlobalRecipe`。
- 新增带条件的配方（`AddCondition`），以及带有消耗和制作回调的配方（不会被消耗的锁链、烟花）。
- 示例首领宝藏袋现在标记为 `PreHardmodeLikeBossBag`：和游戏中的困难模式前首领宝藏袋一样，不会掉落开发者套装。
- 新增旅途模式研究数量（`Item.ResearchUnlockCount`）：示例物品为 100，首领宝藏袋为 3。

## 1.8.0 - 2026-09-27

- 新增带音乐、背景和多人游戏支持的模组生物群系。
- 新增三个示例前缀（`ModPrefix`）。
- 修复钩爪、材料、光照、永燃效果、时钟、套装加成、配饰和鞭子的曲线。

## 1.7.0 - 2026-09-27

- 盔甲和可装备物品现在会显示穿戴效果：头盔、胸甲、护腿、翅膀、胡须、服装和长袍。

## 1.6.0 - 2026-09-27

- 为家具添加交互行为：可坐的椅子、床、门、宝箱、吊灯、篝火和音乐盒。

## 1.5.0 - 2026-09-27

- 支持 tModLoader 格式的模组包：`Assets/`、`Common/`、`Content/` 和 `Localization/`，并在 `main.js` 中定义 `Mod` 类。
- 新增模组网络功能：`NetSend`/`NetReceive` 和 `ModPacket`。
- 新增 `GlobalItem`、`GlobalNPC`、`GlobalProjectile` 和全局掉落物。

## 1.4.0 - 2026-09-26

- 新增模组音效和音乐：`SoundStyle`、`SoundEngine` 以及首领音乐。

## 1.3.1 - 2026-09-26

- 新增模组物品掉落和钓鱼竿线缆。

## 1.3.0 - 2026-09-25

- 新增示例人物：带商店、专属货币、幸福度和肖像的城镇居民。
- 新增首领???之眼及其召唤物品。

## 1.2.0 - 2026-09-25

- 新增宠物、照明宠物、仆从和哨兵。

## 1.1.0 - 2026-09-25

- 新增增益效果、`ModPlayer`、带安全存档和着色工具提示的 1×1 方块。

## 1.0.0 - 2026-09-23

- 首个版本：示例短剑和示例物品。
