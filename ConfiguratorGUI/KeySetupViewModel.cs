using APODWallpaper.Interfaces;
using APODWallpaper.Utils;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace ConfiguratorGUI
{
    /// <summary>
    /// State for the API key setup panel, also receives key errors from APODCache
    /// </summary>
    public class KeySetupViewModel(IHttpClientFactory httpClientFactory, IConfigurationService config) : INotifyPropertyChanged, IApiKeyErrorHandler
    {
        private bool isOpen;
        public bool IsOpen { get => isOpen; private set { isOpen = value; OnPropertyChanged(); } }

        // Why the panel opened, empty when the user opened it
        private string reason = "";
        public string Reason { get => reason; private set { reason = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasReason)); } }
        public bool HasReason => Reason.Length > 0;

        private string keyInput = "";
        public string KeyInput { get => keyInput; set { keyInput = value; OnPropertyChanged(); } }

        private string status = "";
        public string Status { get => status; private set { status = value; OnPropertyChanged(); } }

        private bool isBusy;
        public bool IsBusy { get => isBusy; private set { isBusy = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }

        public string DismissText => config.UsingDemoKey ? "Use the demo key for now" : "Close";

        private ICommand? _openSignupCommand;
        public ICommand OpenSignupCommand => _openSignupCommand ??= new RelayCommand(OpenSignup, _ => true);

        private ICommand? _verifyCommand;
        public ICommand VerifyCommand => _verifyCommand ??= new RelayCommand(async () => await VerifyAsync(), _ => !IsBusy);

        private ICommand? _dismissCommand;
        // Stays enabled while checking to prevent traps
        public ICommand DismissCommand => _dismissCommand ??= new RelayCommand(Dismiss, _ => true);

        private ICommand? _openCommand;
        public ICommand OpenCommand => _openCommand ??= new RelayCommand(() => Open(), _ => true);

        public void Open(string why = "")
        {
            // Keep whatever is being typed if another error arrives
            if (!IsOpen)
            {
                KeyInput = config.UsingDemoKey ? "" : config.API_KEY;
                Status = "";
                Reason = why;
            }
            else if (why.Length > 0)
            {
                Reason = why;
            }
            OnPropertyChanged(nameof(DismissText));
            IsOpen = true;
        }

        /// <summary>
        /// Offers setup on launch until the user has a key or picks the demo key
        /// </summary>
        public void OpenIfNeeded(bool force)
        {
            if (force || (config.UsingDemoKey && !config.KeySetupDismissed)) Open();
        }

        private void OpenSignup()
        {
            try
            {
                Utilities.OpenInBrowser(ApiKey.SignupUrl);
            }
            catch (Win32Exception ex)
            {
                Console.WriteLine($"Couldn't open browser: {ex.Message}");
                Status = $"Couldn't open your browser, go to {ApiKey.SignupUrl} instead.";
            }
        }

        public event EventHandler? KeySaved;

        private CancellationTokenSource? verifyCts;

        private void Dismiss()
        {
            verifyCts?.Cancel();
            if (config.UsingDemoKey) config.KeySetupDismissed = true;
            IsOpen = false;
        }

        private async Task VerifyAsync()
        {
            var key = KeyInput.Trim();
            if (key.Length == 0)
            {
                Status = "Paste your key into the box first.";
                return;
            }
            IsBusy = true;
            Status = "Checking with NASA...";
            using var cts = new CancellationTokenSource();
            verifyCts = cts;
            ApiKeyCheck check;
            try
            {
                check = await ApiKey.CheckAsync(httpClientFactory.CreateClient("APODCache"), config.BaseUrl, key, cts.Token);
            }
            finally
            {
                verifyCts = null;
                IsBusy = false;
            }
            // Panel was closed midway
            if (cts.IsCancellationRequested) return;

            if (!check.Reachable)
            {
                Status = "NASA didn't answer, so the key couldn't be checked. Check your connection, or try again later if NASA's API is down.";
                return;
            }
            if (!check.Accepted)
            {
                Status = check.Error?.Message ?? "NASA rejected that key.";
                return;
            }
            config.API_KEY = key;
            Reason = "";
            OnPropertyChanged(nameof(DismissText));
            Status = check.Error is { Kind: ApiKeyErrorKind.RateLimited }
                ? "Key saved. It's over its limit right now but resets within the hour."
                : check.Limit is int limit
                    ? $"Key saved. NASA allows {limit} requests an hour with it."
                    : "Key saved.";
            KeySaved?.Invoke(this, EventArgs.Empty);
        }

        private bool showingLimitNotice;
        public void Handle(ApiKeyError error)
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (error.SetupHelps)
                {
                    Open(error.Message);
                    return;
                }
                // Cover all explore requests with one note
                if (showingLimitNotice) return;
                showingLimitNotice = true;
                MessageBox.Show(error.Message, "NASA API key", MessageBoxButton.OK, MessageBoxImage.Warning);
                showingLimitNotice = false;
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
