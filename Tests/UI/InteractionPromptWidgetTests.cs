using TMPro;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnscriptedEngine.UI;

namespace UnscriptedEngine.Tests
{
    public sealed class InteractionPromptWidgetTests
    {
        private GameObject root;
        private GameObject first;
        private GameObject second;

        [UnitySetUp] public IEnumerator Setup()
        {
            yield return new EnterPlayMode();
            root = new GameObject("HUD", typeof(RectTransform));
            first = new GameObject("Door", typeof(UInteractableComponent));
            second = new GameObject("Lever", typeof(UInteractableComponent));
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (first != null) Object.DestroyImmediate(first);
            if (second != null) Object.DestroyImmediate(second);
            yield return new ExitPlayMode();
        }

        [Test] public void PromptDisplaysTargetAndBindingWithoutTakingInput()
        {
            InteractionPromptWidget prompt = InteractionPromptWidget.CreateDefault(root.transform);
            CanvasGroup group = prompt.GetComponent<CanvasGroup>();
            Assert.That(group.alpha, Is.Zero);

            InteractionPromptInfo info = first.AddComponent<InteractionPromptInfo>();
            JsonUtility.FromJsonOverwrite("{\"displayName\":\"Front door\",\"actionText\":\"Open\"}", info);
            prompt.SetBindingLabel("F");
            prompt.SetTarget(first.GetComponent<UInteractableComponent>());

            Assert.That(prompt.transform.Find("Panel/Action").GetComponent<TextMeshProUGUI>().text, Is.EqualTo("Press [F]  Open"));
            Assert.That(prompt.transform.Find("Panel/Target").GetComponent<TextMeshProUGUI>().text, Is.EqualTo("Front door"));
            Assert.That(group.alpha, Is.EqualTo(1f));
            Assert.That(group.blocksRaycasts || group.interactable || prompt.Focusable, Is.False);
            Assert.That(prompt.transform.Find("Panel/Progress").gameObject.activeSelf, Is.False);
        }

        [Test] public void LosingOrDisablingTargetHidesPromptAndTargetSwitchRefreshesLabels()
        {
            InteractionPromptWidget prompt = InteractionPromptWidget.CreateDefault(root.transform);
            UInteractableComponent door = first.GetComponent<UInteractableComponent>();
            UInteractableComponent lever = second.GetComponent<UInteractableComponent>();
            JsonUtility.FromJsonOverwrite("{\"interactionMode\":1}", lever);

            prompt.SetTarget(door);
            prompt.SetTarget(lever);
            Assert.That(prompt.transform.Find("Panel/Target").GetComponent<TextMeshProUGUI>().text, Is.EqualTo("Lever"));
            Assert.That(prompt.transform.Find("Panel/Action").GetComponent<TextMeshProUGUI>().text, Does.StartWith("Hold"));
            Assert.That(prompt.transform.Find("Panel/Progress").gameObject.activeSelf, Is.True);

            lever.enabled = false;
            prompt.SetTarget(lever);
            Assert.That(prompt.GetComponent<CanvasGroup>().alpha, Is.Zero);
            Assert.That(prompt.Target, Is.Null);

            prompt.SetTarget(door);
            Object.DestroyImmediate(first);
            first = null;
            prompt.SetTarget(null);
            Assert.That(prompt.GetComponent<CanvasGroup>().alpha, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator HoldProgressTracksCurrentTargetAndReleasesOldSubscription()
        {
            InteractionPromptWidget prompt = InteractionPromptWidget.CreateDefault(root.transform);
            UInteractableComponent door = first.GetComponent<UInteractableComponent>();
            UInteractableComponent lever = second.GetComponent<UInteractableComponent>();
            JsonUtility.FromJsonOverwrite("{\"interactionMode\":1,\"duration\":10}", door);
            JsonUtility.FromJsonOverwrite("{\"interactionMode\":1,\"duration\":10}", lever);

            prompt.SetTarget(door);
            Assert.That(door.TryBeginInteraction(root), Is.True);
            Assert.That(lever.TryBeginInteraction(root), Is.True);
            float deadline = Time.realtimeSinceStartup + 1f;
            while (lever.Progress01 == 0f && Time.realtimeSinceStartup < deadline) yield return null;

            Image fill = prompt.transform.Find("Panel/Progress/Fill").GetComponent<Image>();
            Assert.That(fill.fillAmount, Is.GreaterThan(0f));
            prompt.SetTarget(lever);
            float current = fill.fillAmount;
            Assert.That(current, Is.EqualTo(lever.Progress01).Within(.0001f));

            door.ResetProgress();
            Assert.That(fill.fillAmount, Is.EqualTo(current), "Previous target must no longer update the prompt.");

            prompt.enabled = false;
            Assert.That(prompt.Target, Is.Null);
            lever.ResetProgress();
            Assert.That(fill.fillAmount, Is.EqualTo(current), "Disabled widget must release progress subscription.");
        }
    }
}
