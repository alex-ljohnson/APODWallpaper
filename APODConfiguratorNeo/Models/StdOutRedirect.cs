using Microsoft.UI.Xaml.Controls;
using System.Text;

namespace APODConfiguratorNeo
{
    public class StdOutRedirect(TextBox output) : TextWriter
    {
        public override void WriteLine(string? value)
        {
            if (value != null)
            {
                output.Text += value + "\n";
            }
        }
        public override void Write(string? value)
        {
            if (value != null)
            {
                output.Text += value;
            }
        }

        public override Encoding Encoding { get { return Encoding.UTF8; } }
    }
}
