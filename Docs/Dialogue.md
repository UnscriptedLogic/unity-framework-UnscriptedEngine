# Dialogue

Dialogue assets and sessions live in `UnscriptedEngine.Dialogue` without UI dependencies. Presenters and the scene runner live in `UnscriptedEngine.UI.Dialogue` and require uGUI/TextMeshPro. Each runner creates its own session; reusable assets never store progress.

## Try the example

Use **Tools > UnscriptedEngine > Dialogue > Create Demo** in an empty scene, then enter Play Mode. The example creates a camera if needed, an NPC, input/event system if needed, and three buttons for bottom-panel dialogue, world bubbles, and timed chatter. Continue first reveals the complete text, then advances. Both interactive views use the same branching asset. Switching modes cancels the previous conversation.

Demo scripts are under `Assets/Prototypes/Dialogue`, in their own namespace and assemblies. All generated objects and transient dialogue assets are cleaned up with the demo. Import TMP Essential Resources if the project has no default TMP font.

## Author a conversation

### Overhead greetings on an existing pawn

Select a scene pawn (or open its prefab in Prefab Mode), then choose **Tools > UnscriptedEngine > Dialogue > Create Overhead Speech Bubble**. Set the greeting and optional head anchor, click **Create and Wire Speech Bubble**, and choose a new conversation asset path.

The tool creates a world-space bubble with a name label, text, and tail; a timed `DialogueRunner`; and a concrete `SpeechBubbleInteraction` component on the pawn. It reuses existing `UInteractableComponent` and `DialogueSpeaker` components. Without an existing or explicitly assigned anchor it places a new anchor above the pawn's renderer bounds. Edit the anchor Transform to adjust placement. Edit the bubble RectTransform/scale, colors and TMP settings to change appearance. The presenter and runner stay enabled; the content child starts hidden.

`SpeechBubbleInteraction` subscribes to `UInteractableComponent.InteractedLocally`, so speech starts only on the initiating client (or offline) after successful interaction. It adds no gameplay blocking and does not require a possessed player/controller for timed greetings. Repeated interaction during the same greeting is ignored; interaction after completion plays it again. Disabling the component unsubscribes and cancels only the session it started. `Speak()` and `CancelSpeech()` also work from UnityEvents; `TrySpeak()` reports acceptance to game code.

Keep specific interactions as separate concrete components: for example, put `ShopNpcInteraction` and `SpeechBubbleInteraction` beside each other on the same shop pawn. The shop continues handling authoritative trading while speech handles local presentation. Neither component needs a dependency on the other. This greeting triggers on successful **interaction**, not on successful shop eligibility/session opening; use an explicit shop adapter or event when dialogue must depend on that result. The tool leaves existing interaction distances, repeat policy, event listeners and shop wiring untouched; set Repeatable on the interactable if repeated greetings are needed.

Scene setup supports Undo. The greeting asset remains reusable after Undo and can be removed manually if unwanted. Running the tool twice on the same pawn is rejected; edit the existing component instead. Use one bubble/runner per pawn so different speakers can talk concurrently. Generated greetings have no choice buttons: use the full world-view setup below for branching conversations.

Existing world visuals remain local to each client; this does not broadcast a greeting to nearby observers. Keep pawn transforms at unit scale when possible so world-space bubble size stays predictable.

### Full conversations

1. Create an asset through **Assets > Create > UnscriptedEngine > Dialogue > Conversation**.
2. Add lines, starting at index 0. Each line contains speaker ID and text. `Next Line = -2` advances to the following line; `-1` ends; any nonnegative value jumps to that line index.
3. Add optional choices. Each choice has text and destination index (`-1` ends). A line with choices requires selection and ignores ordinary advance after reveal.
4. Add `DialogueSpeaker` to each participant. Assign a unique ID within this conversation, name, optional portrait, and a child transform above its head. Definitions contain IDs, not scene references.
5. Add `DialogueRunner` and assign asset, presenter, and participants. `Play()`, `Advance()`, `Choose(int)`, and `Cancel()` can be called from game code or UnityEvents.

