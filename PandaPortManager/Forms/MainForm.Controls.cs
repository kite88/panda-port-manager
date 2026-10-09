namespace PandaPortManager;

/// <summary>
/// 自绘控件与工具栏图标绘制：全部嵌套在本窗体里，直接复用主题调色板与 DPI 缩放基值。
/// </summary>
public partial class MainForm
{
    /// <summary>不绘制焦点虚线框的按钮（聚焦时不再出现虚线/边框）。</summary>
    private sealed class FocuslessButton : Button
    {
        protected override bool ShowFocusCues => false;
    }

    /// <summary>
    /// 纯文字按钮：背景/边框/文字全部自绘。
    /// 不用原生 Flat 绘制的原因：它把文字交给 GDI 按「字体行框」垂直居中，而微软雅黑 UI 的
    /// 行框带 internal leading，字形墨迹实际落在中心线偏下（红底白字对比强时最明显）。
    /// 这里改用 GDI+ 的 GenericTypographic 排版（以 em box 度量、不含 internal leading），
    /// 文字才是真正的视觉居中。
    /// </summary>
    private sealed class TextButton : Button
    {
        protected override bool ShowFocusCues => false;

        /// <summary>文字垂直微调（96-DPI 逻辑像素，负值上移），绘制时按当前 DPI 缩放。</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int TextOffsetY { get; set; }

        /// <summary>该按钮文案在给定字体下所需的高度（96-DPI 逻辑像素）：AutoSize 等效高度 + 边框余量。</summary>
        public static int FitHeight(string text, Font font)
        {
            using var probe = new TextButton { Text = text, Font = font };
            return probe.GetPreferredSize(Size.Empty).Height + ButtonHeightSlack;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle cr = ClientRectangle;

            // 背景：悬停/按下色为 Empty 表示「沿用底色」（与原生 Flat 的语义一致）
            Color back = BackColor;
            Color over = FlatAppearance.MouseOverBackColor;
            Color down = FlatAppearance.MouseDownBackColor;
            if (Enabled)
            {
                if (down != Color.Empty && cr.Contains(PointToClient(MousePosition))
                    && (MouseButtons & MouseButtons.Left) == MouseButtons.Left) back = down;
                else if (over != Color.Empty && cr.Contains(PointToClient(MousePosition))) back = over;
            }
            using (var fill = new SolidBrush(back)) g.FillRectangle(fill, cr);

            // 边框：「结束进程」未选中态靠它勾轮廓
            if (FlatAppearance.BorderSize > 0 && FlatAppearance.BorderColor != Color.Empty)
                using (var pen = new Pen(FlatAppearance.BorderColor))
                    g.DrawRectangle(pen, 0, 0, cr.Width - 1, cr.Height - 1);

            int dy = (int)Math.Round(TextOffsetY * DeviceDpi / 96f);
            // 灰度抗锯齿而非 ClearType：ClearType 子像素在深色/彩色按钮底上会打出明显的彩色边
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using var sf = new StringFormat(StringFormat.GenericTypographic)
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
            };
            using var fore = new SolidBrush(Enabled ? ForeColor : SystemColors.GrayText);
            g.DrawString(Text, Font, fore, new Rectangle(0, dy, cr.Width, cr.Height), sf);
        }
    }

    /// <summary>弹窗 OK 等按钮统一样式：扁平无边框 + 主题配色。</summary>
    private static void StyleFlatButton(Button b, bool dark)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = dark ? Color.FromArgb(0x3A, 0x3A, 0x3D) : SystemColors.ControlLight;
        b.FlatAppearance.MouseDownBackColor = dark ? Color.FromArgb(0x45, 0x45, 0x4A) : SystemColors.ControlDark;
        b.BackColor = dark ? DarkControlBack : SystemColors.Control;
        b.ForeColor = dark ? DarkFore : LightFore;
    }

    private void SettingsBtn_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        Color c = _isDark ? DarkFore : LightFore;

        float s = _settingsBtn.DeviceDpi / 96f;
        const int teeth = 8;
        float rOuter = 9f * s;
        float rInner = 6f * s;
        float rHole = 3.2f * s;
        var cx = _settingsBtn.ClientSize.Width / 2f;
        var cy = IconCenterY * s;

        var pts = new PointF[teeth * 2];
        for (int i = 0; i < teeth * 2; i++)
        {
            float angle = i * (float)Math.PI / teeth - (float)Math.PI / 2;
            float r = (i % 2 == 0) ? rOuter : rInner;
            pts[i] = new PointF(cx + r * (float)Math.Cos(angle), cy + r * (float)Math.Sin(angle));
        }

        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddPolygon(pts);
        path.AddEllipse(cx - rHole, cy - rHole, rHole * 2, rHole * 2);
        path.FillMode = System.Drawing.Drawing2D.FillMode.Alternate;

        using var brush = new SolidBrush(c);
        g.FillPath(brush, path);

        DrawToolbarLabel(g, _settingsBtn, R.T("Settings"), c);
    }

    private void AboutBtn_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        Color c = _isDark ? DarkFore : LightFore;

        float s = _aboutBtn.DeviceDpi / 96f;
        float cx = _aboutBtn.ClientSize.Width / 2f;
        float cy = IconCenterY * s;
        float r = 9.5f * s;

        using (var brush = new SolidBrush(c))
            g.FillEllipse(brush, cx - r, cy - r, r * 2, r * 2);

        // 用工具栏背景色在圆内抠出 "i"
        Color hole = _isDark ? DarkToolbarBack : SystemColors.Control;
        TextRenderer.DrawText(g, "i", new Font(Font, FontStyle.Bold),
            new Rectangle((int)(cx - r), (int)(cy - r), (int)(r * 2), (int)(r * 2)), hole,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        DrawToolbarLabel(g, _aboutBtn, R.T("About"), c);
    }

    private static void DrawToolbarLabel(Graphics g, Button b, string text, Color c)
    {
        float s = b.DeviceDpi / 96f;
        TextRenderer.DrawText(g, text, b.Font,
            new Rectangle(0, (int)(b.Height - IconLabelBottom * s), b.Width, (int)(IconLabelHeight * s)), c,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    /// <summary>开启双缓冲的 ListView（自绘表头 + 3 秒刷新时不闪烁）。</summary>
    private sealed class DoubleBufferedListView : ListView
    {
        public DoubleBufferedListView() { DoubleBuffered = true; }
    }

    /// <summary>列排序比较器：端口、远程端口、PID 三列按数值比较，其余按忽略大小写的字符串比较。</summary>
    private sealed class ListViewItemComparer(int column, bool asc) : System.Collections.IComparer
    {
        public int Compare(object? x, object? y)
        {
            if (x is not ListViewItem a || y is not ListViewItem b) return 0;
            // 数字列按数值比较
            if (column is 2 or 4 or 6)
            {
                int.TryParse(a.SubItems[column].Text, out int na);
                int.TryParse(b.SubItems[column].Text, out int nb);
                return asc ? na.CompareTo(nb) : nb.CompareTo(na);
            }
            var sa = a.SubItems[column].Text;
            var sb = b.SubItems[column].Text;
            return asc ? string.Compare(sa, sb, StringComparison.OrdinalIgnoreCase)
                       : string.Compare(sb, sa, StringComparison.OrdinalIgnoreCase);
        }
    }
}
