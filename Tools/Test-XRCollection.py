"""Check low tracked-hand collection and real pick contacts in native Mock OpenXR.

Start the isolated FishingXRSimulator documented in Test-XRV058.py first.
Requires its resource fixture/contact state fields. Does not start Unity or open
ordinary saves; output is simulator evidence, not PICO hardware certification.
"""
import argparse
import importlib.util
import math
from pathlib import Path

spec = importlib.util.spec_from_file_location('xr_collection_base', Path(__file__).with_name('Test-XRV059.py'))
base = importlib.util.module_from_spec(spec)
spec.loader.exec_module(base)


def prepare(checks, *, resource=-1, pickup=-1, equip='FieldGuide'):
    if checks.read('state.json')['notebook']:
        checks.send(b=True)
        checks.send(b=False)
    checks.send(**base.NEUTRAL, fixture='ShoreFirstPerson', resource=-1, pickup=-1, carcass=-1,
                equip=equip, head=dict(x=0, y=1.65, z=0), headEuler=base.ZERO,
                left=base.NORMAL_LEFT, right=base.NORMAL_RIGHT, leftEuler=base.ZERO, rightEuler=base.ZERO,
                applicationPaused=False)
    state = checks.send(resource=resource, pickup=pickup)
    assert 'resourceContact' in state and 'gatherTip' in state, 'Recompile the collection simulator state first'
    state = checks.send(head=dict(x=0, y=.85, z=0), headEuler=dict(x=55, y=0, z=0), seconds=.7)
    assert state['ashore'] and not state['headBlocked'] and not state['notebook'], state
    return state


def loose_pickups(checks):
    for side, pickup in (('right', 1), ('left', 3)):
        state = prepare(checks, pickup=pickup)
        before = state['pickupBagCount']
        target = base.add(base.xyz(state['pickupPosition']), (0, .025, 0))
        # The left-hand case deliberately looks away: physical touch must not
        # depend on a nearer object selected by the avatar/head gaze.
        if side == 'left':
            checks.send(headEuler=dict(x=15, y=100, z=0))
        checks.move_palm(side, target)
        state = checks.send(**{side+'Grip': 1})
        assert state['pickupCollected'] and state['pickupBagCount'] > before, (side, state)
        checks.remember(side+'CrouchedGroundPickup', state)
        count = state['pickupBagCount']
        state = checks.send(seconds=.8)
        assert state['pickupBagCount'] == count, ('Held grip duplicated a pickup', state)
        checks.send(**{side+'Grip': 0})


def hand_resources(checks):
    for side, resource in (('right', 0), ('left', 2)):
        state = prepare(checks, resource=resource)
        before = state['resourceBagCount']
        target = base.xyz(state['resourceContact'])
        state = checks.send(**{side+'Grip': 1})
        assert state['resourceReady'] and state['resourceBagCount'] == before, ('Remote grip collected a resource', state)
        checks.move_palm(side, target)
        state = checks.send(seconds=.3)
        assert state['resourceReady'] and state['resourceBagCount'] == before, ('Moving a held grip created a new press', state)
        checks.send(**{side+'Grip': 0})
        checks.send(**{side+'Active': False, side+'Grip': 1})
        state = checks.send(**{side+'Active': True})
        assert state['resourceReady'] and state['resourceBagCount'] == before, ('Tracking recovery collected on a held grip', state)
        checks.remember(side+'TrackingRecoveryDoesNotCollect', state)
        checks.send(**{side+'Grip': 0})
        state = checks.send(**{side+'Grip': 1}, seconds=.7, capture='collection-'+side+'-low-resource')
        assert not state['resourceReady'] and state['resourceBagCount'] == before+3, (side, state)
        checks.remember(side+'CrouchedResourceGrip', state)
        state = checks.send(seconds=.8)
        assert state['resourceBagCount'] == before+3, ('Held grip repeated a resource settlement', state)
        checks.send(**{side+'Grip': 0})


def controller_for_tip(checks, target):
    state = checks.read('state.json')
    command = checks.read('command.json')
    delta = base.rotate(base.subtract(target, base.xyz(state['gatherTip'])), state['originEuler'], inverse=True)
    return base.vector(base.add(base.xyz(command['right']), delta))


