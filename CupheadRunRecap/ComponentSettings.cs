using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace CupheadRunRecap
{
    public partial class ComponentSettings : UserControl
    {
        public ComponentSettings()
        {
            InitializeComponent();
        }

        public XmlNode UpdateSettings(XmlDocument document)
        {
            XmlElement xmlSettings = document.CreateElement("Settings");

            return xmlSettings;
        }
        public void SetSettings(XmlNode settings)
        {

        }
    }
}
