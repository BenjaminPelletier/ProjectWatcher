using Accord.Video;
using Accord.Video.DirectShow;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenCvSharp.Extensions;
using OpenCvSharp;
using Json.Serialization;

namespace ProjectWatcher
{
    public partial class Main : Form
    {
        private Bitmap _View;

        private enum ViewMode
        {
            Normal,
            Difference
        }
        private bool _DisplayLive = false;
        private ManagedCamera _SelectedCamera = null;
        private ViewMode _ViewMode = ViewMode.Normal;

        private const string PROJECT_FILE_NAME = "project.json";

        public Main()
        {
            InitializeComponent();
        }

        private void Main_Load(object sender, EventArgs e)
        {
            var settings = Properties.Settings.Default;
            if (settings.SettingsInitialized)
            {
                Location = settings.Location;
                Size = settings.Size;
                WindowState = settings.Maximized ? FormWindowState.Maximized : FormWindowState.Normal;
                splitContainer1.SplitterDistance = settings.SC1;
                splitContainer2.SplitterDistance = settings.SC2;
                splitContainer3.SplitterDistance = settings.SC3;
                fbdProject.SelectedPath = settings.ProjectFolder;
            }
            else
            {
                fbdProject.SelectedPath = Directory.GetCurrentDirectory();
            }
            LoadProject();
        }

        void SaveSettings()
        {
            var settings = Properties.Settings.Default;
            settings.Location = Location;
            settings.Size = Size;
            settings.Maximized = this.WindowState == FormWindowState.Maximized;
            settings.SC1 = splitContainer1.SplitterDistance;
            settings.SC2 = splitContainer2.SplitterDistance;
            settings.SC3 = splitContainer3.SplitterDistance;
            settings.ProjectFolder = fbdProject.SelectedPath;
            settings.SettingsInitialized = true;
            settings.Save();
        }


        #region Out of UI thread

        private void ManagedCamera_NewFrame(object sender, FrameSource.NewFrameEventArgs e)
        {
            var mc = sender as ManagedCamera;

            string savedFilename = mc.AddFrame(e.Frame, DateTime.Now);

            if (savedFilename != null && _SelectedCamera == mc)
            {
                if (!this.IsDisposed) this.Invoke(new Action(() => lbHistory.Items.Insert(1, savedFilename)));
            }

            if (_DisplayLive && _SelectedCamera == mc)
            {
                var old = _View;

                if (_ViewMode == ViewMode.Difference)
                {
                    using (Mat m = mc.VisualizeDifferenceFromLastSave(e.Frame))
                    {
                        _View = e.Frame.ToBitmap();
                    }
                }
                else
                {
                    _View = e.Frame.ToBitmap();
                }

                if (!this.IsDisposed) this.Invoke(new Action(() => pbView.Invalidate()));
                if (old != null) old.Dispose();
            }
        }

        private void ManagedCamera_StableImage(object sender, ManagedCamera.StableImageEventArgs e)
        {
            if (sender == _SelectedCamera)
            {
                this.Invoke(new Action(() =>
                {
                    lbHistory.Stable.Add(e.Filename);
                    lbHistory.Invalidate();
                }));
            }
        }

        #endregion

        private void Main_Resize(object sender, EventArgs e)
        {
            SaveSettings();
        }

        private void splitContainer_SplitterMoved(object sender, SplitterEventArgs e)
        {
            SaveSettings();
        }

        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveSettings();
        }

