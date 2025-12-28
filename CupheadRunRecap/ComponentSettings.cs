using LiveSplit.UI;
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
using System.Xml.Linq;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace CupheadRunRecap
{
    public partial class ComponentSettings : UserControl
    {
        public ComponentSettings()
        {
            InitializeComponent();
            txtFilepath.ReadOnly = true;
            txtFilepath.Text = Component.DefaultRunRecapDirectory;
        }

        public string RunRecapDirectory
        {
            get => txtFilepath.Text?.Trim();
            set => txtFilepath.Text = value ?? string.Empty;
        }

        public bool StarSkipDisplayAsInt
        {
           get => chkStarSkipDisplayMethod.Checked;
        }

        public XmlNode GetSettings(XmlDocument document)
        {
            XmlElement xmlSettings = document.CreateElement("Settings");
            CreateSettingsNode(document, xmlSettings);
            return xmlSettings;
        }
        public void SetSettings(XmlNode settings)
        {
            var element = (XmlElement)settings;
            txtFilepath.Text = SettingsHelper.ParseString(element["Filepath"]);
            chkStarSkipDisplayMethod.Checked = SettingsHelper.ParseBool(element["StarSkipDisplayMethod"]);
        }

        private int CreateSettingsNode(XmlDocument document, XmlElement parent)
        {
            return SettingsHelper.CreateSetting(document, parent, "Version", "1.0") ^
                SettingsHelper.CreateSetting(document, parent, "Filepath", txtFilepath.Text) ^
                SettingsHelper.CreateSetting(document, parent, "StarSkipDisplayMethod", chkStarSkipDisplayMethod.Checked);
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select a folder";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtFilepath.Text = dialog.SelectedPath;
                }
            }
        }

        private void chkStarSkipDisplayMethod_CheckedChanged(object sender, EventArgs e)
        {
            //chkStarSkipDisplayMethod.Checked = !chkStarSkipDisplayMethod.Checked;
        }
    }
}
