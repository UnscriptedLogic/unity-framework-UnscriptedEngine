using System;
using System.Collections.Generic;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UnscriptedEngine.BuildHandlers
{
    public interface IBuildable
    {
        void LocalPassBuildConditions<T>(T builder, out List<LocalBuildCondition> localBuildConditions);
    }

    public interface IBuilder<TBuildable, TBuildableContainer> where TBuildable : IBuildable
    {
        TBuildableContainer[] buildableContainers { get; }

        TBuildable WhenGetBuildable(TBuildableContainer buildableObject);
        void WhenCreateBuildable(int index, Vector3 position, Quaternion rotation, TBuildableContainer buildableContainer);
        void OnConditionResult(BuildResult buildResult);
    }

    public struct BuildResult
    {
        private bool passed;
        private string description;

        public bool Passed => passed;
        public string Description => description;

        public BuildResult(bool passed, string description)
        {
            this.passed = passed;
            this.description = description;
        }
    }

    public class AdminBuildCondition<T>
    {
        private string name;
        private Predicate<T> condition;
        private string onFailConditionText;
        private string onPassConditionText;

        public string Name => name;
        public Predicate<T> Condition => condition;
        public string OnFailConditionText => onFailConditionText;
        public string OnPassConditionText => onPassConditionText;

        public AdminBuildCondition(string name, Predicate<T> condition, string onFailConditionText = null, string onPassConditionText = null)
        {
            this.name = name;
            this.condition = condition;
            this.onFailConditionText = onFailConditionText;
            this.onPassConditionText = onPassConditionText;
        }
    }

    public class LocalBuildCondition
    {
        public delegate bool LocalCondition(Vector3 position, Quaternion rotation);

        private string name;
        private LocalCondition condition;
        private string onFailConditionText;
        private string onPassConditionText;

        public string Name => name;
        public LocalCondition Condition => condition;
        public string FailConditionText => onFailConditionText;
        public string PassConditionText => onPassConditionText;

        public LocalBuildCondition(string name, LocalCondition condition, string onFailConditionText, string onPassConditionText)
        {
            this.name = name;
            this.condition = condition;
            this.onFailConditionText = onFailConditionText;
            this.onPassConditionText = onPassConditionText;
        }
    }

    public class BuildHandlerSimple<TBuildable, BuildableContainer, Builder> where TBuildable : IBuildable where Builder : IBuilder<TBuildable, BuildableContainer>
    {
        public delegate GameObject BuildablePreview(int index, Vector3 position, Quaternion rotation);
        public BuildablePreview CreateBuildablePreview;
        public List<AdminBuildCondition<TBuildable>> adminBuildConditions;
        public List<BuildableContainer> buildableObjects;
        private Builder builder;

        public BuildHandlerSimple(Builder builder, List<BuildableContainer> buildableObjects)
        {
            adminBuildConditions = new List<AdminBuildCondition<TBuildable>>();
            this.builder = builder;
            this.buildableObjects = buildableObjects;
        }

        public bool AdminConditionCheck(TBuildable buildable, out BuildResult buildResult)
        {
            for (int i = 0; i < adminBuildConditions.Count; i++)
            {
                if (!adminBuildConditions[i].Condition(buildable))
                {
                    BuildResult failedBuildResult = new BuildResult(false, "Admin Condition " + (i + 1) + ": " + adminBuildConditions[i].Name + " failed.");
                    buildResult = failedBuildResult;
                    return false;
                }
            }

            BuildResult passedBuildResult = new BuildResult(true, "Admin conditions passed.");
            buildResult = passedBuildResult;
            return true;
        }

        public bool LocalConditionCheck(TBuildable buildable, Vector3 position, Quaternion rotation, out BuildResult buildResult)
        {
            buildable.LocalPassBuildConditions(builder, out List<LocalBuildCondition> localBuildConditions);

            for (int i = 0; i < localBuildConditions.Count; i++)
            {
                if (!localBuildConditions[i].Condition(position, rotation))
                {
                    BuildResult failedBuildResult = new BuildResult(false, "Local Condition " + (i + 1) + ": " + localBuildConditions[i].Name + " failed.");
                    buildResult = failedBuildResult;
                    return false;
                }
            }

            BuildResult passedBuildResult = new BuildResult(true, "Local conditions passed!");
            buildResult = passedBuildResult;
            return true;
        }

        public void Build(int index, Vector3 position, Quaternion rotation, Action<BuildResult> BuildResult, bool doAdminPass = true, bool doLocalPass = true)
        {
            TBuildable buildable = builder.WhenGetBuildable(buildableObjects[index]);

            if (doAdminPass)
            {
                if (!AdminConditionCheck(buildable, out BuildResult buildResult))
                {
                    BuildResult?.Invoke(buildResult);
                    return;
                }
            }

            if (doLocalPass)
            {
                if (!LocalConditionCheck(buildable, position, rotation, out BuildResult buildResult))
                {
                    BuildResult?.Invoke(buildResult);
                    return;
                }
            }

            BuildResult passedBuildResult = new BuildResult(true, "All Conditions Passed!");
            BuildResult?.Invoke(passedBuildResult);

            builder.WhenCreateBuildable(index, position, rotation, buildableObjects[index]);
        }
    }
}