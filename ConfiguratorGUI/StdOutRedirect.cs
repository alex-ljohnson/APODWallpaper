using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Controls;
using System.Threading.Tasks;

namespace ConfiguratorGUI
{
    public class StdOutRedirect : TextWriter
    {
        TextBox textBox;
        public StdOutRedirect(TextBox output)
        {
            textBox = output;
        }

        public override void WriteLine(string? value)
        {
            if (value != null)
            {
                textBox.Text += value+"\n";
            }
        }
        public override void Write(string? value)
        {
            if (value != null)
            {
                textBox.Text += value;
            }
        }

        public override Encoding Encoding { get { return Encoding.UTF8; } }
    }
}
