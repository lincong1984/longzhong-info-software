using System.Drawing;
using System.Windows.Forms;

namespace generaControlProcedure
{
    public class KKTab : TabControl
    {
        private int IconWOrH = 16;
        private Image icon = null;

        public KKTab()
            : base()
        {
            this.DrawMode = TabDrawMode.OwnerDrawFixed;

            icon = Properties.Resources.close_4;
            IconWOrH = icon.Width;
            IconWOrH = icon.Height;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle r = GetTabRect(e.Index);
            if (e.Index == this.SelectedIndex)    //当前选中的Tab页，设置不同的样式以示选中
            {
                Brush selected_color = Brushes.SteelBlue; //选中的项的背景色
                g.FillRectangle(selected_color, r); //改变选项卡标签的背景色 
                string title = this.TabPages[e.Index].Text;
                g.DrawString(title, this.Font, new SolidBrush(Color.Black), new PointF(r.X, r.Y + 5));//PointF选项卡标题的位置 
                r.Offset(r.Width - IconWOrH, 2);
                g.DrawImage(icon, new Point(r.X, r.Y));//选项卡上的图标的位置 fntTab = new System.Drawing.Font(e.Font, FontStyle.Bold);
            }
            else//非选中的
            {
                Brush selected_color = Brushes.AliceBlue; //选中的项的背景色
                g.FillRectangle(selected_color, r); //改变选项卡标签的背景色 
                string title = this.TabPages[e.Index].Text + "  ";
                g.DrawString(title, this.Font, new SolidBrush(Color.Black), new PointF(r.X, r.Y + 5));//PointF选项卡标题的位置 
                r.Offset(r.Width - IconWOrH, 2);
                g.DrawImage(icon, new Point(r.X, r.Y));//选项卡上的图标的位置 
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            Point point = e.Location;
            Rectangle r = GetTabRect(this.SelectedIndex);
            r.Offset(r.Width - IconWOrH - 3, 2);
            r.Width = IconWOrH;
            r.Height = IconWOrH;
            if (r.Contains(point)) this.TabPages.RemoveAt(this.SelectedIndex);
        }
    }
}
