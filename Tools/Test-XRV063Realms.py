"""Check fixed telescope and creative realm demos through native Mock OpenXR input.

Start the isolated simulator documented in Test-XRV058.py first. This script
does not start Unity, change production objects directly, or access player saves.
Fixtures only provide initial scenes and supported device approaches; real
controller buttons, sticks and notebook rays perform the actions under test.
"""
import argparse
import importlib.util
from pathlib import Path
import time

spec = importlib.util.spec_from_file_location('xr059', Path(__file__).with_name('Test-XRV059.py'))
base = importlib.util.module_from_spec(spec)
spec.loader.exec_module(base)
ZERO_STICK = dict(x=0, y=0)


class RealmChecks(base.NativeV059Checks):
    def click(self, index=None, contains=None):
        aimed = self.aim(index, contains)
        assert aimed['triggerValue'] < .1 and not aimed['triggerButton'], ('Trigger was not released before this click', aimed)
        target = self.button(aimed, index, contains)
        pressed = self.send(rightTrigger=1)
        released = self.send(rightTrigger=0)
        keys = ('commandSequence', 'commandFrame', 'frame', 'page', 'notebook', 'motionReady',
                'headBlocked', 'leftTracked', 'rightTracked', 'triggerValue', 'triggerButton',
                'rayTarget', 'right', 'rightForward', 'camera', 'feet', 'buttons')
        self.evidence.setdefault('nativeClicks', []).append({
            'sourcePage': aimed['page'], 'index': target['index'], 'label': target['label'],
            'aimed': {key: aimed[key] for key in keys},
            'pressed': {key: pressed[key] for key in keys},
            'released': {key: released[key] for key in keys}})
        return released


def expect_page(checks, state, page, label):
    deadline = time.monotonic()+1.5
    # Observe the single real press through release; never send another click to hide a rejected one.
    while (state['page'] != page or not state['notebook']) and time.monotonic() < deadline:
        state = checks.send(rightTrigger=0, frames=3)
    if state['page'] != page or not state['notebook']:
        failure = checks.send(rightTrigger=0, capture='v063-native-rejected-page-'+str(page))
        checks.remember('RejectedPageTransition'+str(page), failure)
        raise AssertionError((label, 'Expected page '+str(page), failure))
    return state


def wait_state(checks, condition, label, timeout=15):
    deadline = time.monotonic()+timeout
    while time.monotonic() < deadline:
        state = checks.send(frames=3)
        if condition(state):
            return state
    raise AssertionError((label, state))


def prepare(checks, fixture):
    return checks.send(**dict(base.NEUTRAL, fixture=fixture, equip='FieldGuide',
                              head=dict(x=0, y=1.65, z=0), headEuler=base.ZERO,
                              left=base.NORMAL_LEFT, right=base.NORMAL_RIGHT,
                              leftEuler=base.ZERO, rightEuler=base.ZERO,
                              table=-1, orchard=-1, realmDevice=-1, resource=-1,
                              pickup=-1, carcass=-1, homeReview=False,
                              marketReview=False, applicationPaused=False))


def press_a(checks):
    checks.send(a=True)
    return checks.send(a=False)


def align_telescope(checks):
    state = checks.send(rightTrigger=0, rightGrip=0, leftGrip=0)
    stage = state['starStage']
    for _ in range(90):
        dx, dy = state['starTargetYaw']-state['starYaw'], state['starTargetPitch']-state['starPitch']
        assert state['realmObserving'] and state['starStage'] == stage, state
        if max(abs(dx), abs(dy)) < .7:
            state = checks.send(rightStick=ZERO_STICK)
            assert abs(state['starTargetYaw']-state['starYaw']) < 1.4, state
            assert abs(state['starTargetPitch']-state['starPitch']) < 1.4, state
            return state
        state = checks.send(frames=3, seconds=.1,
                            rightStick=dict(x=max(-.8, min(.8, dx*.04)),
                                            y=max(-.8, min(.8, dy*.04))))
    raise AssertionError(('Native right-stick aim failed to converge', state))


