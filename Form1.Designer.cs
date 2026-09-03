namespace BakerScaleConnect
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        //protected override void Dispose(bool disposing)
        //{
        //    if (disposing && (components != null))
        //    {
        //        components.Dispose();
        //    }
        //    base.Dispose(disposing);
        //}

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            label1 = new Label();
            label2 = new Label();
            label3 = new Label();
            groupBox1 = new GroupBox();
            labelVolume = new Label();
            comboVolume = new ComboBox();
            button2 = new Button();
            label6 = new Label();
            button1 = new Button();
            label5 = new Label();
            label4 = new Label();
            groupBox2 = new GroupBox();
            label11 = new Label();
            testAmountTextbox = new TextBox();
            connectionMethodComboBox = new ComboBox();
            tabControl1 = new TabControl();
            tabPage1 = new TabPage();
            button4 = new Button();
            label10 = new Label();
            timeoutTextBox = new TextBox();
            label9 = new Label();
            label8 = new Label();
            portNumber = new TextBox();
            terminalIp = new TextBox();
            tabPage2 = new TabPage();
            button5 = new Button();
            button3 = new Button();
            label7 = new Label();
            serialPortComboBox = new ComboBox();
            btnTestTransaction = new Button();
            Webserver = new GroupBox();
            textBox1 = new TextBox();
            groupBoxCashDrawer = new GroupBox();
            kickDrawerButton = new Button();
            labelCashDrawerPort = new Label();
            cashDrawerPortComboBox = new ComboBox();
            btnReloadCashDrawerPorts = new Button();
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            tabControl1.SuspendLayout();
            tabPage1.SuspendLayout();
            tabPage2.SuspendLayout();
            Webserver.SuspendLayout();
            groupBoxCashDrawer.SuspendLayout();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Segoe UI", 14F);
            label1.Location = new Point(158, 16);
            label1.Margin = new Padding(2, 0, 2, 0);
            label1.Name = "label1";
            label1.Size = new Size(188, 25);
            label1.TabIndex = 0;
            label1.Text = "Baker Street Provider";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(124, 46);
            label2.Margin = new Padding(2, 0, 2, 0);
            label2.Name = "label2";
            label2.Size = new Size(246, 15);
            label2.TabIndex = 1;
            label2.Text = "This helper connects the Odoo PoS system to";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(164, 61);
            label3.Margin = new Padding(2, 0, 2, 0);
            label3.Name = "label3";
            label3.Size = new Size(169, 15);
            label3.TabIndex = 2;
            label3.Text = "locally connected USB devices.";
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(labelVolume);
            groupBox1.Controls.Add(comboVolume);
            groupBox1.Controls.Add(button2);
            groupBox1.Controls.Add(label6);
            groupBox1.Controls.Add(button1);
            groupBox1.Controls.Add(label5);
            groupBox1.Controls.Add(label4);
            groupBox1.Location = new Point(7, 265);
            groupBox1.Margin = new Padding(2);
            groupBox1.Name = "groupBox1";
            groupBox1.Padding = new Padding(2);
            groupBox1.Size = new Size(302, 125);
            groupBox1.TabIndex = 3;
            groupBox1.TabStop = false;
            groupBox1.Text = "Zebra Scanner/Scale";
            // 
            // labelVolume
            // 
            labelVolume.AutoSize = true;
            labelVolume.Location = new Point(5, 92);
            labelVolume.Margin = new Padding(2, 0, 2, 0);
            labelVolume.Name = "labelVolume";
            labelVolume.Size = new Size(104, 15);
            labelVolume.TabIndex = 6;
            labelVolume.Text = "🔊 Beeper Volume:";
            // 
            // comboVolume
            // 
            comboVolume.DropDownStyle = ComboBoxStyle.DropDownList;
            comboVolume.FormattingEnabled = true;
            comboVolume.Items.AddRange(new object[] { "Low", "Medium", "High" });
            comboVolume.Location = new Point(200, 87);
            comboVolume.Margin = new Padding(2);
            comboVolume.Name = "comboVolume";
            comboVolume.Size = new Size(95, 23);
            comboVolume.TabIndex = 7;
            // 
            // button2
            // 
            button2.Location = new Point(200, 55);
            button2.Margin = new Padding(2);
            button2.Name = "button2";
            button2.Size = new Size(93, 28);
            button2.TabIndex = 4;
            button2.Text = "Set Emulation";
            button2.UseVisualStyleBackColor = true;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(5, 68);
            label6.Margin = new Padding(2, 0, 2, 0);
            label6.Name = "label6";
            label6.Size = new Size(149, 15);
            label6.TabIndex = 3;
            label6.Text = "⏳ Keyboard Emulation Off";
            // 
            // button1
            // 
            button1.Location = new Point(200, 24);
            button1.Margin = new Padding(2);
            button1.Name = "button1";
            button1.Size = new Size(93, 27);
            button1.TabIndex = 2;
            button1.Text = "Set SNAPI";
            button1.UseVisualStyleBackColor = true;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(5, 46);
            label5.Margin = new Padding(2, 0, 2, 0);
            label5.Name = "label5";
            label5.Size = new Size(134, 15);
            label5.TabIndex = 1;
            label5.Text = "⏳ Waiting for scanner...";
            // 
            // label4
            // 
            label4.Dock = DockStyle.Fill;
            label4.Location = new Point(2, 18);
            label4.Name = "label4";
            label4.Padding = new Padding(0, 6, 0, 0);
            label4.Size = new Size(298, 105);
            label4.TabIndex = 0;
            label4.Text = "🔍 Discovering scanners...";
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(label11);
            groupBox2.Controls.Add(testAmountTextbox);
            groupBox2.Controls.Add(connectionMethodComboBox);
            groupBox2.Controls.Add(tabControl1);
            groupBox2.Controls.Add(btnTestTransaction);
            groupBox2.Location = new Point(7, 98);
            groupBox2.Margin = new Padding(2);
            groupBox2.Name = "groupBox2";
            groupBox2.Padding = new Padding(2);
            groupBox2.Size = new Size(466, 163);
            groupBox2.TabIndex = 4;
            groupBox2.TabStop = false;
            groupBox2.Text = "PAX Credit Card Terminal";
            // 
            // label11
            // 
            label11.AutoSize = true;
            label11.Location = new Point(315, 74);
            label11.Margin = new Padding(2, 0, 2, 0);
            label11.Name = "label11";
            label11.Size = new Size(92, 15);
            label11.TabIndex = 6;
            label11.Text = "Test Transaction";
            // 
            // testAmountTextbox
            // 
            testAmountTextbox.Location = new Point(315, 90);
            testAmountTextbox.Margin = new Padding(2);
            testAmountTextbox.Name = "testAmountTextbox";
            testAmountTextbox.Size = new Size(119, 23);
            testAmountTextbox.TabIndex = 5;
            testAmountTextbox.Text = "3";
            // 
            // connectionMethodComboBox
            // 
            connectionMethodComboBox.FormattingEnabled = true;
            connectionMethodComboBox.Items.AddRange(new object[] { "TCP", "USB" });
            connectionMethodComboBox.Location = new Point(4, 21);
            connectionMethodComboBox.Margin = new Padding(2);
            connectionMethodComboBox.Name = "connectionMethodComboBox";
            connectionMethodComboBox.Size = new Size(128, 23);
            connectionMethodComboBox.TabIndex = 1;
            connectionMethodComboBox.Text = "USB";
            // 
            // tabControl1
            // 
            tabControl1.Controls.Add(tabPage1);
            tabControl1.Controls.Add(tabPage2);
            tabControl1.Location = new Point(5, 48);
            tabControl1.Margin = new Padding(2);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(306, 111);
            tabControl1.TabIndex = 0;
            // 
            // tabPage1
            // 
            tabPage1.Controls.Add(button4);
            tabPage1.Controls.Add(label10);
            tabPage1.Controls.Add(timeoutTextBox);
            tabPage1.Controls.Add(label9);
            tabPage1.Controls.Add(label8);
            tabPage1.Controls.Add(portNumber);
            tabPage1.Controls.Add(terminalIp);
            tabPage1.Location = new Point(4, 24);
            tabPage1.Margin = new Padding(2);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new Padding(2);
            tabPage1.Size = new Size(298, 83);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "TCP/IP";
            tabPage1.UseVisualStyleBackColor = true;
            // 
            // button4
            // 
            button4.Location = new Point(8, 48);
            button4.Margin = new Padding(2);
            button4.Name = "button4";
            button4.Size = new Size(119, 31);
            button4.TabIndex = 7;
            button4.Text = "Test Connection";
            button4.UseVisualStyleBackColor = true;
            // 
            // label10
            // 
            label10.AutoSize = true;
            label10.Location = new Point(216, 6);
            label10.Margin = new Padding(2, 0, 2, 0);
            label10.Name = "label10";
            label10.Size = new Size(52, 15);
            label10.TabIndex = 6;
            label10.Text = "Timeout";
            label10.Click += label10_Click;
            // 
            // timeoutTextBox
            // 
            timeoutTextBox.Location = new Point(216, 22);
            timeoutTextBox.Margin = new Padding(2);
            timeoutTextBox.Name = "timeoutTextBox";
            timeoutTextBox.Size = new Size(59, 23);
            timeoutTextBox.TabIndex = 5;
            timeoutTextBox.Text = "60000";
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Location = new Point(153, 6);
            label9.Margin = new Padding(2, 0, 2, 0);
            label9.Name = "label9";
            label9.Size = new Size(29, 15);
            label9.TabIndex = 3;
            label9.Text = "Port";
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Location = new Point(8, 6);
            label8.Margin = new Padding(2, 0, 2, 0);
            label8.Name = "label8";
            label8.Size = new Size(32, 15);
            label8.TabIndex = 2;
            label8.Text = "Host";
            // 
            // portNumber
            // 
            portNumber.Location = new Point(153, 22);
            portNumber.Margin = new Padding(2);
            portNumber.Name = "portNumber";
            portNumber.Size = new Size(61, 23);
            portNumber.TabIndex = 1;
            portNumber.Text = "10009";
            // 
            // terminalIp
            // 
            terminalIp.Location = new Point(8, 22);
            terminalIp.Margin = new Padding(2);
            terminalIp.Name = "terminalIp";
            terminalIp.Size = new Size(143, 23);
            terminalIp.TabIndex = 0;
            // 
            // tabPage2
            // 
            tabPage2.Controls.Add(button5);
            tabPage2.Controls.Add(button3);
            tabPage2.Controls.Add(label7);
            tabPage2.Controls.Add(serialPortComboBox);
            tabPage2.Location = new Point(4, 24);
            tabPage2.Margin = new Padding(2);
            tabPage2.Name = "tabPage2";
            tabPage2.Padding = new Padding(2);
            tabPage2.Size = new Size(298, 83);
            tabPage2.TabIndex = 1;
            tabPage2.Text = "USB";
            tabPage2.UseVisualStyleBackColor = true;
            // 
            // button5
            // 
            button5.Location = new Point(104, 52);
            button5.Margin = new Padding(2);
            button5.Name = "button5";
            button5.Size = new Size(119, 26);
            button5.TabIndex = 8;
            button5.Text = "Test Connection";
            button5.UseVisualStyleBackColor = true;
            button5.Click += button5_Click;
            // 
            // button3
            // 
            button3.Location = new Point(4, 53);
            button3.Margin = new Padding(2);
            button3.Name = "button3";
            button3.Size = new Size(96, 26);
            button3.TabIndex = 2;
            button3.Text = "Reload";
            button3.UseVisualStyleBackColor = true;
            button3.Click += button3_Click_1;
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(4, 8);
            label7.Margin = new Padding(2, 0, 2, 0);
            label7.Name = "label7";
            label7.Size = new Size(60, 15);
            label7.TabIndex = 1;
            label7.Text = "Serial Port";
            // 
            // serialPortComboBox
            // 
            serialPortComboBox.FormattingEnabled = true;
            serialPortComboBox.Location = new Point(4, 25);
            serialPortComboBox.Margin = new Padding(2);
            serialPortComboBox.Name = "serialPortComboBox";
            serialPortComboBox.Size = new Size(181, 23);
            serialPortComboBox.TabIndex = 0;
            // 
            // btnTestTransaction
            // 
            btnTestTransaction.Location = new Point(315, 117);
            btnTestTransaction.Margin = new Padding(2);
            btnTestTransaction.Name = "btnTestTransaction";
            btnTestTransaction.Size = new Size(119, 26);
            btnTestTransaction.TabIndex = 4;
            btnTestTransaction.Text = "Test";
            btnTestTransaction.UseVisualStyleBackColor = true;
            // 
            // Webserver
            // 
            Webserver.Controls.Add(textBox1);
            Webserver.Location = new Point(312, 265);
            Webserver.Margin = new Padding(2);
            Webserver.Name = "Webserver";
            Webserver.Padding = new Padding(2);
            Webserver.Size = new Size(161, 125);
            Webserver.TabIndex = 5;
            Webserver.TabStop = false;
            Webserver.Text = "Webserver";
            // 
            // textBox1
            // 
            textBox1.Location = new Point(4, 24);
            textBox1.Margin = new Padding(2);
            textBox1.Multiline = true;
            textBox1.Name = "textBox1";
            textBox1.ReadOnly = true;
            textBox1.Size = new Size(153, 97);
            textBox1.TabIndex = 6;
            textBox1.Text = "✅️ Webserver listening on localhost";
            // 
            // groupBoxCashDrawer
            // 
            groupBoxCashDrawer.Controls.Add(kickDrawerButton);
            groupBoxCashDrawer.Controls.Add(labelCashDrawerPort);
            groupBoxCashDrawer.Controls.Add(cashDrawerPortComboBox);
            groupBoxCashDrawer.Controls.Add(btnReloadCashDrawerPorts);
            groupBoxCashDrawer.Location = new Point(7, 394);
            groupBoxCashDrawer.Margin = new Padding(2);
            groupBoxCashDrawer.Name = "groupBoxCashDrawer";
            groupBoxCashDrawer.Padding = new Padding(2);
            groupBoxCashDrawer.Size = new Size(466, 54);
            groupBoxCashDrawer.TabIndex = 7;
            groupBoxCashDrawer.TabStop = false;
            groupBoxCashDrawer.Text = "Cash Drawer";
            // 
            // kickDrawerButton
            // 
            kickDrawerButton.Location = new Point(281, 22);
            kickDrawerButton.Margin = new Padding(2);
            kickDrawerButton.Name = "kickDrawerButton";
            kickDrawerButton.Size = new Size(98, 23);
            kickDrawerButton.TabIndex = 3;
            kickDrawerButton.Text = "Kick Drawer";
            kickDrawerButton.UseVisualStyleBackColor = true;
            kickDrawerButton.Click += kickDrawerButton_Click;
            // 
            // labelCashDrawerPort
            // 
            labelCashDrawerPort.AutoSize = true;
            labelCashDrawerPort.Location = new Point(4, 22);
            labelCashDrawerPort.Margin = new Padding(2, 0, 2, 0);
            labelCashDrawerPort.Name = "labelCashDrawerPort";
            labelCashDrawerPort.Size = new Size(60, 15);
            labelCashDrawerPort.TabIndex = 0;
            labelCashDrawerPort.Text = "Serial Port";
            // 
            // cashDrawerPortComboBox
            // 
            cashDrawerPortComboBox.FormattingEnabled = true;
            cashDrawerPortComboBox.Location = new Point(69, 21);
            cashDrawerPortComboBox.Margin = new Padding(2);
            cashDrawerPortComboBox.Name = "cashDrawerPortComboBox";
            cashDrawerPortComboBox.Size = new Size(125, 23);
            cashDrawerPortComboBox.TabIndex = 1;
            // 
            // btnReloadCashDrawerPorts
            // 
            btnReloadCashDrawerPorts.Location = new Point(198, 21);
            btnReloadCashDrawerPorts.Margin = new Padding(2);
            btnReloadCashDrawerPorts.Name = "btnReloadCashDrawerPorts";
            btnReloadCashDrawerPorts.Size = new Size(79, 23);
            btnReloadCashDrawerPorts.TabIndex = 2;
            btnReloadCashDrawerPorts.Text = "Reload";
            btnReloadCashDrawerPorts.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(479, 454);
            Controls.Add(groupBoxCashDrawer);
            Controls.Add(Webserver);
            Controls.Add(groupBox2);
            Controls.Add(groupBox1);
            Controls.Add(label3);
            Controls.Add(label2);
            Controls.Add(label1);
            Margin = new Padding(2);
            MaximizeBox = false;
            Name = "Form1";
            Text = "Baker Street Provider";
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            tabControl1.ResumeLayout(false);
            tabPage1.ResumeLayout(false);
            tabPage1.PerformLayout();
            tabPage2.ResumeLayout(false);
            tabPage2.PerformLayout();
            Webserver.ResumeLayout(false);
            Webserver.PerformLayout();
            groupBoxCashDrawer.ResumeLayout(false);
            groupBoxCashDrawer.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label label1;
        private Label label2;
        private Label label3;
        private GroupBox groupBox1;
        private Label label4;
        private Label label5;
        private Button button2;
        private Label label6;
        private Button button1;
        private Label labelVolume;
        private ComboBox comboVolume;
        private GroupBox groupBox2;
        private ComboBox connectionMethodComboBox;
        private TabControl tabControl1;
        private TabPage tabPage1;
        private TabPage tabPage2;
        private TextBox portNumber;
        private TextBox terminalIp;
        private Label label10;
        private TextBox timeoutTextBox;
        private Label label9;
        private Label label8;
        private Label label11;
        private Button btnTestTransaction;
        private Button button4;
        private TextBox testAmountTextbox;
        private Label label7;
        private ComboBox serialPortComboBox;
        private GroupBox Webserver;
        private TextBox textBox1;
        private Button button3;
        private Button button5;
        private GroupBox groupBoxCashDrawer;
        private Label labelCashDrawerPort;
        private ComboBox cashDrawerPortComboBox;
        private Button btnReloadCashDrawerPorts;
        private Button kickDrawerButton;
    }
}
