# NCS Integration Report

- Filter: (all)
- Events: 38
- Errors: 18
- Warnings: 1

## EV_DEMO_CAFE_ARRIVE [static_pass]

- Entry: Immediate
- Sources: Immediate / continuation or code-owned entrance
- Conditions: (none)
- Presentation: NoClass
- Pages/lines/options: 0/0/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SpawnNpc -> obj:adele

## EV_DEMO_CAFE_BEFORE [static_pass]

- Entry: Immediate
- Sources: Immediate / continuation or code-owned entrance
- Conditions: (none)
- Presentation: Conversation/DEMO_CAFE_BEFORE
- Pages/lines/options: 1/1/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: (none)

## EV_DEMO_CAFE_CANCEL [static_pass]

- Entry: Immediate
- Sources: Immediate / continuation or code-owned entrance
- Conditions: (none)
- Presentation: NoClass
- Pages/lines/options: 0/0/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: DespawnNpc -> obj:adele

## EV_DEMO_CAFE_CHAT [static_pass]

- Entry: Immediate
- Sources: Immediate / continuation or code-owned entrance
- Conditions: (none)
- Presentation: Conversation/DEMO_CAFE_CHAT
- Pages/lines/options: 1/1/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: (none)

## EV_DEMO_CAFE_DELAYED_RESERVE [static_pass]

- Entry: Immediate
- Sources: Immediate / continuation or code-owned entrance
- Conditions: (none)
- Presentation: NoClass
- Pages/lines/options: 0/0/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: ReserveCafeVisit -> obj:adele

## EV_DEMO_CAFE_ENDED [static_pass]

- Entry: Immediate
- Sources: Immediate / continuation or code-owned entrance
- Conditions: (none)
- Presentation: NoClass
- Pages/lines/options: 0/0/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> demo.cafe.ended

## EV_DEMO_CAFE_LEAVING [static_pass]

- Entry: Immediate
- Sources: Immediate / continuation or code-owned entrance
- Conditions: (none)
- Presentation: NoClass
- Pages/lines/options: 0/0/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> demo.cafe.leaving

## EV_DEMO_CAFE_RESERVE [static_pass]

- Entry: Immediate
- Sources: Immediate / continuation or code-owned entrance
- Conditions: (none)
- Presentation: NoClass
- Pages/lines/options: 0/0/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: ReserveCafeVisit -> obj:adele

## EV_DEMO_CAFE_SERVED [static_pass]

- Entry: Immediate
- Sources: Immediate / continuation or code-owned entrance
- Conditions: (none)
- Presentation: Conversation/DEMO_CAFE_SERVED
- Pages/lines/options: 1/1/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: (none)

## EV_FIRST_DAY_PERCEPTION_CHECK [static_pass]

- Entry: Immediate
- Sources: Immediate / continuation or code-owned entrance
- Conditions: (none)
- Presentation: NoClass setup=first_day_perception_check
- Pages/lines/options: 0/0/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: first_day_perception
- Results: (none)

## EV_FIRST_DAY_PERCEPTION_FAILURE [static_pass]

- Entry: Immediate
- Sources: Immediate / continuation or code-owned entrance
- Conditions: (none)
- Presentation: NoClass setup=first_day_perception_failure
- Pages/lines/options: 0/0/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: (none)

## EV_FIRST_DAY_PERCEPTION_SUCCESS [static_pass]

- Entry: Immediate
- Sources: Immediate / continuation or code-owned entrance
- Conditions: (none)
- Presentation: NoClass setup=first_day_perception_success
- Pages/lines/options: 0/0/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: (none)

## EV_REVOLUTION [static_pass]

- Entry: Immediate
- Sources: Trigger: T_CAL_D60 / world.calendar @ 60 / Cross
- Conditions: (none)
- Presentation: NoClass setup=revolution
- Pages/lines/options: 0/0/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> revolution; BarAdd -> bar:world.stability

## EV_TEST_DELIVERY_CONSECUTIVE [static_pass]

- Entry: Immediate
- Sources: Action: act_test_delivery_consecutive
- Conditions: (none)
- Presentation: NoClass setup=EV_TEST_DELIVERY_CONSECUTIVE
- Pages/lines/options: 0/0/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: ItemAdd -> delivery_receipt

## EV_TEST_DELIVERY_CONSECUTIVE_MILESTONE [static_pass]

- Entry: Immediate
- Sources: Action: act_test_delivery_consecutive
- Conditions: (none)
- Presentation: NoClass setup=EV_TEST_DELIVERY_CONSECUTIVE_MILESTONE
- Pages/lines/options: 0/0/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: (none)

## EV_TEST_DELIVERY_CUMULATIVE [static_pass]

- Entry: Immediate
- Sources: Action: act_test_delivery_cumulative
- Conditions: (none)
- Presentation: NoClass setup=EV_TEST_DELIVERY_CUMULATIVE
- Pages/lines/options: 0/0/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: ItemAdd -> delivery_receipt

