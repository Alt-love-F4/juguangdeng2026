# 咖啡店数据表

策划来源是 Data 下的 Excel，运行时读取 Assets/Settings/Resources/Luban 下的生成JSON。
表头沿用 Luban 约定：第1行为 ##var，第2行为 ##type，第3行为 ##group，第4行为 ##，第5行开始数据；不要更改前4行。

- Text：菜单与配方文本。
- Ingredient：库存、补货、解锁。
- Device：加工耗时和升级。
- GameParam：营业时长、初始资金、客流等。
- Recipe：配方元数据；steps 已清空，不再是现有配方的权威步骤来源。
- RecipeStep：有序、结构化的制作步骤；设备时间来自 Device，而不是重复嵌入步骤字符串。
- WorldObject：物品、来源、位置、设备、NPC的角色定义和初始表现。
- Npc：稳定身份、外观、初始台词。

先检查：python Assets/ConfigTables/generate_tables.py --validate
生成：python Assets/ConfigTables/generate_tables.py
项目使用等效Luban生成器，schema.xml同步描述表结构；不要手改生成JSON。
新增物品/实体或调整布局后，在CoffeeShop场景运行 Tools/Spotlight/Build Cafe Workspace。
只调价格/时间/库存时重新生成表即可，无需重装场景。

完整说明见 Docs/CafeArchitecture.md。
