using TaleWorlds.Library;

namespace ChatAi
{
    public class ChatViewModel : ViewModel
    {
        private string _message;

        public string Message
        {
            get => _message;
            set
            {
                if (_message != value)
                {
                    _message = value;
                    OnPropertyChanged(nameof(Message));
                }
            }
        }

        // Command to bind to the Send button in the UI
        public void ExecuteSendMessage()
        {
            if (!string.IsNullOrWhiteSpace(Message))
            {
                // Logic to handle sending the message
                InformationManager.DisplayMessage(new InformationMessage($"Message sent: {Message}"));
                Message = string.Empty; // Clear the textbox after sending
            }
            else
            {
                InformationManager.DisplayMessage(new InformationMessage("Cannot send an empty message."));
            }
        }
    }
}
