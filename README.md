# Playtest Notes

Capture feedback while you play. Press **Capture**: the game pauses, a clean screenshot is taken, and the microphone starts recording. Talk, press **Stop**, pick a category, **Submit**. Each note is saved as a folder with the screenshot, the audio, where you were and what the game was doing. An `index.md` lists every note and is written to be handed to Claude.

Unity 6.x, Input System, any render pipeline. The capture code exists only in the Editor and development builds; release builds compile it out.

## Install

The package has no third-party dependencies. Pick one:

- **Embedded:** copy the `com.theworststudio.playtestnotes` folder into your project's `Packages/` folder.
- **Git:** Package Manager > **+** > *Add package from git URL*: `https://github.com/codyrchildress/Unity-Playtest-Notes.git`. Add `#<tag or commit>` to pin a version. For a private repository, git on your machine needs access to GitHub (for example through Git Credential Manager).
- **Local folder:** Package Manager > **+** > *Add package from disk* > `package.json`.

The project must use the Input System, with *Active Input Handling* set to *Input System Package* or *Both*.

## Setup

1. Add a host to your first scene. Use **GameObject > Playtest Notes Host**, or import the **Demo** sample (Package Manager > Playtest Notes > Samples) and drag in `Playtest Notes.prefab`.
2. Optional: create a settings asset with **Assets > Create > Playtest Notes > Settings** and assign it to the host. Without one the host uses the defaults.
3. Optional: set the host's **Tracked Target** to your player. Alternatively, implement `IPlaytestTarget` (see below).
4. Enter Play Mode and press **F8**.

The host keeps itself across scene loads (**Persist Across Scenes**; it must be a root object). If a second host appears, it removes itself.

The **Demo** sample has a scene where a capsule walks around (WASD or left stick). It shows the target and metadata interfaces. The scene builds its level at runtime, so it renders in URP, HDRP or Built-in.

## Using it

| Step | Keyboard | Gamepad | Touch |
|---|---|---|---|
| Capture (pause, screenshot, start recording) | F8 | Hold LB + RB (L1 + R1) for 1 second | Hold three fingers on the screen for 1 second |
| Stop recording | F8 or Enter | A / Cross, or LB + RB | **Stop** button |
| Submit | Enter | A / Cross | **Submit** |
| Re-record | R | X / Square | **Re-record** |
| Delete | Delete or Backspace | B / Circle | **Delete** |
| Next category | Tab | RB / R1 | Tap a category |

- Recording stops by itself after **Max Recording Seconds** (default 60).
- Categories: None, Bug, Idea, Polish, Other.
- Submit and Delete both resume the game. They restore the previous `Time.timeScale`, `AudioListener.pause` and cursor lock state.
- The overlay runs on unscaled time and stays inside the screen's safe area. Its buttons show the key and gamepad hints for devices that are connected.
- A game can start a capture itself, for example from a debug-menu button, with `PlaytestNotesHost.Capture()`. `IsCapturing` says whether a capture is in progress.

### Changing bindings

All bindings live in the settings asset as Input System actions. Edit them in the Inspector like any `InputAction`. You can add bindings, remove them, or use composites such as *One Modifier* for chords. The touch gesture is **Touch Capture Fingers** (0 turns it off).

The defaults stay clear of the SRP **Rendering Debugger** (URP and HDRP), which is active in the Editor and in development builds. It opens on L3 + R3, Ctrl + Backspace, or a three-finger double tap. That is why Capture uses both shoulder buttons, and a three-finger *hold* rather than a tap. The one-second hold on the shoulders also keeps a game's own shoulder presses from starting a capture. In the settings asset it is a *One Modifier* composite (modifiers in any order) with a *Hold* interaction; change the duration or buttons there.

While the overlay is up, the game is paused but its own input actions are still enabled. If your game reacts to the gamepad's A or B buttons while paused (for example, B opens a menu), give Stop/Submit/Delete other buttons, or check `PlaytestNotesHost.IsCapturing` in your pause logic.

## What a note contains