Invalid destinations, missing speakers, duplicate participant IDs, and missing choice templates are rejected before starting. Cycles are allowed. One presenter can serve only one running session at a time; use separate presenters/runners for simultaneous ambient speakers. The owning game decides how many conversations may run at once.

## Create a screen view

Put `ScreenDialoguePresenter` on a bottom-anchored RectTransform under a screen-space Canvas. Add a **child** content panel, TMP line label, optional name/portrait, optional Continue button, and an inactive choice Button template containing a TMP label. Assign those references in the presenter. Keep the presenter enabled; it hides only its content child between conversations. A layout group on the choice parent arranges the pooled buttons.

The presenter can be shown through `UCanvasController.Show(prefab)` and passed into `DialogueRunner.Configure`. Its canvas policy is Passive; the runner owns dialogue input and an optional input-focus lease. Dialogue content can therefore remain visible alongside existing screens without taking ownership of their focus. For choices, an EventSystem/UI input module and GraphicRaycaster must exist. Use normal Unity button navigation for keyboard/gamepad selection.

## Create a world view

Use `WorldDialoguePresenter` under a **World Space** Canvas with GraphicRaycaster and an assigned event camera for clickable choices. Its RectTransform follows the current speaker's head anchor. Author bubble dimensions and scale in the prefab. Assign the viewer camera explicitly when possible; otherwise the presenter resolves `Camera.main` when showing a line.

The bubble can face the camera and hide beyond Maximum Distance or behind the viewer. Maximum Distance = 0 disables distance culling. Visibility radius appears when selected with Show Gizmos enabled. It does not perform physics occlusion checks or overlap avoidance. Materials determine whether scene geometry occludes text. Choose those behaviors for the specific game rather than adding per-NPC raycasts by default.

World dialogue requires a speaker ID on every line; narration with no speaker belongs in a screen presenter. View visibility does not pause the conversation's timer.

## Interaction and input

Wire `UInteractableComponent`'s **On Button Interacted Locally With Source** event to `DialogueRunner.PlayForInteractor(GameObject)`. The runner resolves the interacting pawn's controller for action input/focus. Use the local completion event, not the replicated all-client event, so only the initiating player opens the conversation. The interaction target still needs the normal framework interaction setup.

Enable Block Gameplay for an interactive conversation. Its lease blocks only Input Owner; without an owner it blocks globally. It does not pause simulation or change time scale. Ambient dialogue normally uses Automatic Advance without Block Gameplay. Automatic Delay measures unscaled seconds after reveal. Choice lines always wait for a selection.

For direct action-based advance, assign Input Owner and configure a dedicated action such as `UI/DialogueAdvance` in the project's Input System setup. The default key does not create that action. Avoid sharing the EventSystem's Submit action with dialogue advance: Submit should activate focused buttons, while the dedicated action reveals/advances lines. Existing higher-priority canvas input can consume an action first. Button-only examples need no framework input owner.

No voice playback, localization provider, save/load, condition evaluator, or network replication is included. Multiplayer interaction can open local UI, but authoritative choice effects must be validated by game/server code. Completion and cancellation UnityEvents provide game integration points; `Session.LineChanged` exposes the current line during a running conversation.

## Lifetime and performance

Text is assigned once per line and revealed using TMP `maxVisibleCharacters`; choice buttons are instantiated up to the largest choice count shown, then reused. Hidden bubbles do no positioning work. Cameras and labels are cached. Runners validate all lines when starting, avoiding lookup scans during reveal.

Disabling a runner or presenter cancels its session, hides the view, removes action/presenter subscriptions, and releases only its own focus lease. Destroyed/disabled participants cancel during the next runner update. No new static state or registries are introduced, so domain-reload-disabled sessions cannot retain dialogue progress. Existing EventBus and InputFocus session resets remain in use. Restarting a runner creates a fresh session.

Keep long-lived runners with their scene participants. Cancel before unloading/replacing assets or reconfiguring presenter hierarchies. Presenter instances belong to their scene/UI owner and are reused; the runner does not destroy them.
