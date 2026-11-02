"""Run against the isolated FishingXRSimulator with PICO quality enabled.

Uses native tracked poses, controller rays, buttons and walking input. Fixture setup
is restricted to the simulator's temporary profile; ordinary saves are never opened.
"""
import importlib.util
import math
from pathlib import Path

spec = importlib.util.spec_from_file_location('xr059', Path(__file__).with_name('Test-XRV059.py'))
base = importlib.util.module_from_spec(spec)
spec.loader.exec_module(base)


def dot(a, b):
    return sum(x*y for x, y in zip(a, b))


def face_cashier(checks):
    state = checks.send()
    for _ in range(3):
        local = base.rotate(base.subtract(base.xyz(state['cashierFace']), base.xyz(state['camera'])),
                            state['originEuler'], inverse=True)
        yaw = math.degrees(math.atan2(local[0], local[2]))
        pitch = -math.degrees(math.atan2(local[1], math.hypot(local[0], local[2])))
        state = checks.send(headEuler=dict(x=pitch, y=yaw, z=0))
        if state['cashierVisible']:
            return state
    raise AssertionError(('The native headset cannot see the cashier face', state))


def walk(checks, target):
    for _ in range(100):
        state = checks.read('state.json')
        delta = base.subtract(target, base.xyz(state['feet']))
        if math.hypot(delta[0], delta[2]) < .22:
            return checks.send(leftStick=dict(x=0, y=0))
        local = base.rotate((delta[0], 0, delta[2]), state['cameraEuler'], inverse=True)
        length = math.hypot(local[0], local[2])
        checks.send(frames=3, seconds=.18, leftStick=dict(x=local[0]/length*.6, y=local[2]/length*.6))
    raise AssertionError(('Native walking did not reach the home waypoint', target, state['feet']))


