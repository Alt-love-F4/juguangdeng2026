# NCS Integration Report

- Filter: (all)
- Events: 346
- Errors: 4
- Warnings: 0

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
- Results: SetFlag -> story.arrival.leave.completed

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

## church.1009.s2 [static_pass]

- Entry: BeforeNight
- Sources: Date: day 4 / Night / Once
- Conditions: G1: BOOL_EQ(story.church.1009.s2.completed, false); G1: BOOL_EQ(story.rose.1009.s1.completed, true); G1: BAR_BETWEEN(world.calendar, 4,4)
- Presentation: Conversation/church.1009.s2.conversation setup=church.1009.s2
- Pages/lines/options: 38/36/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.church.1009.s2.completed; SetFlag -> story.church.1009.s2.hat_helped; SetFlag -> known.alienor

## church.1014.s2 [static_pass]

- Entry: AfterBusiness
- Sources: Date: day 9 / Noon / Once
- Conditions: G1: BOOL_EQ(story.church.1014.s2.completed, false); G1: BOOL_EQ(story.neutral.1013.s2.completed, true); G1: BOOL_EQ(story.revolution.1014.s1.completed, true); G1: BAR_BETWEEN(world.calendar, 9,9)
- Presentation: Conversation/church.1014.s2.conversation setup=church.1014.s2
- Pages/lines/options: 54/50/6
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.church.1014.s2.completed

## church.1020.s1 [static_pass]

- Entry: MorningBeforeMap
- Sources: Date: day 15 / Morning / Once
- Conditions: G1: BOOL_EQ(story.church.1020.s1.completed, false); G1: BOOL_EQ(story.church.1014.s2.completed, true); G1: BOOL_EQ(story.revolution.1014.s1.completed, true); G1: BAR_BETWEEN(world.calendar, 15,15)
- Presentation: Conversation/church.1020.s1.conversation setup=church.1020.s1
- Pages/lines/options: 205/195/7
- Letter/reading: 0/0 pages / 0 lines
- Checks: church.1020.s1.check.search_language, church.1020.s1.check.search_courage, church.1020.s1.check.search_wisdom, church.1020.s1.check.search_perception
- Results: SetFlag -> story.church.1020.s1.completed; SetFlag -> story.church.1020.s1.cleared; SetFlag -> church.detention.requested; SetFlag -> church.detention.active; SetFlag -> story.church.1020.s1.cleared; SetFlag -> church.detention.requested; SetFlag -> church.detention.active; SetFlag -> story.church.1020.s1.cleared; SetFlag -> church.detention.requested; SetFlag -> church.detention.active; SetFlag -> story.church.1020.s1.cleared; SetFlag -> church.detention.requested; SetFlag -> church.detention.active

## church.1023.s1 [static_pass]

- Entry: MorningBeforeMap
- Sources: Date: day 18 / Morning / Once
- Conditions: G1: BOOL_EQ(story.church.1023.s1.completed, false); G1: BOOL_EQ(story.church.1020.s1.completed, true); G1: BOOL_EQ(church.detention.active, true); G1: BAR_BETWEEN(world.calendar, 18,18)
- Presentation: Conversation/church.1023.s1.conversation setup=church.1023.s1
- Pages/lines/options: 483/468/7
- Letter/reading: 0/0 pages / 0 lines
- Checks: church.1023.s1.check.altered_release
- Results: SetFlag -> story.church.1023.s1.completed; SetFlag -> church.release.records_anomalous; SetFlag -> church.release.records_anomalous; SetFlag -> church.revolution.intervened; SetFlag -> church.detention.release_failed; QueueEvent -> church.1023.s2; SetFlag -> church.release.records_anomalous; SetFlag -> church.release.records_anomalous; SetFlag -> church.revolution.intervened; SetFlag -> church.detention.release_failed; QueueEvent -> church.1023.s2; SetFlag -> church.release.records_anomalous; SetFlag -> church.release.records_anomalous; SetFlag -> church.revolution.intervened; SetFlag -> church.detention.release_failed; QueueEvent -> church.1023.s2; SetFlag -> church.release.records_anomalous; SetFlag -> church.release.records_anomalous; SetFlag -> church.revolution.intervened; SetFlag -> church.detention.release_failed; QueueEvent -> church.1023.s2; SetFlag -> church.release.records_anomalous; SetFlag -> church.release.records_anomalous; SetFlag -> church.revolution.intervened; SetFlag -> church.detention.release_failed; QueueEvent -> church.1023.s2; SetFlag -> church.release.persuasion; SetFlag -> story.church.1023.s1.release.persuasion; SetFlag -> church.detention.active; SetFlag -> church.release.bribery; SetFlag -> story.church.1023.s1.release.bribery; SetFlag -> story.church.1023.s1.release.bribe; SetFlag -> church.detention.active; SetFlag -> church.release.rescue; SetFlag -> story.church.1023.s1.release.rescue; SetFlag -> church.detention.active; SetFlag -> church.release.rescue; SetFlag -> story.church.1023.s1.release.rescue; SetFlag -> church.detention.active; SetFlag -> church.release.rescue; SetFlag -> story.church.1023.s1.release.rescue; SetFlag -> church.detention.active; SetFlag -> church.release.rescue; SetFlag -> story.church.1023.s1.release.rescue; SetFlag -> church.detention.active; SetFlag -> church.release.rescue; SetFlag -> story.church.1023.s1.release.rescue; SetFlag -> church.detention.active

## church.1023.s2 [static_pass]

- Entry: Immediate
- Sources: QueueEvent: church.1023.s1 -> church.1023.s2; QueueEvent: church.1023.s1 -> church.1023.s2; QueueEvent: church.1023.s1 -> church.1023.s2; QueueEvent: church.1023.s1 -> church.1023.s2; QueueEvent: church.1023.s1 -> church.1023.s2
- Conditions: G1: BOOL_EQ(story.church.1023.s2.completed, false); G1: BOOL_EQ(story.church.1023.s1.completed, true); G1: BOOL_EQ(church.detention.release_failed, true); G1: BOOL_EQ(church.detention.active, true)
- Presentation: Conversation/church.1023.s2.conversation setup=church.1023.s2
- Pages/lines/options: 26/25/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.church.1023.s2.completed; SetFlag -> terminal.church.ending.if2

## church.1104.s1 [static_pass]

- Entry: MorningBeforeMap
- Sources: Date: day 30 / Morning / Once
- Conditions: G1: BOOL_EQ(story.church.1104.s1.completed, false); G1: BOOL_EQ(story.rose.1103.s1.completed, true); G1: BOOL_EQ(church.alienor.sheltered, true); G1: BAR_BETWEEN(world.calendar, 30,30)
- Presentation: Conversation/church.1104.s1.conversation setup=church.1104.s1
- Pages/lines/options: 411/406/8
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.church.1104.s1.completed; SetFlag -> known.alienor

## church.1105.s1 [static_pass]

- Entry: MorningBeforeMap
- Sources: Date: day 31 / Morning / Once
- Conditions: G1: BOOL_EQ(story.church.1105.s1.completed, false); G1: BOOL_EQ(story.rose.1103.s1.completed, true); G1: BOOL_EQ(church.alienor.refused_shelter, true); G1: BAR_BETWEEN(world.calendar, 31,31); G1: BOOL_EQ(church.alienor.sheltered, false); G1: BOOL_EQ(story.rose.1024.s1.completed, true); G2: BOOL_EQ(story.church.1105.s1.completed, false); G2: BOOL_EQ(story.rose.1103.s1.completed, true); G2: BOOL_EQ(church.alienor.refused_shelter, true); G2: BAR_BETWEEN(world.calendar, 31,31); G2: BOOL_EQ(church.alienor.sheltered, false); G2: BOOL_EQ(story.rose.1024.s2.completed, true)
- Presentation: Conversation/church.1105.s1.conversation setup=church.1105.s1
- Pages/lines/options: 86/80/8
- Letter/reading: 0/0 pages / 0 lines
- Checks: church.1105.s1.check.intuition
- Results: SetFlag -> story.church.1105.s1.completed

## church.1105.s2 [static_pass]

- Entry: MorningBeforeMap
- Sources: Date: day 31 / Morning / Once
- Conditions: G1: BOOL_EQ(story.church.1105.s2.completed, false); G1: BOOL_EQ(story.rose.1103.s1.completed, true); G1: BOOL_EQ(church.alienor.sheltered, true); G1: BOOL_EQ(story.church.1104.s1.completed, true); G1: BAR_BETWEEN(world.calendar, 31,31); G1: BOOL_EQ(church.alienor.refused_shelter, false); G1: BOOL_EQ(story.rose.1024.s1.completed, true); G2: BOOL_EQ(story.church.1105.s2.completed, false); G2: BOOL_EQ(story.rose.1103.s1.completed, true); G2: BOOL_EQ(church.alienor.sheltered, true); G2: BOOL_EQ(story.church.1104.s1.completed, true); G2: BAR_BETWEEN(world.calendar, 31,31); G2: BOOL_EQ(church.alienor.refused_shelter, false); G2: BOOL_EQ(story.rose.1024.s2.completed, true)
- Presentation: Conversation/church.1105.s2.conversation setup=church.1105.s2
- Pages/lines/options: 85/79/8
- Letter/reading: 0/0 pages / 0 lines
- Checks: church.1105.s2.check.intuition
- Results: SetFlag -> story.church.1105.s2.completed; SetFlag -> known.alienor

## church.1111.s1 [static_pass]

- Entry: MorningBeforeMap
- Sources: Date: day 37 / Morning / Once
- Conditions: G1: BOOL_EQ(story.church.1111.s1.completed, false); G1: BOOL_EQ(story.revolution.1107.s1.completed, true); G1: BAR_BETWEEN(world.calendar, 37,37); G1: BOOL_EQ(route.neutral, false); G1: BOOL_EQ(route.rose, false); G1: BOOL_EQ(route.church, false); G1: BOOL_EQ(route.revolution, false)
- Presentation: Conversation/church.1111.s1.conversation setup=church.1111.s1
- Pages/lines/options: 67/58/9
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.church.1111.s1.completed; SetFlag -> terminal.church.ending.if4; SetFlag -> route.neutral; QueueEvent -> neutral.route.1111.s1; SetFlag -> route.neutral; QueueEvent -> neutral.route.1111.s2; SetFlag -> route.church; QueueEvent -> church.route.1111.s1; SetFlag -> route.rose; QueueEvent -> rose.route.1111.s2; SetFlag -> route.revolution; QueueEvent -> revolution.route.1111.s2

## church.route.1111.s1 [static_pass]

- Entry: Immediate
- Sources: QueueEvent: church.1111.s1 -> church.route.1111.s1
- Conditions: G1: BOOL_EQ(story.church.route.1111.s1.completed, false); G1: BOOL_EQ(story.church.1111.s1.completed, true); G1: BOOL_EQ(story.church.1104.s1.completed, true); G1: BOOL_EQ(route.neutral, false); G1: BOOL_EQ(route.rose, false); G1: BOOL_EQ(route.church, true); G1: BOOL_EQ(route.revolution, false); G1: BAR_BETWEEN(world.calendar, 37,37)
- Presentation: Conversation/church.route.1111.s1.conversation setup=church.route.1111.s1
- Pages/lines/options: 38/37/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.church.route.1111.s1.completed

## church.route.1112.s1 [static_pass]

- Entry: MorningBeforeMap
- Sources: Date: day 38 / Morning / Once
- Conditions: G1: BOOL_EQ(story.church.route.1112.s1.completed, false); G1: BOOL_EQ(story.church.route.1111.s1.completed, true); G1: BOOL_EQ(route.neutral, false); G1: BOOL_EQ(route.rose, false); G1: BOOL_EQ(route.church, true); G1: BOOL_EQ(route.revolution, false); G1: BAR_BETWEEN(world.calendar, 38,38)
- Presentation: Conversation/church.route.1112.s1.conversation setup=church.route.1112.s1
- Pages/lines/options: 67/65/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.church.route.1112.s1.completed; SetFlag -> known.alienor

## church.route.1113.s1 [static_pass]

- Entry: MorningBeforeMap
- Sources: Date: day 39 / Morning / Once
- Conditions: G1: BOOL_EQ(story.church.route.1113.s1.completed, false); G1: BOOL_EQ(story.church.route.1112.s1.completed, true); G1: BOOL_EQ(route.neutral, false); G1: BOOL_EQ(route.rose, false); G1: BOOL_EQ(route.church, true); G1: BOOL_EQ(route.revolution, false); G1: BAR_BETWEEN(world.calendar, 39,39)
- Presentation: Conversation/church.route.1113.s1.conversation setup=church.route.1113.s1
- Pages/lines/options: 57/54/0
- Letter/reading: 1/1 pages / 13 lines
- Checks: (none)
- Results: SetFlag -> story.church.route.1113.s1.completed

## church.route.1115.s1 [static_pass]

- Entry: MorningBeforeMap
- Sources: Date: day 41 / Morning / Once
- Conditions: G1: BOOL_EQ(story.church.route.1115.s1.completed, false); G1: BOOL_EQ(story.church.route.1113.s1.completed, true); G1: BOOL_EQ(route.neutral, false); G1: BOOL_EQ(route.rose, false); G1: BOOL_EQ(route.church, true); G1: BOOL_EQ(route.revolution, false); G1: BAR_BETWEEN(world.calendar, 41,41)
- Presentation: Conversation/church.route.1115.s1.conversation setup=church.route.1115.s1
- Pages/lines/options: 70/65/4
- Letter/reading: 1/1 pages / 6 lines
- Checks: (none)
- Results: SetFlag -> story.church.route.1115.s1.completed; SetFlag -> church.thibaut.followed; SetFlag -> church.revolution.watch_requested; SetFlag -> known.sade

## church.route.1116.s1 [static_pass]

- Entry: MorningBeforeMap
- Sources: Date: day 42 / Morning / Once
- Conditions: G1: BOOL_EQ(story.church.route.1116.s1.completed, false); G1: BOOL_EQ(story.church.route.1115.s1.completed, true); G1: BOOL_EQ(route.neutral, false); G1: BOOL_EQ(route.rose, false); G1: BOOL_EQ(route.church, true); G1: BOOL_EQ(route.revolution, false); G1: BAR_BETWEEN(world.calendar, 42,42)
- Presentation: Conversation/church.route.1116.s1.conversation setup=church.route.1116.s1
- Pages/lines/options: 427/399/33
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.church.route.1116.s1.completed; SetFlag -> known.alienor

## church.route.1118.s1 [static_pass]