Notes are saved under the **save root**: `<project>/PlaytestNotes` in the Editor, and `<Application.persistentDataPath>/PlaytestNotes` in builds. To change it, set **Save Root** in settings, either as an absolute path or relative to those base folders.

```
PlaytestNotes/
  notes/
    index.md
    20261021T153012Z_ab12cd/
      shot.png          the frame Capture was pressed on, before any overlay was drawn
      audio.wav         16-bit PCM, 16 kHz mono where the microphone supports it
      note.json         everything below
      transcript.txt    written by the transcription step
```

`note.json` holds:

- id, UTC timestamp, category, resolved flag
- scene name and path, session id (one per app launch or Play Mode session)
- tracked target: name, source, position, rotation (quaternion and Euler)
- camera: name, position, rotation, FOV or orthographic size
- build version, Unity version, development flag
- platform, device model, OS, GPU, screen size
- FPS at capture, audio length
- `metadata`, a string dictionary filled by your providers

Fields that are missing when read take their defaults, so older notes stay readable.

Add `PlaytestNotes/` to `.gitignore` unless you want notes in version control.

## Extending

### Tracked target

The host looks for a target in this order:

1. its **Tracked Target** transform;
2. the first enabled `IPlaytestTarget` in the loaded scenes;
3. the main camera.

```csharp
using PlaytestNotes;
using UnityEngine;

public class Player : MonoBehaviour, IPlaytestTarget
{
    public Pose PlaytestPose => new Pose(transform.position, transform.rotation);
}
```

### Game metadata

Implement `IPlaytestMetadataProvider` on any MonoBehaviour. Every enabled provider in the loaded scenes, and in the host's DontDestroyOnLoad scene, is called once per capture, in the captured frame. Prefix your keys with your system's name. If a provider throws, the exception is logged and the note is still saved.

```csharp
using System.Collections.Generic;
using PlaytestNotes;
using UnityEngine;

public class QuestLog : MonoBehaviour, IPlaytestMetadataProvider
{
    public void CollectPlaytestMetadata(IDictionary<string, string> metadata)
    {
        metadata["quest.active"] = currentQuest.Id;
        metadata["quest.step"] = currentStep.ToString();
        metadata["player.health"] = health.ToString();
    }
}
```

`IPlaytestTarget`, `IPlaytestMetadataProvider`, `PlaytestNotesHost` and `PlaytestNotesSettings` are in the `PlaytestNotes.Api` assembly. It compiles into every build, so game code that implements these interfaces builds for release too. In release builds the interfaces are simply never called.

### Transcription

`ITranscriber` (in `PlaytestNotes.Capture`) turns `audio.wav` into text:

```csharp
public interface ITranscriber : IDisposable
{
    string DisplayName { get; }
    Task<string> TranscribeAsync(string wavPath, CancellationToken cancellationToken);
}
```

**Playtest Notes > Transcribe Pending Notes** creates the first implementation it finds. The class needs a public parameterless constructor, and ties are broken by full type name. The menu item runs over every note that has audio but no `transcript.txt`, as a background task you can cancel, and then rebuilds `index.md`. It stops at the first error. An empty transcript means no speech was detected.

