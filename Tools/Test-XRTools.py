"""Native XR tool regression; run while FishingXRSimulator uses an isolated profile."""
import json
import os
from pathlib import Path
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
SIM = ROOT / 'Logs' / 'XRSimulator'
MODE = sys.argv[1] if len(sys.argv) > 1 else 'fixed'
assert MODE in ('baseline', 'fixed')
OUT = ROOT / 'Logs' / 'V052' / 'ToolInput'
OUT.mkdir(parents=True, exist_ok=True)


def read(name):
    for _ in range(30):
        try:
            return json.loads((SIM / name).read_text(encoding='utf-8-sig'))
        except (ValueError, OSError):
            time.sleep(.05)
    raise RuntimeError('Cannot read simulator ' + name)


def send(**changes):
    before = read('state.json')['frame']
    command = read('command.json')
    command.update(capture='', fixture='', equip='')
    command.update(changes)
    temporary = SIM / 'tools-command.tmp'
    temporary.write_text(json.dumps(command), encoding='utf-8')
    os.replace(temporary, SIM / 'command.json')
    deadline = time.monotonic() + 15
    while time.monotonic() < deadline:
        time.sleep(.1)
        state = read('state.json')
        if state['frame'] > before + 8 and state['focused']:
            assert state['xr'] and state['isolated'] and not state['automated']
            assert state['leftTracked'] and state['rightTracked'] and state['seatReady']
            return state
    raise TimeoutError('Simulator did not process live focused XR input')


evidence = {}
initial = read('state.json')
assert initial['xr'] and initial['isolated'] and not initial['automated'], 'An isolated native XR simulator is required'
neutral = dict(a=False, b=False, x=False, y=False, leftClick=False, rightClick=False,
               leftTrigger=0, rightTrigger=0, leftGrip=0, rightGrip=0,
               leftStick=dict(x=0, y=0), rightStick=dict(x=0, y=0))
normal = dict(x=.24, y=1.2, z=.35)
send(**neutral)
send(fixture='ShoreFirstPerson', head=dict(x=0,y=1.65,z=0), headEuler=dict(x=0,y=0,z=0),
     left=dict(x=-.24,y=1.2,z=.35), leftEuler=dict(x=0,y=0,z=0), right=normal, rightEuler=dict(x=0,y=0,z=0))
s = send(equip='HuntingRifle')
assert s['ashore'] and s['motionReady'] and not s['headBlocked'], s
assert s['riflePresented'] and not s['rifleObstructed'], s
start = s['shotsFired']
s = send(rightTrigger=1)
assert s['triggerValue'] > .9, 'Native runtime must receive the trigger value'
evidence['rifle_pressed'] = s
assert s['shotsFired'] > start, 'A normal tracked rifle trigger must fire'
send(rightTrigger=0)
s = send(right=dict(x=.16, y=1.60, z=.10))
evidence['rifle_at_eye'] = s
start = s['photosTaken']
s = send(rightTrigger=1)
evidence['rifle_at_eye_pressed'] = s
if MODE == 'fixed':
    assert not s['cameraRaised'] and s['photosTaken'] == start, 'A raised rifle also activated the camera'
send(rightTrigger=0, right=normal)

# Selecting binoculars must explain the activation; the same hand gesture must not open the camera.
s = send(equip='Binoculars')
if MODE == 'fixed':
    assert s['hintContext'] == 'Binocular' and not s['binocularRaised'], s
s = send(rightTrigger=1)
evidence['binocular_raised'] = s
if MODE == 'fixed':
    assert s['binocularRaised'] and '22 米' in s['message'] and 'A 记录' in s['message'], s
send(rightTrigger=0)
s = send(right=dict(x=.16, y=1.60, z=.10))
evidence['binocular_at_eye'] = s
if MODE == 'fixed':
    assert not s['cameraRaised'], 'Looking through binoculars also opened the camera'
s = send(a=True)
evidence['binocular_record'] = s
if MODE == 'fixed':
    assert '陆地动物' in s['message'] or '已记录' in s['message'] or '自然手册新增' in s['message'], s
send(a=False, right=normal)
s = send(rightTrigger=1)
if MODE == 'fixed':
    assert not s['binocularRaised'], 'A second trigger press must lower binoculars'
send(rightTrigger=0)

# On the boat the shutter cannot also invoke the shore tool action.
send(fixture='FreePlay', right=dict(x=0,y=1.65,z=.1), equip='Lantern')
s = send()
assert s['cameraRaised'] and not s['anchored'] and not s['holdingRod'], s
if MODE == 'fixed':
    assert s['hintContext'] == 'Camera', 'Raised camera did not select camera controls'
start = s['photosTaken']
s = send(rightTrigger=1)
evidence['camera_pressed'] = s
assert s['photosTaken'] == start+1, 'A fresh shutter press must take exactly one photo'
s = send()
assert s['photosTaken'] == evidence['camera_pressed']['photosTaken'], 'Held trigger repeated shutter'
send(b=True)
s = send(b=False)
assert s['notebook'], 'B must open the notebook from the camera'
s = send(b=True)
assert not s['notebook'], 'B must close the notebook'
s = send(b=False)
evidence['camera_menu_recovery'] = s
assert s['photosTaken'] == start+1, 'Closing the notebook fired a held shutter'
send(rightTrigger=0)
s = send(rightTrigger=1)
assert s['photosTaken'] == start+2, 'Releasing and pressing the shutter after the menu must work'
send(rightTrigger=0)
s = send(right=normal)
assert not s['cameraRaised'], 'Lowering the right hand must stow the camera'
evidence['camera_stowed'] = s
(OUT / (MODE + '.json')).write_text(json.dumps(evidence, ensure_ascii=False, indent=2), encoding='utf-8')
print('FIXED PASS: native rifle, camera exclusivity, binocular guidance and shutter recovery evidence saved'
      if MODE == 'fixed' else 'BASELINE OBSERVED: native tool state saved; new exclusivity/guidance guards were not asserted')
