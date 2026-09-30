# Desktop / XR input layer

HumanController is the race boat actuator and reads USVInputRouter; FishingPrototype disables it and is the sole fishing-mode actuator. Boat.Setup still creates HumanController normally.
Editor and Windows default to Desktop; Android defaults to XR. Auto is input-only, never an XR loader or camera switch.
USVInputProcessor is pure C# and tested by Tools/InputTests (18 cases). Runtime sources return held buttons; the processor emits press edges.
Commands are processed in Update so pause can be resumed while physics is stopped. Brake slows horizontal motion only.
Interaction/map events are extension points, not implemented missions or UI. Engine now accepts signed throttle: S/negative axis produces astern thrust, while Shift (gamepad left shoulder) brakes independently. Escape pauses; F is reserved for field tools in fishing mode.
No XR rig, SDK setup, camera migration, or successful Unity/device validation is implied by these files.
See Docs/INTEGRATION_STATUS.md and Docs/VR_DESKTOP_DEVICE_WORKFLOW.md.
