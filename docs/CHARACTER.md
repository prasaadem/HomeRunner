# Character and UI upgrade

The runtime now uses a screen-space Canvas and CanvasScaler with dynamic text instead of the fixed OnGUI debug interface. Floating room labels were removed. This change has not been compiled or visually tested in Unity; verify menus, narrow/wide Game views, restart, pause and keyboard controls in Unity 6.6.

The environment and fallback character are still prototype primitives. No realistic character, textures or animation clips are included in this update.

## Production character prefab

Save a prefab at Assets/Resources/HomeRunner/Runner.prefab. Its root should have identity transform, feet at y=0, forward along +Z, and human scale (approximately 1.8 meters). It should contain a skinned mesh, materials compatible with the active render pipeline, and an Animator with an assigned controller. The loader disables root motion because gameplay owns movement.

Optional controller parameters: Speed (float, meters/second), Grounded (bool), Sliding (bool), Dead (bool). Match these types exactly. Provide idle, run, jump/landing, slide and defeat states and transitions. Pause freezes the Animator. No automatic rigging or clip generation is performed.

A realistic result requires authored anatomy, skin/hair/clothing textures, proper skin weights and animation clips. Higher screen resolution alone cannot supply these assets. The current room props, lighting and materials also require a separate art pass.
