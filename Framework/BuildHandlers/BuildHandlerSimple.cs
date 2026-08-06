using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnscriptedEngine.BuildHandlers
{
    public interface IBuilder<TBuildable, TBuildableContainer>
        where TBuildable : IBuildable
    {
        TBuildable WhenGetBuildable(TBuildableContainer buildableContainer);

        void WhenCreateBuildable(
            int index,
            Vector3 position,
            Quaternion rotation,
            TBuildableContainer buildableContainer);
    }

    public interface IBuildable
    {
        void LocalPassBuildConditions<TBuilder>(
            TBuilder builder,
            out List<LocalBuildCondition> localBuildConditions);
    }

    public sealed class LocalBuildCondition
    {
        public LocalBuildCondition(
            string name,
            Func<Vector3, Quaternion, bool> condition,
            string failMessage,
            string passMessage)
        {
            Name = name;
            Condition = condition;
            FailMessage = failMessage;
            PassMessage = passMessage;
        }

        public string Name { get; }

        public Func<Vector3, Quaternion, bool> Condition { get; }

        public string FailMessage { get; }

        public string PassMessage { get; }
    }

    public sealed class BuildResult
    {
        public BuildResult(bool passed, IReadOnlyList<string> messages)
        {
            Passed = passed;
            Messages = messages;
        }

        public bool Passed { get; }

        public IReadOnlyList<string> Messages { get; }
    }

    public sealed class BuildHandlerSimple<TBuildable, TBuildableContainer, TBuilder>
        where TBuildable : IBuildable
        where TBuilder : IBuilder<TBuildable, TBuildableContainer>
    {
        private readonly TBuilder builder;
        private readonly IReadOnlyList<TBuildableContainer> buildableContainers;

        public BuildHandlerSimple(TBuilder builder, IReadOnlyList<TBuildableContainer> buildableContainers)
        {
            this.builder = builder;
            this.buildableContainers = buildableContainers;
        }

        public void Build(
            int index,
            Vector3 position,
            Quaternion rotation,
            Action<BuildResult> onConditionResult = null)
        {
            if (index < 0 || index >= buildableContainers.Count)
            {
                onConditionResult?.Invoke(new BuildResult(false, new[] { "Buildable index is out of range." }));
                return;
            }

            TBuildableContainer container = buildableContainers[index];
            TBuildable buildable = builder.WhenGetBuildable(container);

            if (buildable == null)
            {
                onConditionResult?.Invoke(new BuildResult(false, new[] { "Buildable component was not found." }));
                return;
            }

            buildable.LocalPassBuildConditions(builder, out List<LocalBuildCondition> localBuildConditions);

            List<string> messages = new();
            bool passed = true;

            foreach (LocalBuildCondition localBuildCondition in localBuildConditions)
            {
                bool conditionPassed = localBuildCondition.Condition(position, rotation);
                passed &= conditionPassed;
                messages.Add(conditionPassed ? localBuildCondition.PassMessage : localBuildCondition.FailMessage);
            }

            if (passed)
            {
                builder.WhenCreateBuildable(index, position, rotation, container);
            }

            onConditionResult?.Invoke(new BuildResult(passed, messages));
        }
    }
}
