# Imported 3D art pass

## Activate
1. Stop Play and pull the latest main branch.
2. Wait for Unity to finish importing.
3. Choose HomeRunner > Build Character and Art. This extracts the included compressed FBX, imports its animation clips, creates a controller and saves Assets/Resources/HomeRunner/Runner.prefab.
4. Press Play in the HomeRunner scene.

The character is the Casual Hoodie mesh from Quaternius' Ultimate Modular Men Pack (CC0), with authored Idle, Run, Roll and Death clips. These are third-party assets, not original artwork created for HomeRunner. Source and attribution are under Assets/ThirdParty/Quaternius.

The 16 imported Kenney furniture meshes include kitchen appliances, sofas, tables, chairs, shelves, plants and media electronics. The runtime now loads these models for room decoration, with primitive decoration as a fallback. Kenney's license is under Assets/ThirdParty.

## What remains
This is a stylized asset integration pass, not finished Subway Surfers / Temple Run quality. It has not been compiled or rendered in Unity here. Verify model facing direction, materials, floor contact, animation transitions, pause/restart and frame rate in Unity 6.6. Generic animation preserves the supplied skeleton. Jump currently moves the model while its locomotion clip plays; a dedicated jump/landing clip, stair animation, speed matching and foot IK remain. Roll is used during the slide input. Gym and garage still need theme-specific equipment and vehicles; architectural shells and hazards remain primitive. The existing scalable UI also needs a dedicated illustrated game HUD and animation pass.

Use a 16:9 Game view for desktop review, then test a narrow view for responsive UI. Source assets are retained in GitHub in their original formats; the character FBX is losslessly compressed to reduce download size.
