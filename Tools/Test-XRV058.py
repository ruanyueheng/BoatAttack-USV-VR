"""Exercise V058 through native OpenXR controller input in an isolated Editor session.

Start Unity 2022.3.62f1 with a graphics device (omit -quit and -nographics):
  -projectPath <repository> -executeMethod BoatAttack.EditorTools.FishingXRSimulator.Run
  -fishing-acceptance -fishing-acceptance-session native-v058
  -fishing-acceptance-case HomeInterior -fishing-xr-simulator -fishing-pico-quality

Then run: python Tools/Test-XRV058.py
Evidence: Logs/V058/NativeXR/native-v058.json and Simulator's capture PNG files.
The script writes only the simulator's isolated input commands and test evidence.
It does not open Unity or access the player's ordinary save slots.
"""

import argparse
import json
import math
import os
from pathlib import Path
import time


ROOT = Path(__file__).resolve().parents[1]
SIM = ROOT / 'Logs' / 'XRSimulator'
NEUTRAL = dict(a=False, b=False, x=False, y=False, leftClick=False, rightClick=False,
               leftTrigger=0, rightTrigger=0, leftGrip=0, rightGrip=0,
               leftStick=dict(x=0, y=0), rightStick=dict(x=0, y=0), table=-1)
NORMAL_LEFT = dict(x=-.24, y=1.2, z=.35)
NORMAL_RIGHT = dict(x=.24, y=1.2, z=.35)
ZERO = dict(x=0, y=0, z=0)


def xyz(value):
    return tuple(value[key] for key in ('x', 'y', 'z'))


def vector(value):
    return dict(zip(('x', 'y', 'z'), value))


def add(a, b):
    return tuple(x+y for x, y in zip(a, b))


def subtract(a, b):
    return tuple(x-y for x, y in zip(a, b))


def distance(a, b):
    return math.sqrt(sum(x*x for x in subtract(a, b)))


def rotate_axis(point, axis, degrees):
    angle = math.radians(degrees)
    c, s = math.cos(angle), math.sin(angle)
    x, y, z = point
    if axis == 'x':
        return x, c*y-s*z, s*y+c*z
    if axis == 'y':
        return c*x+s*z, y, -s*x+c*z
    return c*x-s*y, s*x+c*y, z


def rotate(point, euler, inverse=False):
    # Unity Quaternion.Euler applies Z, then X, then Y; inverse reverses this order.
    axes = ('y', 'x', 'z') if inverse else ('z', 'x', 'y')
    for axis in axes:
        point = rotate_axis(point, axis, euler[axis]*(-1 if inverse else 1))
    return point