## EV_TEST_DELIVERY_CUMULATIVE_MILESTONE [static_pass]

- Entry: Immediate
- Sources: Action: act_test_delivery_cumulative
- Conditions: (none)
- Presentation: NoClass setup=EV_TEST_DELIVERY_CUMULATIVE_MILESTONE
- Pages/lines/options: 0/0/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: (none)

## arrival.accept [static_pass]

- Entry: Immediate
- Sources: QueueEvent: MA70.10.06-S1-2.2 -> arrival.accept
- Conditions: (none)
- Presentation: Conversation/arrival.accept setup=arrival.accept
- Pages/lines/options: 2/14/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: QueueEvent -> event:arrival.complete

## arrival.agent [static_pass]

- Entry: Immediate
- Sources: QueueEvent: arrival.reply.1 -> arrival.agent; QueueEvent: arrival.reply.2 -> arrival.agent
- Conditions: (none)
- Presentation: Conversation/arrival.agent setup=arrival.agent
- Pages/lines/options: 1/7/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: QueueEvent -> event:arrival.check

## arrival.check [static_pass]

- Entry: Immediate
- Sources: QueueEvent: arrival.agent -> arrival.check
- Conditions: (none)
- Presentation: NoClass setup=arrival.check
- Pages/lines/options: 0/0/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: arrival.perception
- Results: (none)

## arrival.complete [static_pass]

- Entry: Immediate
- Sources: QueueEvent: arrival.accept -> arrival.complete
- Conditions: (none)
- Presentation: NoClass setupType=Tpl_Narration
- Pages/lines/options: 0/0/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: (none)

## arrival.decision [static_pass]

- Entry: Immediate
- Sources: QueueEvent: arrival.request -> arrival.decision; QueueEvent: MA70.10.06-S1-3.1 -> arrival.decision
- Conditions: (none)
- Presentation: Conversation/arrival.decision setup=arrival.decision
- Pages/lines/options: 1/1/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: (none)

## arrival.insight [static_pass]

- Entry: Immediate
- Sources: Immediate / continuation or code-owned entrance
- Conditions: (none)
- Presentation: Conversation/arrival.insight setup=arrival.insight
- Pages/lines/options: 1/1/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: QueueEvent -> event:arrival.request

## arrival.leave [static_pass]

- Entry: Immediate
- Sources: QueueEvent: MA70.10.06-S1-3.2 -> arrival.leave
- Conditions: (none)
- Presentation: NoClass setupType=Tpl_Narration
- Pages/lines/options: 0/0/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: (none)

## arrival.refuse [static_pass]

- Entry: Immediate
- Sources: QueueEvent: MA70.10.06-S1-2.1 -> arrival.refuse
- Conditions: (none)
- Presentation: Conversation/arrival.refuse setup=arrival.refuse
- Pages/lines/options: 1/9/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: (none)

## arrival.reply.1 [static_pass]

- Entry: Immediate
- Sources: QueueEvent: MA70.10.06-S1-1.1 -> arrival.reply.1
- Conditions: (none)
- Presentation: Conversation/arrival.reply.1 setup=arrival.reply.1
- Pages/lines/options: 1/1/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: QueueEvent -> event:arrival.agent

## arrival.reply.2 [static_pass]

- Entry: Immediate
- Sources: QueueEvent: MA70.10.06-S1-1.2 -> arrival.reply.2
- Conditions: (none)
- Presentation: Conversation/arrival.reply.2 setup=arrival.reply.2
- Pages/lines/options: 1/1/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: QueueEvent -> event:arrival.agent

## arrival.request [static_pass]

- Entry: Immediate
- Sources: QueueEvent: arrival.insight -> arrival.request
- Conditions: (none)
- Presentation: Conversation/arrival.request setup=arrival.request
- Pages/lines/options: 1/9/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: QueueEvent -> event:arrival.decision

## arrival.start [static_pass]

- Entry: Immediate
- Sources: Immediate / continuation or code-owned entrance
- Conditions: (none)
- Presentation: Conversation/arrival.start setup=arrival.start
- Pages/lines/options: 2/14/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: (none)

## church.1009.s2 [blocked]

- Entry: Immediate
- Sources: Immediate / continuation or code-owned entrance
- Conditions: G1: BAR_BETWEEN(world.calendar, 4,4); G1: BOOL_EQ(story.church.1009.s2.completed, false)
- Presentation: Conversation/church.1009.s2.conversation setup=church.1009.s2
- Pages/lines/options: 4/36/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.church.1009.s2.completed
### Errors
- Construction blocker: D-B03-rose-real-completion


## neutral.1008.s1 [static_pass]

- Entry: MapAction
- Sources: Action: neutral.1008.s1.action @ market
- Conditions: G1: BAR_BETWEEN(world.calendar, 3,3); G1: BOOL_EQ(story.neutral.1008.s1.completed, false); G1: BOOL_EQ(world.map.active, true)
- Presentation: Conversation/neutral.1008.s1
- Pages/lines/options: 9/60/3
- Letter/reading: 0/0 pages / 0 lines
- Checks: neutral.1008.s1.q1b
- Results: SetFlag -> story.neutral.1008.s1.completed

