"""Native OpenXR regression for both nearby-interaction menu routes and the camera.

Start FishingXRSimulator with the explicit isolated acceptance and PICO quality
flags documented in Test-XRV058.py, then run this script. No ordinary saves.
"""
import importlib.util
import math
from pathlib import Path

spec = importlib.util.spec_from_file_location('xr059', Path(__file__).with_name('Test-XRV059.py'))
base = importlib.util.module_from_spec(spec)
spec.loader.exec_module(base)


def look_at(checks, target):
    state = checks.read('state.json')
    for _ in range(3):
        delta = base.rotate(base.subtract(target, base.xyz(state['camera'])), state['originEuler'], inverse=True)
        state = checks.send(headEuler=dict(x=-math.degrees(math.atan2(delta[1], math.hypot(delta[0], delta[2]))),
                                          y=math.degrees(math.atan2(delta[0], delta[2])), z=0))
    return state


def open_nearby(checks, page):
    checks.open_home()
    state = checks.click(index=6)
    assert state['page'] == 9, state
    if page == 6:
        state = checks.click(index=4)
        assert state['page'] == 6, state
    return state


def aim_with_palm(checks, page, position):
    index = 4 if page == 6 else 5
    for _ in range(3):
        state = checks.move_palm('right', position)
        delta = base.rotate(base.subtract(base.xyz(checks.button(state, index=index)['center']), base.xyz(state['right'])),
                            state['originEuler'], inverse=True)
        state = checks.send(rightEuler=dict(x=-math.degrees(math.atan2(delta[1], math.hypot(delta[0], delta[2]))),
                                           y=math.degrees(math.atan2(delta[0], delta[2])), z=0))
    assert base.distance(base.xyz(state['rightPalm']), position) < .1, state
    return state


def nearby(checks, page, pickup):
    checks.send(**base.NEUTRAL, pickup=-1, fixture='ShoreFirstPerson', equip='FieldGuide',
                head=dict(x=0, y=1.65, z=0), headEuler=base.ZERO,
                left=base.NORMAL_LEFT, right=base.NORMAL_RIGHT, leftEuler=base.ZERO, rightEuler=base.ZERO)
    state = checks.send(pickup=pickup)
    state = look_at(checks, base.xyz(state['pickupPosition']))
    assert state['pickupTarget'] == pickup and not state['pickupCollected'], state
    before = state['pickupBagCount']
    open_nearby(checks, page)
    index = 4 if page == 6 else 5
    state = checks.click(index=index)
    assert not state['notebook'] and not state['pickupCollected'] and state['pickupBagCount'] == before, state
    checks.remember('page'+str(page)+'RejectsRemotePickup', state)
    open_nearby(checks, page)
    # Keep the palm beside the world item while pointing the ray at the menu.
    # The standard menu helper returns both hands to their chest-height poses.
    state = checks.read('state.json')
    state = aim_with_palm(checks, page, base.add(base.xyz(state['pickupPosition']), (0, .05, 0)))
    assert base.distance(base.xyz(state['rightPalm']), base.xyz(state['pickupPosition'])) < .2, state
    state = checks.send(rightTrigger=1, leftStick=dict(x=0, y=.8))
    assert state['nearbyPending'] and not state['motionReady'] and not state['pickupCollected'], state
    checks.send(rightTrigger=0, seconds=.3)
    checks.remember('page'+str(page)+'WaitsForNeutralInput', state)
    if page == 6:
        checks.send(rightActive=False)
        state = checks.send(rightActive=True, leftStick=dict(x=0, y=0))
        assert not state['nearbyPending'] and not state['pickupCollected'], state
        checks.remember('trackingLossCancelsQueuedNearbyAction', state)
        open_nearby(checks, page)
        state = aim_with_palm(checks, page, base.add(base.xyz(state['pickupPosition']), (0, .05, 0)))
        state = checks.send(rightTrigger=1)
        checks.send(rightTrigger=0)
    else:
        state = checks.send(leftStick=dict(x=0, y=0))
    assert not state['notebook'] and state['pickupCollected'] and state['pickupBagCount'] > before, state
    count = state['pickupBagCount']
    checks.remember('page'+str(page)+'TrackedPickup', checks.send(seconds=.5, capture='review-nearby-page-'+str(page)))
    state = checks.send(a=True)
    assert state['pickupBagCount'] == count, 'Repeated interaction duplicated the pickup'
    checks.send(a=False)


def guarded_targets(checks):
    checks.send(**base.NEUTRAL, fixture='ShoreFirstPerson', pickup=-1, carcass=-1, equip='HuntingRifle',
                headEuler=base.ZERO, right=base.NORMAL_RIGHT, rightEuler=base.ZERO)
    state = checks.send(carcass=0)
    checks.send(equip='FieldGuide')
    state = look_at(checks, base.xyz(state['carcassPosition']))
    assert state['carcassTarget'] == 0 and not state['carcassCollected'], state
    open_nearby(checks, 9)
    state = aim_with_palm(checks, 9, base.xyz(state['carcassPosition']))
    state = checks.send(rightTrigger=1)
    assert state['carcassCollected'], state
    checks.remember('queuedNearbyCollectsActualCarcass', state)
    checks.send(rightTrigger=0)
    state = checks.send(**base.NEUTRAL, fixture='HomeInterior', carcass=-1, homeReview=True,
                        headEuler=base.ZERO, right=base.NORMAL_RIGHT, rightEuler=base.ZERO)
    state = checks.send(homeReview=False)
    open_nearby(checks, 9)
    state = checks.click(index=5)
    assert state['notebook'] and state['page'] == 18, state
    checks.remember('queuedNearbyOpensHomeStorageAfterNeutral', state)
    checks.send(b=True)
    checks.send(b=False)


