using System.Text;
using System.IO;
using System.Windows.Controls;

namespace ConfiguratorGUI
{
    public class StdOutRedirect(TextBox output) : TextWriter
    {
        public override void WriteLine(string? value)
        {
            if (value != null)
            {
                output.Text += value+"\n";
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
