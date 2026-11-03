"""Exercise the V062 saw against actual native XR grip/pose input.

Use the isolated simulator startup documented in Test-XRV058.py. The optional
--expected-roll sets and verifies a calibration in the isolated simulator.
Without that argument both 0 and 30 degrees are checked. The simulator backs up
and restores ordinary player preferences on cleanup. A final creative-mode round
starts with zero wood and disables fixture material grants throughout.
"""
import argparse
import importlib.util
from pathlib import Path

spec = importlib.util.spec_from_file_location('xr059', Path(__file__).with_name('Test-XRV059.py'))
base = importlib.util.module_from_spec(spec)
spec.loader.exec_module(base)


def set_palm(checks, target, **changes):
    state = checks.read('state.json')
    command = checks.read('command.json')
    correction = base.rotate(base.subtract(target, base.xyz(state['rightPalm'])), state['originEuler'], inverse=True)
    state = checks.send(frames=4, right=base.vector(base.add(base.xyz(command['right']), correction)), **changes)
    assert base.distance(base.xyz(state['rightPalm']), target) < .012, state
    return state


def run(checks, expected_roll):
    checks.send(leftPalmRoll=expected_roll, rightPalmRoll=expected_roll)
    checks.send(leftPalmRoll=999, rightPalmRoll=999)
    state = checks.prepare(2)
    if expected_roll is not None:
        assert abs(state['rightPalmRoll']-expected_roll) < .01, state
    roll = state['rightPalmRoll']
    return saw_round(checks, state, roll)


def saw_round(checks, state, roll, creative=False):
    assert state['creativeMode'] == creative, state
    palm = base.add(base.xyz(state['toolGrip']), base.rotate((0, .026, 0), state['rightPalmEuler']))
    set_palm(checks, palm)
    state = checks.send(rightGrip=1)
    assert state['handcraftHeld'] and state['handcraftKind'] == 2 and state['handcraftStrokes'] == 0, state
    before_wood, before_planks = state['driftwood'], state['planks']
    after_wood = before_wood if creative else before_wood-1
    suffix = ('Creative' if creative else 'Ordinary')+'Roll'+str(roll)
    board_right = base.rotate((0, 0, 1), state['toolEuler'])
    palm = base.add(palm, tuple(.12*x for x in board_right))
    state = set_palm(checks, palm)
    # The rendered middle of the plank is 12 cm from the old invisible kerf.
    assert base.distance(base.xyz(state['toolContact']), base.xyz(state['workPoint'])) < .012, state
    assert state['handcraftStrokes'] == 0, state
    state = checks.send(seconds=.6)
    assert state['handcraftStrokes'] == 0 and state['handcraftHeld'], state
    checks.remember('centreContactAndStationaryGrip'+suffix, state)
    lifted = base.add(palm, (0, .20, 0))
    for offset in (-.045, .045, -.045):
        axis = base.rotate((0, 1, 0), state['toolEuler'])
        state = set_palm(checks, base.add(lifted, tuple(offset*x for x in axis)))
        assert state['handcraftHeld'] and state['handcraftStrokes'] == 0, state
    state = set_palm(checks, palm)
    assert state['handcraftStrokes'] == 0, state
    checks.remember('airStrokesDoNotCut'+suffix, state)

    state = checks.send(rightActive=False)
    assert state['handcraftActive'] and not state['handcraftHeld'], state
    state = checks.send(rightActive=True, rightGrip=1)
    assert not state['handcraftHeld'], 'Tracking recovery must not re-grab a still-squeezed grip'
    checks.send(rightGrip=0)
    state = checks.read('state.json')
    palm = base.add(base.xyz(state['toolGrip']), base.rotate((0, .026, 0), state['rightPalmEuler']))
    set_palm(checks, palm)
    state = checks.send(rightGrip=1)
    assert state['handcraftHeld'], state
    palm = base.add(palm, tuple(.12*x for x in base.rotate((0, 0, 1), state['toolEuler'])))
    set_palm(checks, palm)
    state = checks.send(rightEuler=dict(x=0, y=35, z=0))
    palm = base.add(base.xyz(state['rightPalm']), base.subtract(base.xyz(state['workPoint']), base.xyz(state['toolContact'])))
    state = set_palm(checks, palm)
    assert state['handcraftHeld'] and state['handcraftStrokes'] == 0, state
    checks.remember('trackingReleaseAndAngledRegrip'+suffix, state)
    axis = base.rotate((0, 1, 0), state['toolEuler'])
    previous, greatest = 0, 0
    for stroke in range(16):
        following = .045 if stroke % 2 == 0 else -.045
        for frame in range(1, 7):
            value = previous+(following-previous)*frame/6
            state = set_palm(checks, base.add(palm, tuple(value*x for x in axis)))
            if not state['handcraftActive']:
                break
            assert state['handcraftHeld'], state
            greatest = max(greatest, state['handcraftStrokes'])
        previous = following
        if not state['handcraftActive']:
            break
        if stroke == 4:
            checks.send(seconds=.6, capture='v062-saw-'+('creative-' if creative else '')+'roll-'+str(round(roll)))
    assert not state['handcraftActive'] and greatest >= 8, state
    assert state['driftwood'] == after_wood and state['planks'] == before_planks+4, state
    checks.remember('continuousShortBladeStrokesComplete'+suffix, state)
    state = checks.send(seconds=.6)
    assert state['driftwood'] == after_wood and state['planks'] == before_planks+4, 'Held grip duplicated a saw reward'
    checks.send(rightGrip=0)
    return state


