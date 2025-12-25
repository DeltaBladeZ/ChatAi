using System;
using TaleWorlds.Library;

namespace ChatAi.Battle
{
    public class BattleTextInputVM : ViewModel
    {
        private bool _isVisible;
        private string _commandText = string.Empty;

        public event Action<string> SendRequested;
        public event Action OpenPromptRequested;

        public BattleTextInputVM(bool isVisible = false)
        {
            _isVisible = isVisible;
        }

        [DataSourceProperty]
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible != value)
                {
                    _isVisible = value;
                    OnPropertyChangedWithValue(value, nameof(IsVisible));
                }
            }
        }

        [DataSourceProperty]
        public string CommandText
        {
            get => _commandText;
            set
            {
                if (_commandText != value)
                {
                    _commandText = value;
                    OnPropertyChangedWithValue(value, nameof(CommandText));
                }
            }
        }

        // Bound to "Send" action in overlay (future Gauntlet UI); can be invoked from behavior when using TextInquiry.
        public void ExecuteSend()
        {
            var text = CommandText?.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                SendRequested?.Invoke(text);
                CommandText = string.Empty;
            }
        }

        // Bound to a button in the overlay to open a native prompt (TextInquiry)
        public void ExecuteOpenPrompt()
        {
            OpenPromptRequested?.Invoke();
        }
    }
}

