# Sustained Flame 3D — S0b Design Brief

This project-internal brief defines the first W24 production baseline. It is a design reference, not a texture source.

- The effect represents one continuously burning, locally anchored flame.
- It has a short ignition, an indefinitely sustainable steady burn, a graceful stop, and a visibly distinct interrupt exit.
- The steady burn keeps a readable hot core, a subordinate outer flame, sparse smoke, and sparse detached embers. These layers must not collapse into one rotating picture.
- Motion is turbulent and statistically stable: it may vary frame to frame, but it must not drift, pulse as one synchronized card, or expose a loop seam.
- A real budgeted point light illuminates a separate scene receiver. Additive flame brightness is not evidence of lighting.
- Stop and interrupt both end with bounded cleanup. A stopped effect cannot retain particles or light indefinitely.
- The Runtime Entry starts invisible. Preview looping belongs only to the preview scene.
- The implementation uses a procedural shader and no runtime PNG texture for this baseline.

The visual target is a readable game-scale MVP, not photorealism, cinematic post-processing, or a fixed copied flame silhouette. Final commercial-quality approval remains a user L4 decision.
