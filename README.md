# HomeRunner

Unity 6 desktop infinite-runner prototype: a three-storey endless house.

## Run
1. Clone/download this repository.
2. In Unity Hub, Add project from disk and select this folder. Baseline: Unity 6000.0.40f1; use a compatible Unity 6 editor.
3. Let Unity import packages. If prompted, enable the new Input System and restart the editor. Otherwise set Project Settings > Player > Active Input Handling to Input System Package (New) or Both.
4. Choose **HomeRunner > Create Start Scene**. Press Play, then click Start running.
5. To export, use Unity Build Profiles with the generated scene included.

The bootstrap generates the scene at runtime, so the editor scene is intentionally empty outside Play mode.
This initial slice uses built-in rendering, not URP, to avoid requiring a pipeline asset. URP migration is a later art milestone.

## Controls
A/D or Left/Right: change lane. Space: jump. S: slide. Escape: pause. R: restart.
At each stair junction, left descends, center stays, right ascends. At the top/bottom limits that lane stays level.
Lane selection locks just before the stairs. The next room begins after the landing.
Orange low crates: jump; pink bars: slide; red cabinets: dodge.

## Included
- Five procedurally decorated themes across three visible floors.
- Stair treads and continuous analytical elevation transitions.
- Bounded section streaming, origin rebasing, deterministic patterns.
- Two open lanes per obstacle row; speed capped at 13 m/s.
- Original primitive-built articulated runner with procedural run, jump pose, slide and defeat pose.
- Start, pause, restart, distance and persistent best distance UI.

## Honest prototype status
Source generated and statically inspected; **not compiled or play-tested in Unity in the authoring environment**.
No packaged executable yet. Character is articulated primitives, not the final skinned mesh/rig or authored animation clips.
Decorative props have no colliders. Gameplay collisions are analytical lane/height checks.
Room patterns are deterministic, not a fully randomized constraint solver.
Streaming currently creates/destroys sections, not object pooling; optimize after profiling.
No audio, collectibles, mobile controls, final lighting, independent tests or finished art yet.

See docs/ROADMAP.md for the remaining milestones.
