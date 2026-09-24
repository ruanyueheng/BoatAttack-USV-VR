using BoatAttack;
using UnityEngine;

namespace BoatAttack.UI
{
    public class ColorSelector : MonoBehaviour
    {
        public Color value;
        public bool loop;
        public int startOption;
        private int _currentOption;

        public delegate void UpdateValue(int index);

        public UpdateValue updateVal;

        private void ValueUpdate(int i)
        {
            updateVal?.Invoke(i);
        }

        private void Awake()
        {
            _currentOption = ValidateIndex(startOption);
            UpdateColor();
        }

        public void NextOption()
        {
            _currentOption = ValidateIndex(_currentOption + 1);
            UpdateColor();
            ValueUpdate(_currentOption);
        }

        public void PreviousOption()
        {
            _currentOption = ValidateIndex(_currentOption - 1);
            UpdateColor();
            ValueUpdate(_currentOption);
        }

        public int CurrentOption
        {
            get => _currentOption;
            set
            {
                _currentOption = ValidateIndex(value);
                UpdateColor();
                ValueUpdate(_currentOption);
            }
        }

        private void UpdateColor()
        {
            value = ConstantData.GetPaletteColor(_currentOption);
        }

        private int ValidateIndex(int index)
        {
            var count = ConstantData.PaletteCount;
            if (loop)
            {
                return (int) Mathf.Repeat(index, count);
            }

            return Mathf.Clamp(index, 0, count - 1);
        }
    }
}