def raise_binoculars(checks, state):
    state = checks.send(leftGrip=1, rightGrip=1,
                        leftEuler=dict(x=15, y=20, z=10-state['leftPalmRoll']),
                        rightEuler=dict(x=15, y=20, z=10-state['rightPalmRoll']),
                        left=dict(x=-.094, y=1.65, z=.158), right=dict(x=.094, y=1.65, z=.158))
    for _ in range(2):
        command, changes = checks.read('command.json'), {}
        for side, sign in (('left', -1), ('right', 1)):
            desired = base.add(base.xyz(state['camera']), base.rotate((sign*.094, 0, .158), state['originEuler']))
            correction = base.rotate(base.subtract(desired, base.xyz(state[side+'Palm'])), state['originEuler'], inverse=True)
            changes[side] = base.vector(base.add(base.xyz(command[side]), correction))
        state = checks.send(**changes)
    state = checks.send(rightTrigger=1)
    assert state['binocularOpticsActive'] and not state['headBlocked'], state
    return state


def stars(checks):
    state = prepare(checks, 'StarSanctuary')
    state = wait_state(checks, lambda s: s['realmInside'] and s['realmIsland'] == 3 and not s['realmTransitioning'],
                       'Star sanctuary did not finish its production entrance transition')
    state = checks.send(realmDevice=0)
    assert not state['realmObserving'] and state['starStage'] == 0 and not state['headBlocked'], state
    checks.remember('FixedScopeInitiallyClosed', state)
    # The asynchronous fixture selects its default item after entering the room.
    state = checks.send(equip='Binoculars')
    assert state['gatherItem'] == 'Binoculars' and state['heldItem'] == 'Binoculars' and state['toolActive'], state
    checks.remember('BinocularsEquippedAfterAsynchronousRealmEntry', state)
    state = raise_binoculars(checks, state)
    checks.remember('PortableBinocularsActuallyRaisedBesideFixedScope', state)
    state = press_a(checks)
    assert state['realmObserving'] and state['realmIsland'] == 3 and not state['notebook'], state
    checks.remember('NativeAPrioritizesFixedScopeOverPortableBinoculars', state)
    checks.send(rightTrigger=0, leftGrip=0, rightGrip=0,
                left=base.NORMAL_LEFT, right=base.NORMAL_RIGHT, leftEuler=base.ZERO, rightEuler=base.ZERO)
    state = align_telescope(checks)
    state = checks.send(rightTrigger=1, seconds=.3)
    assert 0 < state['starFocus'] < 1 and state['starStage'] == 0, state
    checks.remember('NativeTriggerBuildsActualContinuousFocus', state)
    state = checks.send(rightActive=False, rightTrigger=1, seconds=.2)
    assert state['starFocus'] == 0 and state['starStage'] == 0, state
    checks.remember('TrackingLossClearsFocusWithoutRecording', state)
    state = checks.send(rightActive=True, rightTrigger=1, seconds=.45)
    assert state['starFocus'] == 0 and state['starStage'] == 0, state
    checks.remember('HeldTriggerAfterTrackingRecoveryCannotResumeFocus', state)
    checks.send(rightTrigger=0)
    checks.send(rightTrigger=1, seconds=.25)
    checks.send(b=True)
    state = checks.send(b=False)
    assert not state['realmObserving'] and not state['notebook'] and state['starFocus'] == 0 and state['starStage'] == 0, state
    checks.remember('NativeBCancelsScopeAndClearsPartialFocus', state)
    state = press_a(checks)
    assert state['realmObserving'], state
    state = checks.send(rightTrigger=1, seconds=.4)
    assert state['starFocus'] == 0 and state['starStage'] == 0, state
    checks.remember('ReopenedScopeRequiresFreshTriggerRelease', state)
    checks.send(rightTrigger=0)
    for stage in range(3):
        state = align_telescope(checks)
        assert state['starStage'] == stage, state
        checks.send(headEuler=dict(x=-state['starPitch'], y=state['starYaw'], z=0))
        checks.send(rightTrigger=1, seconds=.25, capture='v063-native-constellation-'+str(stage))
        state = wait_state(checks, lambda s: s['starStage'] == stage+1,
                           'Sustained native focus did not record constellation '+str(stage), timeout=8)
        checks.remember('NativeFocusRecordsConstellation'+str(stage), state)
        checks.send(rightTrigger=0)
    checks.send(b=True)
    state = checks.send(b=False, rightTrigger=0, headEuler=base.ZERO)
    assert not state['realmObserving'] and state['starStage'] == 3, state
    checks.remember('AllThreeConstellationsRetainedAfterLeavingScope', state)
    state = checks.send(realmDevice=3, equip='FieldGuide', a=False, b=False,
                        rightTrigger=0, leftGrip=0, rightGrip=0,
                        left=base.NORMAL_LEFT, right=base.NORMAL_RIGHT,
                        leftEuler=base.ZERO, rightEuler=base.ZERO, headEuler=base.ZERO)
    assert state['realmInside'] and state['realmIsland'] == 3 and not state['realmObserving'], state
    assert not state['realmCleared'][3], ('The first-reward fixture already cleared the star realm', state)
    checks.remember('ActualStarEchoApproachBeforeNativeA', state)
    coins = state['coins']
    checks.send(a=False, rightTrigger=0, seconds=.2)
    state = press_a(checks)
    assert state['realmInside'] and state['realmIsland'] == 3 and state['realmCleared'][3], (
        'The real A press at realmDevice=3 did not claim the star echo; inspect the unmodified target selection', state)
    assert state['coins'] == coins+90, ('First star completion must pay exactly 90 coins', state)
    checks.remember('NativeAClaimsCompletedStarEchoOnce', state)
    state = checks.send(seconds=.6, capture='v063-native-star-echo-first-reward')
    checks.remember('StarEchoRewardFeedbackCapture', state)
    if state['hintContext'] == 'Story':
        # B is the production story skip button; no domain command or direct modal manipulation is used.
        checks.send(b=False)
        checks.send(b=True)
        checks.send(b=False)
        state = wait_state(checks, lambda s: s['hintContext'] != 'Story',
                           'The ordinary B story-skip button did not release the reward film', timeout=8)
        checks.remember('NativeBClosesStarRewardFilm', state)
    assert not state['notebook'] and state['realmInside'] and state['realmCleared'][3], state
    checks.send(a=False, rightTrigger=0, seconds=.2)
    state = press_a(checks)
    assert state['realmInside'] and state['realmIsland'] == 3 and state['realmCleared'][3] and state['coins'] == coins+90, (
        'Repeated A changed or paid the settled star echo again', state)
    checks.remember('RepeatedNativeACannotDuplicateStarCompletionReward', state)
    checks.send(seconds=.6, capture='v063-native-star-echo-settled')