- Entry: MorningBeforeMap
- Sources: Date: day 44 / Morning / Once
- Conditions: G1: BOOL_EQ(story.church.route.1118.s1.completed, false); G1: BOOL_EQ(story.church.route.1116.s1.completed, true); G1: BOOL_EQ(route.neutral, false); G1: BOOL_EQ(route.rose, false); G1: BOOL_EQ(route.church, true); G1: BOOL_EQ(route.revolution, false); G1: BAR_BETWEEN(world.calendar, 44,44)
- Presentation: Conversation/church.route.1118.s1.conversation setup=church.route.1118.s1
- Pages/lines/options: 73/71/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.church.route.1118.s1.completed

## church.route.1123.s1 [blocked]

- Entry: MorningBeforeMap
- Sources: Date: day 49 / Morning / Once
- Conditions: G1: BOOL_EQ(story.church.route.1123.s1.completed, false); G1: BOOL_EQ(story.church.route.1118.s1.completed, true); G1: BOOL_EQ(construction.church.route.1123.s1.ready, true); G1: BOOL_EQ(route.neutral, false); G1: BOOL_EQ(route.rose, false); G1: BOOL_EQ(route.church, true); G1: BOOL_EQ(route.revolution, false); G1: BAR_BETWEEN(world.calendar, 49,49)
- Presentation: Conversation/church.route.1123.s1.conversation setup=church.route.1123.s1
- Pages/lines/options: 186/182/3
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.church.route.1123.s1.completed; SetFlag -> terminal.church.ending.ry1; SetFlag -> known.sade; SetFlag -> known.alienor; SetFlag -> terminal.church.ending.ry2
### Errors
- Construction blocker: source.RY1115_duplicate_choice_reference_needs_author_confirmation


