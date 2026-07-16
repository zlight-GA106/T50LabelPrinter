
namespace Supvan.T50PRO.SDK.Demo
{
    partial class frmDemo
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.btnSearch = new System.Windows.Forms.Button();
            this.cmbDevicePaths = new System.Windows.Forms.ComboBox();
            this.btnPrint = new System.Windows.Forms.Button();
            this.lblmsg = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // btnSearch
            // 
            this.btnSearch.Location = new System.Drawing.Point(382, 45);
            this.btnSearch.Name = "btnSearch";
            this.btnSearch.Size = new System.Drawing.Size(75, 23);
            this.btnSearch.TabIndex = 0;
            this.btnSearch.Text = "查询设备";
            this.btnSearch.UseVisualStyleBackColor = true;
            this.btnSearch.Click += new System.EventHandler(this.btnSearch_Click);
            // 
            // cmbDevicePaths
            // 
            this.cmbDevicePaths.FormattingEnabled = true;
            this.cmbDevicePaths.Location = new System.Drawing.Point(22, 47);
            this.cmbDevicePaths.Name = "cmbDevicePaths";
            this.cmbDevicePaths.Size = new System.Drawing.Size(335, 20);
            this.cmbDevicePaths.TabIndex = 1;
            // 
            // btnPrint
            // 
            this.btnPrint.Location = new System.Drawing.Point(382, 100);
            this.btnPrint.Name = "btnPrint";
            this.btnPrint.Size = new System.Drawing.Size(75, 23);
            this.btnPrint.TabIndex = 2;
            this.btnPrint.Text = "打印";
            this.btnPrint.UseVisualStyleBackColor = true;
            this.btnPrint.Click += new System.EventHandler(this.btnPrint_Click);
            // 
            // lblmsg
            // 
            this.lblmsg.Location = new System.Drawing.Point(22, 93);
            this.lblmsg.Name = "lblmsg";
            this.lblmsg.Size = new System.Drawing.Size(335, 40);
            this.lblmsg.TabIndex = 3;
            this.lblmsg.Text = "lblmsg";
            // 
            // frmDemo
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(471, 208);
            this.Controls.Add(this.lblmsg);
            this.Controls.Add(this.btnPrint);
            this.Controls.Add(this.cmbDevicePaths);
            this.Controls.Add(this.btnSearch);
            this.Name = "frmDemo";
            this.Text = "Supvan.T50PRO.SDK.Demo";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnSearch;
        private System.Windows.Forms.ComboBox cmbDevicePaths;
        private System.Windows.Forms.Button btnPrint;
        private System.Windows.Forms.Label lblmsg;
    }
}

