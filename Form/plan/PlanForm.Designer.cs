namespace generaControlProcedure
{
    partial class PlanForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PlanForm));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.planCase = new System.Windows.Forms.ToolStripSplitButton();
            this.当前计划ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.完成计划ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.未生产计划ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.refreshtoolStripButton = new System.Windows.Forms.ToolStripButton();
            this.savetoolStripButton = new System.Windows.Forms.ToolStripButton();
            this.amendtoolStripButton = new System.Windows.Forms.ToolStripButton();
            this.deletetoolStripButton = new System.Windows.Forms.ToolStripButton();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.panel1 = new System.Windows.Forms.Panel();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.choice = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.urgent = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.toolStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolStrip1
            // 
            this.toolStrip1.AutoSize = false;
            this.toolStrip1.Font = new System.Drawing.Font("宋体", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.planCase,
            this.refreshtoolStripButton,
            this.savetoolStripButton,
            this.amendtoolStripButton,
            this.deletetoolStripButton});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(1264, 49);
            this.toolStrip1.TabIndex = 0;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // planCase
            // 
            this.planCase.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.planCase.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.当前计划ToolStripMenuItem,
            this.完成计划ToolStripMenuItem,
            this.未生产计划ToolStripMenuItem});
            this.planCase.Image = ((System.Drawing.Image)(resources.GetObject("planCase.Image")));
            this.planCase.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.planCase.Name = "planCase";
            this.planCase.Size = new System.Drawing.Size(105, 46);
            this.planCase.Text = "计划情况";
            // 
            // 当前计划ToolStripMenuItem
            // 
            this.当前计划ToolStripMenuItem.Name = "当前计划ToolStripMenuItem";
            this.当前计划ToolStripMenuItem.Size = new System.Drawing.Size(178, 24);
            this.当前计划ToolStripMenuItem.Text = "当前计划";
            this.当前计划ToolStripMenuItem.Click += new System.EventHandler(this.当前计划ToolStripMenuItem_Click);
            // 
            // 完成计划ToolStripMenuItem
            // 
            this.完成计划ToolStripMenuItem.Name = "完成计划ToolStripMenuItem";
            this.完成计划ToolStripMenuItem.Size = new System.Drawing.Size(178, 24);
            this.完成计划ToolStripMenuItem.Text = "完成计划";
            this.完成计划ToolStripMenuItem.Click += new System.EventHandler(this.完成计划ToolStripMenuItem_Click);
            // 
            // 未生产计划ToolStripMenuItem
            // 
            this.未生产计划ToolStripMenuItem.Name = "未生产计划ToolStripMenuItem";
            this.未生产计划ToolStripMenuItem.Size = new System.Drawing.Size(178, 24);
            this.未生产计划ToolStripMenuItem.Text = "未生产计划";
            this.未生产计划ToolStripMenuItem.Click += new System.EventHandler(this.未生产计划ToolStripMenuItem_Click);
            // 
            // refreshtoolStripButton
            // 
            this.refreshtoolStripButton.Image = ((System.Drawing.Image)(resources.GetObject("refreshtoolStripButton.Image")));
            this.refreshtoolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.refreshtoolStripButton.Name = "refreshtoolStripButton";
            this.refreshtoolStripButton.Size = new System.Drawing.Size(79, 46);
            this.refreshtoolStripButton.Text = "刷 新";
            this.refreshtoolStripButton.Click += new System.EventHandler(this.refreshtoolStripButton_Click);
            // 
            // savetoolStripButton
            // 
            this.savetoolStripButton.Image = ((System.Drawing.Image)(resources.GetObject("savetoolStripButton.Image")));
            this.savetoolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.savetoolStripButton.Name = "savetoolStripButton";
            this.savetoolStripButton.Size = new System.Drawing.Size(79, 46);
            this.savetoolStripButton.Text = "保 存";
            this.savetoolStripButton.Click += new System.EventHandler(this.savetoolStripButton_Click);
            // 
            // amendtoolStripButton
            // 
            this.amendtoolStripButton.Image = ((System.Drawing.Image)(resources.GetObject("amendtoolStripButton.Image")));
            this.amendtoolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.amendtoolStripButton.Name = "amendtoolStripButton";
            this.amendtoolStripButton.Size = new System.Drawing.Size(79, 46);
            this.amendtoolStripButton.Text = "修 改";
            this.amendtoolStripButton.Click += new System.EventHandler(this.amendtoolStripButton_Click);
            // 
            // deletetoolStripButton
            // 
            this.deletetoolStripButton.Image = ((System.Drawing.Image)(resources.GetObject("deletetoolStripButton.Image")));
            this.deletetoolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.deletetoolStripButton.Name = "deletetoolStripButton";
            this.deletetoolStripButton.Size = new System.Drawing.Size(79, 46);
            this.deletetoolStripButton.Text = "删 除";
            this.deletetoolStripButton.Click += new System.EventHandler(this.deletetoolStripButton_Click);
            // 
            // dataGridView1
            // 
            this.dataGridView1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridView1.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.Single;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("宋体", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridView1.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.choice,
            this.urgent});
            this.dataGridView1.Location = new System.Drawing.Point(11, 13);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.RowTemplate.Height = 23;
            this.dataGridView1.Size = new System.Drawing.Size(1240, 889);
            this.dataGridView1.TabIndex = 1;
            this.dataGridView1.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.cellcontentclick);
            // 
            // panel1
            // 
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel1.Controls.Add(this.textBox1);
            this.panel1.Controls.Add(this.dataGridView1);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 49);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1264, 936);
            this.panel1.TabIndex = 2;
            // 
            // textBox1
            // 
            this.textBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBox1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.textBox1.Font = new System.Drawing.Font("宋体", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.textBox1.ForeColor = System.Drawing.Color.Red;
            this.textBox1.Location = new System.Drawing.Point(11, 904);
            this.textBox1.Name = "textBox1";
            this.textBox1.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.textBox1.Size = new System.Drawing.Size(1240, 29);
            this.textBox1.TabIndex = 2;
            // 
            // choice
            // 
            this.choice.HeaderText = "选择";
            this.choice.Name = "choice";
            // 
            // urgent
            // 
            this.urgent.DisplayStyle = System.Windows.Forms.DataGridViewComboBoxDisplayStyle.ComboBox;
            this.urgent.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.urgent.HeaderText = "加急";
            this.urgent.Items.AddRange(new object[] {
            "是",
            "否"});
            this.urgent.Name = "urgent";
            // 
            // PlanForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1264, 985);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.toolStrip1);
            this.Name = "PlanForm";
            this.Text = "计划窗口";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.Load += new System.EventHandler(this.PlanForm_Load);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripSplitButton planCase;
        private System.Windows.Forms.ToolStripMenuItem 当前计划ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 完成计划ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 未生产计划ToolStripMenuItem;
        private System.Windows.Forms.ToolStripButton refreshtoolStripButton;
        private System.Windows.Forms.ToolStripButton savetoolStripButton;
        private System.Windows.Forms.ToolStripButton amendtoolStripButton;
        private System.Windows.Forms.ToolStripButton deletetoolStripButton;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.DataGridViewCheckBoxColumn choice;
        private System.Windows.Forms.DataGridViewComboBoxColumn urgent;
    }
}