## daily.cat.01 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣地大教堂 @ 圣地大教堂
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(story.church.1009.s2.completed, true)
- Presentation: Reading/daily.cat.01.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.cat.01.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.cat.02 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣地大教堂 @ 圣地大教堂
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 7); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(story.church.1009.s2.completed, true)
- Presentation: Reading/daily.cat.02.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.cat.02.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.cat.03 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣地大教堂 @ 圣地大教堂
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.cat.01.completed, true); G1: BAR_GTE(world.calendar, 18); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(story.church.1009.s2.completed, true)
- Presentation: Reading/daily.cat.03.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.cat.03.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.cat.04 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣地大教堂 @ 圣地大教堂
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.cat.01.completed, true); G1: BAR_GTE(world.calendar, 31); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(story.church.1009.s2.completed, true)
- Presentation: Reading/daily.cat.04.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.cat.04.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.cat.05 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣地大教堂 @ 圣地大教堂
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(story.church.1009.s2.completed, true)
- Presentation: Reading/daily.cat.05.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.cat.05.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.cat.06 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣地大教堂 @ 圣地大教堂
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(story.church.1009.s2.completed, true)
- Presentation: Reading/daily.cat.06.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.cat.06.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.cat.07 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣地大教堂 @ 圣地大教堂
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 7); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(story.church.1009.s2.completed, true)
- Presentation: Reading/daily.cat.07.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.cat.07.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.cat.08 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣地大教堂 @ 圣地大教堂
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.cat.07.completed, true); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(story.church.1009.s2.completed, true)
- Presentation: Conversation/daily.cat.08.presentation
- Pages/lines/options: 3/5/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.daily.cat.08.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.cat.09 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣地大教堂 @ 圣地大教堂
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.cat.01.completed, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(story.church.1009.s2.completed, true)
- Presentation: Reading/daily.cat.09.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.cat.09.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.cat.10 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣地大教堂 @ 圣地大教堂
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.cat.01.completed, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(story.church.1009.s2.completed, true)
- Presentation: Reading/daily.cat.10.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.cat.10.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.cat.11 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣地大教堂 @ 圣地大教堂
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.cat.01.completed, true); G1: BAR_GTE(world.calendar, 14); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(story.church.1009.s2.completed, true)
- Presentation: Reading/daily.cat.11.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.cat.11.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.cat.12 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣地大教堂 @ 圣地大教堂
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.cat.01.completed, true); G1: BAR_GTE(world.calendar, 14); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(story.church.1009.s2.completed, true)
- Presentation: Reading/daily.cat.12.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.cat.12.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.cat.13 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣地大教堂 @ 圣地大教堂
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.cat.03.completed, true); G1: BAR_GTE(world.calendar, 18); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(story.church.1009.s2.completed, true)
- Presentation: Reading/daily.cat.13.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.cat.13.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.cat.14 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣地大教堂 @ 圣地大教堂
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.cat.03.completed, true); G1: BAR_GTE(world.calendar, 18); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(story.church.1009.s2.completed, true)
- Presentation: Reading/daily.cat.14.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.cat.14.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.cat.15 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣地大教堂 @ 圣地大教堂
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 24); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(story.church.1009.s2.completed, true)
- Presentation: Reading/daily.cat.15.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.cat.15.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.cat.16 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣地大教堂 @ 圣地大教堂
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.cat.01.completed, true); G1: BAR_GTE(world.calendar, 24); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(story.church.1009.s2.completed, true)
- Presentation: Reading/daily.cat.16.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.cat.16.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.cat.17 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣地大教堂 @ 圣地大教堂
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.cat.01.completed, true); G1: BAR_GTE(world.calendar, 31); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(story.church.1009.s2.completed, true)
- Presentation: Conversation/daily.cat.17.presentation
- Pages/lines/options: 3/5/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.daily.cat.17.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.cat.18 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣地大教堂 @ 圣地大教堂
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.cat.04.completed, true); G1: BAR_GTE(world.calendar, 31); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(story.church.1009.s2.completed, true)
- Presentation: Reading/daily.cat.18.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.cat.18.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.cat.19 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣地大教堂 @ 圣地大教堂
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 37); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(story.church.1009.s2.completed, true)
- Presentation: Reading/daily.cat.19.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.cat.19.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.cat.20 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣地大教堂 @ 圣地大教堂
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.cat.01.completed, true); G1: BAR_GTE(world.calendar, 37); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(story.church.1009.s2.completed, true)
- Presentation: Reading/daily.cat.20.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.cat.20.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.cat.21 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣地大教堂 @ 圣地大教堂
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.cat.01.completed, true); G1: BAR_GTE(world.calendar, 42); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(story.church.1009.s2.completed, true)
- Presentation: Reading/daily.cat.21.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 5 lines
- Checks: (none)
- Results: SetFlag -> story.daily.cat.21.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.cat.22 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣地大教堂 @ 圣地大教堂
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.cat.01.completed, true); G1: BAR_GTE(world.calendar, 42); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(story.church.1009.s2.completed, true)
- Presentation: Reading/daily.cat.22.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.cat.22.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.cat.r01 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣地大教堂 @ 圣地大教堂
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.cat.01.completed, true); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(story.church.1009.s2.completed, true)
- Presentation: Reading/daily.cat.r01.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.cat.r01.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.cat.r02 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣地大教堂 @ 圣地大教堂
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.cat.03.completed, true); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(story.church.1009.s2.completed, true)
- Presentation: Reading/daily.cat.r02.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.cat.r02.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.civ.01 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.市政厅 @ 市政厅
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(known.adele, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.civ.01.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.civ.01.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.civ.02 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.市政厅 @ 市政厅
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.civ.01.completed, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(known.adele, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.civ.02.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.civ.02.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.civ.03 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.市政厅 @ 市政厅
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.civ.01.completed, true); G1: BAR_GTE(world.calendar, 18); G1: BOOL_EQ(known.adele, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.civ.03.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.civ.03.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.civ.04 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.市政厅 @ 市政厅
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.civ.01.completed, true); G1: BAR_GTE(world.calendar, 31); G1: BOOL_EQ(known.adele, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.civ.04.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.civ.04.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.civ.05 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.市政厅 @ 市政厅
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.civ.01.completed, true); G1: BOOL_EQ(known.adele, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.civ.05.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.civ.05.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.civ.06 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.市政厅 @ 市政厅
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.civ.01.completed, true); G1: BOOL_EQ(known.adele, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.civ.06.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.civ.06.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.civ.07 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.市政厅 @ 市政厅
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.civ.01.completed, true); G1: BAR_GTE(world.calendar, 7); G1: BOOL_EQ(known.adele, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.civ.07.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 3 lines
- Checks: (none)
- Results: SetFlag -> story.daily.civ.07.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.civ.08 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.市政厅 @ 市政厅
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.civ.05.completed, true); G1: BOOL_EQ(known.adele, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Conversation/daily.civ.08.presentation
- Pages/lines/options: 3/4/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.daily.civ.08.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.civ.09 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.市政厅 @ 市政厅
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.civ.01.completed, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(known.adele, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.civ.09.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.civ.09.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.civ.10 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.市政厅 @ 市政厅
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.civ.01.completed, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(known.adele, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.civ.10.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.civ.10.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.civ.11 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.市政厅 @ 市政厅
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.civ.01.completed, true); G1: BAR_GTE(world.calendar, 14); G1: BOOL_EQ(known.adele, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.civ.11.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.civ.11.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.civ.12 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.市政厅 @ 市政厅
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.civ.01.completed, true); G1: BAR_GTE(world.calendar, 14); G1: BOOL_EQ(known.adele, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.civ.12.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.civ.12.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.civ.13 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.市政厅 @ 市政厅
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.civ.01.completed, true); G1: BAR_GTE(world.calendar, 18); G1: BOOL_EQ(known.adele, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.civ.13.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.civ.13.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.civ.14 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.市政厅 @ 市政厅
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.civ.01.completed, true); G1: BAR_GTE(world.calendar, 18); G1: BOOL_EQ(known.adele, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.civ.14.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.civ.14.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.civ.15 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.市政厅 @ 市政厅
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.civ.01.completed, true); G1: BAR_GTE(world.calendar, 24); G1: BOOL_EQ(known.adele, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.civ.15.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 3 lines
- Checks: (none)
- Results: SetFlag -> story.daily.civ.15.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.civ.16 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.市政厅 @ 市政厅
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.civ.01.completed, true); G1: BAR_GTE(world.calendar, 24); G1: BOOL_EQ(known.adele, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.civ.16.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.civ.16.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.civ.17 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.市政厅 @ 市政厅
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.civ.01.completed, true); G1: BAR_GTE(world.calendar, 31); G1: BOOL_EQ(known.adele, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.civ.17.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.civ.17.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.civ.18 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.市政厅 @ 市政厅
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.civ.17.completed, true); G1: BAR_GTE(world.calendar, 31); G1: BOOL_EQ(known.adele, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Conversation/daily.civ.18.presentation
- Pages/lines/options: 3/4/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.daily.civ.18.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.civ.19 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.市政厅 @ 市政厅
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.civ.01.completed, true); G1: BAR_GTE(world.calendar, 37); G1: BOOL_EQ(known.adele, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.civ.19.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 5 lines
- Checks: (none)
- Results: SetFlag -> story.daily.civ.19.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.civ.20 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.市政厅 @ 市政厅
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 37); G1: BOOL_EQ(known.adele, true); G1: BOOL_EQ(known.lefebvre, true)
- Presentation: Reading/daily.civ.20.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.civ.20.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.civ.21 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.市政厅 @ 市政厅
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.civ.01.completed, true); G1: BAR_GTE(world.calendar, 42); G1: BOOL_EQ(known.adele, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.civ.21.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 3 lines
- Checks: (none)
- Results: SetFlag -> story.daily.civ.21.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.civ.22 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.市政厅 @ 市政厅
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.civ.01.completed, true); G1: BAR_GTE(world.calendar, 42); G1: BOOL_EQ(known.adele, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.civ.22.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.civ.22.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.civ.r01 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.市政厅 @ 市政厅
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.civ.01.completed, true); G1: BOOL_EQ(known.adele, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.civ.r01.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.civ.r01.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.civ.r02 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.市政厅 @ 市政厅
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 31); G1: BOOL_EQ(known.adele, true); G1: BOOL_EQ(known.lefebvre, true)
- Presentation: Reading/daily.civ.r02.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.civ.r02.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.crt.01 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.首都法院 @ 首都法院
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(known.eugene, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.crt.01.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.crt.01.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.crt.02 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.首都法院 @ 首都法院
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.crt.01.completed, true); G1: BAR_GTE(world.calendar, 6); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.crt.02.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.crt.02.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.crt.03 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.首都法院 @ 首都法院
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.crt.01.completed, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.crt.03.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.crt.03.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.crt.04 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.首都法院 @ 首都法院
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.crt.r02.completed, true); G1: BAR_GTE(world.calendar, 31); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.crt.04.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.crt.04.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.crt.05 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.首都法院 @ 首都法院
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(known.eugene, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.crt.05.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 3 lines
- Checks: (none)
- Results: SetFlag -> story.daily.crt.05.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.crt.06 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.首都法院 @ 首都法院
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.crt.01.completed, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.crt.06.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.crt.06.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.crt.07 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.首都法院 @ 首都法院
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.crt.01.completed, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.crt.07.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.crt.07.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.crt.08 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.首都法院 @ 首都法院
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.crt.01.completed, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.crt.08.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.crt.08.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.crt.09 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.首都法院 @ 首都法院
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.crt.01.completed, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(known.eugene, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.crt.09.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.crt.09.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.crt.10 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.首都法院 @ 首都法院
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.crt.06.completed, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Conversation/daily.crt.10.presentation
- Pages/lines/options: 3/4/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.daily.crt.10.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.crt.11 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.首都法院 @ 首都法院
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.crt.01.completed, true); G1: BAR_GTE(world.calendar, 14); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.crt.11.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 5 lines
- Checks: (none)
- Results: SetFlag -> story.daily.crt.11.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.crt.12 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.首都法院 @ 首都法院
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.crt.01.completed, true); G1: BAR_GTE(world.calendar, 14); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.crt.12.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.crt.12.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.crt.13 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.首都法院 @ 首都法院
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.crt.01.completed, true); G1: BAR_GTE(world.calendar, 18); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.crt.13.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.crt.13.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.crt.14 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.首都法院 @ 首都法院
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.crt.01.completed, true); G1: BAR_GTE(world.calendar, 18); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.crt.14.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.crt.14.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.crt.15 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.首都法院 @ 首都法院
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.crt.01.completed, true); G1: BAR_GTE(world.calendar, 24); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.crt.15.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 3 lines
- Checks: (none)
- Results: SetFlag -> story.daily.crt.15.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.crt.16 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.首都法院 @ 首都法院
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.crt.01.completed, true); G1: BAR_GTE(world.calendar, 24); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.crt.16.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.crt.16.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.crt.17 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.首都法院 @ 首都法院
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.crt.01.completed, true); G1: BAR_GTE(world.calendar, 31); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.crt.17.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.crt.17.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.crt.18 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.首都法院 @ 首都法院
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.crt.01.completed, true); G1: BAR_GTE(world.calendar, 31); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.crt.18.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.crt.18.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.crt.19 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.首都法院 @ 首都法院
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.crt.01.completed, true); G1: BAR_GTE(world.calendar, 37); G1: BOOL_EQ(known.eugene, true)
- Presentation: Conversation/daily.crt.19.presentation
- Pages/lines/options: 3/4/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.daily.crt.19.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.crt.20 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.首都法院 @ 首都法院
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.crt.01.completed, true); G1: BAR_GTE(world.calendar, 37); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.crt.20.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.crt.20.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.crt.21 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.首都法院 @ 首都法院
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.crt.01.completed, true); G1: BAR_GTE(world.calendar, 42); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.crt.21.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.crt.21.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.crt.22 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.首都法院 @ 首都法院
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.crt.01.completed, true); G1: BAR_GTE(world.calendar, 42); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.crt.22.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.crt.22.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.crt.r01 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.首都法院 @ 首都法院
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.crt.01.completed, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.crt.r01.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.crt.r01.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.crt.r02 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.首都法院 @ 首都法院
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.crt.01.completed, true); G1: BAR_GTE(world.calendar, 31); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.crt.r02.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.crt.r02.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.gar.01 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.花园区 @ 花园区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(known.sorel, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(story.revolution.1014.s1.completed, true)
- Presentation: Reading/daily.gar.01.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.gar.01.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.gar.02 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.花园区 @ 花园区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 7); G1: BOOL_EQ(known.sorel, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(story.revolution.1014.s1.completed, true)
- Presentation: Reading/daily.gar.02.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 5 lines
- Checks: (none)
- Results: SetFlag -> story.daily.gar.02.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.gar.03 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.花园区 @ 花园区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.gar.01.completed, true); G1: BAR_GTE(world.calendar, 18); G1: BOOL_EQ(known.sorel, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(story.revolution.1014.s1.completed, true)
- Presentation: Reading/daily.gar.03.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.gar.03.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.gar.04 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.花园区 @ 花园区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.gar.01.completed, true); G1: BAR_GTE(world.calendar, 31); G1: BOOL_EQ(known.sorel, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(story.revolution.1014.s1.completed, true)
- Presentation: Reading/daily.gar.04.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.gar.04.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.gar.05 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.花园区 @ 花园区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.gar.01.completed, true); G1: BOOL_EQ(known.sorel, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(story.revolution.1014.s1.completed, true)
- Presentation: Reading/daily.gar.05.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.gar.05.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.gar.06 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.花园区 @ 花园区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.gar.01.completed, true); G1: BOOL_EQ(known.sorel, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(story.revolution.1014.s1.completed, true)
- Presentation: Reading/daily.gar.06.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 3 lines
- Checks: (none)
- Results: SetFlag -> story.daily.gar.06.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.gar.07 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.花园区 @ 花园区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.gar.01.completed, true); G1: BOOL_EQ(known.sorel, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(story.revolution.1014.s1.completed, true)
- Presentation: Reading/daily.gar.07.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.gar.07.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.gar.08 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.花园区 @ 花园区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.gar.01.completed, true); G1: BOOL_EQ(known.sorel, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(story.revolution.1014.s1.completed, true)
- Presentation: Reading/daily.gar.08.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.gar.08.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.gar.09 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.花园区 @ 花园区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.gar.01.completed, true); G1: BAR_GTE(world.calendar, 14); G1: BOOL_EQ(known.sorel, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(story.revolution.1014.s1.completed, true)
- Presentation: Reading/daily.gar.09.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.gar.09.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.gar.10 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.花园区 @ 花园区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.gar.01.completed, true); G1: BAR_GTE(world.calendar, 14); G1: BOOL_EQ(known.sorel, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(story.revolution.1014.s1.completed, true)
- Presentation: Reading/daily.gar.10.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.gar.10.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.gar.11 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.花园区 @ 花园区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.gar.01.completed, true); G1: BAR_GTE(world.calendar, 18); G1: BOOL_EQ(known.sorel, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(story.revolution.1014.s1.completed, true)
- Presentation: Reading/daily.gar.11.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.gar.11.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.gar.12 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.花园区 @ 花园区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.gar.01.completed, true); G1: BAR_GTE(world.calendar, 18); G1: BOOL_EQ(known.sorel, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(story.revolution.1014.s1.completed, true)
- Presentation: Conversation/daily.gar.12.presentation
- Pages/lines/options: 3/4/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.daily.gar.12.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.gar.13 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.花园区 @ 花园区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.gar.01.completed, true); G1: BAR_GTE(world.calendar, 24); G1: BOOL_EQ(known.sorel, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(story.revolution.1014.s1.completed, true)
- Presentation: Reading/daily.gar.13.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.gar.13.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.gar.14 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.花园区 @ 花园区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.gar.01.completed, true); G1: BAR_GTE(world.calendar, 24); G1: BOOL_EQ(known.sorel, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(story.revolution.1014.s1.completed, true)
- Presentation: Reading/daily.gar.14.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 3 lines
- Checks: (none)
- Results: SetFlag -> story.daily.gar.14.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.gar.15 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.花园区 @ 花园区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.gar.01.completed, true); G1: BAR_GTE(world.calendar, 31); G1: BOOL_EQ(known.sorel, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(story.revolution.1014.s1.completed, true)
- Presentation: Reading/daily.gar.15.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.gar.15.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.gar.16 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.花园区 @ 花园区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.gar.01.completed, true); G1: BAR_GTE(world.calendar, 31); G1: BOOL_EQ(known.sorel, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(story.revolution.1014.s1.completed, true)
- Presentation: Reading/daily.gar.16.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.gar.16.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.gar.17 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.花园区 @ 花园区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.gar.01.completed, true); G1: BAR_GTE(world.calendar, 37); G1: BOOL_EQ(known.sorel, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(story.revolution.1014.s1.completed, true)
- Presentation: Reading/daily.gar.17.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.gar.17.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.gar.18 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.花园区 @ 花园区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.gar.01.completed, true); G1: BAR_GTE(world.calendar, 37); G1: BOOL_EQ(known.sorel, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(story.revolution.1014.s1.completed, true)
- Presentation: Reading/daily.gar.18.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 3 lines
- Checks: (none)
- Results: SetFlag -> story.daily.gar.18.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.gar.19 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.花园区 @ 花园区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.gar.01.completed, true); G1: BAR_GTE(world.calendar, 42); G1: BOOL_EQ(known.sorel, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(story.revolution.1014.s1.completed, true)
- Presentation: Reading/daily.gar.19.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.gar.19.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.gar.20 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.花园区 @ 花园区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.gar.01.completed, true); G1: BAR_GTE(world.calendar, 42); G1: BOOL_EQ(known.sorel, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(story.revolution.1014.s1.completed, true)
- Presentation: Conversation/daily.gar.20.presentation
- Pages/lines/options: 3/4/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.daily.gar.20.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.gar.21 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.花园区 @ 花园区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.gar.01.completed, true); G1: BAR_GTE(world.calendar, 42); G1: BOOL_EQ(known.sorel, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(story.revolution.1014.s1.completed, true)
- Presentation: Reading/daily.gar.21.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.gar.21.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.gar.22 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.花园区 @ 花园区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.gar.01.completed, true); G1: BAR_GTE(world.calendar, 42); G1: BOOL_EQ(known.sorel, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(story.revolution.1014.s1.completed, true)
- Presentation: Reading/daily.gar.22.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.gar.22.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.gar.r01 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.花园区 @ 花园区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.gar.01.completed, true); G1: BOOL_EQ(known.sorel, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(story.revolution.1014.s1.completed, true)
- Presentation: Reading/daily.gar.r01.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.gar.r01.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.gar.r02 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.花园区 @ 花园区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.gar.03.completed, true); G1: BOOL_EQ(known.sorel, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(story.revolution.1014.s1.completed, true)
- Presentation: Reading/daily.gar.r02.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.gar.r02.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.mkt.01 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.market @ market
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 3); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(story.neutral.1008.s1.completed, true)
- Presentation: Reading/daily.mkt.01.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.mkt.01.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.mkt.02 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.market @ market
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.mkt.01.completed, true); G1: BAR_GTE(world.calendar, 7); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(story.neutral.1008.s1.completed, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.mkt.02.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.mkt.02.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.mkt.03 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.market @ market
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.mkt.01.completed, true); G1: BAR_GTE(world.calendar, 18); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(story.neutral.1008.s1.completed, true)
- Presentation: Reading/daily.mkt.03.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.mkt.03.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.mkt.04 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.market @ market
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.mkt.01.completed, true); G1: BAR_GTE(world.calendar, 31); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(story.neutral.1008.s1.completed, true)
- Presentation: Reading/daily.mkt.04.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.mkt.04.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.mkt.05 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.market @ market
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(story.neutral.1008.s1.completed, true)
- Presentation: Reading/daily.mkt.05.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.mkt.05.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.mkt.06 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.market @ market
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.mkt.01.completed, true); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(story.neutral.1008.s1.completed, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.mkt.06.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.mkt.06.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.mkt.07 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.market @ market
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.mkt.01.completed, true); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(story.neutral.1008.s1.completed, true)
- Presentation: Conversation/daily.mkt.07.presentation
- Pages/lines/options: 3/4/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.daily.mkt.07.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.mkt.08 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.market @ market
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 7); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(story.neutral.1008.s1.completed, true)
- Presentation: Reading/daily.mkt.08.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.mkt.08.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.mkt.09 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.market @ market
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.mkt.01.completed, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(story.neutral.1008.s1.completed, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.mkt.09.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.mkt.09.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.mkt.10 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.market @ market
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.mkt.01.completed, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(story.neutral.1008.s1.completed, true)
- Presentation: Reading/daily.mkt.10.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.mkt.10.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.mkt.11 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.market @ market
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.mkt.01.completed, true); G1: BAR_GTE(world.calendar, 14); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(story.neutral.1008.s1.completed, true)
- Presentation: Reading/daily.mkt.11.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.mkt.11.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.mkt.12 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.market @ market
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 14); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(story.neutral.1008.s1.completed, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.mkt.12.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.mkt.12.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.mkt.13 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.market @ market
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.mkt.01.completed, true); G1: BAR_GTE(world.calendar, 18); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(story.neutral.1008.s1.completed, true)
- Presentation: Reading/daily.mkt.13.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.mkt.13.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.mkt.14 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.market @ market
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.mkt.01.completed, true); G1: BAR_GTE(world.calendar, 18); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(story.neutral.1008.s1.completed, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.mkt.14.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.mkt.14.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.mkt.15 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.market @ market
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 18); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(story.neutral.1008.s1.completed, true)
- Presentation: Conversation/daily.mkt.15.presentation
- Pages/lines/options: 3/4/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.daily.mkt.15.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.mkt.16 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.market @ market
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.mkt.01.completed, true); G1: BAR_GTE(world.calendar, 24); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(story.neutral.1008.s1.completed, true)
- Presentation: Reading/daily.mkt.16.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.mkt.16.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.mkt.17 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.market @ market
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.mkt.01.completed, true); G1: BAR_GTE(world.calendar, 31); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(story.neutral.1008.s1.completed, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.mkt.17.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.mkt.17.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.mkt.18 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.market @ market
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.mkt.01.completed, true); G1: BAR_GTE(world.calendar, 31); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(story.neutral.1008.s1.completed, true)
- Presentation: Reading/daily.mkt.18.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.mkt.18.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.mkt.19 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.market @ market
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.mkt.01.completed, true); G1: BAR_GTE(world.calendar, 37); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(story.neutral.1008.s1.completed, true)
- Presentation: Reading/daily.mkt.19.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.mkt.19.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.mkt.20 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.market @ market
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 37); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(story.neutral.1008.s1.completed, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.mkt.20.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.mkt.20.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.mkt.21 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.market @ market
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.mkt.01.completed, true); G1: BAR_GTE(world.calendar, 42); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(story.neutral.1008.s1.completed, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Conversation/daily.mkt.21.presentation
- Pages/lines/options: 3/4/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.daily.mkt.21.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.mkt.22 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.market @ market
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.mkt.01.completed, true); G1: BAR_GTE(world.calendar, 42); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(story.neutral.1008.s1.completed, true)
- Presentation: Reading/daily.mkt.22.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.mkt.22.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.mkt.r01 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.market @ market
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.mkt.01.completed, true); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(story.neutral.1008.s1.completed, true)
- Presentation: Reading/daily.mkt.r01.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.mkt.r01.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.mkt.r02 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.market @ market
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.mkt.03.completed, true); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(story.neutral.1008.s1.completed, true)
- Presentation: Reading/daily.mkt.r02.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.mkt.r02.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.news.01 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣徒街 @ 圣徒街
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_BETWEEN(world.calendar, 5,5); G1: BOOL_EQ(news.public.5.7e9cc445, true); G1: BOOL_EQ(known.pascal, true); G1: BOOL_EQ(known.pascal, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.news.01.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.news.01.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.news.02 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.library @ library
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_BETWEEN(world.calendar, 6,6); G1: BOOL_EQ(news.public.6.837fdfd7, true); G1: BOOL_EQ(known.sylvain, true); G1: BOOL_EQ(story.rose.1011.s1.completed, true); G1: BOOL_EQ(availability.sylvain, true)
- Presentation: Reading/daily.news.02.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.news.02.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.news.03 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.market @ market
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_BETWEEN(world.calendar, 7,7); G1: BOOL_EQ(news.public.7.29af38b6, true); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(story.neutral.1008.s1.completed, true)
- Presentation: Reading/daily.news.03.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.news.03.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.news.04 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣徒街 @ 圣徒街
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_BETWEEN(world.calendar, 10,10); G1: BOOL_EQ(news.public.10.12ed1d36, true); G1: BOOL_EQ(known.pascal, true); G1: BOOL_EQ(known.pascal, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.news.04.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.news.04.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.news.05 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.market @ market
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_BETWEEN(world.calendar, 18,18); G1: BOOL_EQ(news.public.18.d7760931, true); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(story.neutral.1008.s1.completed, true)
- Presentation: Reading/daily.news.05.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.news.05.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.news.06 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.市政厅 @ 市政厅
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_BETWEEN(world.calendar, 31,31); G1: BOOL_EQ(news.public.31.58d0aef1, true); G1: BOOL_EQ(known.adele, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.news.06.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.news.06.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.news.07 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣地大教堂 @ 圣地大教堂
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_BETWEEN(world.calendar, 4,4); G1: BOOL_EQ(story.church.1009.s2.completed, true); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(story.church.1009.s2.completed, true)
- Presentation: Reading/daily.news.07.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.news.07.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.news.08 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.library @ library
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_BETWEEN(world.calendar, 4,4); G1: BOOL_EQ(story.church.1009.s2.completed, true); G1: BOOL_EQ(known.sylvain, true); G1: BOOL_EQ(story.rose.1011.s1.completed, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.news.08.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.news.08.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.news.09 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣西尔万郊区 @ 圣西尔万郊区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_BETWEEN(world.calendar, 5,5); G1: BOOL_EQ(news.public.5.7e9cc445, true); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.eugene, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.news.09.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.news.09.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.news.10 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.首都法院 @ 首都法院
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_BETWEEN(world.calendar, 6,6); G1: BOOL_EQ(news.public.6.837fdfd7, true); G1: BOOL_EQ(known.eugene, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.news.10.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.news.10.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.news.11 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.旧城区 @ 旧城区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_BETWEEN(world.calendar, 7,7); G1: BOOL_EQ(news.public.7.ce650510, true); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.sparrow, true); G1: BAR_GTE(world.calendar, 6)
- Presentation: Reading/daily.news.11.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.news.11.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.news.12 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.花园区 @ 花园区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_BETWEEN(world.calendar, 7,7); G1: BOOL_EQ(news.public.7.c1b54ba7, true); G1: BOOL_EQ(known.sorel, true); G1: BOOL_EQ(known.colette, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(story.revolution.1014.s1.completed, true)
- Presentation: Reading/daily.news.12.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.news.12.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.news.13 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.市政厅 @ 市政厅
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_BETWEEN(world.calendar, 10,10); G1: BOOL_EQ(news.public.10.8140f96a, true); G1: BOOL_EQ(known.adele, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.news.13.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.news.13.completed; TrainAttribute -> player.perception; TrainAttribute -> player.perception

## daily.news.14 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣西尔万郊区 @ 圣西尔万郊区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_BETWEEN(world.calendar, 18,18); G1: BOOL_EQ(news.public.18.d7760931, true); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.eugene, true); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.news.14.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.news.14.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.news.15 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.旧城区 @ 旧城区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_BETWEEN(world.calendar, 31,31); G1: BOOL_EQ(news.public.31.58d0aef1, true); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.sparrow, true); G1: BAR_GTE(world.calendar, 6)
- Presentation: Reading/daily.news.15.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.news.15.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.old.01 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.旧城区 @ 旧城区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 6); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.sparrow, true); G1: BAR_GTE(world.calendar, 6)
- Presentation: Reading/daily.old.01.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.old.01.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.old.02 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.旧城区 @ 旧城区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.old.01.completed, true); G1: BAR_GTE(world.calendar, 7); G1: BOOL_EQ(known.sparrow, true); G1: BAR_GTE(world.calendar, 6)
- Presentation: Reading/daily.old.02.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.old.02.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.old.03 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.旧城区 @ 旧城区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.old.01.completed, true); G1: BAR_GTE(world.calendar, 18); G1: BOOL_EQ(known.sparrow, true); G1: BAR_GTE(world.calendar, 6)
- Presentation: Reading/daily.old.03.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.old.03.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.old.04 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.旧城区 @ 旧城区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.old.03.completed, true); G1: BAR_GTE(world.calendar, 31); G1: BOOL_EQ(known.sparrow, true); G1: BAR_GTE(world.calendar, 6)
- Presentation: Reading/daily.old.04.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 5 lines
- Checks: (none)
- Results: SetFlag -> story.daily.old.04.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.old.05 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.旧城区 @ 旧城区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.old.01.completed, true); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.sparrow, true); G1: BAR_GTE(world.calendar, 6)
- Presentation: Reading/daily.old.05.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.old.05.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.old.06 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.旧城区 @ 旧城区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.colette, true); G1: BAR_GTE(world.calendar, 6)
- Presentation: Reading/daily.old.06.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.old.06.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.old.07 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.旧城区 @ 旧城区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.old.01.completed, true); G1: BAR_GTE(world.calendar, 7); G1: BOOL_EQ(known.sparrow, true); G1: BAR_GTE(world.calendar, 6)
- Presentation: Reading/daily.old.07.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.old.07.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.old.08 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.旧城区 @ 旧城区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.old.05.completed, true); G1: BOOL_EQ(known.sparrow, true); G1: BAR_GTE(world.calendar, 6)
- Presentation: Conversation/daily.old.08.presentation
- Pages/lines/options: 3/5/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.daily.old.08.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.old.09 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.旧城区 @ 旧城区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.old.08.completed, true); G1: BOOL_EQ(known.sparrow, true); G1: BAR_GTE(world.calendar, 6)
- Presentation: Reading/daily.old.09.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.old.09.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.old.10 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.旧城区 @ 旧城区
- Conditions: G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.sparrow, true); G1: BAR_GTE(world.calendar, 6); G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 10)
- Presentation: Reading/daily.old.10.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.old.10.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.old.11 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.旧城区 @ 旧城区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 14); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.colette, true); G1: BAR_GTE(world.calendar, 6)
- Presentation: Reading/daily.old.11.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.old.11.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.old.12 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.旧城区 @ 旧城区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 14); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.sparrow, true); G1: BAR_GTE(world.calendar, 6)
- Presentation: Reading/daily.old.12.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.old.12.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.old.13 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.旧城区 @ 旧城区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.old.03.completed, true); G1: BAR_GTE(world.calendar, 18); G1: BOOL_EQ(known.sparrow, true); G1: BAR_GTE(world.calendar, 6)
- Presentation: Reading/daily.old.13.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.old.13.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.old.14 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.旧城区 @ 旧城区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 18); G1: BOOL_EQ(known.sparrow, true); G1: BAR_GTE(world.calendar, 6)
- Presentation: Conversation/daily.old.14.presentation
- Pages/lines/options: 3/5/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.daily.old.14.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.old.15 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.旧城区 @ 旧城区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.old.14.completed, true); G1: BAR_GTE(world.calendar, 19); G1: BOOL_EQ(known.sparrow, true); G1: BAR_GTE(world.calendar, 6)
- Presentation: Reading/daily.old.15.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.old.15.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.old.16 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.旧城区 @ 旧城区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 24); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.colette, true); G1: BAR_GTE(world.calendar, 6)
- Presentation: Reading/daily.old.16.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.old.16.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.old.17 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.旧城区 @ 旧城区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 31); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.sparrow, true); G1: BAR_GTE(world.calendar, 6)
- Presentation: Reading/daily.old.17.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 3 lines
- Checks: (none)
- Results: SetFlag -> story.daily.old.17.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.old.18 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.旧城区 @ 旧城区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.old.01.completed, true); G1: BAR_GTE(world.calendar, 31); G1: BOOL_EQ(known.sparrow, true); G1: BAR_GTE(world.calendar, 6)
- Presentation: Reading/daily.old.18.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.old.18.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.old.19 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.旧城区 @ 旧城区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 37); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.colette, true); G1: BAR_GTE(world.calendar, 6)
- Presentation: Reading/daily.old.19.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.old.19.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.old.20 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.旧城区 @ 旧城区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 37); G1: BOOL_EQ(known.sparrow, true); G1: BAR_GTE(world.calendar, 6)
- Presentation: Conversation/daily.old.20.presentation
- Pages/lines/options: 3/5/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.daily.old.20.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.old.21 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.旧城区 @ 旧城区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.old.01.completed, true); G1: BAR_GTE(world.calendar, 42); G1: BOOL_EQ(known.sparrow, true); G1: BAR_GTE(world.calendar, 6)
- Presentation: Reading/daily.old.21.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 3 lines
- Checks: (none)
- Results: SetFlag -> story.daily.old.21.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.old.22 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.旧城区 @ 旧城区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.old.01.completed, true); G1: BAR_GTE(world.calendar, 42); G1: BOOL_EQ(known.sparrow, true); G1: BAR_GTE(world.calendar, 6)
- Presentation: Reading/daily.old.22.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.old.22.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.old.r01 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.旧城区 @ 旧城区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.old.01.completed, true); G1: BOOL_EQ(known.sparrow, true); G1: BAR_GTE(world.calendar, 6)
- Presentation: Reading/daily.old.r01.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.old.r01.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.old.r02 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.旧城区 @ 旧城区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.old.01.completed, true); G1: BAR_GTE(world.calendar, 31); G1: BOOL_EQ(known.sparrow, true); G1: BAR_GTE(world.calendar, 6)
- Presentation: Reading/daily.old.r02.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.old.r02.completed; TrainAttribute -> player.courage; TrainAttribute -> player.courage

