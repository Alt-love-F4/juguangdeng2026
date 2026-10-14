# 咖啡成分、类型与出品评价

## 唯一权威表与 API

运行时配置来源为 `Assets/Resources/FreeCafe/catalog.json`。**类型识别与订单评价共用 `products[].evaluation.amounts`**，不存在另一张权威名称/配方表。`drinkNameRules` 仅保留旧序列化类型兼容，当前为空且分类不读取它。本文为普通项目说明；数值以 JSON 为准。

代码入口：

- `CafeWorld.ClassifyCoffee(WorldEntity)`：在 `CafeWorld.Classification.cs` 返回只读 `CafeCoffeeClassification`，包含 `productId`、`label`、`known`、`amountScore`、`standardScore`、`concentrationScore`、`total`、`description`。
- `CafeWorld.CompositionName(entity)` 直接委托 `ClassifyCoffee(entity).label`；类型不再要求含量分达到90。
- `CafeWorld.EvaluateProductComposition(entity, product)`：在 `CafeWorld.ProductEvaluation.cs` 返回只读评价；即使分类为未知，也可按某订单独立评价。
- 两个查询均不改变内容、温度、份数、成分账本或世界状态，不发出 `Changed`；结果为当次快照，不是缓存的可变数据。

空或无实际正量内容返回 `label="空容器"`；非空且不匹配任何产品返回 `label="迷之饮品"`。两者 `productId=null`、`known=false`，快照评分为0。未知类型不是删除/隐藏原料，详情仍显示真实内容与原始成分。

## 原料、单位与真实原始成分

`ingredientRules` 覆盖15种份料；设备、来源与杯具不放入该表。字段为 `id/label/liquid/soluble/gramsPerUnit/millilitersPerUnit/coffeeKind`。液体按实际ml占用容量，固体按g保存；小杯240ml、大杯400ml，固体不凭空增加液体体积。

| 原料 | 每份ml | 每份g | 说明 |
|---|---:|---:|---|
| water/hotwater、milk/hotmilk、coffee、cocoa.drink、matcha.drink | 200 | 0 | 液体；热/冷水归一为water，热/冷奶归一为milk |
| milkfoam | 20 | 0 | 独立液体原料奶泡，不归一为milk；计入总体积 |
| cream、honey、cocoa.powder、matcha.powder、grounds、beans | 0 | 12 | 不同固体身份；豆子必须先研磨 |
| sugar | 0 | 5 | 不默认放入所有产品白名单 |

库存为 `WorldEntity.contents : List<Portion>`。实际 `milliliters/grams`、`temperature`、`coffeeKind` 与内部 `productionScore` 都是事实。`Portion.composition : List<ConstituentAmount>` 是该份液体内部的来源账本，条目包含 `ingredientId/grams/milliliters`，不是一份重复的自由库存。

例如12g粉+200ml水生成200ml咖啡后，粉末不再单列一份自由固体，内部账本保留 `grounds12g + water200ml`；倒出一半时液体100ml、内部粉6g/水100ml同步拆分。合并时账本相加，稀释只增加水、不生成咖啡粉；加热/冷却不丢失粉水来源。可可/抹茶水组合也保存对应粉末的原始质量。`consumeCatalyst=true` 表示移入生成液的内部成分，并非质量消失。

新运行态使用实际正量ml/g识别类型，0量的成分不满足必需目标、不作为额外原料。仅有旧 `units` 而没有实际ml/g不能凭空得到类型；存档/旧份数归一应先补齐真实量。订单评分仍保留按份数回退：记录量优先，记录量为0且units>0时使用原料每份量；负值/NaN/Infinity不参与量计算。

## 咖啡来源、温度与浓度

所有咖啡物理ID仍为 `coffee`，不创建另一套黑咖啡/精粹实体。

| 来源 | 定义与变化 |
|---|---|
| cold_brew | 普通粉水咖啡常温状态；升至温热/沸腾变black |
| black | 普通粉水咖啡温热/沸腾状态；冷却至常温变cold_brew |
| essence | 精密计时热水萃取完成后的来源；加热、冷却、兑水不抹掉精粹来源 |