def open_demo(checks):
    checks.open_home()
    expect_page(checks, checks.click(index=7), 14, 'Home settings button did not open settings')
    state = expect_page(checks, checks.click(index=7), 37, 'Settings creative button did not open creative mode')
    if not state['creativeMode']:
        state = checks.click(index=0)
    assert state['creativeMode'], state
    state = expect_page(checks, checks.click(index=5), 39, 'Creative realm-demo button rejected one real released trigger press')
    if not state['creativeRealmTravel']:
        state = checks.click(index=4)
    assert state['creativeRealmTravel'], state
    return state


def demo(checks):
    prepare(checks, 'HomeInterior')
    state = wait_state(checks, lambda s: s['ashore'] and not s['realmInside'] and not s['realmTransitioning']
                       and base.distance(base.xyz(s['feet']), base.xyz(s['home'])) < 25,
                       'Home fixture did not finish before opening the demonstration menus')
    state = checks.send(equip='FieldGuide')
    assert state['gatherItem'] == 'FieldGuide' and not state['realmInside'], state
    for island in range(4):
        state = open_demo(checks)
        checks.remember('NativeDemoMenuBeforeIsland'+str(island), state)
        state = checks.click(index=island)
        assert not state['notebook'] and not state['realmInside'] and state['ashore'], state
        assert base.distance(base.xyz(state['feet']), base.xyz(state['realmEntrances'][island])) < 1.4, state
        checks.remember('NativeMenuTeleportsToRealIslandEntrance'+str(island), state)
        # A fixture is deliberately not used here: the ordinary A interaction must enter.
        press_a(checks)
        state = wait_state(checks, lambda s: s['realmInside'] and s['realmIsland'] == island and not s['realmTransitioning'],
                           'Native A could not enter island '+str(island))
        checks.remember('NativeAEntersRealm'+str(island), state)
        open_demo(checks)
        assert checks.click(index=5)['page'] == 40
        state = checks.click(index=island)
        assert state['page'] == 40 and '确认重置' in checks.button(state, index=0)['label'], state
        state = checks.click(index=0)
        assert state['page'] == 39 and not state['realmInside'] and state['ashore'], state
        assert not state['realmCleared'][island], state
        assert base.distance(base.xyz(state['feet']), base.xyz(state['realmEntrances'][island])) < 1.4, state
        checks.remember('NativeResetLeavesRealmSafelyBeforeReset'+str(island), state)
        checks.send(seconds=.6, capture='v063-native-reset-realm-'+str(island))
    assert checks.click(index=5)['page'] == 40
    checks.click(index=4)
    state = checks.click(index=0)
    assert state['page'] == 39 and not any(state['realmCleared']), state
    checks.remember('NativeAllRealmResetConfirmation', state)
    state = checks.click(index=6)
    assert all(count >= 2 for count in state['housedAnimals']), state
    housed, coins = state['housedAnimals'], state['coins']
    checks.remember('NativeCreativeButtonPopulatesEveryHabitat', state)
    state = checks.click(index=6)
    assert state['housedAnimals'] == housed and state['coins'] == coins, state
    checks.remember('NativeAnimalShowcaseIsIdempotent', state)
    checks.send(seconds=.6, capture='v063-native-realm-demo-menu')
    checks.send(b=True)
    checks.send(b=False)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--only', choices=('stars', 'demo', 'all'), default='all')
    parser.add_argument('--output', type=Path, default=base.ROOT / 'Logs/V063/NativeXR/native-realms.json')
    parser.add_argument('--timeout', type=float, default=30)
    args = parser.parse_args()
    checks = RealmChecks(args.output, args.timeout)
    try:
        state = checks.read('state.json')
        required = {'realmInside', 'realmObserving', 'realmTransitioning', 'starTargetYaw', 'starTargetPitch',
                    'starFocus', 'realmEntrances', 'realmCleared', 'housedAnimals', 'creativeRealmTravel'}
        assert required.issubset(state), ('Recompile the V063 native simulator', required-set(state))
        checks.evidence['selectedChecks'] = args.only
        if args.only in ('stars', 'all'):
            stars(checks)
        if args.only in ('demo', 'all'):
            demo(checks)
        checks.save(True)
        print('V063 NATIVE REALMS PASS: '+args.only)
        print(args.output)
    except BaseException as error:
        checks.save(False, error)
        raise
    finally:
        try:
            checks.send(**dict(base.NEUTRAL, headEuler=base.ZERO, left=base.NORMAL_LEFT,
                               right=base.NORMAL_RIGHT, leftEuler=base.ZERO, rightEuler=base.ZERO,
                               realmDevice=-1, resource=-1))
        except (AssertionError, KeyError, OSError, RuntimeError, TimeoutError):
            pass


if __name__ == '__main__':
    main()