## daily.out.01 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣西尔万郊区 @ 圣西尔万郊区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.eugene, true); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.out.01.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.out.01.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.out.02 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣西尔万郊区 @ 圣西尔万郊区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 6); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.out.02.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.out.02.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.out.03 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣西尔万郊区 @ 圣西尔万郊区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.out.01.completed, true); G1: BAR_GTE(world.calendar, 18); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.out.03.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.out.03.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.out.04 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣西尔万郊区 @ 圣西尔万郊区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.out.01.completed, true); G1: BAR_GTE(world.calendar, 31); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.out.04.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.out.04.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.out.05 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣西尔万郊区 @ 圣西尔万郊区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.out.01.completed, true); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.out.05.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.out.05.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.out.06 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣西尔万郊区 @ 圣西尔万郊区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.out.01.completed, true); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.out.06.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.out.06.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.out.07 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣西尔万郊区 @ 圣西尔万郊区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.out.01.completed, true); G1: BAR_GTE(world.calendar, 7); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.out.07.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.out.07.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.out.08 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣西尔万郊区 @ 圣西尔万郊区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.out.01.completed, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.out.08.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.out.08.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.out.09 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣西尔万郊区 @ 圣西尔万郊区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.out.01.completed, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.out.09.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.out.09.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.out.10 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣西尔万郊区 @ 圣西尔万郊区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.out.09.completed, true); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Conversation/daily.out.10.presentation
- Pages/lines/options: 3/5/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.daily.out.10.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.out.11 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣西尔万郊区 @ 圣西尔万郊区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.out.01.completed, true); G1: BAR_GTE(world.calendar, 14); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.out.11.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.out.11.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.out.12 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣西尔万郊区 @ 圣西尔万郊区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.out.01.completed, true); G1: BAR_GTE(world.calendar, 14); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.out.12.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.out.12.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.out.13 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣西尔万郊区 @ 圣西尔万郊区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.out.01.completed, true); G1: BAR_GTE(world.calendar, 18); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.out.13.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 5 lines
- Checks: (none)
- Results: SetFlag -> story.daily.out.13.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.out.14 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣西尔万郊区 @ 圣西尔万郊区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.out.13.completed, true); G1: BAR_GTE(world.calendar, 18); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.out.14.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.out.14.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.out.15 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣西尔万郊区 @ 圣西尔万郊区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.out.01.completed, true); G1: BAR_GTE(world.calendar, 24); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.out.15.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 3 lines
- Checks: (none)
- Results: SetFlag -> story.daily.out.15.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.out.16 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣西尔万郊区 @ 圣西尔万郊区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.out.01.completed, true); G1: BAR_GTE(world.calendar, 24); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.out.16.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 5 lines
- Checks: (none)
- Results: SetFlag -> story.daily.out.16.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.out.17 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣西尔万郊区 @ 圣西尔万郊区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.out.01.completed, true); G1: BAR_GTE(world.calendar, 31); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.out.17.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.out.17.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.out.18 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣西尔万郊区 @ 圣西尔万郊区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.out.17.completed, true); G1: BAR_GTE(world.calendar, 31); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.out.18.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.out.18.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.out.19 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣西尔万郊区 @ 圣西尔万郊区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.out.17.completed, true); G1: BAR_GTE(world.calendar, 37); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Conversation/daily.out.19.presentation
- Pages/lines/options: 3/5/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.daily.out.19.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.out.20 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣西尔万郊区 @ 圣西尔万郊区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.out.01.completed, true); G1: BAR_GTE(world.calendar, 37); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.out.20.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.out.20.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.out.21 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣西尔万郊区 @ 圣西尔万郊区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.out.01.completed, true); G1: BAR_GTE(world.calendar, 42); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.out.21.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 3 lines
- Checks: (none)
- Results: SetFlag -> story.daily.out.21.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.out.22 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣西尔万郊区 @ 圣西尔万郊区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.out.01.completed, true); G1: BAR_GTE(world.calendar, 42); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.out.22.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.out.22.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.out.r01 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣西尔万郊区 @ 圣西尔万郊区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.out.01.completed, true); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.out.r01.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.out.r01.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.out.r02 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣西尔万郊区 @ 圣西尔万郊区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.out.03.completed, true); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.out.r02.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.out.r02.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.st.01 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣徒街 @ 圣徒街
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(known.pascal, true); G1: BOOL_EQ(known.pascal, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.st.01.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.st.01.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.st.02 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣徒街 @ 圣徒街
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.st.01.completed, true); G1: BAR_GTE(world.calendar, 5); G1: BOOL_EQ(known.pascal, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.st.02.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.st.02.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.st.03 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣徒街 @ 圣徒街
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.st.01.completed, true); G1: BAR_GTE(world.calendar, 14); G1: BOOL_EQ(known.pascal, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.st.03.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.st.03.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.st.04 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣徒街 @ 圣徒街
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.st.01.completed, true); G1: BAR_GTE(world.calendar, 15); G1: BOOL_EQ(known.pascal, true)
- Presentation: Reading/daily.st.04.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.st.04.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.st.05 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣徒街 @ 圣徒街
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(known.pascal, true); G1: BOOL_EQ(known.pascal, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.st.05.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.st.05.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.st.06 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣徒街 @ 圣徒街
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.st.05.completed, true); G1: BOOL_EQ(known.pascal, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.st.06.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.st.06.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.st.07 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣徒街 @ 圣徒街
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 7); G1: BOOL_EQ(known.pascal, true); G1: BOOL_EQ(known.pascal, true)
- Presentation: Reading/daily.st.07.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 3 lines
- Checks: (none)
- Results: SetFlag -> story.daily.st.07.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.st.08 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣徒街 @ 圣徒街
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.st.01.completed, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(known.pascal, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.st.08.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.st.08.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.st.09 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣徒街 @ 圣徒街
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(known.pascal, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Conversation/daily.st.09.presentation
- Pages/lines/options: 3/5/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.daily.st.09.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.st.10 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣徒街 @ 圣徒街
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 14); G1: BOOL_EQ(known.pascal, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.st.10.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.st.10.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.st.11 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣徒街 @ 圣徒街
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 14); G1: BOOL_EQ(known.pascal, true); G1: BOOL_EQ(known.pascal, true)
- Presentation: Reading/daily.st.11.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.st.11.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.st.12 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣徒街 @ 圣徒街
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.st.01.completed, true); G1: BAR_GTE(world.calendar, 15); G1: BOOL_EQ(known.pascal, true)
- Presentation: Reading/daily.st.12.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.st.12.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.st.13 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣徒街 @ 圣徒街
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 18); G1: BOOL_EQ(known.pascal, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.st.13.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.st.13.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.st.14 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣徒街 @ 圣徒街
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.st.11.completed, true); G1: BAR_GTE(world.calendar, 19); G1: BOOL_EQ(known.pascal, true)
- Presentation: Reading/daily.st.14.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.st.14.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.st.15 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣徒街 @ 圣徒街
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 24); G1: BOOL_EQ(known.pascal, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.st.15.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.st.15.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.st.16 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣徒街 @ 圣徒街
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 24); G1: BOOL_EQ(known.pascal, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Conversation/daily.st.16.presentation
- Pages/lines/options: 3/5/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.daily.st.16.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.st.17 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣徒街 @ 圣徒街
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 31); G1: BOOL_EQ(known.pascal, true); G1: BOOL_EQ(known.pascal, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.st.17.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.st.17.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.st.18 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣徒街 @ 圣徒街
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.st.17.completed, true); G1: BAR_GTE(world.calendar, 31); G1: BOOL_EQ(known.pascal, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.st.18.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.st.18.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.st.19 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣徒街 @ 圣徒街
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 37); G1: BOOL_EQ(known.pascal, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.st.19.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.st.19.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.st.20 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣徒街 @ 圣徒街
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 37); G1: BOOL_EQ(known.pascal, true); G1: BOOL_EQ(known.pascal, true)
- Presentation: Reading/daily.st.20.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.st.20.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.st.21 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣徒街 @ 圣徒街
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.st.01.completed, true); G1: BAR_GTE(world.calendar, 42); G1: BOOL_EQ(known.pascal, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.st.21.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.st.21.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.st.22 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣徒街 @ 圣徒街
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.st.01.completed, true); G1: BAR_GTE(world.calendar, 42); G1: BOOL_EQ(known.pascal, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.st.22.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.st.22.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.st.r01 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣徒街 @ 圣徒街
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.st.01.completed, true); G1: BOOL_EQ(known.pascal, true)
- Presentation: Reading/daily.st.r01.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.st.r01.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.st.r02 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣徒街 @ 圣徒街
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.st.03.completed, true); G1: BOOL_EQ(known.pascal, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.st.r02.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.st.r02.completed; TrainAttribute -> player.language; TrainAttribute -> player.language

## daily.uni.01 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.library @ library
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 6); G1: BOOL_EQ(known.sylvain, true); G1: BOOL_EQ(story.rose.1011.s1.completed, true); G1: BOOL_EQ(availability.sylvain, true)
- Presentation: Reading/daily.uni.01.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.uni.01.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.uni.02 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.library @ library
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 6); G1: BOOL_EQ(known.sylvain, true); G1: BOOL_EQ(story.rose.1011.s1.completed, true); G1: BOOL_EQ(availability.sylvain, true)
- Presentation: Reading/daily.uni.02.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.uni.02.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.uni.03 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.library @ library
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.uni.01.completed, true); G1: BAR_GTE(world.calendar, 14); G1: BOOL_EQ(known.sylvain, true); G1: BOOL_EQ(story.rose.1011.s1.completed, true); G1: BOOL_EQ(availability.sylvain, true)
- Presentation: Reading/daily.uni.03.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.uni.03.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.uni.04 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.library @ library
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.uni.03.completed, true); G1: BAR_GTE(world.calendar, 31); G1: BOOL_EQ(known.sylvain, true); G1: BOOL_EQ(story.rose.1011.s1.completed, true); G1: BOOL_EQ(availability.sylvain, true)
- Presentation: Reading/daily.uni.04.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.uni.04.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.uni.05 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.library @ library
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(known.sylvain, true); G1: BOOL_EQ(known.sylvain, true); G1: BOOL_EQ(story.rose.1011.s1.completed, true); G1: BOOL_EQ(availability.sylvain, true)
- Presentation: Reading/daily.uni.05.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.uni.05.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.uni.06 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.library @ library
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.uni.01.completed, true); G1: BOOL_EQ(known.sylvain, true); G1: BOOL_EQ(story.rose.1011.s1.completed, true); G1: BOOL_EQ(availability.sylvain, true)
- Presentation: Reading/daily.uni.06.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.uni.06.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.uni.07 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.library @ library
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 11); G1: BOOL_EQ(known.sylvain, true); G1: BOOL_EQ(story.rose.1011.s1.completed, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.uni.07.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.uni.07.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.uni.08 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.library @ library
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.uni.07.completed, true); G1: BAR_GTE(world.calendar, 11); G1: BOOL_EQ(known.sylvain, true); G1: BOOL_EQ(story.rose.1011.s1.completed, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.uni.08.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.uni.08.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.uni.09 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.library @ library
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(known.sylvain, true); G1: BOOL_EQ(known.sylvain, true); G1: BOOL_EQ(story.rose.1011.s1.completed, true); G1: BOOL_EQ(availability.sylvain, true)
- Presentation: Conversation/daily.uni.09.presentation
- Pages/lines/options: 3/4/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.daily.uni.09.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.uni.10 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.library @ library
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 14); G1: BOOL_EQ(known.sylvain, true); G1: BOOL_EQ(story.rose.1011.s1.completed, true); G1: BOOL_EQ(availability.sylvain, true)
- Presentation: Reading/daily.uni.10.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.uni.10.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.uni.11 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.library @ library
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.uni.01.completed, true); G1: BAR_GTE(world.calendar, 14); G1: BOOL_EQ(known.sylvain, true); G1: BOOL_EQ(story.rose.1011.s1.completed, true); G1: BOOL_EQ(availability.sylvain, true)
- Presentation: Reading/daily.uni.11.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.uni.11.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.uni.12 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.library @ library
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 18); G1: BOOL_EQ(known.sylvain, true); G1: BOOL_EQ(story.rose.1011.s1.completed, true); G1: BOOL_EQ(availability.sylvain, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.uni.12.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.uni.12.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.uni.13 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.library @ library
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 18); G1: BOOL_EQ(known.sylvain, true); G1: BOOL_EQ(story.rose.1011.s1.completed, true); G1: BOOL_EQ(availability.sylvain, true)
- Presentation: Reading/daily.uni.13.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.uni.13.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.uni.14 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.library @ library
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 24); G1: BOOL_EQ(known.sylvain, true); G1: BOOL_EQ(story.rose.1011.s1.completed, true); G1: BOOL_EQ(availability.sylvain, true)
- Presentation: Reading/daily.uni.14.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.uni.14.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.uni.15 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.library @ library
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.uni.01.completed, true); G1: BAR_GTE(world.calendar, 24); G1: BOOL_EQ(known.sylvain, true); G1: BOOL_EQ(story.rose.1011.s1.completed, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.uni.15.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 3 lines
- Checks: (none)
- Results: SetFlag -> story.daily.uni.15.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.uni.16 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.library @ library
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 31); G1: BOOL_EQ(known.sylvain, true); G1: BOOL_EQ(story.rose.1011.s1.completed, true); G1: BOOL_EQ(availability.sylvain, true)
- Presentation: Reading/daily.uni.16.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.uni.16.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.uni.17 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.library @ library
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.uni.01.completed, true); G1: BAR_GTE(world.calendar, 31); G1: BOOL_EQ(known.sylvain, true); G1: BOOL_EQ(story.rose.1011.s1.completed, true); G1: BOOL_EQ(availability.sylvain, true)
- Presentation: Reading/daily.uni.17.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.uni.17.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.uni.18 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.library @ library
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 37); G1: BOOL_EQ(known.sylvain, true); G1: BOOL_EQ(story.rose.1011.s1.completed, true); G1: BOOL_EQ(availability.sylvain, true)
- Presentation: Conversation/daily.uni.18.presentation
- Pages/lines/options: 3/4/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.daily.uni.18.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.uni.19 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.library @ library
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.uni.01.completed, true); G1: BAR_GTE(world.calendar, 37); G1: BOOL_EQ(known.sylvain, true); G1: BOOL_EQ(story.rose.1011.s1.completed, true); G1: BOOL_EQ(availability.sylvain, true)
- Presentation: Reading/daily.uni.19.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.uni.19.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.uni.20 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.library @ library
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 42); G1: BOOL_EQ(known.sylvain, true); G1: BOOL_EQ(story.rose.1011.s1.completed, true); G1: BOOL_EQ(availability.sylvain, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.uni.20.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 3 lines
- Checks: (none)
- Results: SetFlag -> story.daily.uni.20.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.uni.21 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.library @ library
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 42); G1: BOOL_EQ(known.sylvain, true); G1: BOOL_EQ(story.rose.1011.s1.completed, true); G1: BOOL_EQ(availability.sylvain, true)
- Presentation: Reading/daily.uni.21.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.uni.21.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.uni.22 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.library @ library
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.uni.21.completed, true); G1: BAR_GTE(world.calendar, 42); G1: BOOL_EQ(known.sylvain, true); G1: BOOL_EQ(story.rose.1011.s1.completed, true); G1: BOOL_EQ(availability.sylvain, true)
- Presentation: Reading/daily.uni.22.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.uni.22.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.uni.r01 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.library @ library
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.uni.01.completed, true); G1: BOOL_EQ(known.sylvain, true); G1: BOOL_EQ(story.rose.1011.s1.completed, true); G1: BOOL_EQ(availability.sylvain, true)
- Presentation: Reading/daily.uni.r01.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.uni.r01.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.uni.r02 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.library @ library
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(story.daily.uni.03.completed, true); G1: BAR_GTE(world.calendar, 31); G1: BOOL_EQ(known.sylvain, true); G1: BOOL_EQ(story.rose.1011.s1.completed, true); G1: BOOL_EQ(availability.sylvain, true)
- Presentation: Reading/daily.uni.r02.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.uni.r02.completed; TrainAttribute -> player.wisdom; TrainAttribute -> player.wisdom