温度的权威值为 `Portion.temperatureCelsius`（0～100°C），`temperature` 三档枚举只保留给旧存档和规则条件兼容。旧数据尚无数值温度时按Room20°C／Warm60°C／Boiling100°C迁移。默认低于50°C视为冷、50°C起为热、95°C起为沸腾档；普通咖啡按该热阈值转为冷萃/黑咖啡，精粹来源不变。

**当前关闭自然降温**：静置、切换区域和长时间Tick不会降低温度。炉台在 `heatSeconds` 内逐步升到 `heatingTargetCelsius`，中断保留当前值；混入低温内容物才会按混合温度降低。

混温采用同等比热/密度的玩法近似：`T混合 = Σ(T × 热权重) / Σ热权重`，热权重为实际液体ml＋尚未合成的自由固体g；液体内部溯源的粉量不重复计重。只计实际接收量，不把溢出或洒出的量算入容器。加入、倾倒、滤出、分杯与合并都保留数值温度，不再把平均数舍入成0/1/2。

`catalog.production` 可编辑：`ambientTemperatureCelsius=20`、`warmTemperatureCelsius=60`、`boilingTemperatureCelsius=100`、`hotThresholdCelsius=50`、`boilingThresholdCelsius=95`、`heatingTargetCelsius=100`。旧 `boilingSeconds/warmSeconds` 仅保留兼容，不再启动降温计时。温度面板显示°C及0～100刻度。

`CoffeeGroundsGrams(Portion)` / `CoffeeWaterMl(Portion)` 是只读原始成分汇总API。浓度 `S = 原始咖啡粉g / 原始水ml`；它不是“杯中咖啡占全部液体的比例”，加入奶泡/牛奶不替换原咖啡内部粉水量。兑水会增加原始水并降低S。

前端成分标签使用“浓/中度/淡 + 冷萃咖啡液/黑咖啡液/精粹咖啡液”，不显示内部品质评级。精粹内部 `productionScore` 分为优秀100／良好70／糟糕30；它与浓度独立，参与订单评价，但不拼进前端液体名。

`compositionSettings : CoffeeCompositionSettings` 的可调字段：

| 字段 | 默认 | 含义 |
|---|---:|---|
| defaultCoffeeStrength | .06g/ml | 老咖啡完全无来源账本时的默认粉水比；只读API可纯计算回退，不修改库存 |
| strongCoffeeStrength | .075g/ml | S≥此值显示“浓” |
| mediumCoffeeStrength | .045g/ml | 未达浓且S≥此值显示“中度”；其余“淡” |

显式账本只有水没有粉时浓度为0，不能再以旧数据回退凭空补粉。调用 `EnsureCoffeeProvenance`、组合/倾倒/加工等写入路径可进行真实归一；分类/评价查询不能隐式调用写入归一。

## 数据驱动组合规则

`compositionRules` 按声明顺序、有限轮次应用。每条含 `id/label/catalystId/inputId/inputCoffeeKind/outputId/outputCoffeeKind/minimumTemperature/maximumTemperature/precisionOnly/consumeCatalyst`；温度为0～2闭区间。

| 规则 | 催化原料 | 输入→输出 | 温度 | 消耗到内部账本 |
|---|---|---|---|---|
| grounds-water / grounds-hotwater | grounds | water/hotwater→coffee cold_brew | 0～2 | 是 |
| cold-brew-heated | 无 | coffee cold_brew→black | 1～2 | 无催化原料 |
| black-coffee-cooled | 无 | coffee black→cold_brew | 0 | 无催化原料 |
| cocoa-water / cocoa-hotwater | cocoa.powder | water/hotwater→cocoa.drink | 0～2 | 是 |
| matcha-water / matcha-hotwater | matcha.powder | water/hotwater→matcha.drink | 0～2 | 是 |

当前8条规则。粉水组合优先级为咖啡、可可、抹茶；同一份水不能被复制给所有粉。黑/冷萃转换温度区间不相交，不会无限循环。精密滤杯不执行普通容器的粉水即时合成，而按实际流经水量消耗粉床。

