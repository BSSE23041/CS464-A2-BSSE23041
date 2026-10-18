# CS464 Assignment 02 · Fasiha Rohail · BSSE23041

- **Video:** [add your YouTube (unlisted) or Google Drive link here]
- **Input:** Legacy Input with Active Input Handling set to Both. All keys are read in `Update`.
- **Ground check:** `Physics.CheckSphere` at the feet of the player (radius 0.2 m). Only the Ground layer is in the mask, so the player's own collider is never hit. The sphere is drawn as a gizmo, green when grounded and red in the air.
- **Camera:** Exact smoothing in `LateUpdate` with `1 - e^(-sharpness * dt)`, plus look-ahead from the player's speed. The camera looks at the point it is aiming at, not at the player.
- **Juice:** A squash on landing, done in a coroutine with `Ease.OutBack`. It starts only when Jerry lands after more than 0.1 s in the air, and it ends at exactly the normal scale.

## Tuning

| Setting | Value | Why this number |
|---|---|---|
| Speed | 5 m/s | Jerry is running away from Tom, so he needs to feel quick. But 5 m/s is still slow enough to line up a jump before an obstacle. |
| Jump height | 1.8 m | High enough to clear every obstacle in my three levels with some room left, and not so high that he floats over everything. |
| Time to apex | 0.61 s | This comes from the jump height: 5.94 m/s divided by 9.81 gives about 0.61 s. |
| Fall multiplier | 2.2x | The jump goes up for 0.61 s but comes down in 0.41 s. Jerry lands fast, which helps when obstacles are close together. |
| Coyote time | 0.1 s | At 5 m/s that is 0.5 m past the edge. If Jerry runs off an obstacle and presses jump a little late, it still works. |
| Jump buffer | 0.12 s | A press up to 0.12 s before landing still jumps on landing. This helps in level 5 where obstacles are close and jumps follow each other. |
| Camera smoothing | sharpness 5 | The camera trails Jerry by about 1 m at 5 m/s (speed divided by sharpness). It feels smooth but still keeps up, and it looks the same at any frame rate. |
| Look-ahead | 0.35 s | 0.35 s at 5 m/s is 1.75 m, so the camera looks a bit ahead of Jerry and the player can see what is coming. |

## Four answers

1. **The jump, in numbers.** The jump height is 1.8 m and gravity is 9.81, so the launch speed is v = sqrt(2 x 9.81 x 1.8), which is about 5.94 m/s. The mass is 1 kg, so the jump force (mass x v) is also 5.94. It takes about 0.61 s to reach the top. With the fall multiplier of 2.2 the way down takes about 0.41 s, so a whole jump lasts about 1.02 s. At 5 m/s that carries Jerry about 5.1 m forward. My levels have no gaps, only obstacles to jump over, and the jump goes 1.8 m high, so it clears all of them.
2. **Update and FixedUpdate.** In `Update` the script reads the keys, works out the move direction from the camera, checks the ground, keeps the coyote and buffer timers, queues the jump and starts the squash. In `FixedUpdate` it sets the velocity from the move direction (keeping the up and down speed), turns Jerry to face where he is going, does the queued jump once, and adds the extra gravity when falling. If I swapped them, the jump key would often be missed, because a key press only lasts one frame and `FixedUpdate` does not run on every frame (at 144 fps about two frames in three have no `FixedUpdate`). The physics would also be mixed up with the frame rate.
3. **Vector3.Dot.** I did not use `Vector3.Dot` in my code. What I really used was `Vector3.ClampMagnitude` (so diagonal movement is not faster), `Vector3.Lerp` and `LerpUnclamped` (camera and squash), `Quaternion.Slerp` and `LookRotation` (turning Jerry), `Mathf.Exp` (camera smoothing), `Mathf.Sqrt` (jump speed) and `Physics.CheckSphere` (ground check). Where it could go: `Vector3.Dot(transform.forward, move)`. If the result is positive, Jerry is moving the way he is facing. If it is negative, he is moving backwards. If it is close to 0, he is moving sideways. It could be used for a different animation or sound when he moves backwards.
4. **The juice.** The effect is a squash on landing, and the curve is `Ease.OutBack`. It lasts 0.2 s, which is 12 frames at 60 fps. The first quarter (about 3 frames) squashes Jerry down, and the other 9 frames spring him back with a small overshoot. I kept it short because in level 3 the jumps come quickly, and a longer squash would still be playing when Jerry jumps again. It is still long enough to see.

## Levels

| Level | What the controller must do there | What I changed for this jump |
|---|---|---|
| Level03 | Run to the hiding spot and jump over a few simple obstacles. There is plenty of space, so this is where the player learns the controls. | Nothing. The jump already clears every obstacle. |
| Level04 | Jump over more obstacles in a tighter space and reach more than one hiding spot, using the camera to see ahead. | Nothing. The jump already clears every obstacle. |
| Level05 | Jump over many obstacles in a row in a small space and get to the hiding spots. The coyote time and jump buffer help here because the jumps follow each other quickly. | Nothing. The jump already clears every obstacle. |