## daily.visit.01 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.旧城区 @ 旧城区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.sparrow, true); G1: BAR_GTE(world.calendar, 6)
- Presentation: Reading/daily.visit.01.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 3 lines
- Checks: (none)
- Results: SetFlag -> story.daily.visit.01.completed

## daily.visit.02 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣徒街 @ 圣徒街
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(known.pascal, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.visit.02.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.visit.02.completed

## daily.visit.03 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣西尔万郊区 @ 圣西尔万郊区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.eugene, true); G1: BOOL_EQ(known.sparrow, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.visit.03.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 3 lines
- Checks: (none)
- Results: SetFlag -> story.daily.visit.03.completed

## daily.visit.04 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.market @ market
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 3); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(known.colette, true); G1: BOOL_EQ(story.neutral.1008.s1.completed, true)
- Presentation: Reading/daily.visit.04.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 3 lines
- Checks: (none)
- Results: SetFlag -> story.daily.visit.04.completed

## daily.visit.05 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.圣地大教堂 @ 圣地大教堂
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 4); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(known.sade, true); G1: BOOL_EQ(story.church.1009.s2.completed, true)
- Presentation: Reading/daily.visit.05.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.visit.05.completed

## daily.visit.06 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.library @ library
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(known.sylvain, true); G1: BOOL_EQ(known.sylvain, true); G1: BOOL_EQ(story.rose.1011.s1.completed, true); G1: BOOL_EQ(availability.sylvain, true)
- Presentation: Reading/daily.visit.06.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.visit.06.completed

## daily.visit.07 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.市政厅 @ 市政厅
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(known.adele, true); G1: BOOL_EQ(availability.adele, true)
- Presentation: Reading/daily.visit.07.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 3 lines
- Checks: (none)
- Results: SetFlag -> story.daily.visit.07.completed

## daily.visit.08 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.首都法院 @ 首都法院
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BOOL_EQ(known.eugene, true); G1: BOOL_EQ(known.eugene, true)
- Presentation: Reading/daily.visit.08.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.daily.visit.08.completed

## daily.visit.09 [static_pass]

- Entry: MapAction
- Sources: Action: daily.action.花园区 @ 花园区
- Conditions: G1: BOOL_EQ(world.map.active, true); G1: BOOL_EQ(church.detention.active, false); G1: BAR_BETWEEN(world.calendar, 2,48); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(known.sorel, true); G1: BAR_GTE(world.calendar, 10); G1: BOOL_EQ(story.revolution.1014.s1.completed, true)
- Presentation: Reading/daily.visit.09.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 5 lines
- Checks: (none)
- Results: SetFlag -> story.daily.visit.09.completed

## neutral.1008.s1 [static_pass]

- Entry: MapAction
- Sources: Action: neutral.1008.s1.action @ market
- Conditions: G1: BOOL_EQ(story.neutral.1008.s1.completed, false); G1: BAR_BETWEEN(world.calendar, 3,3); G1: BOOL_EQ(world.map.active, true)
- Presentation: Conversation/neutral.1008.s1
- Pages/lines/options: 10/60/3
- Letter/reading: 0/0 pages / 0 lines
- Checks: neutral.1008.s1.check.q1b
- Results: SetFlag -> known.colette; SetFlag -> story.neutral.1008.s1.completed

## neutral.1010.s1 [static_pass]

- Entry: Immediate
- Sources: Date: day 5 / Morning / Once
- Conditions: G1: BOOL_EQ(story.neutral.1010.s1.completed, false); G1: BOOL_EQ(story.rose.1009.s1.completed, true)
- Presentation: Conversation/neutral.1010.s1
- Pages/lines/options: 9/52/3
- Letter/reading: 0/0 pages / 0 lines
- Checks: neutral.1010.s1.check.q1b, neutral.1010.s1.check.q1c
- Results: QueueEvent -> neutral.1010.s2; SetFlag -> story.neutral.1010.s1.completed

## neutral.1010.s2 [static_pass]

- Entry: Immediate
- Sources: QueueEvent: neutral.1010.s1 -> neutral.1010.s2
- Conditions: G1: BOOL_EQ(story.neutral.1010.s2.completed, false); G1: BOOL_EQ(story.neutral.1010.s1.completed, true)
- Presentation: Conversation/neutral.1010.s2
- Pages/lines/options: 16/67/6
- Letter/reading: 0/0 pages / 0 lines
- Checks: neutral.1010.s2.check.q1b, neutral.1010.s2.check.q2a
- Results: SetFlag -> known.antoine; SetFlag -> story.neutral.1010.s2.completed

## neutral.1013.s2 [static_pass]