def set_creative(checks, enabled):
    if checks.read('state.json')['handcraftActive']:
        checks.send(rightGrip=0, b=True)
        checks.send(b=False)
    checks.open_home()
    state = checks.click(index=7)
    assert state['page'] == 14, state
    state = checks.click(index=7)
    assert state['page'] == 37, state
    if state['creativeMode'] != enabled:
        state = checks.click(index=0)
    assert state['creativeMode'] == enabled, state
    checks.send(b=True)
    return checks.send(b=False)


def creative_without_wood(checks):
    state = checks.send(**dict(base.NEUTRAL, fixture='ShoreFirstPerson', equip='FieldGuide',
                              supplyMaterials=False, head=dict(x=0, y=1.65, z=0), headEuler=base.ZERO,
                              left=base.NORMAL_LEFT, right=base.NORMAL_RIGHT,
                              leftEuler=base.ZERO, rightEuler=base.ZERO))
    assert state['driftwood'] == 0 and not state['creativeMode'], ('Fresh fixture must really have no wood', state)
    try:
        state = set_creative(checks, True)
        assert state['driftwood'] == 0, 'Turning on creative mode must not mint fixture material'
        # Menu aiming rotates the native controller; reset its pose before establishing the saw's grip frame.
        state = checks.send(table=2, supplyMaterials=False, left=base.NORMAL_LEFT, right=base.NORMAL_RIGHT,
                            leftEuler=base.ZERO, rightEuler=base.ZERO)
        assert state['driftwood'] == 0 and not state['handcraftActive'], ('The table fixture must not grant wood', state)
        checks.remember('creativeSawStartsWithActuallyZeroWood', state)
        state = saw_round(checks, state, state['rightPalmRoll'], creative=True)
        assert state['driftwood'] == 0 and state['creativeMode'], state
        checks.remember('creativeSawFinishesWithZeroWoodAndFourRealPlanks', state)
    finally:
        set_creative(checks, False)
        checks.send(supplyMaterials=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--expected-roll', type=float)
    parser.add_argument('--output', type=Path, default=base.ROOT / 'Logs/V062/NativeXR/native-saw.json')
    args = parser.parse_args()
    checks = base.NativeV059Checks(args.output, 25)
    try:
        for roll in ((0, 30) if args.expected_roll is None else (args.expected_roll,)):
            run(checks, roll)
        creative_without_wood(checks)
        checks.save(True)
        print('V062 NATIVE SAW PASS: real timber contact, tracked grip, angled short strokes, one real exchange')
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
