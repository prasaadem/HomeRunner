# HomeRunner roadmap

## Milestone 1: generated prototype (source delivered, runtime verification pending)
- Unity bootstrap and scene creation menu
- Three lanes, jump/slide, camera, restart and score
- Five room theme kits, three floors, committed stair routes
- Procedural articulated placeholder character

## Next: Unity verification
- Import with Unity 6; fix compile/import errors before expanding content.
- Confirm new Input System is enabled.
- Play all nine source-floor/lane combinations.
- Confirm section boundaries preserve elevation and camera continuity.
- Test jump/slide/dodge, low-frame-rate behaviour, pause, death and restart.
- Run 20 minutes; profile retained objects and generation spikes.
- Check visibility under upper floors; adjust camera/occlusion.
- Verify player cannot intersect decor while in either outer lane.

## Milestone 2: generation and performance
Extract controller, room generator, art factory, UI and route graph into separate components.
Object pools; seeded weighted room variants; explicit socket compatibility and route validator.
Unity EditMode tests for connections, bounds and reproducibility; PlayMode movement and soak tests.

## Milestone 3: art and character
Original modeled props rather than mostly boxes; garage car, kitchen island, recliners, gym bench.
Blender-generated skinned character, humanoid skeleton and exported animation clips.
Stair gait, landing transitions, foot IK and camera occlusion.
URP lighting/material pass after baseline performance measurement.

## Milestone 4: game loop and release
Collectibles, sound, effects, touch controls, settings, accessibility and difficulty tuning.
Profile on target hardware. Produce and verify desktop build.