def pick_contacts(checks):
    for item, button, amount in (('StonePickaxe', 'a', 3), ('IronPickaxe', 'rightTrigger', 4)):
        state = prepare(checks, resource=4, equip=item)
        assert state['resourceReady'] and state['toolActive'] and state['heldItem'] == item, state
        before = state['resourceBagCount']
        # Park the pick away from the target before the manual edge. The move
        # into contact is >40cm, so SampleTool's teleport guard cannot mask a
        # stale-pose/manual-input failure with an automatic swing afterwards.
        target = base.xyz(state['resourceContact'])
        parked = base.add(target, (0, .8, 0))
        checks.send(right=controller_for_tip(checks, parked))
        state = checks.send(seconds=.7)
        assert state['resourceReady'] and base.distance(base.xyz(state['gatherTip']), target) > .4, state
        near = controller_for_tip(checks, target)
        state = checks.send(right=near, **{button: 1}, seconds=.7, capture='collection-'+item.lower()+'-contact')
        assert not state['resourceReady'] and state['resourceBagCount'] == before+amount, (item, button, state)
        checks.remember(item+'SameFrameContactAnd'+button, state)
        state = checks.send(seconds=.8)
        assert state['resourceBagCount'] == before+amount, ('Held input duplicated mined ore', state)
        checks.send(**{button: 0})

    state = prepare(checks, resource=4, equip='Axe')
    before = state['resourceBagCount']
    near = controller_for_tip(checks, base.xyz(state['resourceContact']))
    state = checks.send(right=near, rightTrigger=1, seconds=.7)
    assert state['resourceReady'] and state['resourceBagCount'] == before, ('The wrong tool harvested iron ore', state)
    checks.remember('WrongToolStillCannotMineIron', state)
    checks.send(rightTrigger=0)


def contextual_aquarium(checks):
    walk_spec = importlib.util.spec_from_file_location('collection_home_walk', Path(__file__).with_name('Test-XRV060.py'))
    home_walk = importlib.util.module_from_spec(walk_spec)
    walk_spec.loader.exec_module(home_walk)
    if checks.read('state.json')['notebook']:
        checks.send(b=True)
        checks.send(b=False)
    checks.send(homeReview=False)
    state = checks.send(**base.NEUTRAL, fixture='HomeInterior', resource=-1, pickup=-1, carcass=-1,
                        equip='FieldGuide', homeReview=True, head=dict(x=0, y=1.65, z=0), headEuler=base.ZERO,
                        left=base.NORMAL_LEFT, right=base.NORMAL_RIGHT, leftEuler=base.ZERO, rightEuler=base.ZERO,
                        applicationPaused=False)
    checks.send(homeReview=False)
    home = base.xyz(state['home'])
    for offset in ((0, 0, -2.8), (0, 0, -7.4), (8.3, 0, -7.4), (8.3, 0, -5.65)):
        state = home_walk.walk(checks, base.add(home, offset))
    target = base.add(base.xyz(state['aquarium']), (0, .9, 0))
    for _ in range(2):
        delta = base.rotate(base.subtract(target, base.xyz(state['camera'])), state['originEuler'], inverse=True)
        state = checks.send(headEuler=dict(x=-math.degrees(math.atan2(delta[1], math.hypot(delta[0], delta[2]))),
                                           y=math.degrees(math.atan2(delta[0], delta[2])), z=0))
    assert state['aquariumCount'] > 0 and not state['notebook'], state
    for side in ('left', 'right'):
        state = checks.send(**{side+'Active': False}, a=True)
        assert not state['notebook'], ('Untracked A opened the aquarium interface', side, state)
        state = checks.send(**{side+'Active': True})
        assert not state['notebook'], ('Tracking recovery repeated held A into the aquarium interface', side, state)
        checks.remember(side+'TrackingLossAndRecoveryBlocksAquariumA', state)
        checks.send(a=False)
    state = checks.send(a=True, seconds=.6, capture='collection-native-aquarium-context')
    assert state['notebook'] and state['page'] == 21, ('Tracked A failed to open the real aquarium', state)
    checks.remember('TrackedAquariumAOpensHomeEcology', state)
    checks.send(a=False)
    checks.send(b=True)
    checks.send(b=False)


