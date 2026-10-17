using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnscriptedEngine.Tests
{
    public class LogServiceTests
    {
        [SetUp]
        public void SetUp()
        {
            LogService.Enabled = true;
            LogService.ClearCategoryFilter();
        }

        [TearDown]
        public void TearDown() => SetUp();

        [Test]
        public void RoutesSeverityAndIdentifiesSource()
        {
            LogAssert.Expect(LogType.Log, "[Movement] [LogServiceTests] jump");
            this.Log("jump", "Movement");
            LogAssert.Expect(LogType.Warning, "[General] [LogServiceTests] warning");
            LogService.Warning<LogServiceTests>("warning");
            LogAssert.Expect(LogType.Error, "[General] [LogService] error");
            LogService.Error(typeof(LogService), "error");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void GlobalSwitchOverridesBuildOptInForEverySeverity()
        {
            LogService.Enabled = false;
            this.Log("hidden", allowInBuild: true);
            this.Warning("hidden", allowInBuild: true);
            this.Error("hidden", allowInBuild: true);
            Assert.That(LogService.IsEnabled(allowInBuild: true), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void FilterNormalizesMatchesAndCanBeReplacedOrCleared()
        {
            LogService.SetCategoryFilter(" movement ", "Save");
            LogAssert.Expect(LogType.Log, "[MOVEMENT] [LogServiceTests] visible");
            this.Log("visible", "MOVEMENT");
            this.Error("hidden", "Audio", true);
            LogService.SetCategoryFilter("Audio");
            this.Warning("hidden", "Save");
            LogService.ClearCategoryFilter();
            LogAssert.Expect(LogType.Log, "[Save] [LogServiceTests] visible");
            this.Log("visible", "Save");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void MissingCategoryAndSourceHaveStableFallbacks()
        {
            LogService.SetCategoryFilter(" ");
            LogAssert.Expect(LogType.Log, "[General] [Unknown] message");
            LogService.Log(null, "message", null);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void UnityContextIsPreserved()
        {
            ILogHandler previous = Debug.unityLogger.logHandler;
            var capture = new ContextCapture();
            var source = new GameObject("Log source");
            try
            {
                Debug.unityLogger.logHandler = capture;
                source.Log("message");
                Assert.That(capture.Context, Is.SameAs(source));
            }
            finally
            {
                Debug.unityLogger.logHandler = previous;
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void SessionResetRestoresDefaults()
        {
            LogService.Enabled = false;
            LogService.SetCategoryFilter("Movement");
            typeof(LogService).GetMethod("ResetSessionState", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, null);
            Assert.That(LogService.IsEnabled("Audio"), Is.True);
        }

        private sealed class ContextCapture : ILogHandler
        {
            public UnityEngine.Object Context;
            public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
                => Context = context;
            public void LogException(Exception exception, UnityEngine.Object context) => Context = context;
        }
    }
}