def run(checks):
    if checks.read('state.json')['notebook']:
        checks.send(b=True)
        checks.send(b=False)
    state = checks.send(**base.NEUTRAL, fixture='HomeInterior', headEuler=dict(x=15, y=0, z=0),
                        left=base.NORMAL_LEFT, right=base.NORMAL_RIGHT,
                        leftEuler=base.ZERO, rightEuler=base.ZERO)
    assert state['xr'] and state['isolated'] and not state['automated']
    for item in ('Axe', 'Shovel', 'Rake', 'StonePickaxe', 'IronPickaxe', 'HuntingSpear'):
        checks.send(equip=item, rightEuler=base.ZERO, right=base.NORMAL_RIGHT)
        for index, euler in enumerate((base.ZERO, dict(x=20, y=15, z=45), dict(x=-25, y=-20, z=-35))):
            state = checks.send(rightEuler=euler)
            assert state['toolActive'] and state['heldItem'] == item, (item, state)
            assert base.distance(base.xyz(state['visualRightPalm']), base.xyz(state['rightPalm'])) < .09, (item, 'visible palm')
            if item != 'HuntingSpear':
                axis = base.xyz(state['heldRight' if item == 'Shovel' else 'heldUp'])
                assert abs(dot(axis, base.xyz(state['rightKnuckles']))) < .03, (item, 'handle must cross the fingers')
                assert state['gripRadius'] > 0, (item, 'missing physical finger surface')
            checks.remember(item+'Pose'+str(index), state)
        before = checks.send(rightEuler=base.ZERO, right=base.NORMAL_RIGHT)
        state = checks.send(right=dict(x=.34, y=1.25, z=.40), seconds=.7, capture='v060-native-'+item.lower())
        palm_delta = base.subtract(base.xyz(state['rightPalm']), base.xyz(before['rightPalm']))
        tool_delta = base.subtract(base.xyz(state['heldPosition']), base.xyz(before['heldPosition']))
        assert base.distance(palm_delta, tool_delta) < .02, (item, 'tool detached while moving')

    state = checks.send(**base.NEUTRAL, equip='CaptureNet', right=base.NORMAL_RIGHT,
                        rightEuler=base.ZERO, headEuler=dict(x=25, y=0, z=0))
    assert state['captureNetActive'] and not state['toolActive'], state
    assert base.distance(base.xyz(state['captureNetPalm']), base.xyz(state['rightPalm'])) < .02, state
    checks.send(seconds=.7, capture='v060-native-net-grip')
    state = checks.send(rightActive=False)
    assert not state['captureNetActive'], state
    checks.remember('captureNetHidesOnTrackingLoss', state)
    state = checks.send(rightActive=True)
    assert state['captureNetActive'], state
    checks.remember('captureNetGripAndTrackingRecovered', state)
    checks.send(headEuler=base.ZERO)
    checks.binoculars()
    state = checks.send(**base.NEUTRAL, fixture='HomeInterior', homeReview=True,
                        left=base.NORMAL_LEFT, right=base.NORMAL_RIGHT, rightEuler=base.ZERO, headEuler=base.ZERO)
    assert state['chestCapacity'] == 144 and state['chestLastItem'] == 'SeaTreasure', state
    checks.send(homeReview=False, a=True)
    state = checks.send(a=False)
    assert state['notebook'] and state['page'] == 18, state
    state = checks.click(index=7)
    assert state['page'] == 19, state
    for _ in range(23):
        state = checks.click(index=8)
    assert state['pageOffset'] == 138, state
    assert any(button['index']==5 and button['label'].startswith('144 ') for button in state['buttons']) and state['chestLastItem']=='SeaTreasure', state['buttons']
    checks.remember('finalChestPage', checks.send(seconds=.7, capture='v060-native-chest-page-24'))
    before = state['bagTreasure']
    state = checks.click(index=5)
    assert state['chestLastItem'] == 'Empty' and state['bagTreasure'] == before+1, state
    checks.remember('withdrawSlot144', state)
    checks.send(b=True)
    checks.send(b=False, right=dict(x=.24, y=.85, z=.3))
    home = base.xyz(state['home'])
    for offset in ((0, 0, -2.8), (0, 0, -7.4), (8.3, 0, -7.4), (8.3, 0, -5.65)):
        state = walk(checks, base.add(home, offset))
    delta = base.subtract(base.add(base.xyz(state['aquarium']), (0, .9, 0)), base.xyz(state['camera']))
    local = base.rotate(delta, state['originEuler'], inverse=True)
    look = dict(x=-math.degrees(math.atan2(local[1], math.hypot(local[0], local[2]))),
                y=math.degrees(math.atan2(local[0], local[2])), z=0)
    state = checks.send(headEuler=look, seconds=.7, capture='v060-native-outdoor-aquarium')
    assert state['aquariumCount'] > 0 and state['aquarium']['x'] > state['home']['x']+7, state
    checks.remember('walkFromChestToOutdoorFish', state)
    checks.send(a=True)
    state = checks.send(a=False)
    assert state['notebook'] and state['page'] == 21, state
    checks.remember('outdoorAquariumInteraction', state)
    checks.send(b=True)
    checks.send(b=False)
    state = checks.send(**base.NEUTRAL, fixture='ShopAttendant', headEuler=base.ZERO, seconds=.8)
    face_cashier(checks)
    gestures = set()
    for index in range(18):
        state = checks.send(seconds=2)
        assert state['cashierVisible'], state
        gestures.add(state['cashierGesture'])
        if index in (0, 5, 10):
            checks.send(seconds=.7, capture='v060-native-cashier-'+str(index))
    assert len(gestures) >= 4, gestures
    checks.remember('cashierActivities', sorted(gestures))


def main():
    checks = base.NativeV059Checks(base.ROOT / 'Logs/V060/NativeXR/native-v060.json', 25)
    try:
        run(checks)
        checks.save(True)
        print('V060 NATIVE XR PASS: calibrated grips, 144-slot paging/withdrawal, island walk/aquarium, cashier activities')
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
