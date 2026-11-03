"""V062 map/camera hint exclusion through the isolated native OpenXR simulator.

Start the simulator as documented in Test-XRV058.py with a fresh acceptance
session, then run this file. Real controller poses/buttons drive production
code. This does not test Android file ownership or headset stereo comfort.
"""
import importlib.util
import math
from pathlib import Path

spec = importlib.util.spec_from_file_location('xr059', Path(__file__).with_name('Test-XRV059.py'))
base = importlib.util.module_from_spec(spec)
spec.loader.exec_module(base)


def look_at_left(checks):
    state = checks.read('state.json')
    for _ in range(3):
        delta = base.rotate(base.subtract(base.xyz(state['left']), base.xyz(state['camera'])),
                            state['originEuler'], inverse=True)
        state = checks.send(headEuler=dict(x=-math.degrees(math.atan2(delta[1], math.hypot(delta[0], delta[2]))),
                                           y=math.degrees(math.atan2(delta[0], delta[2])), z=0))
    return state


def run(checks):
    state = checks.send(**base.NEUTRAL, fixture='ShoreFirstPerson', equip='FieldGuide',
                        applicationPaused=False, head=dict(x=0, y=1.65, z=0), headEuler=base.ZERO,
                        left=base.NORMAL_LEFT, right=base.NORMAL_RIGHT,
                        leftEuler=base.ZERO, rightEuler=base.ZERO)
    required = {'palmMapShown', 'palmMapVisible', 'leftHintVisible', 'rightHintVisible',
                'cameraVisible', 'albumDirectory', 'lastPhotoPath'}
    assert required.issubset(state), ('Recompile the V062 simulator with optical diagnostics', required-set(state))
    state = look_at_left(checks)
    assert state['leftHintVisible'] and not state['palmMapVisible'], state
    checks.remember('controllerHintsInitiallyVisible', state)

    state = checks.send(leftEuler=dict(x=0, y=0, z=90), seconds=.6, capture='v062-palm-map-clear')
    assert state['palmMapShown'] and state['palmMapVisible'] and not state['leftHintVisible'], state
    checks.remember('palmMapExcludesLeftControllerHint', state)
    state = checks.send(leftEuler=base.ZERO, frames=1)
    if state['palmMapVisible']:
        assert not state['leftHintVisible'], 'The tag returned before the chart finished fading'
    checks.remember('palmMapFadeKeepsHintHidden', state)
    state = checks.send(seconds=.6)
    assert not state['palmMapShown'] and not state['palmMapVisible'] and state['leftHintVisible'], state
    checks.remember('leftHintReturnsAfterChartStows', state)

    state = checks.send(headEuler=base.ZERO, right=dict(x=0, y=1.65, z=.3), seconds=.3)
    assert state['cameraVisible'] and not state['cameraRaised'], state
    assert not state['leftHintVisible'] and not state['rightHintVisible'], state
    checks.remember('nearEyeCameraPropAlsoClearsControllerTags', state)
    state = checks.send(right=dict(x=0, y=1.65, z=.1), seconds=.4, capture='v062-camera-clear')
    assert state['cameraRaised'] and not state['leftHintVisible'] and not state['rightHintVisible'], state
    before = state['photosTaken']
    state = checks.send(rightTrigger=1, seconds=.3)
    assert state['photosTaken'] == before+1 and not state['notebook'], state
    path = Path(state['lastPhotoPath'])
    assert path.is_file() and path.stat().st_size > 1000, ('The native shutter did not persist a JPG', state)
    assert path.parent == Path(state['albumDirectory']), state
    checks.remember('nativePhotoSavedInIsolatedAlbum', state)
    state = checks.send(seconds=.3)
    assert state['photosTaken'] == before+1, 'A held shutter took another photo'
    checks.send(rightTrigger=0, right=base.NORMAL_RIGHT)
    state = look_at_left(checks)
    assert not state['cameraVisible'] and not state['cameraRaised'] and state['leftHintVisible'], state
    checks.remember('hintsReturnAfterStowingCamera', state)


def main():
    checks = base.NativeV059Checks(base.ROOT / 'Logs/V062/NativeXR/native-optics.json', 30)
    try:
        run(checks)
        checks.save(True)
        print('V062 OPTICS NATIVE PASS: palm map/camera clear hints and isolated shutter persists once')
    except BaseException as error:
        checks.save(False, error)
        raise
    finally:
        try:
            checks.send(**base.NEUTRAL, leftEuler=base.ZERO, right=base.NORMAL_RIGHT, rightEuler=base.ZERO)
        except (AssertionError, OSError, RuntimeError, TimeoutError):
            pass


if __name__ == '__main__':
    main()