**Built-in: whisper.unity (local, no network).** The `PlaytestNotes.Whisper.Editor` assembly compiles only when [whisper.unity](https://github.com/Macoron/whisper.unity) is installed:

1. Package Manager > **+** > *Add package from git URL*: `https://github.com/Macoron/whisper.unity.git?path=/Packages/com.whisper.unity`
2. Download a ggml model, for example `ggml-base.en.bin` from <https://huggingface.co/ggerganov/whisper.cpp>, to `<project>/PlaytestNotes/Models/`. Keep it outside `Assets/` so it never ships in a build.
3. In the settings asset, set **Whisper Model Path** (relative to the project folder) and **Whisper Language** (`en`, or `auto` to detect it).
4. Run **Playtest Notes > Transcribe Pending Notes**.

Transcription runs in the Editor only. Notes captured on a device get transcribed after you copy them into the project (see below).

## Release builds

The capture code (`PlaytestNotes.Capture`) compiles only when this define constraint holds:

```
UNITY_EDITOR || DEVELOPMENT_BUILD || PLAYTEST_NOTES
```

- **Editor and Development builds:** on.
- **Release builds:** off. Add `PLAYTEST_NOTES` to *Player Settings > Scripting Define Symbols* to ship it in a release build anyway, for example to external testers.
- **Off everywhere,** including the Editor tools: add `PLAYTEST_NOTES_DISABLE`.

When the capture code is compiled out, `PlaytestNotesHost` stays in place and does nothing, so scenes and prefabs never get missing-script warnings. Nothing in a release build references `Microphone`, so Unity adds no microphone permission to it.

## Mobile and handhelds

- **Touch:** holding three fingers on the screen for one second captures. The overlay buttons are at least about 90 px tall at 1600×900 reference size.
- **Android:** the microphone permission is requested the first time you capture, and the game stays paused while the prompt is up. If permission is denied, the note keeps its screenshot and data. Development builds include `RECORD_AUDIO` in the merged manifest, because Unity adds it whenever `Microphone` is compiled in.
- **iOS:** set *Player Settings > Microphone Usage Description*. The permission is requested the same way.
- **Gamepads:** every step has a gamepad binding (see the table above).

**Getting notes off a device (Android):**

```bash
adb pull /sdcard/Android/data/<your.package.name>/files/PlaytestNotes/notes PlaytestNotes/
```

Then run **Playtest Notes > Rebuild Index**. Note folders from any number of devices can be merged into one `notes/` folder.

## Editor tools

- **Playtest Notes > Notes Window** lists notes, newest first. Each row has:
  - a thumbnail (click to open the full screenshot);
  - category, editable;
  - transcript and metadata;
  - a **Resolved** toggle;
  - **Frame in Scene**, **Play audio**, **Show folder** and **Delete**.

  Edits are written back to `note.json` and the index is rebuilt.
- **Scene view pins:** a pin is drawn at each note's position for notes captured in an open scene, colored by category. Resolved notes appear faded, or hidden when *Show resolved* is off. Clicking a pin puts the Scene view camera exactly where the game camera was, with the same FOV. Toggle pins with **Playtest Notes > Show Scene Pins**.
- **Rebuild Index** and **Open Notes Folder** are in the same menu.

The Editor tools use the first `PlaytestNotesSettings` asset in the project (by path) to find the save root.

## Handing notes to Claude

`notes/index.md` is written for this. Each entry lists:

- the number, category, scene and time;
- a relative link to the screenshot;
- the position and camera;
- build, device and FPS;
- your metadata;
- the transcript.

All paths are relative, so the folder works anywhere.

1. Play, capture notes, then run **Transcribe Pending Notes**.
2. In Claude Code, from your project folder:

   ```
   Read PlaytestNotes/notes/index.md and the screenshots it links. For each open note, find the scene objects and code involved, and propose a fix. Start with Bugs. Don't change anything yet; give me a list.
   ```

   Claude Code can open the linked `shot.png` files and each `note.json` (for exact poses and metadata) on its own. For Claude on the web or desktop, zip the `notes/` folder or attach `index.md` plus the screenshots you care about.
3. Mark notes **Resolved** in the Notes window as you fix them. The index shows the open/resolved count, and you can ask Claude to work only on open notes.

Transcripts are speech-to-text and can mishear words. The index says so, so Claude treats them as approximate.

## Assemblies

| Assembly | Compiles in | Contains |
|---|---|---|
| `PlaytestNotes.Api` | everything | interfaces, `NoteCategory`, settings, host |
| `PlaytestNotes.Capture` | Editor, development builds, `PLAYTEST_NOTES` | capture flow, overlay, microphone, storage, `ITranscriber` |
| `PlaytestNotes.Editor` | Editor | window, pins, menus, transcription runner |
| `PlaytestNotes.Whisper.Editor` | Editor, with `com.whisper.unity` | `WhisperTranscriber` |
| `PlaytestNotes.Tests.Editor` | Editor tests | JSON, WAV, storage and index tests |