class NativeChecks:
    def __init__(self, output, timeout):
        self.output = output
        self.timeout = timeout
        self.started = time.time()
        self.evidence = {'startedUtc': time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()),
                         'checks': {}, 'scope': 'Native Mock OpenXR input; no PICO hardware/stereo comfort claim'}

    def read(self, name):
        for _ in range(30):
            try:
                return json.loads((SIM / name).read_text(encoding='utf-8-sig'))
            except (ValueError, OSError):
                time.sleep(.05)
        raise RuntimeError('Cannot read simulator ' + name)

    def remember(self, name, state):
        self.evidence['checks'][name] = state

    def send(self, frames=9, seconds=0, **changes):
        before = self.read('state.json')['frame']
        command = self.read('command.json')
        command.update(capture='', fixture='', equip='')
        command.update(changes)
        temporary = SIM / 'v058-command.tmp'
        temporary.write_text(json.dumps(command), encoding='utf-8')
        os.replace(temporary, SIM / 'command.json')
        deadline, not_before = time.monotonic()+self.timeout, time.monotonic()+seconds
        while time.monotonic() < deadline:
            time.sleep(.1)
            failure = SIM / 'failure.txt'
            if failure.exists() and failure.stat().st_mtime >= self.started:
                raise RuntimeError(failure.read_text(encoding='utf-8-sig'))
            state = self.read('state.json')
            if state['frame'] >= before+frames and state['focused'] and time.monotonic() >= not_before:
                assert state['xr'] and state['isolated'] and not state['automated'], state
                assert state['leftTracked'] and state['rightTracked'] and state['seatReady'], state
                return state
        raise TimeoutError('Simulator did not process live, focused native XR input')

    def button(self, state, index=None, contains=None):
        matches = [button for button in state['buttons']
                   if (index is None or button['index'] == index)
                   and (contains is None or contains in button['label'])]
        assert len(matches) == 1, ('Expected one active button', index, contains, state['buttons'])
        return matches[0]

    def aim(self, index=None, contains=None):
        state = self.send(rightTrigger=0, rightGrip=0, leftGrip=0,
                          right=NORMAL_RIGHT, left=NORMAL_LEFT)
        for _ in range(3):
            target = self.button(state, index, contains)
            local = rotate(subtract(xyz(target['center']), xyz(state['right'])), state['originEuler'], inverse=True)
            yaw = math.degrees(math.atan2(local[0], local[2]))
            pitch = -math.degrees(math.atan2(local[1], math.hypot(local[0], local[2])))
            state = self.send(rightEuler=dict(x=pitch, y=yaw, z=0))
            actual = xyz(state['rightForward'])
            desired = subtract(xyz(self.button(state, index, contains)['center']), xyz(state['right']))
            length = math.sqrt(sum(value*value for value in desired))
            if length > .25 and sum(a*b for a, b in zip(actual, desired))/length > .999:
                assert state['rayTarget'], ('The controller ray misses every active button', state)
                return state
        raise AssertionError(('Cannot aim the native controller ray at button', index, contains, state))

    def click(self, index=None, contains=None):
        self.aim(index, contains)
        state = self.send(rightTrigger=1)
        self.send(rightTrigger=0)
        return state

    def open_home(self):
        state = self.read('state.json')
        if state['notebook']:
            self.send(b=True)
            self.send(b=False)
        self.send(b=True)
        state = self.send(b=False)
        assert state['notebook'] and state['page'] == 0, state
        return state

    def purchases_and_facilities(self):
        self.send(**NEUTRAL, fixture='HomeInterior', head=dict(x=0, y=1.65, z=0), headEuler=ZERO,
                  left=NORMAL_LEFT, right=NORMAL_RIGHT, leftEuler=ZERO, rightEuler=ZERO)
        state = self.send()
        assert state['ashore'] and not state['creativeMode'], ('Use an ordinary funded HomeInterior fixture', state)
        self.open_home()
        state = self.click(index=5)
        assert state['page'] == 4, state
        state = self.click(index=6)
        assert state['page'] == 5, state
        state = self.click(index=6)
        assert '制作补给' in state['detail'], state
        state = self.aim(index=0, contains='木板')
        before_coins, before_planks = state['coins'], state['planks']
        assert before_coins >= 36, ('The isolated fixture needs at least 36 coins', state)
        state = self.send(rightTrigger=1, capture='v058-quick-purchase')
        assert state['planks'] == before_planks+6 and state['coins'] == before_coins-18, state
        self.remember('quickPurchasePaidAndAdded', state)
        state = self.send(seconds=1, rightTrigger=1)
        assert state['planks'] == before_planks+6 and state['coins'] == before_coins-18, 'Held trigger repeated the transaction'
        self.remember('heldTriggerDoesNotRepeat', state)
        self.send(rightTrigger=0)
        state = self.send(rightTrigger=1)
        assert state['planks'] == before_planks+12 and state['coins'] == before_coins-36, state
        self.remember('releaseAndPressBuysExactlyOneMore', state)
        self.send(rightTrigger=0)

        # Close/reopen the actual menu and inspect the actual inventory page, retaining the same career.
        self.open_home()
        state = self.click(index=0)
        assert state['page'] == 1, state
        assert state['planks'] == before_planks+12 and state['coins'] == before_coins-36, state
        self.remember('realBackpackContainsPurchasedPlanks', self.send(seconds=.7, capture='v058-purchased-backpack'))
        state = self.click(contains='家园建设')
        assert state['page'] == 21, state
        expected = ('水族箱', '兔子小窝', '林间狐舍', '陆龟围栏', '海鸟栖架', '海龟浅水池')
        labels = [self.button(state, index=i)['label'] for i in range(6)]
        for label, title in zip(labels, expected):
            assert title in label, ('A facility is missing from the first ecology page', title, labels)
        assert state['planks'] == before_planks+12 and state['coins'] == before_coins-36, state
        self.remember('allSixFacilityActionsVisible', self.send(seconds=.7, capture='v058-six-facilities'))
        self.send(b=True)
        self.send(b=False)

    def binoculars(self):
        state = self.send(**NEUTRAL, fixture='ShoreFirstPerson', equip='Binoculars',
                          head=dict(x=0, y=1.65, z=0), headEuler=ZERO,
                          left=NORMAL_LEFT, right=NORMAL_RIGHT, leftEuler=ZERO, rightEuler=ZERO)
        state = self.send()
        assert 'binocularOpticsActive' in state, 'Recompile the Editor with the V058 simulator state fields'
        assert state['toolActive'] and state['heldItem'] == 'Binoculars' and not state['headBlocked'], state
        assert not state['binocularOpticsActive'], 'Lowered binoculars activated magnification'
        self.remember('loweredBinocularsInactive', state)

        # Start with the ordinary controller roll calibration and a deliberately tilted instrument.
        # Position correction below uses measured native palms, including the user's calibration.
        state = self.send(leftGrip=1, rightGrip=1,
                          leftEuler=dict(x=15, y=20, z=10-state['leftPalmRoll']),
                          rightEuler=dict(x=15, y=20, z=10-state['rightPalmRoll']),
                          left=dict(x=-.094, y=1.65, z=.158), right=dict(x=.094, y=1.65, z=.158))
        for _ in range(2):
            command = self.read('command.json')
            changes = {}
            for side, sign in (('left', -1), ('right', 1)):
                desired = add(xyz(state['camera']), rotate((sign*.094, 0, .158), state['originEuler']))
                correction = rotate(subtract(desired, xyz(state[side+'Palm'])), state['originEuler'], inverse=True)
                changes[side] = vector(add(xyz(command[side]), correction))
            state = self.send(**changes)
        assert not state['binocularEyeAlignment'], 'Trigger assistance ran without a trigger hold'
        photos = state['photosTaken']
        state = self.send(rightTrigger=1, capture='v058-binocular-aligned')
        assert state['triggerValue'] > .9 and state['binocularEyeAlignment'] and state['binocularOpticsActive'], state
        assert not state['cameraRaised'] and state['photosTaken'] == photos, 'Binocular trigger also activated the camera'
        left_eye, right_eye = xyz(state['binocularLeftEyepiece']), xyz(state['binocularRightEyepiece'])
        separation = distance(left_eye, right_eye)
        assert .05 <= separation <= .08 and abs(separation-state['binocularLensSeparation']) < .001, state
        rear = tuple((a+b)*.5 for a, b in zip(left_eye, right_eye))
        expected_rear = add(xyz(state['camera']), rotate((0, 0, .065), state['originEuler']))
        assert distance(rear, expected_rear) < .015, ('Eyepieces are not seated at the headset eye centre', state)
        assert 0 < state['binocularFov'] < 30, state
        self.remember('twoGripsNearEyesTriggerAssistsRealOptics', state)
        state = self.send(seconds=.5, rightTrigger=1)
        assert state['binocularOpticsActive'] and state['binocularEyeAlignment'], state
        self.remember('alignmentStableWhileHeld', state)
        state = self.send(leftGrip=0)
        assert not state['binocularOpticsActive'], 'Releasing a hand did not close the optical view'
        self.remember('releaseEitherGripClosesOptics', state)
        state = self.send(**NEUTRAL, left=NORMAL_LEFT, right=NORMAL_RIGHT,
                          leftEuler=ZERO, rightEuler=ZERO)
        assert not state['binocularOpticsActive'], state
        self.remember('loweringAndReleasingRestoresNormalView', state)

    def save(self, passed, error=None):
        self.evidence['passed'] = passed
        if error is not None:
            self.evidence['error'] = repr(error)
        self.output.parent.mkdir(parents=True, exist_ok=True)
        self.output.write_text(json.dumps(self.evidence, ensure_ascii=False, indent=2), encoding='utf-8')


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--output', type=Path, default=ROOT / 'Logs/V058/NativeXR/native-v058.json')
    parser.add_argument('--timeout', type=float, default=20, help='Maximum seconds for one native input command')
    args = parser.parse_args()
    checks = NativeChecks(args.output, args.timeout)
    try:
        initial = checks.read('state.json')
        assert initial['xr'] and initial['isolated'] and not initial['automated'], 'Start the isolated native XR simulator first'
        checks.purchases_and_facilities()
        checks.binoculars()
        checks.save(True)
        print('V058 NATIVE XR PASS: paid real backpack purchases, trigger edge, six facilities, dual-grip assisted optics')
        print(args.output)
    except BaseException as error:
        checks.save(False, error)
        raise
    finally:
        # Release controls even after an assertion; do not terminate the user's simulator session.
        try:
            checks.send(**NEUTRAL)
        except (AssertionError, OSError, RuntimeError, TimeoutError):
            pass


if __name__ == '__main__':
    main()