        private void cmdAddCamera_Click(object sender, EventArgs e)
        {
            var dialog = new CameraChooserDialog();
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                AddCamera(dialog.Camera);
            }
        }

        private void AddCamera(CameraData cd)
        {
            var devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            int index = -1;
            for (int i = 0; i < devices.Count; i++)
            {
                if (devices[i].MonikerString == cd.Moniker)
                {
                    index = i;
                    break;
                }
            }
            if (index == -1)
            {
                MessageBox.Show("Could not find camera " + cd.Name + " (moniker " + cd.Moniker + ")");
                return;
            }
            var device = new VideoCaptureDevice(cd.Moniker);
            var deviceCaps = device.VideoCapabilities[device.VideoCapabilities.IndexOfMax(caps => caps.FrameSize.Width * caps.FrameSize.Height)];
            var mc = new ManagedCamera(cd.Name, cd.Moniker, index, deviceCaps.FrameSize.Width, deviceCaps.FrameSize.Height);
            mc.NewFrame += ManagedCamera_NewFrame;
            mc.NewStableImage += ManagedCamera_StableImage;
            lbCameras.Items.Add(mc);
            lbCameras.SelectedIndex = lbCameras.Items.Count - 1;
        }

        private void cmdCapture_Click(object sender, EventArgs e)
        {
            if (cmdCapture.Text == "Start capturing")
            {
                cmdAddCamera.Enabled = false;
                openToolStripMenuItem.Enabled = false;
                foreach (var mc in lbCameras.Items.As<ManagedCamera>())
                {
                    mc.Start();
                }
                cmdCapture.Text = "Stop capturing";
            }
            else
            {
                foreach (var mc in lbCameras.Items.As<ManagedCamera>())
                {
                    mc.Stop();
                }
                cmdAddCamera.Enabled = true;
                openToolStripMenuItem.Enabled = true;
                cmdCapture.Text = "Start capturing";
            }
        }

        private void pbView_Paint(object sender, PaintEventArgs e)
        {
            if (_View != null)
            {
                float scale = Math.Min((float)pbView.ClientSize.Width / _View.Width, (float)pbView.ClientSize.Height / _View.Height);
                e.Graphics.DrawImage(_View, 0.5f * (pbView.ClientSize.Width - _View.Width * scale), 0.5f * (pbView.ClientSize.Height - _View.Height * scale), _View.Width * scale, _View.Height * scale);
            }
        }

        private void lbCameras_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lbCameras.SelectedIndex >= 0)
            {
                var mc = lbCameras.SelectedItem as ManagedCamera;
                _SelectedCamera = mc;

                bool nowSelected = lbHistory.SelectedIndex == 0 || lbHistory.Items.Count == 0;
                DateTime selected = lbHistory.SelectedIndex > 1 ? (lbHistory.SelectedItem as string).ToDateTime().Value : DateTime.Now;
                lbHistory.SelectedIndex = -1;

                lbHistory.SuspendLayout();
                lbHistory.Items.Clear();
                lbHistory.Stable.Clear();
                lbHistory.Items.Add("Now");
                int index = 0;
                foreach (DateTime t in mc.GetHistory().OrderByDescending(t => t))
                {
                    lbHistory.Items.Add(t.ToFilename());
                    if (t >= selected)
                    {
                        index++;
                    }
                }
                foreach (var filename in mc.Descriptor.StableImages)
                {
                    lbHistory.Stable.Add(filename);
                }
                lbHistory.SelectedIndex = index;
                lbHistory.ResumeLayout();
            }
        }

        private void lbHistory_SelectedIndexChanged(object sender, EventArgs e)
        {
            _DisplayLive = lbHistory.SelectedIndex == 0;
            if (lbHistory.SelectedIndex > 0)
            {
                var old = _View;
                _View = new Bitmap(_SelectedCamera.Load(lbHistory.SelectedItem as string));
                pbView.Invalidate();
                if (old != null) old.Dispose();
            }
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (fbdProject.ShowDialog(this) == DialogResult.OK)
            {
                Directory.SetCurrentDirectory(fbdProject.SelectedPath);
                LoadProject();
                SaveSettings();
            }
        }

        private void LoadProject()
        {
            if (File.Exists(PROJECT_FILE_NAME))
            {
                var project = JsonTranslator.Singleton.MakeObject<ProjectData>(JsonObject.Parse(File.ReadAllText(PROJECT_FILE_NAME)));
                foreach (Note note in project.Notes.OrderByDescending(n => n.Timestamp))
                {
                    lbNotes.Items.Add(note);
                }
            }
            foreach (DirectoryInfo di in new DirectoryInfo(Directory.GetCurrentDirectory()).GetDirectories())
            {
                string filename = Path.Combine(di.FullName, ManagedCamera.CAMERA_DATA_FILENAME);
                if (File.Exists(filename))
                {
                    CameraData cd = JsonTranslator.Singleton.MakeObject<CameraData>(JsonObject.Parse(File.ReadAllText(filename)));
                    AddCamera(cd);
                }
            }
        }

        private void SaveProject()
        {
            var project = new ProjectData();
            foreach (var note in lbNotes.Items.As<Note>())
            {
                project.Notes.Add(note);
            }
            File.WriteAllText(PROJECT_FILE_NAME, JsonTranslator.Singleton.MakeJson(project).ToMultilineString());
        }

        private void txtNote_Enter(object sender, EventArgs e)
        {
            txtNote.SelectAll();
        }

        private void AddNote(string value)
        {
            var note = new Note() { Timestamp = DateTime.Now, Value = value };
            lbNotes.Items.Insert(0, note);
            SaveProject();
            lbNotes.SelectedIndex = lbNotes.Items.Count - 1;
        }

        private void txtNote_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                AddNote(txtNote.Text);
                e.Handled = true;
            }
        }

        private void lbNotes_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lbNotes.SelectedIndex >= 0)
            {
                Note note = lbNotes.SelectedItem as Note;
                txtNote.Text = note.Value;
                txtNote.SelectAll();
                if (_SelectedCamera != null)
                {
                    for (int i = 1; i < lbHistory.Items.Count; i++)
                    {
                        DateTime t = lbHistory.Items[i].ToString().ToDateTime().Value;
                        if (t < note.Timestamp)
                        {
                            lbHistory.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
        }
    }
}
