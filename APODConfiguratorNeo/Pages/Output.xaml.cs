using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace APODConfiguratorNeo.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Output : Page
    {
        private readonly StdOutRedirect redirect;
        public Output()
        {
            InitializeComponent();
            redirect = new StdOutRedirect(TxtOutput);
            Console.SetOut(redirect);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is string s)
            {
                TxtOutput.Text = s;
            }
        }

        private void BtnClearOut_Click(object sender, RoutedEventArgs e)
        {
            TxtOutput.Text = "";
        }
    }
}