## oct07.s1 [static_pass]

- Entry: MapAction
- Sources: Action: oct07.cafe.s1 @ cafe
- Conditions: G1: BAR_BETWEEN(world.calendar, 2,2); G1: BOOL_EQ(story.oct07.s1.completed, false)
- Presentation: Conversation/oct07.s1.conversation setup=oct07.s1
- Pages/lines/options: 2/40/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.oct07.s1.completed

## oct07.s2 [static_pass]

- Entry: BeforeNight
- Sources: Date: day 2 / Night / Once
- Conditions: G1: BAR_BETWEEN(world.calendar, 2,2); G1: BOOL_EQ(story.oct07.s2.completed, false); G1: BOOL_EQ(story.oct07.s1.completed, true)
- Presentation: Conversation/oct07.s2.conversation setup=oct07.s2
- Pages/lines/options: 7/34/0
- Letter/reading: 2/1 pages / 7 lines
- Checks: oct07.s2.perception
- Results: SetFlag -> story.oct07.s2.completed; SetFlag -> story.oct09.meeting.known

## revolution.1013.s2 [blocked]

- Entry: Immediate
- Sources: Immediate / continuation or code-owned entrance
- Conditions: G1: BAR_BETWEEN(world.calendar, 8,8); G1: BOOL_EQ(story.oct07.s2.completed, true); G1: BOOL_EQ(story.revolution.1013.s2.completed, false)
- Presentation: Conversation/revolution.1013.s2.conversation setup=revolution.1013.s2
- Pages/lines/options: 6/52/3
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.revolution.1013.s2.completed
### Errors
- Construction blocker: C-B01
- Construction blocker: C-B02
- Construction blocker: C-B05
- Construction blocker: C-G04


## revolution.1014.s1 [blocked]

- Entry: MorningBeforeMap
- Sources: Date: day 9 / Morning / Once
- Conditions: G1: BAR_BETWEEN(world.calendar, 9,9); G1: BOOL_EQ(story.oct07.s2.completed, true); G1: BOOL_EQ(story.revolution.1014.s1.completed, false)
- Presentation: Conversation/revolution.1014.s1.conversation setup=revolution.1014.s1
- Pages/lines/options: 23/171/12
- Letter/reading: 2/1 pages / 1 lines
- Checks: revolution.1014.s1.wisdom4, revolution.1014.s1.perception6, revolution.1014.s1.wisdom5
- Results: SetFlag -> story.revolution.1014.s1.completed
### Errors
- Construction blocker: C-B03
- Construction blocker: C-B04
- Construction blocker: C-B05
- Construction blocker: B-real-completion
- Construction blocker: C-G02
- Construction blocker: C-G07


## revolution.1028.s1 [blocked]

- Entry: BeforeNight
- Sources: Date: day 23 / Night / Once
- Conditions: G1: BAR_BETWEEN(world.calendar, 23,23); G1: BOOL_EQ(story.oct07.s2.completed, true); G1: BOOL_EQ(story.revolution.1028.s1.completed, false)
- Presentation: Conversation/revolution.1028.s1.conversation setup=revolution.1028.s1
- Pages/lines/options: 10/144/3
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.revolution.1028.s1.completed
### Errors
- Construction blocker: C-B05
- Construction blocker: C-B06
- Construction blocker: C-G07


## rose.1009.s1 [static_pass]

- Entry: MapAction
- Sources: Action: rose.1009.s1.action @ 圣徒街
- Conditions: G1: BAR_BETWEEN(world.calendar, 4,4); G1: BOOL_EQ(story.oct09.meeting.known, true)
- Presentation: Conversation/rose.1009.s1.conversation setup=rose.1009.s1
- Pages/lines/options: 9/67/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: rose.1009.s1.perception
- Results: SetFlag -> story.rose.1009.s1.completed

## rose.1011.s1 [blocked]

- Entry: MorningBeforeMap
- Sources: Date: day 6 / Morning / Once
- Conditions: G1: BAR_BETWEEN(world.calendar, 6,6); G1: BOOL_EQ(story.rose.1011.s1.completed, false); G1: BOOL_EQ(story.neutral.1010.s1.completed, true); G1: BOOL_EQ(story.rose.1009.s1.completed, true)
- Presentation: Conversation/rose.1011.s1.conversation setup=rose.1011.s1
- Pages/lines/options: 27/123/11
- Letter/reading: 1/1 pages / 3 lines
- Checks: rose.1011.s1.check.sparrow, rose.1011.s1.check.portrait, rose.1011.s1.check.wisdom
- Results: (none)
### Errors
- Construction blocker: B-B05
- Construction blocker: B-C02
- Construction blocker: B-C04
- Construction blocker: B-C07

### Warnings
- onceOnly event has no result rows; confirm that this is intentional.

