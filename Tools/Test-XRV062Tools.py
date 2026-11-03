"""Check V062 tool grips and discoverable story/calibration menus in native OpenXR.

Start the isolated PICO-quality simulator documented in Test-XRV058.py first.
Run this script with --only tools, --only ui, or the default all. It never starts
Unity or opens normal save slots. Captures are single-eye simulator evidence.
"""
import argparse
import importlib.util
import math
from pathlib import Path

spec = importlib.util.spec_from_file_location('xr059', Path(__file__).with_name('Test-XRV059.py'))
base = importlib.util.module_from_spec(spec)
spec.loader.exec_module(base)


def dot(a, b):
    return sum(x*y for x, y in zip(a, b))


def unit(value):
    length = math.sqrt(dot(value, value))
    assert length > 1e-6, ('Missing grip axis', value)
    return tuple(x/length for x in value)


def cross(a, b):
    return a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]


def prepare(checks):
    if checks.read('state.json')['notebook']:
        checks.send(b=True)
        checks.send(b=False)
    return checks.send(**base.NEUTRAL, fixture='HomeInterior', equip='FieldGuide',
                       head=dict(x=0, y=1.65, z=0), headEuler=dict(x=25, y=0, z=0),
                       left=base.NORMAL_LEFT, right=base.NORMAL_RIGHT,
                       leftEuler=base.ZERO, rightEuler=base.ZERO,
                       leftPalmRoll=999, rightPalmRoll=999, applicationPaused=False,
                       homeReview=False, pickup=-1, carcass=-1)


def inspect_grip(checks, item, roll, pose, state):
    checks.remember(item+'Roll'+str(roll)+pose, state)
    assert state['toolActive'] and state['heldItem'] == item, (item, roll, pose, 'tool not presented')
    assert state['leftPalmRoll'] == roll and state['rightPalmRoll'] == roll, (item, roll, pose, 'calibration not applied')
    palm_error = base.distance(base.xyz(state['visualRightPalm']), base.xyz(state['rightPalm']))
    assert palm_error < .09, (item, roll, pose, 'visible hand detached from tracked palm', palm_error)
    assert state['gripRadius'] > 0, (item, roll, pose, 'missing physical finger grip surface')
    grip = unit(base.subtract(base.xyz(state['gripEnd']), base.xyz(state['gripStart'])))
    knuckles = unit(base.xyz(state['rightKnuckles']))
    if item == 'Shovel':
        shaft_alignment = dot(unit(base.xyz(state['heldUp'])), knuckles)
        palm_right = unit(cross(unit(base.xyz(state['rightPalmUp'])), knuckles))
        crossbar_alignment = dot(grip, palm_right)
        assert shaft_alignment > .985, (item, roll, pose, 'shaft must extend along the knuckles', shaft_alignment)
        assert crossbar_alignment > .985, (item, roll, pose, 'D-grip crossbar must span the palm', crossbar_alignment)
    else:
        across_fingers = abs(dot(grip, knuckles))
        assert across_fingers < .03, (item, roll, pose, 'handle must cross the fingers', across_fingers)


def tools(checks):
    prepare(checks)
    poses = (('Normal', base.ZERO), ('Tilted', dict(x=20, y=15, z=45)),
             ('Rotated', dict(x=-25, y=-20, z=-35)))
    for roll in (0, 30):
        checks.send(leftPalmRoll=roll, rightPalmRoll=roll)
        # The sentinel lets subsequent real UI commands change calibration freely.
        checks.send(leftPalmRoll=999, rightPalmRoll=999)
        for item in ('Axe', 'Shovel', 'Rake', 'StonePickaxe', 'IronPickaxe', 'HuntingSpear'):
            checks.send(equip=item, left=base.NORMAL_LEFT, right=base.NORMAL_RIGHT,
                        leftEuler=base.ZERO, rightEuler=base.ZERO)
            for pose, euler in poses:
                capture = ('v062-native-'+item.lower()+'-roll'+str(roll)+'-'+pose.lower()
                           if pose == 'Normal' or item == 'Shovel' and roll == 0 else '')
                state = checks.send(rightEuler=euler, leftEuler=dict(x=-euler['x'], y=-euler['y'], z=-euler['z']),
                                    seconds=.6 if capture else 0, capture=capture)
                inspect_grip(checks, item, roll, pose, state)
            before = checks.send(rightEuler=base.ZERO, leftEuler=base.ZERO, right=base.NORMAL_RIGHT)
            state = checks.send(right=dict(x=.34, y=1.25, z=.40),
                                seconds=.6 if item == 'Shovel' and roll == 0 else 0,
                                capture='v062-native-shovel-roll0-translated' if item == 'Shovel' and roll == 0 else '')
            inspect_grip(checks, item, roll, 'Translated', state)
            palm_delta = base.subtract(base.xyz(state['rightPalm']), base.xyz(before['rightPalm']))
            tool_delta = base.subtract(base.xyz(state['heldPosition']), base.xyz(before['heldPosition']))
            assert base.distance(palm_delta, tool_delta) < .02, (item, roll, 'translation changed the physical grip offset')


