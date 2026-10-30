#!/bin/sh
# Usage: run_locked.sh <Suite> [env assignments...]  — waits for the project lock, then runs one validation suite.
cd "E:/University/Homework/虚拟现实作业/BoatAttack"
suite=$1; shift
for i in $(seq 1 240); do
  out=$(env "$@" powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Run-UnityValidation.ps1 -Suite "$suite" 2>&1); code=$?
  if echo "$out" | grep -q "Close the Unity editor"; then sleep 30; continue; fi
  # another agent's half-finished edit breaks compilation; wait for it rather than reporting a false failure
  log=$(echo "$out" | grep -o 'usv-validation-[0-9a-f]*\.log' | head -1)
  if [ $code -ne 0 ] && [ -n "$log" ] && grep -qE "error CS|another Unity instance is running" "Logs/$log"; then sleep 60; continue; fi
  echo "$out" | tail -40; exit $code
done
echo "lock never released"; exit 1