- Entry: Immediate
- Sources: Date: day 8 / Morning / Once
- Conditions: G1: BOOL_EQ(story.neutral.1013.s2.completed, false); G1: BOOL_EQ(story.neutral.1010.s1.completed, true)
- Presentation: Conversation/neutral.1013.s2
- Pages/lines/options: 21/99/6
- Letter/reading: 0/0 pages / 0 lines
- Checks: neutral.1013.s2.check.q1b, neutral.1013.s2.check.q1c, neutral.1013.s2.check.q2a
- Results: SetFlag -> neutral.antoine.observation; SetFlag -> story.neutral.1013.s2.completed; QueueEvent -> revolution.1013.s2

## neutral.1018.s1 [static_pass]

- Entry: Immediate
- Sources: Date: day 13 / Noon / Once
- Conditions: G1: BOOL_EQ(story.neutral.1018.s1.completed, false); G1: BOOL_EQ(story.neutral.1013.s2.completed, true)
- Presentation: Conversation/neutral.1018.s1
- Pages/lines/options: 13/75/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> neutral.newspaper.recommendation; SetFlag -> story.neutral.1018.s1.completed

## neutral.1027.s1 [static_pass]

- Entry: Immediate
- Sources: Date: day 22 / Noon / Once
- Conditions: G1: BOOL_EQ(story.neutral.1027.s1.completed, false); G1: BOOL_EQ(story.rose.1016.s1.completed, true)
- Presentation: Conversation/neutral.1027.s1
- Pages/lines/options: 9/144/3
- Letter/reading: 0/0 pages / 0 lines
- Checks: neutral.1027.s1.check.q1a, neutral.1027.s1.check.q1b
- Results: SetFlag -> neutral.alienor.dialogue; SetFlag -> story.neutral.1027.s1.completed

## neutral.1030.s1 [static_pass]

- Entry: Immediate
- Sources: Date: day 25 / Noon / Once
- Conditions: G1: BOOL_EQ(story.neutral.1030.s1.completed, false); G1: BAR_GTE(adele.affection, 3)
- Presentation: Conversation/neutral.1030.s1
- Pages/lines/options: 10/78/3
- Letter/reading: 0/0 pages / 0 lines
- Checks: neutral.1030.s1.check.q1b
- Results: SetFlag -> neutral.adele.dreams_unlocked; SetFlag -> neutral.adele.city_talk_closed; SetFlag -> story.neutral.1030.s1.completed

## neutral.route.1111.s1 [static_pass]

- Entry: Immediate
- Sources: QueueEvent: church.1111.s1 -> neutral.route.1111.s1
- Conditions: G1: BOOL_EQ(story.neutral.route.1111.s1.completed, false); G1: BOOL_EQ(route.neutral, true); G1: BOOL_EQ(route.rose, false); G1: BOOL_EQ(route.church, false); G1: BOOL_EQ(route.revolution, false)
- Presentation: Conversation/neutral.route.1111.s1
- Pages/lines/options: 11/213/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.neutral.route.1111.s1.ending.a; SetFlag -> story.neutral.route.1111.s1.completed

## neutral.route.1111.s2 [static_pass]

- Entry: Immediate
- Sources: QueueEvent: church.1111.s1 -> neutral.route.1111.s2
- Conditions: G1: BOOL_EQ(story.neutral.route.1111.s2.completed, false); G1: BOOL_EQ(route.neutral, true); G1: BOOL_EQ(route.rose, false); G1: BOOL_EQ(route.church, false); G1: BOOL_EQ(route.revolution, false)
- Presentation: Conversation/neutral.route.1111.s2
- Pages/lines/options: 3/114/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.neutral.route.1111.s2.completed

## neutral.route.1112.s1 [static_pass]

- Entry: Immediate
- Sources: Date: day 38 / Morning / Once
- Conditions: G1: BOOL_EQ(story.neutral.route.1112.s1.completed, false); G1: BOOL_EQ(story.neutral.route.1111.s2.completed, true); G1: BOOL_EQ(neutral.antoine.observation, true); G1: BOOL_EQ(route.neutral, true); G1: BOOL_EQ(route.rose, false); G1: BOOL_EQ(route.church, false); G1: BOOL_EQ(route.revolution, false)
- Presentation: Conversation/neutral.route.1112.s1
- Pages/lines/options: 7/53/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.neutral.route.1112.s1.completed

## neutral.route.1118.s1 [static_pass]

- Entry: Immediate
- Sources: Date: day 44 / Morning / Once
- Conditions: G1: BOOL_EQ(story.neutral.route.1118.s1.completed, false); G1: BOOL_EQ(story.neutral.route.1111.s2.completed, true); G1: BOOL_EQ(route.neutral, true); G1: BOOL_EQ(route.rose, false); G1: BOOL_EQ(route.church, false); G1: BOOL_EQ(route.revolution, false); G1: BAR_GTE(alienor.affection, 3)
- Presentation: Conversation/neutral.route.1118.s1
- Pages/lines/options: 13/124/3
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.neutral.route.1118.s1.completed

## neutral.route.1122.s1 [static_pass]

- Entry: Immediate
- Sources: Date: day 48 / Morning / Once
- Conditions: G1: BOOL_EQ(story.neutral.route.1122.s1.completed, false); G1: BOOL_EQ(story.neutral.route.1118.s1.completed, true); G1: BOOL_EQ(route.neutral, true); G1: BOOL_EQ(route.rose, false); G1: BOOL_EQ(route.church, false); G1: BOOL_EQ(route.revolution, false); G1: BOOL_EQ(neutral.investigation.shelter, true)
- Presentation: Conversation/neutral.route.1122.s1
- Pages/lines/options: 14/163/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.neutral.route.1122.s1.ending.b; SetFlag -> story.neutral.route.1122.s1.completed

## neutral.route.1129.s1 [blocked]

- Entry: Immediate
- Sources: Date: day 55 / Morning / Once
- Conditions: G1: BOOL_EQ(story.neutral.route.1129.s1.completed, false); G1: BOOL_EQ(story.neutral.route.1122.s1.completed, true); G1: BOOL_EQ(neutral.stayed, true); G1: BOOL_EQ(neutral.association.member, true); G1: BOOL_EQ(route.neutral, true); G1: BOOL_EQ(route.rose, false); G1: BOOL_EQ(route.church, false); G1: BOOL_EQ(route.revolution, false); G1: BOOL_EQ(blocked.neutral.route.1129.s1.neutral_revenue_threshold, true)
- Presentation: Conversation/neutral.route.1129.s1
- Pages/lines/options: 5/147/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> neutral.association.committee; SetFlag -> story.neutral.route.1129.s1.completed
### Errors
- Construction blocker: neutral_revenue_threshold


## neutral.route.1130.s1 [static_pass]

- Entry: Immediate
- Sources: Date: day 56 / Morning / Once
- Conditions: G1: BOOL_EQ(story.neutral.route.1130.s1.completed, false); G1: BOOL_EQ(story.neutral.route.1129.s1.completed, true); G1: BOOL_EQ(neutral.association.committee, true); G1: BOOL_EQ(neutral.stayed, true); G1: BOOL_EQ(route.neutral, true); G1: BOOL_EQ(route.rose, false); G1: BOOL_EQ(route.church, false); G1: BOOL_EQ(route.revolution, false)
- Presentation: Conversation/neutral.route.1130.s1
- Pages/lines/options: 13/170/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: neutral.route.1130.s1.check.negotiation
- Results: SetFlag -> neutral.negotiation.success; SetFlag -> neutral.negotiation.failed; QueueEvent -> neutral.route.1130.s2; SetFlag -> story.neutral.route.1130.s1.ending.d; SetFlag -> story.neutral.route.1130.s1.completed

## neutral.route.1130.s2 [static_pass]

- Entry: Immediate
- Sources: Date: day 56 / Morning / Once; QueueEvent: neutral.route.1130.s1 -> neutral.route.1130.s2
- Conditions: G1: BOOL_EQ(story.neutral.route.1130.s2.completed, false); G1: BOOL_EQ(story.neutral.route.1122.s1.completed, true); G1: BOOL_EQ(neutral.stayed, true); G1: BOOL_EQ(route.neutral, true); G1: BOOL_EQ(route.rose, false); G1: BOOL_EQ(route.church, false); G1: BOOL_EQ(route.revolution, false); G1: BOOL_EQ(neutral.association.member, false); G2: BOOL_EQ(story.neutral.route.1130.s2.completed, false); G2: BOOL_EQ(route.neutral, true); G2: BOOL_EQ(route.rose, false); G2: BOOL_EQ(route.church, false); G2: BOOL_EQ(route.revolution, false); G2: BOOL_EQ(story.neutral.route.1122.s1.completed, true); G2: BOOL_EQ(neutral.stayed, true); G2: BOOL_EQ(neutral.negotiation.failed, true)
- Presentation: Conversation/neutral.route.1130.s2
- Pages/lines/options: 10/96/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.neutral.route.1130.s2.ending.c; SetFlag -> story.neutral.route.1130.s2.completed

## news.bulletin.12ed1d36 [static_pass]

- Entry: MorningBeforeMap
- Sources: Date: day 10 / Morning / Once
- Conditions: (none)
- Presentation: Notice/news.bulletin.12ed1d36.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.news.bulletin.12ed1d36.completed; SetFlag -> news.public.10.12ed1d36

## news.bulletin.29af38b6 [static_pass]

- Entry: MorningBeforeMap
- Sources: Date: day 7 / Morning / Once
- Conditions: (none)
- Presentation: Notice/news.bulletin.29af38b6.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.news.bulletin.29af38b6.completed; SetFlag -> news.public.7.29af38b6

## news.bulletin.58d0aef1 [static_pass]

- Entry: MorningBeforeMap
- Sources: Date: day 31 / Morning / Once
- Conditions: (none)
- Presentation: Notice/news.bulletin.58d0aef1.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.news.bulletin.58d0aef1.completed; SetFlag -> news.public.31.58d0aef1

## news.bulletin.7e9cc445 [static_pass]

- Entry: MorningBeforeMap
- Sources: Date: day 5 / Morning / Once
- Conditions: (none)
- Presentation: Notice/news.bulletin.7e9cc445.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.news.bulletin.7e9cc445.completed; SetFlag -> news.public.5.7e9cc445

## news.bulletin.8140f96a [static_pass]

- Entry: MorningBeforeMap
- Sources: Date: day 10 / Morning / Once
- Conditions: (none)
- Presentation: Notice/news.bulletin.8140f96a.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.news.bulletin.8140f96a.completed; SetFlag -> news.public.10.8140f96a

## news.bulletin.837fdfd7 [static_pass]

- Entry: MorningBeforeMap
- Sources: Date: day 6 / Morning / Once
- Conditions: (none)
- Presentation: Notice/news.bulletin.837fdfd7.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.news.bulletin.837fdfd7.completed; SetFlag -> news.public.6.837fdfd7

## news.bulletin.c1b54ba7 [static_pass]

- Entry: MorningBeforeMap
- Sources: Date: day 7 / Morning / Once
- Conditions: (none)
- Presentation: Notice/news.bulletin.c1b54ba7.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.news.bulletin.c1b54ba7.completed; SetFlag -> news.public.7.c1b54ba7

## news.bulletin.ce650510 [static_pass]

- Entry: MorningBeforeMap
- Sources: Date: day 7 / Morning / Once
- Conditions: (none)
- Presentation: Notice/news.bulletin.ce650510.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.news.bulletin.ce650510.completed; SetFlag -> news.public.7.ce650510

## news.bulletin.d7760931 [static_pass]

- Entry: MorningBeforeMap
- Sources: Date: day 18 / Morning / Once
- Conditions: (none)
- Presentation: Notice/news.bulletin.d7760931.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.news.bulletin.d7760931.completed; SetFlag -> news.public.18.d7760931

## newspaper.1007 [static_pass]

- Entry: MorningBeforeMap
- Sources: Date: day 2 / Morning / Once
- Conditions: (none)
- Presentation: Reading/newspaper.1007.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 17 lines
- Checks: (none)
- Results: SetFlag -> story.newspaper.1007.completed

## newspaper.1014 [static_pass]

- Entry: MorningBeforeMap
- Sources: Date: day 9 / Morning / Once
- Conditions: (none)
- Presentation: Reading/newspaper.1014.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 24 lines
- Checks: (none)
- Results: SetFlag -> story.newspaper.1014.completed

## newspaper.1021 [static_pass]

- Entry: MorningBeforeMap
- Sources: Date: day 16 / Morning / Once
- Conditions: (none)
- Presentation: Reading/newspaper.1021.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 25 lines
- Checks: (none)
- Results: SetFlag -> story.newspaper.1021.completed

## newspaper.1028 [static_pass]

- Entry: MorningBeforeMap
- Sources: Date: day 23 / Morning / Once
- Conditions: (none)
- Presentation: Reading/newspaper.1028.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 25 lines
- Checks: (none)
- Results: SetFlag -> story.newspaper.1028.completed

## newspaper.1104 [static_pass]

- Entry: MorningBeforeMap
- Sources: Date: day 30 / Morning / Once
- Conditions: (none)
- Presentation: Reading/newspaper.1104.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 24 lines
- Checks: (none)
- Results: SetFlag -> story.newspaper.1104.completed

## newspaper.1111 [static_pass]

- Entry: MorningBeforeMap
- Sources: Date: day 37 / Morning / Once
- Conditions: (none)
- Presentation: Reading/newspaper.1111.presentation
- Pages/lines/options: 0/0/0
- Letter/reading: 0/1 pages / 26 lines
- Checks: (none)
- Results: SetFlag -> story.newspaper.1111.completed

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

## revolution.1013.s2 [static_pass]

- Entry: Immediate
- Sources: QueueEvent: neutral.1013.s2 -> revolution.1013.s2
- Conditions: G1: BAR_BETWEEN(world.calendar, 8,8); G1: BOOL_EQ(story.revolution.1013.s2.completed, false); G1: BOOL_EQ(story.revolution.terminal, false); G1: BOOL_EQ(story.neutral.1013.s2.completed, true); G1: BOOL_EQ(story.oct07.s2.completed, true)
- Presentation: Conversation/revolution.1013.s2.conversation setup=revolution.1013.s2
- Pages/lines/options: 54/52/3
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.revolution.1013.s2.completed

## revolution.1014.s1 [static_pass]

- Entry: MorningBeforeMap
- Sources: Date: day 9 / Morning / Once
- Conditions: G1: BAR_BETWEEN(world.calendar, 9,9); G1: BOOL_EQ(story.revolution.1014.s1.completed, false); G1: BOOL_EQ(story.revolution.terminal, false); G1: BOOL_EQ(story.oct07.s2.completed, true); G1: BOOL_EQ(story.rose.1011.s1.completed, true)
- Presentation: Conversation/revolution.1014.s1.conversation setup=revolution.1014.s1
- Pages/lines/options: 249/242/12
- Letter/reading: 2/1 pages / 1 lines
- Checks: revolution.1014.s1.check.wisdom4, revolution.1014.s1.check.perception6, revolution.1014.s1.check.wisdom5, revolution.1014.s1.check.language3
- Results: SetFlag -> story.revolution.sorel.organization.known; SetFlag -> story.revolution.cafe.organization.known; SetFlag -> known.sorel; SetFlag -> story.revolution.1014.s1.completed

