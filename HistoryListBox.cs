using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ProjectWatcher
{
    class HistoryListBox : ListBox
    {
        public readonly HashSet<string> Stable = new HashSet<string>();

        public HistoryListBox()
        {
            this.DrawMode = DrawMode.OwnerDrawFixed;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index >= 0 && e.Index < Items.Count)
            {
                string displayValue = Items[e.Index].ToString();
                if (Stable.Contains(displayValue))
                {
                    TextRenderer.DrawText(e.Graphics, displayValue, e.Font, e.Bounds.Location, Color.DarkRed);
                }
                else
                {
                    TextRenderer.DrawText(e.Graphics, displayValue, e.Font, e.Bounds.Location, Color.Black);
                }
            }
            e.DrawFocusRectangle();
        }
    }
}