def ui(checks):
    prepare(checks)
    checks.send(headEuler=base.ZERO, leftPalmRoll=30, rightPalmRoll=30)
    checks.send(leftPalmRoll=999, rightPalmRoll=999)
    checks.open_home()
    state = checks.click(index=6)
    assert state['page'] == 9 and state['mainObjective'], ('Homepage did not expose the current main story', state)
    assert '追踪主线' in checks.button(state, index=6)['label'], state['buttons']
    checks.remember('HomepageToCurrentMainStory', checks.send(seconds=.6, capture='v062-native-main-story'))
    state = checks.click(index=6)
    assert not state['notebook'] and state['navigationTarget'] == state['mainStoryNavigationTarget'], state
    checks.remember('NativeMainStoryTrackingMarksCurrentDestination', state)

    checks.open_home()
    state = checks.click(index=7)
    assert state['page'] == 14 and '手部角度' in checks.button(state, index=8)['label'], state['buttons']
    state = checks.click(index=8)
    assert state['page'] == 35, state
    state = checks.click(index=5)
    assert state['leftPalmRoll'] == 0 and state['rightPalmRoll'] == 0, state
    checks.remember('DirectSettingsEntryZerosBothHands', checks.send(seconds=.6, capture='v062-native-hand-angle-zero'))
    state = checks.click(index=4)
    assert state['leftPalmRoll'] == 30 and state['rightPalmRoll'] == 30, state
    checks.remember('NativeDefaultButtonRestoresBothHandsTo30', checks.send(seconds=.6, capture='v062-native-hand-angle-default'))
    state = checks.click(index=8)
    assert state['page'] == 14, state
    state = checks.click(index=1)
    assert state['page'] == 22 and '手部角度' in checks.button(state, index=6)['label'], state['buttons']
    state = checks.click(index=6)
    assert state['page'] == 35, state
    checks.remember('ComfortPageAlsoReachesHandCalibration', state)
    checks.send(b=True)
    checks.send(b=False)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--only', choices=('tools', 'ui', 'all'), default='all')
    parser.add_argument('--output', type=Path, default=base.ROOT / 'Logs/V062/NativeXR/native-tools-ui.json')
    parser.add_argument('--timeout', type=float, default=25)
    args = parser.parse_args()
    checks = base.NativeV059Checks(args.output, args.timeout)
    original = None
    try:
        original = checks.read('state.json')
        assert original['xr'] and original['isolated'] and not original['automated'], 'Start the isolated native OpenXR simulator first'
        assert 'gripStart' in original and 'mainStoryNavigationTarget' in original, 'Recompile the V062 simulator first'
        checks.evidence['selectedChecks'] = args.only
        if args.only in ('tools', 'all'):
            tools(checks)
        if args.only in ('ui', 'all'):
            ui(checks)
        checks.save(True)
        print('V062 NATIVE XR PASS: '+args.only)
        print(args.output)
    except BaseException as error:
        checks.save(False, error)
        raise
    finally:
        if original is not None:
            try:
                checks.send(**base.NEUTRAL, leftPalmRoll=original['leftPalmRoll'], rightPalmRoll=original['rightPalmRoll'])
                checks.send(leftPalmRoll=999, rightPalmRoll=999)
            except (AssertionError, KeyError, OSError, RuntimeError, TimeoutError):
                pass


if __name__ == '__main__':
    main()