## revolution.1028.s1 [static_pass]

- Entry: BeforeNight
- Sources: Date: day 23 / Night / Once
- Conditions: G1: BAR_BETWEEN(world.calendar, 23,23); G1: BOOL_EQ(story.revolution.1028.s1.completed, false); G1: BOOL_EQ(story.revolution.terminal, false); G1: BOOL_EQ(story.oct07.s2.completed, true)
- Presentation: Conversation/revolution.1028.s1.conversation setup=revolution.1028.s1
- Pages/lines/options: 141/139/3
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.revolution.1028.s1.completed

## revolution.1029.s1 [static_pass]

- Entry: BeforeNight
- Sources: Date: day 24 / Night / Once
- Conditions: G1: BAR_BETWEEN(world.calendar, 24,24); G1: BOOL_EQ(story.revolution.1029.s1.completed, false); G1: BOOL_EQ(story.revolution.terminal, false); G1: BOOL_EQ(story.church.1020.s1.cleared, true); G1: BOOL_EQ(church.release.persuasion, false); G1: BOOL_EQ(church.release.bribery, false); G1: BOOL_EQ(church.release.rescue, false); G1: BOOL_EQ(story.rose.1024.allegiance, true); G1: BOOL_EQ(story.rose.1024.safe_refusal, false); G1: BOOL_EQ(story.rose.1026.s2.completed, true); G1: BOOL_EQ(story.church.1020.s1.completed, true); G1: BOOL_EQ(story.revolution.1028.s1.completed, true); G2: BAR_BETWEEN(world.calendar, 24,24); G2: BOOL_EQ(story.revolution.1029.s1.completed, false); G2: BOOL_EQ(story.revolution.terminal, false); G2: BOOL_EQ(story.church.1020.s1.cleared, true); G2: BOOL_EQ(church.release.persuasion, false); G2: BOOL_EQ(church.release.bribery, false); G2: BOOL_EQ(church.release.rescue, false); G2: BOOL_EQ(story.rose.1024.allegiance, false); G2: BOOL_EQ(story.rose.1024.safe_refusal, true); G2: BOOL_EQ(story.church.1020.s1.completed, true); G2: BOOL_EQ(story.revolution.1028.s1.completed, true); G3: BAR_BETWEEN(world.calendar, 24,24); G3: BOOL_EQ(story.revolution.1029.s1.completed, false); G3: BOOL_EQ(story.revolution.terminal, false); G3: BOOL_EQ(story.church.1020.s1.cleared, false); G3: BOOL_EQ(church.release.persuasion, true); G3: BOOL_EQ(church.release.bribery, false); G3: BOOL_EQ(church.release.rescue, false); G3: BOOL_EQ(story.church.1023.s1.completed, true); G3: BOOL_EQ(story.rose.1024.allegiance, true); G3: BOOL_EQ(story.rose.1024.safe_refusal, false); G3: BOOL_EQ(story.rose.1026.s2.completed, true); G3: BOOL_EQ(story.church.1020.s1.completed, true); G3: BOOL_EQ(story.revolution.1028.s1.completed, true); G4: BAR_BETWEEN(world.calendar, 24,24); G4: BOOL_EQ(story.revolution.1029.s1.completed, false); G4: BOOL_EQ(story.revolution.terminal, false); G4: BOOL_EQ(story.church.1020.s1.cleared, false); G4: BOOL_EQ(church.release.persuasion, true); G4: BOOL_EQ(church.release.bribery, false); G4: BOOL_EQ(church.release.rescue, false); G4: BOOL_EQ(story.church.1023.s1.completed, true); G4: BOOL_EQ(story.rose.1024.allegiance, false); G4: BOOL_EQ(story.rose.1024.safe_refusal, true); G4: BOOL_EQ(story.church.1020.s1.completed, true); G4: BOOL_EQ(story.revolution.1028.s1.completed, true); G5: BAR_BETWEEN(world.calendar, 24,24); G5: BOOL_EQ(story.revolution.1029.s1.completed, false); G5: BOOL_EQ(story.revolution.terminal, false); G5: BOOL_EQ(story.church.1020.s1.cleared, false); G5: BOOL_EQ(church.release.persuasion, false); G5: BOOL_EQ(church.release.bribery, true); G5: BOOL_EQ(church.release.rescue, false); G5: BOOL_EQ(story.church.1023.s1.completed, true); G5: BOOL_EQ(story.rose.1024.allegiance, true); G5: BOOL_EQ(story.rose.1024.safe_refusal, false); G5: BOOL_EQ(story.rose.1026.s2.completed, true); G5: BOOL_EQ(story.church.1020.s1.completed, true); G5: BOOL_EQ(story.revolution.1028.s1.completed, true); G6: BAR_BETWEEN(world.calendar, 24,24); G6: BOOL_EQ(story.revolution.1029.s1.completed, false); G6: BOOL_EQ(story.revolution.terminal, false); G6: BOOL_EQ(story.church.1020.s1.cleared, false); G6: BOOL_EQ(church.release.persuasion, false); G6: BOOL_EQ(church.release.bribery, true); G6: BOOL_EQ(church.release.rescue, false); G6: BOOL_EQ(story.church.1023.s1.completed, true); G6: BOOL_EQ(story.rose.1024.allegiance, false); G6: BOOL_EQ(story.rose.1024.safe_refusal, true); G6: BOOL_EQ(story.church.1020.s1.completed, true); G6: BOOL_EQ(story.revolution.1028.s1.completed, true); G7: BAR_BETWEEN(world.calendar, 24,24); G7: BOOL_EQ(story.revolution.1029.s1.completed, false); G7: BOOL_EQ(story.revolution.terminal, false); G7: BOOL_EQ(story.church.1020.s1.cleared, false); G7: BOOL_EQ(church.release.persuasion, false); G7: BOOL_EQ(church.release.bribery, false); G7: BOOL_EQ(church.release.rescue, true); G7: BOOL_EQ(story.church.1023.s1.completed, true); G7: BOOL_EQ(story.rose.1024.allegiance, true); G7: BOOL_EQ(story.rose.1024.safe_refusal, false); G7: BOOL_EQ(story.rose.1026.s2.completed, true); G7: BOOL_EQ(story.church.1020.s1.completed, true); G7: BOOL_EQ(story.revolution.1028.s1.completed, true); G8: BAR_BETWEEN(world.calendar, 24,24); G8: BOOL_EQ(story.revolution.1029.s1.completed, false); G8: BOOL_EQ(story.revolution.terminal, false); G8: BOOL_EQ(story.church.1020.s1.cleared, false); G8: BOOL_EQ(church.release.persuasion, false); G8: BOOL_EQ(church.release.bribery, false); G8: BOOL_EQ(church.release.rescue, true); G8: BOOL_EQ(story.church.1023.s1.completed, true); G8: BOOL_EQ(story.rose.1024.allegiance, false); G8: BOOL_EQ(story.rose.1024.safe_refusal, true); G8: BOOL_EQ(story.church.1020.s1.completed, true); G8: BOOL_EQ(story.revolution.1028.s1.completed, true)
- Presentation: Conversation/revolution.1029.s1.conversation setup=revolution.1029.s1
- Pages/lines/options: 689/685/5
- Letter/reading: 1/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.revolution.1029.s1.cooperated; SetFlag -> story.revolution.1029.s1.refused; SetFlag -> known.mary; SetFlag -> story.revolution.1029.s1.completed

## revolution.1107.s1 [static_pass]

- Entry: BeforeNight
- Sources: Date: day 33 / Night / Once
- Conditions: G1: BOOL_EQ(story.revolution.1029.s1.cooperated, true); G1: BAR_BETWEEN(world.calendar, 33,33); G1: BOOL_EQ(story.revolution.1107.s1.completed, false); G1: BOOL_EQ(story.revolution.terminal, false); G1: BOOL_EQ(story.rose.1024.allegiance, true); G1: BOOL_EQ(story.rose.1024.safe_refusal, false); G1: BOOL_EQ(story.revolution.1029.s1.completed, true); G2: BOOL_EQ(story.revolution.1029.s1.cooperated, true); G2: BAR_BETWEEN(world.calendar, 33,33); G2: BOOL_EQ(story.revolution.1107.s1.completed, false); G2: BOOL_EQ(story.revolution.terminal, false); G2: BOOL_EQ(story.rose.1024.allegiance, false); G2: BOOL_EQ(story.rose.1024.safe_refusal, true); G2: BOOL_EQ(story.revolution.1029.s1.completed, true)
- Presentation: Conversation/revolution.1107.s1.conversation setup=revolution.1107.s1
- Pages/lines/options: 278/275/7
- Letter/reading: 0/0 pages / 0 lines
- Checks: revolution.1107.s1.check.letters, revolution.1107.s1.check.timing, revolution.1107.s1.check.route, revolution.1107.s1.check.alliance, revolution.1107.s1.check.challenge
- Results: BarAdd -> lesou.affection; BarAdd -> lesou.affection; BarAdd -> lesou.affection; BarAdd -> lesou.affection; BarAdd -> lesou.affection; SetFlag -> known.lesou; SetFlag -> story.revolution.revolution_date.known; SetFlag -> story.revolution.rose.plan.known; SetFlag -> story.revolution.cafe.transfer_plan.known; SetFlag -> story.revolution.1107.s1.completed

## revolution.route.1111.s2 [static_pass]

- Entry: Immediate
- Sources: QueueEvent: church.1111.s1 -> revolution.route.1111.s2
- Conditions: G1: BOOL_EQ(story.revolution.1029.s1.cooperated, true); G1: BAR_BETWEEN(world.calendar, 37,37); G1: BOOL_EQ(story.revolution.route.1111.s2.completed, false); G1: BOOL_EQ(route.church, false); G1: BOOL_EQ(route.neutral, false); G1: BOOL_EQ(route.rose, false); G1: BOOL_EQ(story.revolution.terminal, false); G1: BOOL_EQ(story.church.1111.s1.completed, true); G1: BOOL_EQ(story.revolution.1107.s1.completed, true); G1: BOOL_EQ(route.revolution, true)
- Presentation: Conversation/revolution.route.1111.s2.conversation setup=revolution.route.1111.s2
- Pages/lines/options: 11/9/0
- Letter/reading: 1/1 pages / 4 lines
- Checks: (none)
- Results: SetFlag -> story.revolution.protection_request.sent; SetFlag -> story.revolution.route.1111.s2.completed

## revolution.route.1112.s1 [static_pass]

- Entry: AfterBusiness
- Sources: Date: day 38 / Noon / Once
- Conditions: G1: BAR_BETWEEN(world.calendar, 38,38); G1: BOOL_EQ(story.revolution.route.1112.s1.completed, false); G1: BOOL_EQ(route.church, false); G1: BOOL_EQ(route.neutral, false); G1: BOOL_EQ(route.rose, false); G1: BOOL_EQ(story.revolution.terminal, false); G1: BOOL_EQ(story.revolution.route.1111.s2.completed, true); G1: BOOL_EQ(route.revolution, true)
- Presentation: Conversation/revolution.route.1112.s1.conversation setup=revolution.route.1112.s1
- Pages/lines/options: 177/174/5
- Letter/reading: 0/0 pages / 0 lines
- Checks: revolution.route.1112.s1.check.wisdom12, revolution.route.1112.s1.check.language10
- Results: SetFlag -> story.revolution.mary.contradiction.known; SetFlag -> story.revolution.mary.unwilling.known; SetFlag -> story.revolution.mary.pistol.received; SetFlag -> story.revolution.private_invitation.received; SetFlag -> story.revolution.route.1112.s1.completed

## revolution.route.1115.s1 [static_pass]

- Entry: AfterBusiness
- Sources: Date: day 41 / Noon / Once
- Conditions: G1: BAR_BETWEEN(world.calendar, 41,41); G1: BOOL_EQ(story.revolution.route.1115.s1.completed, false); G1: BOOL_EQ(route.church, false); G1: BOOL_EQ(route.neutral, false); G1: BOOL_EQ(route.rose, false); G1: BOOL_EQ(story.revolution.terminal, false); G1: BOOL_EQ(story.revolution.route.1112.s1.completed, true); G1: BOOL_EQ(route.revolution, true)
- Presentation: Conversation/revolution.route.1115.s1.conversation setup=revolution.route.1115.s1
- Pages/lines/options: 303/298/11
- Letter/reading: 0/0 pages / 0 lines
- Checks: revolution.route.1115.s1.check.structure9
- Results: SetFlag -> story.revolution.alliance_structure.known; SetFlag -> story.revolution.lesou.coordinator.known; SetFlag -> story.revolution.badge.received; SetFlag -> story.revolution.route.1115.s1.completed

## revolution.route.1122.s1 [static_pass]

- Entry: AfterBusiness
- Sources: Date: day 48 / Noon / Once
- Conditions: G1: BAR_BETWEEN(world.calendar, 48,48); G1: BOOL_EQ(story.revolution.route.1122.s1.completed, false); G1: BOOL_EQ(route.church, false); G1: BOOL_EQ(route.neutral, false); G1: BOOL_EQ(route.rose, false); G1: BOOL_EQ(story.revolution.terminal, false); G1: BOOL_EQ(story.revolution.route.1115.s1.completed, true); G1: BOOL_EQ(route.revolution, true)
- Presentation: Conversation/revolution.route.1122.s1.conversation setup=revolution.route.1122.s1
- Pages/lines/options: 342/336/7
- Letter/reading: 2/1 pages / 1 lines
- Checks: (none)
- Results: SetFlag -> story.revolution.route.1122.s1.completed

## revolution.route.1123.s1 [static_pass]

- Entry: BeforeNight
- Sources: Date: day 49 / Night / Once
- Conditions: G1: BAR_BETWEEN(world.calendar, 49,49); G1: BOOL_EQ(story.revolution.route.1123.s1.completed, false); G1: BOOL_EQ(route.church, false); G1: BOOL_EQ(route.neutral, false); G1: BOOL_EQ(route.rose, false); G1: BOOL_EQ(story.revolution.terminal, false); G1: BOOL_EQ(story.revolution.route.1122.s1.completed, true); G1: BOOL_EQ(route.revolution, true)
- Presentation: Conversation/revolution.route.1123.s1.conversation setup=revolution.route.1123.s1
- Pages/lines/options: 275/273/3
- Letter/reading: 0/0 pages / 0 lines
- Checks: revolution.route.1123.s1.check.mary_final
- Results: SetFlag -> story.revolution.mary.assassination.stopped; SetFlag -> story.revolution.route.1123.s1.completed