def animal_showcase(checks):
    realm_spec = importlib.util.spec_from_file_location('collection_animal_ui', Path(__file__).with_name('Test-XRV063Realms.py'))
    ui = importlib.util.module_from_spec(realm_spec)
    realm_spec.loader.exec_module(ui)
    if checks.read('state.json')['notebook']:
        checks.send(b=True)
        checks.send(b=False)
    checks.send(homeReview=False)
    state = checks.send(**dict(base.NEUTRAL, fixture='HomeInterior', resource=-1, pickup=-1, carcass=-1,
                              equip='FieldGuide', homeReview=True, head=dict(x=0, y=1.65, z=0), headEuler=base.ZERO,
                              left=base.NORMAL_LEFT, right=base.NORMAL_RIGHT, leftEuler=base.ZERO, rightEuler=base.ZERO,
                              realmDevice=-1, marketReview=False, applicationPaused=False))
    state = checks.send(homeReview=False)
    assert 'housedAnimals' in state and len(state['housedAnimals']) == 5, state
    checks.open_home()
    ui.expect_page(checks, checks.click(index=7), 14, 'The real B-menu settings entry did not open')
    state = ui.expect_page(checks, checks.click(index=7), 37, 'Settings did not open creative mode')
    if state['creativeMode']:
        checks.click(index=0)
        ui.wait_state(checks, lambda s: not s['creativeMode'], 'The UI could not disable the previous creative policy')
    checks.click(index=0)
    state = ui.wait_state(checks, lambda s: s['creativeMode'], 'The actual creative toggle did not enable the policy')
    before, coins = list(state['housedAnimals']), state['coins']
    checks.remember('NativeCreativeModeEnabledBeforeAnimalPreparation', state)
    checks.click(index=6, contains='补齐养殖动物')
    state = ui.wait_state(checks, lambda s: all(n >= 2 for n in s['housedAnimals']), 'The real showcase button did not populate all five habitats')
    populated = list(state['housedAnimals'])
    assert state['coins'] == coins, ('Preparing showcase animals changed the wallet', state)
    checks.remember('NativeAnimalShowcaseButtonPopulatesFiveHabitats', state)
    checks.click(index=6, contains='补齐养殖动物')
    state = checks.send(seconds=.7, capture='v063-native-animal-showcase-menu')
    assert state['housedAnimals'] == populated and state['coins'] == coins, ('Repeated preparation duplicated animals or charged coins', state)
    checks.remember('NativeAnimalShowcasePreparationIsIdempotent', state)
    checks.evidence['animalPreparation'] = {'beforeHoused': before, 'afterHoused': populated,
                                          'sourceLayout': 'FishingHomeEcologyWorld.HabitatPosition: home+(-7.5+kind*4,groundY,7.5)',
                                          'scope': 'Real native UI and walking; visual animal visibility requires inspecting the captured renders'}
    checks.send(b=True)
    checks.send(b=False, headEuler=base.ZERO, left=base.NORMAL_LEFT, right=base.NORMAL_RIGHT,
                leftEuler=base.ZERO, rightEuler=base.ZERO)
    animal_views(checks)


def animal_views(checks):
    walk_spec = importlib.util.spec_from_file_location('collection_animal_view_walk', Path(__file__).with_name('Test-XRV060.py'))
    home_walk = importlib.util.module_from_spec(walk_spec)
    walk_spec.loader.exec_module(home_walk)
    state = checks.read('state.json')
    populated = list(state['housedAnimals'])
    assert len(populated) == 5 and all(n >= 2 for n in populated), ('Prepare five habitats through the real menu before capturing views', state)
    assert state['ashore'] and not state['notebook'] and not state['realmInside'], state
    home = base.xyz(state['home'])
    relative = base.subtract(base.xyz(state['feet']), home)
    assert math.hypot(relative[0], relative[2]) < 25, ('Animal views must continue the prepared home career', state)
    checks.evidence['continuedProfile'] = state['profile']
    checks.evidence['housedBeforeWalking'] = populated
    # The old (10.5,-7.4) waypoint cuts into the well at (10.2,-8).
    # Pass the aquarium on its west side, then the pens on their east side.
    if relative[2] > 10.8:
        approach = []
    elif relative[0] > 7:
        approach = [(relative[0], 0, -5.65), (6.65, 0, -5.65)]
    else:
        approach = [(0, 0, -2.8), (0, 0, -7.4), (8.3, 0, -7.4), (8.3, 0, -5.65), (6.65, 0, -5.65)]
    if relative[2] <= 10.8:
        approach += [(6.65, 0, 6.1), (10.65, 0, 6.1), (10.65, 0, 11.5), (.5, 0, 11.5)]
    for offset in approach+[(.5, 0, 19)]:
        state = home_walk.walk(checks, base.add(home, offset))
        assert state['housedAnimals'] == populated and not state['headBlocked'], ('Walking lost the prepared career or entered a solid surface', state)

    def capture_animals(target, stem):
        state = checks.read('state.json')
        for _ in range(2):
            delta = base.rotate(base.subtract(target, base.xyz(state['camera'])), state['originEuler'], inverse=True)
            state = checks.send(headEuler=dict(x=-math.degrees(math.atan2(delta[1], math.hypot(delta[0], delta[2]))),
                                               y=math.degrees(math.atan2(delta[0], delta[2])), z=0))
        state = checks.send(seconds=1, capture=stem)
        assert state['housedAnimals'] == populated and not state['notebook'] and not state['headBlocked'], state
        checks.remember(stem, state)
        return state

    capture_animals(base.add(home, (.5, .5, 8.5)), 'v063-native-animal-habitats-wide')
    home_walk.walk(checks, base.add(home, (.5, 0, 11.5)))
    for kind, name in enumerate(('rabbit', 'fox', 'tortoise', 'gull', 'seaturtle')):
        x = -7.5+kind*4
        state = home_walk.walk(checks, base.add(home, (x, 0, 11.5)))
        assert state['housedAnimals'] == populated and not state['headBlocked'], state
        capture_animals(base.add(home, (x-.75, .85 if kind == 3 else .35, 8.7)), 'v063-native-housed-'+name+'-pair')