def camera(checks):
    state = checks.send(**base.NEUTRAL, fixture='HomeInterior', pickup=-1, equip='FieldGuide',
                        applicationPaused=False, head=dict(x=0, y=1.65, z=0), headEuler=base.ZERO,
                        left=base.NORMAL_LEFT, right=base.NORMAL_RIGHT, leftEuler=base.ZERO, rightEuler=base.ZERO)
    taken = state['photosTaken']
    state = checks.send(right=dict(x=0, y=1.65, z=.1), rightTrigger=.8)
    assert state['cameraRaised'] and state['localHandsHidden'] and not state['toolActive'], state
    assert not state['notebook'] and state['photosTaken'] == taken, 'Same-frame raising/pressing leaked into the field guide'
    checks.remember('sameFrameRaiseAndTriggerKeepsCameraOwnership', state)
    checks.send(rightTrigger=0)
    state = checks.send(rightTrigger=.8)
    assert state['photosTaken'] == taken+1, state
    state = checks.send(seconds=.7)
    assert state['photosTaken'] == taken+1, 'Held shutter repeated'
    checks.send(rightActive=False)
    state = checks.send(rightActive=True)
    assert state['photosTaken'] == taken+1, 'Tracking recovery fired the held shutter'
    state = checks.send(applicationPaused=True, rightTrigger=0)
    assert not state['cameraRaised'], state
    state = checks.send(rightTrigger=.8)
    assert state['photosTaken'] == taken+1 and not state['cameraRaised'] and not state['notebook'], state
    checks.remember('applicationPauseBlocksCameraWhileStillFocused', state)
    state = checks.send(applicationPaused=False)
    assert state['photosTaken'] == taken+1, 'Application resume fired the held shutter'
    checks.send(rightTrigger=0)
    state = checks.send(rightTrigger=.8, capture='review-native-camera', seconds=1)
    assert state['photosTaken'] == taken+2, state
    checks.remember('nativeCameraShutterAndTrackingRecovery', state)
    state = checks.send(rightTrigger=0, right=base.NORMAL_RIGHT)
    assert not state['cameraRaised'] and not state['localHandsHidden'] and state['toolActive'], state
    checks.remember('stowingCameraRestoresHandsAndEquippedTool', state)


def rifle_boundaries(checks):
    state = checks.send(**base.NEUTRAL, fixture='ShoreFirstPerson', pickup=-1, carcass=-1,
                        applicationPaused=False, equip='HuntingRifle', headEuler=base.ZERO,
                        right=base.NORMAL_RIGHT, left=base.NORMAL_LEFT,
                        rightEuler=base.ZERO, leftEuler=base.ZERO)
    shots, photos = state['shotsFired'], state['photosTaken']
    state = checks.send(a=True)
    assert state['shotsFired'] == shots and state['photosTaken'] == photos, state
    checks.send(a=False)
    state = checks.send(rightTrigger=1, seconds=.5)
    assert state['shotsFired'] > shots and not state['cameraRaised'], state
    state = checks.send(rightActive=False)
    shots = state['shotsFired']
    state = checks.send(seconds=.3)
    assert state['shotsFired'] == shots, state
    state = checks.send(rightActive=True, seconds=.3)
    assert state['shotsFired'] == shots, 'Tracking recovery fired a held rifle trigger'
    checks.send(rightTrigger=0)
    state = checks.send(rightTrigger=1, seconds=.3)
    assert state['shotsFired'] > shots and state['photosTaken'] == photos, state
    checks.send(b=True)
    state = checks.send(b=False)
    assert state['notebook'], state
    shots = state['shotsFired']
    state = checks.send(seconds=.3)
    assert state['shotsFired'] == shots, 'An open notebook allowed rifle firing'
    checks.send(b=True)
    state = checks.send(b=False, seconds=.3)
    assert not state['notebook'] and state['shotsFired'] == shots, 'Closing the menu fired a held rifle trigger'
    checks.remember('nativeRifleTrackingMenuAndCameraExclusion', state)
    checks.send(rightTrigger=0)


def main():
    checks = base.NativeV059Checks(base.ROOT / 'Logs/VRReview/native-review.json', 25)
    try:
        nearby(checks, 6, 0)
        nearby(checks, 9, 1)
        guarded_targets(checks)
        camera(checks)
        rifle_boundaries(checks)
        checks.save(True)
        print('VR REVIEW NATIVE PASS: both nearby menu routes collect once; camera shutter and tracking recovery')
    except BaseException as error:
        checks.save(False, error)
        raise
    finally:
        try:
            checks.send(**base.NEUTRAL)
        except (AssertionError, OSError, RuntimeError, TimeoutError):
            pass


if __name__ == '__main__':
    main()