## revolution.route.1124.s1 [static_pass]

- Entry: MorningBeforeMap
- Sources: Date: day 50 / Morning / Once
- Conditions: G1: BAR_BETWEEN(world.calendar, 50,50); G1: BOOL_EQ(story.revolution.route.1124.s1.completed, false); G1: BOOL_EQ(route.church, false); G1: BOOL_EQ(route.neutral, false); G1: BOOL_EQ(route.rose, false); G1: BOOL_EQ(story.revolution.terminal, false); G1: BOOL_EQ(story.revolution.route.1123.s1.completed, true); G1: BOOL_EQ(route.revolution, true)
- Presentation: Conversation/revolution.route.1124.s1.conversation setup=revolution.route.1124.s1
- Pages/lines/options: 167/162/8
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.revolution.terminal; SetFlag -> story.revolution.end01.selected; SetFlag -> story.revolution.end02.selected; SetFlag -> story.revolution.route.1124.s1.completed

## rose.1009.s1 [static_pass]

- Entry: MapAction
- Sources: Action: rose.1009.s1.action @ 圣徒街
- Conditions: G1: BOOL_EQ(story.rose.1009.s1.completed, false); G1: BOOL_EQ(story.oct07.s2.completed, true); G1: BAR_BETWEEN(world.calendar, 4,4)
- Presentation: Conversation/rose.1009.s1.conversation setup=rose.1009.s1
- Pages/lines/options: 9/67/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: rose.1009.s1.perception
- Results: SetFlag -> story.rose.1009.s1.completed

## rose.1011.s1 [static_pass]

- Entry: MorningBeforeMap
- Sources: Date: day 6 / Morning / Once
- Conditions: G1: BOOL_EQ(story.rose.1011.s1.completed, false); G1: BOOL_EQ(story.rose.1009.s1.completed, true); G1: BOOL_EQ(story.neutral.1010.s1.completed, true)
- Presentation: Conversation/rose.1011.s1.conversation setup=rose.1011.s1
- Pages/lines/options: 35/126/11
- Letter/reading: 1/1 pages / 3 lines
- Checks: (none)
- Results: SetFlag -> story.rose.1011.s1.completed; SetFlag -> story.rose.1011.s1.sylvain_appreciation; SetFlag -> known.sparrow; SetFlag -> known.sylvain; SetFlag -> story.adele.away

## rose.1012.s1 [static_pass]

- Entry: MorningBeforeMap
- Sources: Date: day 7 / Morning / Once
- Conditions: G1: BOOL_EQ(story.rose.1012.s1.completed, false); G1: BOOL_EQ(story.rose.1011.s1.completed, true); G1: BOOL_EQ(story.church.1009.s2.completed, true)
- Presentation: Conversation/rose.1012.s1.conversation setup=rose.1012.s1
- Pages/lines/options: 15/77/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.rose.1012.s1.completed; SetFlag -> known.alienor; SetFlag -> story.adele.away

## rose.1016.s1 [static_pass]

- Entry: MorningBeforeMap
- Sources: Date: day 11 / Morning / Once
- Conditions: G1: BOOL_EQ(story.rose.1016.s1.completed, false); G1: BOOL_EQ(story.rose.1011.s1.completed, true); G1: BOOL_EQ(story.rose.1011.s1.sylvain_appreciation, true)
- Presentation: Conversation/rose.1016.s1.conversation setup=rose.1016.s1
- Pages/lines/options: 41/124/20
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.rose.1016.s1.completed; SetFlag -> known.sade; SetFlag -> known.alienor; SetFlag -> story.adele.away; SetFlag -> story.sylvain.unavailable

## rose.1019.s1 [static_pass]

- Entry: AfterBusiness
- Sources: Date: day 14 / Noon / Once
- Conditions: G1: BOOL_EQ(story.rose.1019.s1.completed, false); G1: BOOL_EQ(story.rose.1016.s1.completed, true)
- Presentation: Conversation/rose.1019.s1.conversation setup=rose.1019.s1
- Pages/lines/options: 10/42/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.rose.1019.s1.completed; SetFlag -> story.adele.away; SetFlag -> story.sylvain.unavailable

## rose.1024.s1 [static_pass]

- Entry: AfterBusiness
- Sources: Date: day 19 / Noon / Once
- Conditions: G1: BOOL_EQ(story.rose.1024.s1.completed, false); G1: BOOL_EQ(story.church.1020.s1.completed, true); G1: BOOL_EQ(story.church.1020.s1.cleared, true)
- Presentation: Conversation/rose.1024.s1.conversation setup=rose.1024.s1
- Pages/lines/options: 8/113/6
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.rose.1024.s1.completed; SetFlag -> story.rose.1024.allegiance; SetFlag -> story.rose.ending.trust_limit.pending; SetFlag -> story.adele.away

## rose.1024.s2 [static_pass]

- Entry: AfterBusiness
- Sources: Date: day 19 / Noon / Once
- Conditions: G1: BOOL_EQ(story.rose.1024.s2.completed, false); G1: BOOL_EQ(story.church.1023.s1.completed, true); G1: BOOL_EQ(church.release.bribery, true); G1: BOOL_EQ(church.release.rescue, false); G2: BOOL_EQ(story.rose.1024.s2.completed, false); G2: BOOL_EQ(story.church.1023.s1.completed, true); G2: BOOL_EQ(church.release.rescue, true); G2: BOOL_EQ(church.release.bribery, false)
- Presentation: Conversation/rose.1024.s2.conversation setup=rose.1024.s2
- Pages/lines/options: 20/116/9
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.rose.1024.s2.completed; BarAdd -> adele.affection; SetFlag -> story.rose.1024.allegiance; SetFlag -> story.rose.1024.safe_refusal; SetFlag -> story.rose.ending.trust_limit.pending; SetFlag -> story.adele.away

## rose.1025.s2 [static_pass]

- Entry: BeforeNight
- Sources: Date: day 20 / Night / Once
- Conditions: G1: BOOL_EQ(story.rose.1025.s2.completed, false); G1: BOOL_EQ(story.rose.1024.allegiance, true); G1: BOOL_EQ(story.rose.ending.trust_limit.pending, false)
- Presentation: Conversation/rose.1025.s2.conversation setup=rose.1025.s2
- Pages/lines/options: 2/11/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.rose.1025.s2.completed

## rose.1026.s2 [static_pass]

- Entry: BeforeNight
- Sources: Date: day 21 / Night / Once
- Conditions: G1: BOOL_EQ(story.rose.1026.s2.completed, false); G1: BOOL_EQ(story.rose.1024.allegiance, true); G1: BOOL_EQ(story.rose.ending.trust_limit.pending, false); G1: BOOL_EQ(story.rose.1025.s2.completed, true)
- Presentation: Conversation/rose.1026.s2.conversation setup=rose.1026.s2
- Pages/lines/options: 9/35/3
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.rose.1026.s2.completed; SetFlag -> story.rose.1026.s2.prisoner_list_received

## rose.1101.s1 [static_pass]

- Entry: AfterBusiness
- Sources: Date: day 27 / Noon / Once
- Conditions: G1: BOOL_EQ(story.rose.1101.s1.completed, false); G1: BOOL_EQ(story.revolution.1029.s1.completed, true); G1: BOOL_EQ(story.rose.1024.allegiance, true); G1: BOOL_EQ(story.revolution.1029.s1.refused, true); G1: BOOL_EQ(story.revolution.1029.s1.cooperated, false); G1: BOOL_EQ(story.rose.1024.s1.completed, true); G2: BOOL_EQ(story.rose.1101.s1.completed, false); G2: BOOL_EQ(story.revolution.1029.s1.completed, true); G2: BOOL_EQ(story.rose.1024.allegiance, true); G2: BOOL_EQ(story.revolution.1029.s1.cooperated, true); G2: BOOL_EQ(story.revolution.1029.s1.refused, false)
- Presentation: Conversation/rose.1101.s1.conversation setup=rose.1101.s1
- Pages/lines/options: 4/53/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.rose.1101.s1.completed; SetFlag -> story.rose.ending.job_requirements.pending; QueueEvent -> rose.ending.job_requirements; SetFlag -> story.adele.away; SetFlag -> story.adele.away

## rose.1103.s1 [static_pass]

- Entry: BeforeNight
- Sources: Date: day 29 / Night / Once
- Conditions: G1: BOOL_EQ(story.rose.1103.s1.completed, false); G1: BOOL_EQ(story.rose.1101.s1.completed, true); G1: BOOL_EQ(story.rose.1024.allegiance, true); G1: BOOL_EQ(story.rose.ending.job_requirements.pending, false)
- Presentation: Conversation/rose.1103.s1.conversation setup=rose.1103.s1
- Pages/lines/options: 15/145/4
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.rose.1103.s1.completed; BarAdd -> alienor.affection; SetFlag -> story.rose.1103.s1.sheltered; SetFlag -> church.alienor.sheltered; SetFlag -> church.alienor.refused_shelter; SetFlag -> story.rose.1103.s1.identity_known; SetFlag -> known.alienor; SetFlag -> story.adele.away

## rose.ending.job_requirements [static_pass]

- Entry: Immediate
- Sources: QueueEvent: rose.1101.s1 -> rose.ending.job_requirements
- Conditions: G1: BOOL_EQ(story.rose.ending.job_requirements.completed, false); G1: BOOL_EQ(story.rose.ending.job_requirements.pending, true)
- Presentation: Conversation/rose.ending.job_requirements.conversation setup=rose.ending.job_requirements
- Pages/lines/options: 2/28/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.rose.ending.job_requirements.completed; SetFlag -> story.rose.ending.job_requirements.terminal

## rose.ending.trust_limit [static_pass]

- Entry: BeforeNight
- Sources: Date: day 21 / Night / Once
- Conditions: G1: BOOL_EQ(story.rose.ending.trust_limit.completed, false); G1: BOOL_EQ(story.rose.ending.trust_limit.pending, true)
- Presentation: Conversation/rose.ending.trust_limit.conversation setup=rose.ending.trust_limit
- Pages/lines/options: 4/48/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.rose.ending.trust_limit.completed; SetFlag -> story.rose.ending.trust_limit.terminal

## rose.route.1111.s2 [static_pass]

- Entry: Immediate
- Sources: QueueEvent: church.1111.s1 -> rose.route.1111.s2
- Conditions: G1: BOOL_EQ(story.rose.route.1111.s2.completed, false); G1: BOOL_EQ(route.rose, true); G1: BOOL_EQ(route.neutral, false); G1: BOOL_EQ(route.church, false); G1: BOOL_EQ(route.revolution, false); G1: BOOL_EQ(story.church.1111.s1.completed, true)
- Presentation: Conversation/rose.route.1111.s2.conversation setup=rose.route.1111.s2
- Pages/lines/options: 2/21/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.rose.route.1111.s2.completed

## rose.route.1115.s1 [blocked]

- Entry: BeforeNight
- Sources: Date: day 41 / Night / Once
- Conditions: G1: BOOL_EQ(story.rose.route.1115.s1.completed, false); G1: BOOL_EQ(route.rose, true); G1: BOOL_EQ(route.neutral, false); G1: BOOL_EQ(route.church, false); G1: BOOL_EQ(route.revolution, false); G1: BOOL_EQ(story.rose.route.1111.s2.completed, true); G1: BOOL_EQ(integration.rose.route.1115.s1.ready, true)
- Presentation: Conversation/rose.route.1115.s1.conversation setup=rose.route.1115.s1
- Pages/lines/options: 16/177/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.rose.route.1115.s1.completed; SetFlag -> story.rose.route.1115.s1.terminal.ideal_world; SetFlag -> story.rose.route.1115.s1.plan_known; SetFlag -> story.adele.away
### Errors
- Construction blocker: candor_baseline_and_gap
- Construction blocker: perception_failure_tail_unspecified


## rose.route.1122.s1 [static_pass]

- Entry: BeforeNight
- Sources: Date: day 48 / Night / Once
- Conditions: G1: BOOL_EQ(story.rose.route.1122.s1.completed, false); G1: BOOL_EQ(route.rose, true); G1: BOOL_EQ(route.neutral, false); G1: BOOL_EQ(route.church, false); G1: BOOL_EQ(route.revolution, false); G1: BOOL_EQ(story.rose.route.1115.s1.completed, true); G1: BOOL_EQ(story.rose.route.1115.s1.plan_known, true)
- Presentation: Conversation/rose.route.1122.s1.conversation setup=rose.route.1122.s1
- Pages/lines/options: 5/57/2
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.rose.route.1122.s1.completed; SetFlag -> story.rose.route.ending.unforgiven.pending; QueueEvent -> rose.route.ending.unforgiven; SetFlag -> story.rose.route.ending.bloom.pending; QueueEvent -> rose.route.ending.bloom; SetFlag -> story.adele.away

## rose.route.ending.bloom [static_pass]

- Entry: Immediate
- Sources: QueueEvent: rose.route.1122.s1 -> rose.route.ending.bloom
- Conditions: G1: BOOL_EQ(story.rose.route.ending.bloom.completed, false); G1: BOOL_EQ(route.rose, true); G1: BOOL_EQ(route.neutral, false); G1: BOOL_EQ(route.church, false); G1: BOOL_EQ(route.revolution, false); G1: BOOL_EQ(story.rose.route.ending.bloom.pending, true)
- Presentation: Conversation/rose.route.ending.bloom.conversation setup=rose.route.ending.bloom
- Pages/lines/options: 2/109/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.rose.route.ending.bloom.completed; SetFlag -> story.rose.route.ending.bloom.terminal; SetFlag -> story.adele.away

## rose.route.ending.unforgiven [static_pass]

- Entry: Immediate
- Sources: QueueEvent: rose.route.1122.s1 -> rose.route.ending.unforgiven
- Conditions: G1: BOOL_EQ(story.rose.route.ending.unforgiven.completed, false); G1: BOOL_EQ(route.rose, true); G1: BOOL_EQ(route.neutral, false); G1: BOOL_EQ(route.church, false); G1: BOOL_EQ(route.revolution, false); G1: BOOL_EQ(story.rose.route.ending.unforgiven.pending, true)
- Presentation: Conversation/rose.route.ending.unforgiven.conversation setup=rose.route.ending.unforgiven
- Pages/lines/options: 2/20/0
- Letter/reading: 0/0 pages / 0 lines
- Checks: (none)
- Results: SetFlag -> story.rose.route.ending.unforgiven.completed; SetFlag -> story.rose.route.ending.unforgiven.terminal