def gull_view(checks):
    walk_spec = importlib.util.spec_from_file_location('collection_gull_walk', Path(__file__).with_name('Test-XRV060.py'))
    home_walk = importlib.util.module_from_spec(walk_spec)
    walk_spec.loader.exec_module(home_walk)
    state = checks.read('state.json')
    profile, housed = state['profile'], list(state['housedAnimals'])
    assert state['ashore'] and not state['realmInside'] and not state['notebook'] and housed[3] >= 2, state
    home = base.xyz(state['home'])
    assert base.distance(base.xyz(state['feet']), home) < 25, state
    checks.evidence['continuedProfile'] = profile
    checks.evidence['housedBeforeWalking'] = housed
    checks.evidence['visualScope'] = 'Two side views of the existing gull pair; the formal HUD remains enabled. Inspect the PNGs before claiming both birds are visible.'
    # Both stations remain in front of the closed pens. The gulls perch at
    # habitat+(slot x -.72/-.24, y .595, z -.2), not the ground-animal run z+1.16.
    for offset, bias, stem in (((6.1, 0, 12.4), 28, 'v063-native-housed-gull-pair-east-oblique'),
                               ((3.0, 0, 12.7), -28, 'v063-native-housed-gull-pair-west-oblique')):
        state = home_walk.walk(checks, base.add(home, offset))
        assert state['profile'] == profile and state['housedAnimals'] == housed and not state['headBlocked'], state
        target = (home[0]+4.02, state['feet']['y']+.72, home[2]+7.3)
        delta = base.rotate(base.subtract(target, base.xyz(state['camera'])), state['originEuler'], inverse=True)
        yaw = math.degrees(math.atan2(delta[0], delta[2]))
        pitch = -math.degrees(math.atan2(delta[1], math.hypot(delta[0], delta[2])))
        # A normal head turn lets the lazy-follow card settle first; the final
        # view leaves the birds 28 degrees off centre instead of behind the HUD.
        checks.send(headEuler=dict(x=pitch, y=yaw+110, z=0), seconds=1.8)
        checks.send(headEuler=dict(x=pitch, y=yaw+bias, z=0), seconds=1.8)
        state = checks.send(seconds=.7, capture=stem)
        assert state['profile'] == profile and state['housedAnimals'] == housed and not state['headBlocked'] and not state['notebook'], state
        checks.remember(stem, state)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--only', choices=('all', 'pickup', 'resource', 'pick', 'context', 'animals', 'animal-views', 'gull-view'), default='all')
    args = parser.parse_args()
    filename = {'context': 'native-context-routing.json', 'animals': 'native-animal-showcase.json',
                'animal-views': 'native-animal-showcase-views.json', 'gull-view': 'native-gull-view.json'}.get(args.only, 'native-collection.json')
    checks = base.NativeV059Checks(base.ROOT / 'Logs/CollectionReview' / filename, 25)
    checks.evidence['selectedChecks'] = args.only
    try:
        if args.only in ('all', 'pickup'):
            loose_pickups(checks)
        if args.only in ('all', 'resource'):
            hand_resources(checks)
        if args.only in ('all', 'pick'):
            pick_contacts(checks)
        if args.only in ('all', 'context'):
            contextual_aquarium(checks)
        if args.only in ('all', 'animals'):
            animal_showcase(checks)
        if args.only == 'animal-views':
            animal_views(checks)
        if args.only == 'gull-view':
            gull_view(checks)
        checks.save(True)
        print('NATIVE COLLECTION PASS')
    except Exception as error:
        checks.save(False, error)
        raise
    finally:
        try:
            released = checks.send(**dict(base.NEUTRAL, resource=-1, pickup=-1, carcass=-1,
                                          realmDevice=-1, homeReview=False, marketReview=False))
            checks.remember('FinalInputsReleased', released)
            checks.save(checks.evidence.get('passed', False))
        except Exception as cleanup_error:
            print('NATIVE COLLECTION CLEANUP: '+type(cleanup_error).__name__+': '+str(cleanup_error))


if __name__ == '__main__':
    main()
