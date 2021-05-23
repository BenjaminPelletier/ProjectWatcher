using Accord.Video.DirectShow;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ProjectWatcher
{
    public partial class CameraChooserDialog : Form
    {
        public CameraData Camera
        {
            get
            {
                var cd = lbCameras.SelectedItem as CameraData;
                return new CameraData(txtCameraName.Text, cd.Moniker);
            }
        }

        public CameraChooserDialog()
        {
            InitializeComponent();
        }

        private void CameraChooserDialog_Load(object sender, EventArgs e)
        {
            var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            for (int i = 0; i < videoDevices.Count; i++)
            {
                lbCameras.Items.Add(new CameraData(videoDevices[i].Name, videoDevices[i].MonikerString));
            }
            if (lbCameras.Items.Count > 0)
            {
                lbCameras.SelectedIndex = 0;
            }
        }

        private void cmdCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void cmdOk_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void lbCameras_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lbCameras.SelectedIndex >= 0)
            {
                cmdOk.Enabled = true;
                txtCameraName.Text = (lbCameras.SelectedItem as CameraData).Name;
                txtCameraName.Enabled = true;
            }
            else
            {
                cmdOk.Enabled = false;
                txtCameraName.Text = "";
                txtCameraName.Enabled = false;
            }
        }
    }
}
