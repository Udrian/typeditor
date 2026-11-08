#nullable enable annotations

using Avalonia.Controls;
using Avalonia;

namespace TypeDitor.View.Panels.PropertyTemplates
{
    /// <summary>
    /// A TextBox that only accepts numeric input (integers or decimals).
    /// </summary>
    public class NumericTextBox : TextBox
    {
        private bool _isUpdating;

        /// <summary>Identifies the <see cref="AllowNegative"/> styled property.</summary>
        public static readonly StyledProperty<bool> AllowNegativeProperty =
            AvaloniaProperty.Register<NumericTextBox, bool>(nameof(AllowNegative), true);

        /// <summary>Identifies the <see cref="AllowDecimals"/> styled property.</summary>
        public static readonly StyledProperty<bool> AllowDecimalsProperty =
            AvaloniaProperty.Register<NumericTextBox, bool>(nameof(AllowDecimals), false);

        /// <summary>Gets or sets whether the input may contain a leading minus sign.</summary>
        public bool AllowNegative
        {
            get => GetValue(AllowNegativeProperty);
            set => SetValue(AllowNegativeProperty, value);
        }

        /// <summary>Gets or sets whether the input may contain a decimal separator.</summary>
        public bool AllowDecimals
        {
            get => GetValue(AllowDecimalsProperty);
            set => SetValue(AllowDecimalsProperty, value);
        }

        /// <summary>Gets the style key used to apply <see cref="TextBox"/> styles.</summary>
        protected override Type StyleKeyOverride => typeof(TextBox);

        /// <summary>Initializes a numeric text box and filters text as it changes.</summary>
        public NumericTextBox()
        {
            TextChanged += OnTextChanged;
        }

        private void OnTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (_isUpdating)
                return;

            var currentText = Text ?? string.Empty;
            var filteredText = string.Empty;
            var hasDecimal = false;
            var hasNegative = false;

            foreach (var c in currentText)
            {
                if (char.IsDigit(c))
                {
                    filteredText += c;
                    continue;
                }

                if (c == '-' && AllowNegative && !hasNegative && filteredText.Length == 0)
                {
                    filteredText += c;
                    hasNegative = true;
                    continue;
                }

                if ((c == '.' || c == ',') && AllowDecimals && !hasDecimal)
                {
                    filteredText += '.';
                    hasDecimal = true;
                }
            }

            if (filteredText != currentText)
            {
                _isUpdating = true;

                var cursorPos = CaretIndex;
                Text = filteredText;
                CaretIndex = Math.Max(0, Math.Min(cursorPos, filteredText.Length));

                _isUpdating = false;
            }

            // Clear any data validation errors since we're handling input filtering ourselves
            ClearValue(DataValidationErrors.ErrorsProperty);
        }
    }
}

