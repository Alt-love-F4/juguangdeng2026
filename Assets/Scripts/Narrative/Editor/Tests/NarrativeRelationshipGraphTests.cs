using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Spotlight.Narrative.Editor.Tests
{
    public sealed class NarrativeRelationshipGraphTests
    {
        private static NarrativeConfiguration Config()
        {
            var c = new NarrativeConfiguration();
            c.events.Add(new NarrativeEventDefinition { eventId="neutral.1008.s1", presentationKey="a", entry=NarrativeEntryPoint.MapAction });
            c.events.Add(new NarrativeEventDefinition { eventId="rose.1009.s1", presentationKey="b", eventProperties=new List<NarrativeEventProperty>{new NarrativeEventProperty{property="blockedBy",value="source.pending"}} });
            c.events.Add(new NarrativeEventDefinition { eventId="church.1014.s2", presentationKey="c" });
            c.presentations.conversationPages.Add(new NarrativeConversationPageDefinition { pageId="pa",presentationKey="a",optionGroupKey="q" });
            c.presentations.conversationLines.Add(new NarrativeConversationLineDefinition {lineId="la",pageId="pa",seq=1,textKey="body"});
            c.choices.Add(new NarrativeChoiceDefinition{choiceId="yes",groupKey="q",textKey="option",resultOwnerId="yes.owner"});
            c.texts.Add(new NarrativeTextDefinition{key="body",content="当前原稿正文"});c.texts.Add(new NarrativeTextDefinition{key="option",content="选择合作"});
            c.conditionKeys.Add(new NarrativeConditionKeyDefinition{key="met"});
            c.results.Add(new NarrativeResultDefinition{resultId="set",eventId="yes.owner",resultType=NarrativeResultType.SetFlag,target="met",value="true"});
            c.results.Add(new NarrativeResultDefinition{resultId="queue",eventId="yes.owner",resultType=NarrativeResultType.QueueEvent,target="event:rose.1009.s1"});
            c.conditions.Add(new NarrativeConditionDefinition{conditionId="g1",ownerType="event",ownerId="rose.1009.s1",groupIndex=1,condType="BOOL_EQ",target="met",value="true"});
            c.conditions.Add(new NarrativeConditionDefinition{conditionId="g2",ownerType="event",ownerId="rose.1009.s1",groupIndex=2,condType="BOOL_EQ",target="host.ready",value="true"});
            return c;
        }
        [Test] public void ChoiceOwnedFactsAndQueueAreSourceAttributedWithConditionalEvidence()
        {
            var graph=NarrativeRelationshipGraph.Build(Config());
            var a=NarrativeRelationshipGraph.EventId("neutral.1008.s1");var b=NarrativeRelationshipGraph.EventId("rose.1009.s1");
            Assert.That(graph.Edges.Any(e=>e.From==a&&e.To==b&&e.Kind==StoryLinkKind.Continuation&&e.Conditional),Is.True);
            Assert.That(graph.Edges.Any(e=>e.From==a&&e.To==b&&e.Kind==StoryLinkKind.Prerequisite&&e.ConditionGroup==1&&e.Evidence.Contains("选择合作")),Is.True);
            Assert.That(graph.Find(b).Blocked,Is.True);
        }
        [Test] public void UnproducedFactDoesNotInventAStoryProducerOrRunningState()
        {
            var graph=NarrativeRelationshipGraph.Build(Config());
            Assert.That(graph.Find("fact:host.ready"),Is.Not.Null);
            var edge=graph.Edges.Single(e=>e.From=="fact:host.ready");
            Assert.That(edge.ConditionGroup,Is.EqualTo(2));Assert.That(edge.Kind,Is.EqualTo(StoryLinkKind.Unresolved));
            Assert.That(edge.Evidence,Does.Contain("组内 AND，组间 OR"));
        }
        [Test] public void ChronologicalNeighborsAreNotAutomaticallyConnected()
        {
            var graph=NarrativeRelationshipGraph.Build(Config());
            Assert.That(graph.Find("event:church.1014.s2").Day,Is.EqualTo(9));
            Assert.That(graph.Connections("event:church.1014.s2"),Is.Empty);
        }
        [Test] public void ClickingContentCanReadAllPagesAndChoicesWithoutMutatingConfiguration()
        {
            var c=Config();var before=c.results.Count;var graph=NarrativeRelationshipGraph.Build(c);var blocks=graph.Content("event:neutral.1008.s1");
            Assert.That(blocks[0].Text,Does.Contain("当前原稿正文").And.Contain("选择合作").And.Contain("yes"));
            Assert.That(c.results.Count,Is.EqualTo(before));Assert.That(c.conditionKeys[0].initialValue,Is.False);
            Assert.That(graph.Describe("event:rose.1009.s1"),Does.Contain("source.pending").And.Contain("组间 OR"));
        }
        [Test] public void ActualGeneratedConfigurationIsFullyBrowsableAndIncludesRealCrossLineEdges()
        {
            var c=LubanNarrativeLoader.Load();var graph=NarrativeRelationshipGraph.Build(c);
            Assert.That(graph.Nodes.Count(n=>n.Kind==StoryNodeKind.Event),Is.EqualTo(c.events.Count));
            Assert.That(graph.StoryCount,Is.GreaterThanOrEqualTo(62));
            Assert.That(graph.BlockedCount,Is.EqualTo(c.events.Count(e=>NarrativeRelationshipGraph.IsStory(e.eventId)&&e.eventProperties.Any(p=>p.property=="blockedBy"&&!string.IsNullOrWhiteSpace(p.value)))));
            Assert.That(graph.Edges.Any(e=>e.From=="event:neutral.1013.s2"&&e.To=="event:revolution.1013.s2"&&e.Kind==StoryLinkKind.Continuation),Is.True);
            var rose=graph.Content("event:rose.1011.s1");Assert.That(rose.Any(b=>b.Text.Contains("taste:blackcoffee:90:1")),Is.True);
            Assert.That(rose.Any(b=>b.Text.Contains("本分支终点")),Is.True);
            Assert.That(graph.Nodes.Any(n=>n.Kind==StoryNodeKind.Ending&&!n.Disabled),Is.True);
        }
    }
}
