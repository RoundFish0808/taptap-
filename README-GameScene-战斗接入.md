# GameScene 道路随机生怪与战斗接入

## 1. 怪物预制体

直接使用 Project 文件夹中保存的怪物预制体，无需放入场景，也无需手动添加脚本。运行时会在生成的实例上自动补上 RoadActorView 并寻找子物体的 Image。若需要指定图片或血量文字，可以自行添加 RoadActorView，以下是可选配置：
- 根物体使用 RectTransform，适用于现有 UI Image 怪物。
- Artwork：拖入显示怪物图片的 Image（可在子物体上）。
- Health Text：可选，拖入预制体内已有的普通 UI Text。不填也能战斗。
- Animation：留空。不需要 ActorAnimator，也不需要设置 Idle/Move/Attack 状态名。
- 原有 Animator 可以保留，默认动画正常循环；其动画片段不要修改预制体根物体的位置。
保存预制体。

## 2. 怪物显示父物体

在道路所在的 Canvas 下手动建一个 UI 空物体，命名 MonsterRoot。保持缩放为1，不挂 Layout Group；放在道路图片之后，保证怪物显示在道路上方。它只是容器，不需要挂脚本。

## 3. GamePlay 绑定

选中挂 FixedLoopGamePlay 的场景物体：
- Monster Prefab：拖入 Project 中刚保存的怪物预制体，字段接收 GameObject 预制体，运行时自动配置显示组件。
- Monster Root：拖入场景中的 MonsterRoot。
- GridMapView 的 Player：保留场景人物根物体。
- Road Nodes：保留已有按顺序排列的道路节点，不需要再配置刷怪点。
- Start Button、Pause Button、Round Text：保留已有绑定，不需额外添加 On Click 事件。
- Hero Health Text：可选的已有普通 UI Text，用于显示人物血量。

人物无需挂状态机或配置 Animator。如果需要受伤变色/血量文字反馈，可以在人物根物体添加 RoadActorView 并填写 Artwork；不加也能移动与攻击。

## 4. 参数与运行

默认值仅供原型测试，可在 Inspector 调整：
- Spawn Interval Seconds = 3：累计回合运行3秒后生成第一只，之后每3秒尝试生成一只。
- Road Capacity = 4：道路同时最多4只怪。节点不足时按可用空节点生成。
- Monster Health = 8、Monster Attack = 2。
- Hero Health = 100、Hero Attack = 4、Attack Seconds = 1。
- Step Seconds = 0.3：相邻节点之间移动时间。

随机生成会避开人物当前节点和已有怪物节点；没有空位时跳过本次生成。人物到达怪物节点后停止移动，每个攻击间隔先攻击一次，怪物存活则反击，怪物死亡后销毁实例并继续移动。目前用伤害与可选闪红反馈表现攻击，不切换人物攻击动画。

每次开始运行一圈，回到起点并清完起点的怪物后结束本回合。共10回合。暂停同时停止移动、伤害、生怪计时及 RoadActorView 管理的 Animator；只能点击开始继续当前回合。等待下一回合时也停止生怪，场上未击杀的怪物保留。人物血量归零则停止本局。

代码职责：FixedLoopGamePlay 负责场景绑定；PatrolSession 负责规则衔接；MonsterSpawnClock 负责产怪计时；AutoBattle 负责伤害；RoadCombatPresenter 负责预制体实例和显示；LoopWalker/RoundFlow 负责移动和回合。当前流程不使用 ActorStateMachine 或 ActorAnimator。