### 滤杯粉量消耗公式

- 初始粉量 `G0` 在本轮首次过水时固定；目标水量 `Videal = brewIdealMl × G0 / brewReferenceGroundsGrams`。
- `Vpassed` 只累加实际通过滤杯的水，不是倒入上壶的水，也不是奶或其他液体。
- 剩余粉量 `Gremaining = G0 × max(0, 1 − min(Vpassed / Videal, 1))`。每个流出小段把对应粉量写入流出的咖啡内部账本。
- 默认12g粉、目标200ml：100ml经过后剩6g，200ml经过后剩0g；浮点边界会显式用完余粉，不留下极小残渣。
- 超过目标后的水不再生成额外粉量，只稀释已有咖啡；欠水则保留未消耗粉。注奶不会消耗粉床。
- 固定的本轮G0保证目标和消耗率不随剩余粉递减而漂移；过水后不能追加粉，清空/新一轮组合后重新计算。
- 可编辑参数位于 `catalog.production`：`brewReferenceGroundsGrams`、`brewIdealMl`、`brewIdealSeconds` 及金/绿区参数；CoffeeShop 的 `FreeCafeExtractionConfig` 会在启动时覆盖对应目标参数，调整场景时以该组件为准。

## 同一张产品表如何判定类型

每个 `evaluation.amounts` 条目均是必需成分：必须有实际正量、身份匹配、咖啡来源匹配、满足目标温度条件；所有正量的自由库存原料都必须匹配某目标。缺料、错误咖啡来源、额外糖/豆子/蜂蜜/可可、冷牛奶替代要求的热牛奶都不能冒称产品。

**类型只检查定义成分是否有实际正量，不限制含量或比例。** 例如50ml纯精粹仍属精粹咖啡；拿铁的咖啡、热牛奶、奶泡不必同比例添加，只要各有正量且身份、来源、温度正确就仍属拿铁。极少或过多的奶泡也不改变类型。推荐量、含量分、总分与内部水准只参与评分，不参与选择产品种类。

`ProductAmountSpec` 字段：

| 字段 | 默认 | 用途 |
|---|---|---|
| ingredientId / coffeeKind | 无/空 | 目标原料ID与可选来源 cold_brew/black/essence |
| idealMl / idealGrams | 0 | 推荐实际量，恰好一者>0；评分使用 |
| tolerance | .2 | 推荐量±20%免扣含量分；0≤值<1 |
| minimumStandard | 0 | 0～100内部水准软阈值；不是类型门槛 |
| classificationMinimumRatio | 0 | 旧液体比例下限；保留序列化与配置校验，分类不使用 |
| classificationMaximumRatio | 1 | 旧液体比例上限；保留序列化与配置校验，分类不使用 |
| classificationMinimumGrams | 0 | 旧固体克数下限；保留序列化与配置校验，分类不使用 |
| classificationMaximumGrams | 0 | 旧固体克数上限；保留序列化与配置校验，分类不使用 |
| minimumTemperature | -1 | -1不限制；0/1/2为至少常温/温热/沸腾；共同参与分类与匹配量评分 |

所有旧 `classificationMinimumRatio/classificationMaximumRatio/classificationMinimumGrams/classificationMaximumGrams` 含量限制均已退出分类，保留字段仅用于旧数据兼容。既有结构校验仍生效：比例须在0～1且下限≤上限；克数须非负且非零上限≥下限；NaN/Infinity仍为非法配置。合法但严格的旧范围不会拒绝正量成分（例如比例0～0、1～1或固体0.1～0.2g）。`classificationPriority` 仍用于多条匹配产品的优先级，并非含量限制。

`ProductEvaluationSpec` 字段：

