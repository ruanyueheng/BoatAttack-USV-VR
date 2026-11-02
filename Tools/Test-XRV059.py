"""Exercise V059 handcraft fixes using native Mock OpenXR controller input.

Start the isolated simulator described by Tools/Test-XRV058.py, using session
native-v059. Then run: python Tools/Test-XRV059.py
This checks the production XR Tick path; it does not open Unity, modify normal
save slots, or certify PICO comfort, stereo rendering or physical loudness.
"""
import argparse
import json
import os
from pathlib import Path
import runpy
import time

BASE = runpy.run_path(str(Path(__file__).with_name('Test-XRV058.py')))
NativeChecks = BASE['NativeChecks']
ROOT, SIM = BASE['ROOT'], BASE['SIM']
xyz, vector, add, subtract = (BASE[name] for name in ('xyz', 'vector', 'add', 'subtract'))
distance, rotate = BASE['distance'], BASE['rotate']
NORMAL_LEFT, NORMAL_RIGHT, ZERO = (BASE[name] for name in ('NORMAL_LEFT', 'NORMAL_RIGHT', 'ZERO'))
NEUTRAL = dict(BASE['NEUTRAL'], leftActive=True, rightActive=True)


class NativeV059Checks(NativeChecks):
    def send(self, frames=9, seconds=0, **changes):
        command = self.read('command.json')
        command.update(capture='', fixture='', equip='')
        command.update(changes)
        command['sequence'] = command.get('sequence', 0)+1
        temporary = SIM / 'v059-command.tmp'
        temporary.write_text(json.dumps(command), encoding='utf-8')
        os.replace(temporary, SIM / 'command.json')
        deadline, not_before = time.monotonic()+self.timeout, time.monotonic()+seconds
        while time.monotonic() < deadline:
            time.sleep(.1)
            failure = SIM / 'failure.txt'
            if failure.exists() and failure.stat().st_mtime >= self.started:
                raise RuntimeError(failure.read_text(encoding='utf-8-sig'))
            state = self.read('state.json')
            if (state.get('commandSequence') == command['sequence']
                    and state['frame'] >= state['commandFrame']+frames
                    and state['focused'] and time.monotonic() >= not_before):
                assert state['xr'] and state['isolated'] and not state['automated'], state
                assert state['seatReady'], state
                for side in ('left', 'right'):
                    assert state[side+'Tracked'] == command.get(side+'Active', True), state
                return state
        raise TimeoutError('Simulator did not process the live native V059 input command')

    def prepare(self, table):
        self.send(**dict(NEUTRAL, fixture='ShoreFirstPerson', equip='FieldGuide',
                         head=dict(x=0, y=1.65, z=0), headEuler=ZERO,
                         left=NORMAL_LEFT, right=NORMAL_RIGHT, leftEuler=ZERO, rightEuler=ZERO))
        state = self.send(table=table)
        assert not state['handcraftActive'] and state['ashore'] and not state['headBlocked'] and not state['creativeMode'], state
        assert 'workpieceFingerprint' in state and 'handcraftLeft' in state, 'Recompile the V059 simulator first'
        return state

    def attached(self, state, side):
        assert state['handcraftActive'] and state['handcraftHeld'] and state['handcraftKind'] == 0, state
        assert state['handcraftLeft'] == (side == 'left'), state
        target = add(xyz(state[side+'Palm']), rotate((0, -.026, 0), state[side+'PalmEuler']))
        assert distance(xyz(state['toolGrip']), target) < .006, ('The sieve handle detached from the native palm', state)

    def sieve(self):
        for side, marker in (('right', 'sieveGripA'), ('right', 'sieveGripB'),
                             ('left', 'sieveGripA'), ('left', 'sieveGripB')):
            state = self.prepare(0)
            rest = xyz(state['toolPosition'])
            before_sand, before_shells = state['sand'], state['shells']
            palm = add(xyz(state[marker]), rotate((0, .026, 0), state[side+'PalmEuler']))
            self.move_palm(side, palm)
            state = self.send(**{side+'Grip': 1})
            self.attached(state, side)
            assert state['handcraftStrokes'] == 0, 'The entry squeeze falsely counted a sieve stroke'
            self.remember(side+'DirectGrab'+marker, state)
            lifted = add(palm, add((0, .24, 0), rotate((.12, 0, 0), state['originEuler'])))
            state = self.move_palm(side, lifted)
            self.attached(state, side)
            assert state['toolPosition']['y'] > rest[1]+.18, state
            state = self.send(**{side+'Euler': dict(x=12, y=18, z=-15)})
            self.attached(state, side)
            self.remember(side+'LiftAndTilt'+marker, state)
            strokes = state['handcraftStrokes']
            state = self.send(seconds=.6)
            assert state['handcraftStrokes'] == strokes, 'Holding the lifted sieve still crafted progress'
            # Head yaw changes the rendered rig/palms; raw controller coordinates stay constant.
            state = self.send(headEuler=dict(x=0, y=24, z=0))
            assert state['handcraftStrokes'] == strokes, 'Head motion falsely advanced sieve strokes'
            self.attached(state, side)
            self.remember(side+'StationaryNativeHandIgnoresHeadMotion'+marker, state)
            state = self.send(headEuler=ZERO, **{side+'Grip': 0})
            assert state['handcraftActive'] and not state['handcraftHeld'], state
            assert distance(xyz(state['toolPosition']), rest) < .002, state
            assert state['sand'] == before_sand and state['shells'] == before_shells, state
            self.remember(side+'ReleaseReturnsSieveToTray'+marker, state)
            self.send(b=True)
            state = self.send(b=False)
            assert not state['handcraftActive'] and state['sand'] == before_sand, state

        # Tracking loss is exercised by deactivating the native interaction profile.
        state = self.prepare(0)
        rest = xyz(state['toolPosition'])
        palm = add(xyz(state['sieveGripB']), rotate((0, .026, 0), state['leftPalmEuler']))
        self.move_palm('left', palm)
        self.attached(self.send(leftGrip=1), 'left')
        state = self.send(leftActive=False)
        assert state['handcraftActive'] and not state['handcraftHeld'], state
        assert distance(xyz(state['toolPosition']), rest) < .002, state
        self.remember('nativeTrackingLossReleasesSieve', state)
        state = self.send(leftActive=True, leftGrip=1)
        assert not state['handcraftHeld'], 'Tracking recovery attached a still-squeezed grip'
        self.remember('trackingRecoveryNeedsRelease', state)
        self.send(leftGrip=0)
        self.move_palm('left', palm)
        state = self.send(leftGrip=1)
        self.attached(state, 'left')
        self.remember('releaseAndRegripWorksAfterTrackingLoss', state)
        self.send(leftGrip=0, b=True)
        self.send(b=False)

        # Finish one real recipe with the left hand, using only native grip/pose input.
        state = self.prepare(0)
        palm = add(xyz(state['sieveGripA']), rotate((0, .026, 0), state['leftPalmEuler']))
        self.move_palm('left', palm)
        state = self.send(leftGrip=1)
        self.attached(state, 'left')
        before_sand, before_shells = state['sand'], state['shells']
        native = xyz(self.read('command.json')['left'])
        greatest = state['handcraftStrokes']
        for stroke in range(16):
            target = add(native, ((-.09 if stroke % 2 == 0 else .09), 0, 0))
            state = self.send(seconds=.18, left=vector(target))
            if not state['handcraftActive']:
                break
            self.attached(state, 'left')
            greatest = max(greatest, state['handcraftStrokes'])
        assert greatest >= 10 and not state['handcraftActive'], ('Native alternating strokes did not finish the sieve', state)
        assert state['sand'] == before_sand-2 and state['shells'] == before_shells+1, state
        self.remember('nativeLeftHandShakingCommitsOneRealRecipe', state)
        state = self.send(seconds=.6)
        assert state['sand'] == before_sand-2 and state['shells'] == before_shells+1 and not state['handcraftActive'], state
        self.remember('heldGripCannotDuplicateSieveReward', state)
        self.send(leftGrip=0)

    def craft_feedback(self):
        for table, material, audio in ((2, 'driftwood', 'sawPlayCount'), (3, 'ironIngots', 'forgePlayCount')):
            state = self.prepare(table)
            before = state[material]
            plays, emissions = state[audio], state['workDebrisEmitted']
            palm = add(xyz(state['toolGrip']), rotate((0, .026, 0), state['rightPalmEuler']))
            self.move_palm('right', palm)
            state = self.send(rightGrip=1)
            assert state['handcraftHeld'] and state['handcraftKind'] == table, state
            assert state['handcraftStrokes'] == 0, 'The entry squeeze falsely counted a workpiece stroke'
            original_mesh = state['workpieceFingerprint']
            original_size = xyz(state['workpieceSize'])
            lifted = add(palm, add((0, .28, 0), rotate((.24, 0, 0), state['originEuler'])))
            state = self.move_palm('right', lifted)
            assert state['handcraftHeld'] and state['handcraftStrokes'] == 0, state
            assert state['workpieceFingerprint'] == original_mesh and state[audio] == plays and state['workDebrisEmitted'] == emissions, state
            self.remember('airMotionHasNoEffects'+str(table), state)
            state = self.move_palm('right', palm)
            strokes = state['handcraftStrokes']
            for stroke in range(3):
                if table == 3:
                    self.move_palm('right', add(palm, (0, .14, 0)))
                    state = self.move_palm('right', add(palm, (0, -.08, 0)))
                else:
                    axis = rotate((0, 1, 0), state['toolEuler'])
                    state = self.move_palm('right', add(palm, tuple((.09 if stroke % 2 == 0 else -.09)*x for x in axis)))
            assert state['handcraftStrokes'] > strokes and state[audio] > plays, state
            assert abs(state['workpieceFingerprint']-original_mesh) > .01 and state['workDebrisEmitted'] > emissions, state
            assert state['workDebrisBothDirections'], ('No emitted debris on both sides of the workpiece', state)
            if table == 3:
                assert state['workpieceSize']['x'] > original_size[0] and state['workpieceSize']['y'] < original_size[1], state
            self.remember('nativeStrokesChangeGeometryAudioAndBilateralDebris'+str(table), state)
            self.send(seconds=.5, capture='v059-native-workpiece-'+str(table))
            state = self.send(rightGrip=0, b=True)
            assert not state['handcraftActive'] and state[material] == before, state
            self.send(b=False)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--output', type=Path, default=ROOT / 'Logs/V059/NativeXR/native-v059.json')
    parser.add_argument('--timeout', type=float, default=20)
    parser.add_argument('--only', choices=('sieve', 'craft', 'all'), default='all')
    args = parser.parse_args()
    checks = NativeV059Checks(args.output, args.timeout)
    try:
        initial = checks.read('state.json')
        assert initial['xr'] and initial['isolated'] and not initial['automated'], 'Start the isolated native XR simulator first'
        checks.evidence['selectedChecks'] = args.only
        if args.only in ('sieve', 'all'):
            checks.sieve()
        if args.only in ('craft', 'all'):
            checks.craft_feedback()
        checks.save(True)
        print('V059 NATIVE XR PASS: '+args.only)
        print(args.output)
    except BaseException as error:
        checks.save(False, error)
        raise
    finally:
        try:
            checks.send(**NEUTRAL)
        except (AssertionError, OSError, RuntimeError, TimeoutError):
            pass


if __name__ == '__main__':
    main()
