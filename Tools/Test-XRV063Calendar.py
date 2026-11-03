"""Exercise the unified VR calendar and festival merchant through native OpenXR.

Run the isolated PICO-quality simulator documented by Test-XRV058.py first.
This script drives controller buttons and rays. Only market placement and the
initial career use an explicit isolated fixture; purchases use the real UI.
"""
import argparse
import importlib.util
from pathlib import Path

spec = importlib.util.spec_from_file_location('xr059', Path(__file__).with_name('Test-XRV059.py'))
base = importlib.util.module_from_spec(spec)
spec.loader.exec_module(base)


def close_notebook(checks):
    if checks.read('state.json')['notebook']:
        checks.send(b=True)
        checks.send(b=False)


def creative(checks, enabled):
    state = checks.read('state.json')
    if state['creativeMode'] == enabled:
        return
    checks.open_home()
    checks.click(index=7)
    checks.click(index=7)
    state = checks.click(index=0)
    assert state['creativeMode'] == enabled, ('Creative-mode UI did not apply', state)
    close_notebook(checks)


def calendar(checks):
    close_notebook(checks)
    checks.send(**dict(base.NEUTRAL, fixture='HomeInterior', equip='FieldGuide',
                head=dict(x=0, y=1.65, z=0), headEuler=base.ZERO,
                left=base.NORMAL_LEFT, right=base.NORMAL_RIGHT,
                leftEuler=base.ZERO, rightEuler=base.ZERO,
                marketReview=False, table=-1, orchard=-1, pickup=-1, carcass=-1))
    checks.open_home()
    checks.click(index=7)
    state = checks.click(index=6)
    assert state['page'] == 34, state
    dates = [b for b in state['buttons'] if 700 <= b['index'] < 728]
    assert len(dates) == 28, ('The month grid is missing dates', dates)
    checks.remember('SettingsUsesTwentyEightDayCalendar', checks.send(seconds=.6, capture='v063-native-month-calendar'))
    before_day, before_coins = state['day'], state['coins']
    checks.click(index=741)
    state = checks.click(index=761)
    expected_day = (state['calendarYear']-1)*112+state['calendarSeason']*28+13
    assert state['calendarDate'] == 14 and state['calendarHour'] == 10, state
    assert expected_day > before_day, state
    checks.remember('NextFestivalIsSelectedAtTradingTime', checks.send(seconds=.6, capture='v063-native-calendar-festival'))
    state = checks.click(index=760)
    assert state['day'] == expected_day and state['coins'] == before_coins, state
    checks.remember('NativeDateJumpPreservesWallet', state)
    checks.click(index=740)
    checks.click(index=740)
    checks.click(index=700)
    state = checks.click(index=760)
    assert state['day'] == expected_day and '只能向前' in state['classroomMessage'], state
    checks.remember('NativeBackwardDateJumpIsRefused', state)
    before_profile = state['profile']
    state = checks.click(index=763)
    assert state['profile'] == before_profile and not state['classroom'], state
    assert '确认重建' in checks.button(state, index=763)['label'], state['buttons']
    checks.remember('IndependentResetNeedsSecondExplicitPress', state)
    checks.click(index=765)
    close_notebook(checks)
    checks.open_home()
    checks.click(index=3)
    state = checks.click(index=204)
    assert state['page'] == 3 and len([b for b in state['buttons'] if 700 <= b['index'] < 728]) == 28, state
    assert state['day'] == expected_day, state
    checks.remember('JournalAndSettingsReachSameCalendarGrid', checks.send(seconds=.6, capture='v063-native-calendar-journal-entry'))
    close_notebook(checks)


def merchant(checks):
    close_notebook(checks)
    creative(checks, False)
    checks.send(**base.NEUTRAL, fixture='Calendar', marketReview=False,
                head=dict(x=0, y=1.65, z=0), headEuler=base.ZERO,
                left=base.NORMAL_LEFT, right=base.NORMAL_RIGHT,
                leftEuler=base.ZERO, rightEuler=base.ZERO)
    state = checks.send(marketReview=True, seconds=.8)
    assert state['marketVisible'] and state['day'] == 13 and not state['ashore'] and state['speed'] < 2.5, state
    state = checks.send(a=True)
    checks.send(a=False)
    assert state['notebook'] and state['page'] == 41, ('A did not open the VR merchant catalogue', state)
    checks.remember('NativeAOpensActualFestivalCatalogue', checks.send(seconds=.6, capture='v063-native-festival-market'))
    trades, coins = state['marketTrades'], state['coins']
    state = checks.click(index=0)
    assert state['marketTrades'] == trades+1 and state['coins'] == coins-40, state
    checks.remember('NativeMerchantBaitPurchaseSettlesOnce', state)
    state = checks.click(index=0)
    assert state['marketTrades'] == trades+1 and state['coins'] == coins-40, state
    checks.remember('NativeSoldItemCannotChargeTwice', state)
    checks.send(leftActive=False)
    state = checks.click(index=1)
    assert state['marketTrades'] == trades+1 and state['coins'] == coins-40, state
    checks.remember('MerchantPurchaseRefusesLostTracking', state)
    checks.send(leftActive=True)
    state = checks.click(index=1)
    assert state['marketTrades'] == trades+2 and state['coins'] == coins-70, state
    checks.remember('NativeMerchantSeedsPurchase', state)
    state = checks.click(index=3)
    assert state['marketTrades'] == trades+3 and state['coins'] == coins-130, state
    checks.remember('NativeFestivalKeepsakePurchase', checks.send(seconds=.8, capture='v063-native-festival-keepsake'))
    # The keepsake opens the production letter; B closes it through the native reader.
    checks.send(b=True)
    checks.send(b=False)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--only', choices=('calendar', 'merchant', 'all'), default='all')
    parser.add_argument('--output', type=Path, default=base.ROOT / 'Logs/V063/NativeXR/calendar-merchant.json')
    parser.add_argument('--timeout', type=float, default=25)
    args = parser.parse_args()
    checks = base.NativeV059Checks(args.output, args.timeout)
    original = None
    try:
        original = checks.read('state.json')
        assert original['xr'] and original['isolated'] and not original['automated'], 'Start the isolated native simulator first'
        assert 'calendarYear' in original, 'Recompile the V063 calendar/merchant simulator first'
        if args.only in ('calendar', 'all'):
            calendar(checks)
        if args.only in ('merchant', 'all'):
            merchant(checks)
        checks.save(True)
        print('V063 NATIVE CALENDAR/MERCHANT PASS: '+args.only)
        print(args.output)
    except BaseException as error:
        checks.save(False, error)
        raise
    finally:
        if original is not None:
            try:
                checks.send(**base.NEUTRAL, marketReview=False)
                creative(checks, original['creativeMode'])
            except (AssertionError, KeyError, OSError, RuntimeError, TimeoutError):
                pass


if __name__ == '__main__':
    main()
