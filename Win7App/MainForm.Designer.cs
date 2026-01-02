namespace Win7App
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Button buttonStart;
        private System.Windows.Forms.Button buttonStop;
        private System.Windows.Forms.Label labelStatus;
        private System.Windows.Forms.ComboBox comboScreens;
        private System.Windows.Forms.TextBox textLog;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label labelPassword;
        private System.Windows.Forms.TextBox textPassword;
        private System.Windows.Forms.CheckBox chkAutoStart;
        private System.Windows.Forms.CheckBox chkStartWithWindows;
        private System.Windows.Forms.CheckBox chkEnableAudio;
        private System.Windows.Forms.CheckBox chkEnableHttps;
        private System.Windows.Forms.Label labelIPs;
        private System.Windows.Forms.Button buttonQR;
        private System.Windows.Forms.FlowLayoutPanel panelQR;
        private System.Windows.Forms.Label labelUacStatus;
        private System.Windows.Forms.Button buttonUacToggle;
        private System.Windows.Forms.GroupBox groupUac;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.buttonStart = new System.Windows.Forms.Button();
            this.buttonStop = new System.Windows.Forms.Button();
            this.labelStatus = new System.Windows.Forms.Label();
            this.comboScreens = new System.Windows.Forms.ComboBox();
            this.textLog = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.labelPassword = new System.Windows.Forms.Label();
            this.textPassword = new System.Windows.Forms.TextBox();
            this.chkAutoStart = new System.Windows.Forms.CheckBox();
            this.chkStartWithWindows = new System.Windows.Forms.CheckBox();
            this.chkEnableAudio = new System.Windows.Forms.CheckBox();
            this.chkEnableHttps = new System.Windows.Forms.CheckBox();
            this.labelIPs = new System.Windows.Forms.Label();
            this.buttonQR = new System.Windows.Forms.Button();
            this.panelQR = new System.Windows.Forms.FlowLayoutPanel();
            this.labelUacStatus = new System.Windows.Forms.Label();
            this.buttonUacToggle = new System.Windows.Forms.Button();
            this.groupUac = new System.Windows.Forms.GroupBox();
            this.groupUac.SuspendLayout();
            this.SuspendLayout();
            // 
            // buttonStart
            // 
            this.buttonStart.Location = new System.Drawing.Point(12, 39);
            this.buttonStart.Name = "buttonStart";
            this.buttonStart.Size = new System.Drawing.Size(75, 23);
            this.buttonStart.TabIndex = 0;
            this.buttonStart.Text = "Start Server";
            this.buttonStart.UseVisualStyleBackColor = true;
            this.buttonStart.Click += new System.EventHandler(this.buttonStart_Click);
            // 
            // buttonStop
            // 
            this.buttonStop.Enabled = false;
            this.buttonStop.Location = new System.Drawing.Point(93, 39);
            this.buttonStop.Name = "buttonStop";
            this.buttonStop.Size = new System.Drawing.Size(75, 23);
            this.buttonStop.TabIndex = 1;
            this.buttonStop.Text = "Stop";
            this.buttonStop.UseVisualStyleBackColor = true;
            this.buttonStop.Click += new System.EventHandler(this.buttonStop_Click);
            // 
            // labelStatus
            // 
            this.labelStatus.AutoSize = true;
            this.labelStatus.Location = new System.Drawing.Point(174, 44);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new System.Drawing.Size(40, 13);
            this.labelStatus.TabIndex = 2;
            this.labelStatus.Text = "Status: Stopped";
            // 
            // comboScreens
            // 
            this.comboScreens.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboScreens.FormattingEnabled = true;
            this.comboScreens.Location = new System.Drawing.Point(12, 12);
            this.comboScreens.Name = "comboScreens";
            this.comboScreens.Size = new System.Drawing.Size(250, 21);
            this.comboScreens.TabIndex = 3;
            // 
            // textLog
            // 
            this.textLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.textLog.Location = new System.Drawing.Point(12, 180);
            this.textLog.Multiline = true;
            this.textLog.Name = "textLog";
            this.textLog.ReadOnly = true;
            this.textLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textLog.Size = new System.Drawing.Size(360, 80);
            this.textLog.TabIndex = 4;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(268, 15);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(80, 13);
            this.label1.TabIndex = 5;
            this.label1.Text = "HTTP:8080 HTTPS:8081";
            // 
            // labelPassword
            // 
            this.labelPassword.AutoSize = true;
            this.labelPassword.Location = new System.Drawing.Point(12, 71);
            this.labelPassword.Name = "labelPassword";
            this.labelPassword.Size = new System.Drawing.Size(56, 13);
            this.labelPassword.TabIndex = 6;
            this.labelPassword.Text = "Password:";
            // 
            // textPassword
            // 
            this.textPassword.Location = new System.Drawing.Point(74, 68);
            this.textPassword.Name = "textPassword";
            this.textPassword.Size = new System.Drawing.Size(188, 20);
            this.textPassword.TabIndex = 7;
            this.textPassword.Text = "1234";
            // 
            // chkAutoStart
            // 
            this.chkAutoStart.AutoSize = true;
            this.chkAutoStart.Location = new System.Drawing.Point(12, 94);
            this.chkAutoStart.Name = "chkAutoStart";
            this.chkAutoStart.Size = new System.Drawing.Size(75, 17);
            this.chkAutoStart.TabIndex = 8;
            this.chkAutoStart.Text = "Auto-start";
            this.chkAutoStart.UseVisualStyleBackColor = true;
            this.chkAutoStart.CheckedChanged += new System.EventHandler(this.chkAutoStart_CheckedChanged);
            // 
            // chkStartWithWindows
            // 
            this.chkStartWithWindows.AutoSize = true;
            this.chkStartWithWindows.Location = new System.Drawing.Point(93, 94);
            this.chkStartWithWindows.Name = "chkStartWithWindows";
            this.chkStartWithWindows.Size = new System.Drawing.Size(100, 17);
            this.chkStartWithWindows.TabIndex = 9;
            this.chkStartWithWindows.Text = "Win Startup";
            this.chkStartWithWindows.UseVisualStyleBackColor = true;
            this.chkStartWithWindows.CheckedChanged += new System.EventHandler(this.chkStartWithWindows_CheckedChanged);
            // 
            // chkEnableAudio
            // 
            this.chkEnableAudio.AutoSize = true;
            this.chkEnableAudio.Checked = true;
            this.chkEnableAudio.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkEnableAudio.Location = new System.Drawing.Point(199, 94);
            this.chkEnableAudio.Name = "chkEnableAudio";
            this.chkEnableAudio.Size = new System.Drawing.Size(65, 17);
            this.chkEnableAudio.TabIndex = 10;
            this.chkEnableAudio.Text = "Audio";
            this.chkEnableAudio.UseVisualStyleBackColor = true;
            // 
            // chkEnableHttps
            // 
            this.chkEnableHttps.AutoSize = true;
            this.chkEnableHttps.Location = new System.Drawing.Point(270, 94);
            this.chkEnableHttps.Name = "chkEnableHttps";
            this.chkEnableHttps.Size = new System.Drawing.Size(60, 17);
            this.chkEnableHttps.TabIndex = 14;
            this.chkEnableHttps.Text = "HTTPS";
            this.chkEnableHttps.UseVisualStyleBackColor = true;
            // 
            // labelIPs
            // 
            this.labelIPs.Location = new System.Drawing.Point(12, 118);
            this.labelIPs.Name = "labelIPs";
            this.labelIPs.Size = new System.Drawing.Size(280, 26);
            this.labelIPs.TabIndex = 11;
            this.labelIPs.Text = "Server IPs: -";
            // 
            // buttonQR
            // 
            this.buttonQR.Location = new System.Drawing.Point(298, 113);
            this.buttonQR.Name = "buttonQR";
            this.buttonQR.Size = new System.Drawing.Size(74, 23);
            this.buttonQR.TabIndex = 12;
            this.buttonQR.Text = "Show QR";
            this.buttonQR.UseVisualStyleBackColor = true;
            this.buttonQR.Click += new System.EventHandler(this.buttonQR_Click);
            // 
            // panelQR
            // 
            this.panelQR.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.panelQR.AutoScroll = true;
            this.panelQR.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelQR.Location = new System.Drawing.Point(12, 147);
            this.panelQR.Name = "panelQR";
            this.panelQR.Size = new System.Drawing.Size(360, 0);
            this.panelQR.TabIndex = 13;
            this.panelQR.Visible = false;
            // 
            // groupUac
            // 
            this.groupUac.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.groupUac.Controls.Add(this.labelUacStatus);
            this.groupUac.Controls.Add(this.buttonUacToggle);
            this.groupUac.Location = new System.Drawing.Point(12, 268);
            this.groupUac.Name = "groupUac";
            this.groupUac.Size = new System.Drawing.Size(360, 55);
            this.groupUac.TabIndex = 15;
            this.groupUac.TabStop = false;
            this.groupUac.Text = "UAC Screen Capture";
            // 
            // labelUacStatus
            // 
            this.labelUacStatus.Location = new System.Drawing.Point(6, 16);
            this.labelUacStatus.Name = "labelUacStatus";
            this.labelUacStatus.Size = new System.Drawing.Size(230, 32);
            this.labelUacStatus.TabIndex = 0;
            this.labelUacStatus.Text = "Checking UAC status...";
            // 
            // buttonUacToggle
            // 
            this.buttonUacToggle.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonUacToggle.Location = new System.Drawing.Point(242, 18);
            this.buttonUacToggle.Name = "buttonUacToggle";
            this.buttonUacToggle.Size = new System.Drawing.Size(112, 28);
            this.buttonUacToggle.TabIndex = 1;
            this.buttonUacToggle.Text = "Disable Secure";
            this.buttonUacToggle.UseVisualStyleBackColor = true;
            this.buttonUacToggle.Click += new System.EventHandler(this.buttonUacToggle_Click);
            // 
            // MainForm
            //  
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(384, 335);
            this.Controls.Add(this.groupUac);
            this.Controls.Add(this.chkEnableHttps);
            this.Controls.Add(this.panelQR);
            this.Controls.Add(this.buttonQR);
            this.Controls.Add(this.labelIPs);
            this.Controls.Add(this.chkEnableAudio);
            this.Controls.Add(this.chkStartWithWindows);
            this.Controls.Add(this.chkAutoStart);
            this.Controls.Add(this.textPassword);
            this.Controls.Add(this.labelPassword);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.textLog);
            this.Controls.Add(this.comboScreens);
            this.Controls.Add(this.labelStatus);
            this.Controls.Add(this.buttonStop);
            this.Controls.Add(this.buttonStart);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.Text = "Win7 Virtual Monitor Streamer";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.groupUac.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
