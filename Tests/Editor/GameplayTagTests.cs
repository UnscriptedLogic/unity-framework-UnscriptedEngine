using System.Collections.Generic;
using System.Reflection;
using Framework.GameplayTag;
using NUnit.Framework;
using UnityEngine;

namespace UnscriptedEngine.Tests
{
    public class GameplayTagTests
    {
        private readonly List<Object> assets = new();
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private T Asset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>(); assets.Add(asset); return asset;
        }
        private GameplayTag Tag(string path, GameplayTag parent = null)
        {
            var tag = Asset<GameplayTag>(); Set(tag, "path", path); Set(tag, "parent", parent); return tag;
        }
        [TearDown] public void Cleanup()
        {
            foreach (var asset in assets) Object.DestroyImmediate(asset);
            assets.Clear();
        }
        [TestCase("  Combat . Melee  ", "Combat.Melee")]
        [TestCase("   ", "")]
        public void ValidationNormalizesPath(string input, string expected)
        {
            var tag = Tag(input);
            typeof(GameplayTag).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tag, null);
            Assert.That(tag.Path, Is.EqualTo(expected));
        }
        [Test] public void StableIdMatchesKnownFnvVector() => Assert.That(Tag("hello").Id, Is.EqualTo(0xa430d84680aabd0bUL));
        [Test] public void EmptyTagIsInvalid() => Assert.That(Tag("").IsValid, Is.False);
        [Test] public void SamePathAssetsMatchAndCanRemoveEachOther()
        {
            var first = Tag("Combat"); var equivalent = Tag("Combat");
            var container = new GameplayTagContainer(); container.Add(first);
            Assert.That(first.Matches(equivalent, false), Is.True);
            Assert.That(container.Remove(equivalent), Is.True);
            Assert.That(container.Count, Is.Zero);
        }
        [Test] public void EditingTagInvalidatesDatabaseAndContainerCaches()
        {
            var tag = Tag("Before"); var database = Asset<GameplayTagDatabase>();
            Set(database, "tags", new List<GameplayTag> { tag });
            var container = new GameplayTagContainer(); container.Add(tag);
            Assert.That(database.TryGet("Before", out _), Is.True);
            Set(tag, "path", "After");
            typeof(GameplayTag).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tag, null);
            Assert.That(database.TryGet("Before", out _), Is.False);
            Assert.That(database.TryGet("After", out var found), Is.True);
            Assert.That(found, Is.SameAs(tag));
            Assert.That(container.ContainsExact(tag), Is.True);
            Assert.That(container.ContainsExact(Tag("Before")), Is.False);
        }
        [Test] public void ChildMatchesParentButParentDoesNotMatchChild()
        {
            var parent = Tag("Combat"); var child = Tag("Combat.Melee", parent);
            Assert.That(child.Matches(parent), Is.True);
            Assert.That(parent.Matches(child), Is.False);
            Assert.That(child.Matches(parent, false), Is.False);
            Assert.That(child.IsChildOf(child), Is.False);
        }
        [Test] public void ContainerExpandsAncestorsWithoutAddingExactEntries()
        {
            var parent = Tag("Combat"); var child = Tag("Combat.Melee", parent);
            var container = new GameplayTagContainer(); container.Add(child);
            Assert.That(container.Has(parent), Is.True);
            Assert.That(container.ContainsExact(parent), Is.False);
            Assert.That(container.Count, Is.EqualTo(1));
        }
        [Test] public void DuplicateIdsCannotBeAddedTwice()
        {
            var container = new GameplayTagContainer(); container.Add(Tag("Combat"));
            Assert.That(container.Add(Tag("Combat")), Is.False);
            Assert.That(container.Count, Is.EqualTo(1));
        }
        [Test] public void RemovingChildPreservesSharedAncestorForSibling()
        {
            var parent = Tag("Combat"); var first = Tag("Combat.Melee", parent); var second = Tag("Combat.Ranged", parent);
            var container = new GameplayTagContainer(); container.Add(first); container.Add(second);
            Assert.That(container.Remove(first), Is.True);
            Assert.That(container.Has(parent), Is.True);
            Assert.That(container.Has(first), Is.False);
            container.Remove(second); Assert.That(container.Has(parent), Is.False);
        }
        [Test] public void ClearRemovesExpandedAndExactEntries()
        {
            var tag = Tag("Combat"); var container = new GameplayTagContainer(); container.Add(tag); container.Clear();
            Assert.That(container.Count, Is.Zero); Assert.That(container.Has(tag), Is.False);
        }
        [Test] public void DeserializeRebuildsRuntimeLookup()
        {
            // Create every asset before warming the cache: an unrelated revision change
            // must not accidentally repair a broken deserialization callback.
            var oldParent = Tag("Old"); var oldChild = Tag("Old.Child", oldParent);
            var parent = Tag("Combat"); var child = Tag("Combat.Melee", parent);
            var incoming = new GameplayTagContainer(); incoming.Add(child);
            string serialized = JsonUtility.ToJson(incoming);
            var container = new GameplayTagContainer(); container.Add(oldChild);
            Assert.That(container.Has(oldParent), Is.True);
            Assert.That(container.ContainsExact(oldChild), Is.True);

            JsonUtility.FromJsonOverwrite(serialized, container);

            Assert.That(container.Tags, Is.EqualTo(new[] { child }));
            Assert.That(container.ContainsExact(child), Is.True);
            Assert.That(container.Has(parent), Is.True);
            Assert.That(container.ContainsExact(parent), Is.False);
            Assert.That(container.ContainsExact(oldChild), Is.False);
            Assert.That(container.Has(oldParent), Is.False);
        }
        [Test] public void EmptyQuerySetsHaveLogicalDefaults()
        {
            var container = new GameplayTagContainer();
            Assert.That(container.HasAll(), Is.True); Assert.That(container.HasAny(), Is.False); Assert.That(container.HasNone(), Is.True);
        }
        [Test] public void QueryCombinesRequiredAnyAndBlockedTags()
        {
            var required = Tag("Required"); var choice = Tag("Choice"); var blocked = Tag("Blocked");
            var query = Asset<GameplayTagQuery>();
            Set(query, "requiredTags", new[] { required }); Set(query, "anyTags", new[] { choice }); Set(query, "blockedTags", new[] { blocked });
            var container = new GameplayTagContainer(); container.Add(required);
            Assert.That(query.Evaluate(container), Is.False);
            container.Add(choice); Assert.That(query.Evaluate(container), Is.True);
            container.Add(blocked); Assert.That(query.Evaluate(container), Is.False);
        }
        [TestCase(false, false, false, false)]
        [TestCase(false, true, false, false)]
        [TestCase(true, false, false, false)]
        [TestCase(true, true, false, true)]
        [TestCase(true, true, true, false)]
        [TestCase(false, true, true, false)]
        public void QueryEnforcesEachClause(bool includeRequired, bool includeChoice, bool includeBlocked, bool expected)
        {
            var required = Tag("Required"); var choice = Tag("Choice"); var blocked = Tag("Blocked");
            var query = Asset<GameplayTagQuery>();
            Set(query, "requiredTags", new[] { required });
            Set(query, "anyTags", new[] { choice });
            Set(query, "blockedTags", new[] { blocked });
            var container = new GameplayTagContainer();
            if (includeRequired) container.Add(required);
            if (includeChoice) container.Add(choice);
            if (includeBlocked) container.Add(blocked);
            Assert.That(query.Evaluate(container), Is.EqualTo(expected));
        }
        [Test] public void QueryRejectsNullContainer() => Assert.That(Asset<GameplayTagQuery>().Evaluate(null), Is.False);
        [Test] public void DatabaseNormalizesLookupAndReportsMissingTag()
        {
            var tag = Tag("Combat.Melee"); var database = Asset<GameplayTagDatabase>();
            Set(database, "tags", new List<GameplayTag> { tag });
            Assert.That(database.TryGet(" Combat . Melee ", out var found), Is.True);
            Assert.That(found, Is.SameAs(tag)); Assert.That(database.TryGet("Missing", out _), Is.False);
        }
    }
}