| 字段 | 默认/当前 | 含义 |
|---|---|---|
| amounts | 非空 | 类型识别与评分共用的全部必需成分 |
| classify | true | false只供评价、不参与类型选择 |
| classificationPriority | 默认0，当前100 | 高者优先；同优先级按数组先后，绝不按含量分挑选 |
| amountWeight / standardWeight | .65/.35 | 两类评分权重，非负且和>0，使用时归一 |
| extraIngredientPenalty | .15 | 每种额外归一身份扣总分15%，碎片不重复计数 |
| enforceCup | false | 可选杯型硬判零；不改变类型和分项 |
| concentrationWeight | 默认0；咖啡商品.2、非咖啡0 | 浓度在咖啡水准中的混合权重0～1；0兼容原有评分 |
| idealCoffeeStrength | .06 | 理想原始粉g/水ml，必须有限正数 |
| coffeeStrengthTolerance | .2 | 浓度理想值的相对免罚容差，0≤值<1 |

上述类为可变配置DTO，构造世界时校验；不是共享的可变分类结果。运行期变更配置须保持约束，并自行触发需要的UI刷新。非法权重/理想值/温度/范围、重复归一目标会在目录校验时拒绝；评价查询面对畸形参数安全返回0，分类忽略该行。

## 当前11个产品（现有9个ID保留，新增2个）

价格、现有ID和解锁门槛保留；当前全部声望0，`minimumScore=60`、最多重做1次。新增冷萃与奶咖啡也进入同一订单产品池，会扩展原来的9项轮换。杯型是参考，所有evaluation当前不强制杯型。

| ID / 显示名 | 必需身份与推荐量 | 杯型 | 价格 |
|---|---|---|---:|
| espresso / 精粹咖啡 | essence咖啡200ml | 小 | 50 |
| latte / 拿铁 | essence180ml、热牛奶200ml、薄奶泡20ml | 大 | 80 |
| honeylatte / 蜂蜜拿铁 | essence180ml、热牛奶200ml、薄奶泡20ml、蜂蜜12g | 大 | 100 |
| matchalatte / 抹茶拿铁 | 抹茶液200ml、牛奶200ml | 大 | 80 |
| cocoa / 热可可 | 可可液200ml、牛奶200ml | 大 | 100 |
| macchiato / 玛奇朵 | essence160ml、奶泡80ml；没有牛奶目标 | 小 | 80 |
| mocha / 摩卡 | black320ml、可可粉12g、奶油12g；没有牛奶目标 | 大 | 120 |
| blackcoffee / 黑咖啡 | black240ml | 小 | 50 |
| creamcoffee / 奶油黑咖啡 | black240ml、奶油12g | 大 | 100 |
| cold_brew / 冷萃咖啡 | cold_brew240ml | 小 | 50 |
| milkcoffee / 奶咖啡 | black200ml、热牛奶200ml | 大 | 80 |

“热牛奶”条件是当前 `temperature≥1`，不只是旧specId叫hotmilk；所有别名仍按milk归一。拿铁和蜂蜜拿铁中的“薄奶泡20ml”只是推荐用量，偏少或偏多只影响含量评分，不限制类型。玛奇朵只要求essence+奶泡正量，没有热牛奶行；有牛奶的拿铁不会因为奶泡偏多而套用玛奇朵。摩卡直接向黑咖啡加可可粉与奶油；无自由清水时可可粉仍为真实自由固体，不能用可可液/单独可可代替该目标。精粹咖啡目标水准70，其余目标60；来源错误不能仅靠高水准代替。

`products.ingredients` 仅保留旧份数/界面参考，与新理想量同步；`recipes/operations` 可作旧玩法或指引，不限制制作模式加料顺序。旧 `requiredTemperature/idealCoffeeRatio/idealSweetness` 继续保留兼容，但新评分不会偷偷用它们；真实热奶门槛来自amount.minimumTemperature。非咖啡商品“热可可”保留旧菜单名，当前amounts不要求温度，实际温度单独显示。

## 评分全部公式（与分类独立）

定义同一个量偏差函数 `F(A,I,T)`：

