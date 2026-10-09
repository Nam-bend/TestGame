# TestGame

Unity football gameplay project based on the original
[sixs-unity-dev-test](https://gitlab.com/luongnd28200/sixs-unity-dev-test) repository.

## Run

1. Open this project in Unity **6000.3.21f1**.
2. Open `Assets/Scenes/Location soccer field.unity`.
3. Enter Play Mode. Move with WASD or arrow keys; use the Kick, Auto Kick and Reset buttons.

## Validation

Save the scene, then use **Tools > Jammo > Test Current Scene** to run the gameplay checks.
The check enters Play Mode, moves objects and reloads the scene.

The repository includes `Assets` (with `.meta` files), `Packages` and `ProjectSettings`.
Unity caches, local logs, generated builds and validation copies are excluded.

## Windows standalone

Use **Tools > Jammo > Build Windows**. The output is `Builds/Windows/JammoFootball.exe`.
Distribute the executable together with `JammoFootball_Data`, `MonoBleedingEdge`,
`D3D12`, `UnityPlayer.dll` and `UnityCrashHandler64.exe`; do not send the executable alone.
Exclude any `*_DoNotShip` debug directory from the delivery archive.

The Windows x64 build from gameplay commit `839d2f6` completed with zero build errors.
Its standalone smoke test passed startup grounding, Auto Kick, scoring, camera
pause/return and scene reset. To repeat it, run the executable with
`-jammo-smoke-test -logFile smoke.log` (it exits automatically).

The camera uses a fixed broadcast angle. Shots follow scripted paths to the goal;
mouse orbit and physically bouncing ball flight are not implemented.
