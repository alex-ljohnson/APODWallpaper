using System.IO;
using System.Text;
using System.Windows.Controls;

namespace ConfiguratorGUI
{
    public class StdOutRedirect(TextBox output) : TextWriter
    {
        public override void WriteLine(string? value)
        {
            if (value != null)
            {
                Append(value + "\n");
            }
        }
        public override void Write(string? value)
        {
            if (value != null)
            {
                Append(value);
            }
        }

        // TextBox is UI thread only, background requests log too. Always queue to keep line order
        private void Append(string text)
        {
            output.Dispatcher.InvokeAsync(() => output.Text += text);
        }

        public override Encoding Encoding { get { return Encoding.UTF8; } }
    }
}