1. `D = max(0, abs(A-I) - I*T)`。
2. A>0：`F = clamp(100*(1-D/(I*(1-T))),0,100)`；A≤0：0。
3. 每个目标匹配量A只累计正确身份、咖啡来源和minimumTemperature的份料；用idealMl或idealGrams以及tolerance计算含量分。
4. 咖啡加工水准Q为匹配咖啡的体积加权productionScore，坏值按0；非咖啡原料存在为100，缺料为0。
5. 对每个咖啡目标，`S = sum(匹配咖啡原始grounds g)/sum(匹配咖啡原始water ml)`；无水为0。浓度分 `C=F(S,idealCoffeeStrength,coffeeStrengthTolerance)`。
6. 咖啡混合水准 `Q'=Q*(1-concentrationWeight)+C*concentrationWeight`；非咖啡仍使用原水准。若Q'<minimumStandard且阈值M>0，使用 `Q'^2/M` 作为该目标水准分。
7. 各必需目标等权平均得到amountScore与standardScore。`total=(amountScore*amountWeight+standardScore*standardWeight)/(amountWeight+standardWeight)`。
8. 按额外原料身份种数N乘 `max(0,1-N*extraIngredientPenalty)`。错误咖啡来源/温度已视为该目标缺量，不再按同一原料身份双扣；旧已反应粉残渣评分兼容不重复罚，严格类型分类则不忽略未经归一的额外自由粉末。
9. 若enforceCup=true且杯型错误，总分0，分项不变。

结果 `concentrationScore` 为各咖啡目标浓度分按匹配咖啡ml加权平均，无咖啡为0；此值即使weight=0也可观察，不影响关闭浓度维度后的总分。配置开启时description额外含“浓度”分项。

例如200ml essence、水准100、内部12g/200ml：含量100、浓度100、水准100。内部水改为400ml而液体/订单量仍记200ml的纯评价实验：S=.03、浓度62.5、水准92.5（权重.2），**含量仍100**。真实兑水路径应同步增加液体ml，届时还会独立改变含量分。精品/一般/糟糕总分等级（≥90/≥60/其余）及精密萃取内部100/70/30水准只供后端评价，不参与类型、不显示在液体成分名。

## 奶泡接入与扩展步骤

- `milkfoam` 实体为可搬动份料capabilities5，复用 `artId="milk"`，不创建新美术图。实体Prefab库仍需独立的milkfoam规格映射；JSON不会自动生成Prefab绑定。
- 搅拌器 `foam-milk` / `foam-hotmilk` 操作分别将milk/hotmilk打成milkfoam，保留实际输入ml与来源；20ml每份是默认取料/旧数据回退，不意味着200ml输入自动损失到20ml。打发后的真实产物再按目标ml倒入。
- 搅拌器保持固定Processor，不因液体产物改成可搬动容器。
- 添加香草等原料：先新增entities与ingredientRules（保留GUID资源映射），可选添加来源和加工操作；若添加订单，在同一products行的evaluation.amounts列出所有必需成分及来源/温度条件、理想量/水准/浓度参数，不另加drinkNameRules，也不添加分类含量门槛。
- 明确产品ID、显示名、价格、杯型、门槛、教程；同步ingredients，审核新增产品对订单轮换的影响。纯数量推荐值变更只影响评分，不改变类型识别。

## 回归检查清单

- 11种理想商品均唯一识别；同比缩小后仍是同类但含量分下降；拿铁任意非同比正量也能识别，严重偏离推荐量时评分低。
- 普通粉水消耗自由粉行、内部g/ml守恒；倒出/分杯/合并/兑水/存档后浓度与原始质量可追溯。
- 普通冷热互转，essence不因温度丢失来源；名称使用浓度而非隐藏品质。
- 必需成分为0、只有units没有实际量、冷奶替代热奶、可可单独、豆子/额外糖、错误coffeeKind均不能冒称。
- 拿铁奶泡任意正比例均接受，包括0.01%和99%；奶泡与热牛奶不可互换，必需目标0量不能靠units回退满足。
- 类型识别完全不读推荐量分门槛；评价可以独立返回低分、不影响原始库存。
- 浓度权重0保留旧评分；启用时浓度进入水准而非含量，软阈值公式保持不变。
- 旧分类比例/克数上下限无论宽窄均不限制正量类型识别；非法值/反向范围/重复目标仍按既有结构校验拒绝；资源复用和Prefab规格映射按奶泡ID验证。
