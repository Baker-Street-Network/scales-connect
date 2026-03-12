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
            label7 = new Label();
            button2 = new Button();
            label6 = new Label();
            button1 = new Button();
            label5 = new Label();
            label4 = new Label();
            groupBox2 = new GroupBox();
            comboBox1 = new ComboBox();
            tabControl1 = new TabControl();
            tabPage1 = new TabPage();
            label10 = new Label();
            timeoutTextBox = new TextBox();
            btnTestTransaction = new Button();
            label9 = new Label();
            label8 = new Label();
            portNumber = new TextBox();
            terminalIp = new TextBox();
            tabPage2 = new TabPage();
            button4 = new Button();
            testAmountTextbox = new TextBox();
            label11 = new Label();
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            tabControl1.SuspendLayout();
            tabPage1.SuspendLayout();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Segoe UI", 14F);
            label1.Location = new Point(172, 29);
            label1.Name = "label1";
            label1.Size = new Size(321, 45);
            label1.TabIndex = 0;
            label1.Text = "Baker Street Provider";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(113, 88);
            label2.Name = "label2";
            label2.Size = new Size(432, 30);
            label2.TabIndex = 1;
            label2.Text = "This helper connects the Odoo PoS system to";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(182, 118);
            label3.Name = "label3";
            label3.Size = new Size(296, 30);
            label3.TabIndex = 2;
            label3.Text = "locally connected USB devices.";
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(labelVolume);
            groupBox1.Controls.Add(comboVolume);
            groupBox1.Controls.Add(label7);
            groupBox1.Controls.Add(button2);
            groupBox1.Controls.Add(label6);
            groupBox1.Controls.Add(button1);
            groupBox1.Controls.Add(label5);
            groupBox1.Controls.Add(label4);
            groupBox1.Location = new Point(12, 509);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(517, 285);
            groupBox1.TabIndex = 3;
            groupBox1.TabStop = false;
            groupBox1.Text = "Zebra Scanner/Scale";
            // 
            // labelVolume
            // 
            labelVolume.AutoSize = true;
            labelVolume.Location = new Point(8, 225);
            labelVolume.Name = "labelVolume";
            labelVolume.Size = new Size(193, 30);
            labelVolume.TabIndex = 6;
            labelVolume.Text = "🔊 Beeper Volume:";
            // 
            // comboVolume
            // 
            comboVolume.DropDownStyle = ComboBoxStyle.DropDownList;
            comboVolume.FormattingEnabled = true;
            comboVolume.Items.AddRange(new object[] { "Low", "Medium", "High" });
            comboVolume.Location = new Point(341, 225);
            comboVolume.Name = "comboVolume";
            comboVolume.Size = new Size(160, 38);
            comboVolume.TabIndex = 7;
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(6, 44);
            label7.Name = "label7";
            label7.Size = new Size(348, 30);
            label7.TabIndex = 5;
            label7.Text = "✅️ Webserver listening on localhost";
            // 
            // button2
            // 
            button2.Location = new Point(341, 179);
            button2.Name = "button2";
            button2.Size = new Size(160, 40);
            button2.TabIndex = 4;
            button2.Text = "Set Emulation";
            button2.UseVisualStyleBackColor = true;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(8, 179);
            label6.Name = "label6";
            label6.Size = new Size(270, 30);
            label6.TabIndex = 3;
            label6.Text = "⏳ Keyboard Emulation Off";
            // 
            // button1
            // 
            button1.Location = new Point(341, 133);
            button1.Name = "button1";
            button1.Size = new Size(160, 40);
            button1.TabIndex = 2;
            button1.Text = "Set SNAPI";
            button1.UseVisualStyleBackColor = true;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(8, 133);
            label5.Name = "label5";
            label5.Size = new Size(244, 30);
            label5.TabIndex = 1;
            label5.Text = "⏳ Waiting for scanner...";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(6, 89);
            label4.Name = "label4";
            label4.Size = new Size(258, 30);
            label4.TabIndex = 0;
            label4.Text = "🔍 Discovering scanners...";
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(label11);
            groupBox2.Controls.Add(testAmountTextbox);
            groupBox2.Controls.Add(comboBox1);
            groupBox2.Controls.Add(tabControl1);
            groupBox2.Controls.Add(btnTestTransaction);
            groupBox2.Location = new Point(12, 197);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(757, 306);
            groupBox2.TabIndex = 4;
            groupBox2.TabStop = false;
            groupBox2.Text = "PAX Credit Card Terminal";
            // 
            // comboBox1
            // 
            comboBox1.FormattingEnabled = true;
            comboBox1.Items.AddRange(new object[] { "TCP", "USB" });
            comboBox1.Location = new Point(6, 52);
            comboBox1.Name = "comboBox1";
            comboBox1.Size = new Size(216, 38);
            comboBox1.TabIndex = 1;
            comboBox1.Text = "USB";
            // 
            // tabControl1
            // 
            tabControl1.Controls.Add(tabPage1);
            tabControl1.Controls.Add(tabPage2);
            tabControl1.Location = new Point(8, 96);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(525, 200);
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
            tabPage1.Location = new Point(4, 39);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new Padding(3);
            tabPage1.Size = new Size(517, 157);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "TCP/IP";
            tabPage1.UseVisualStyleBackColor = true;
            // 
            // label10
            // 
            label10.AutoSize = true;
            label10.Location = new Point(371, 12);
            label10.Name = "label10";
            label10.Size = new Size(89, 30);
            label10.TabIndex = 6;
            label10.Text = "Timeout";
            label10.Click += label10_Click;
            // 
            // timeoutTextBox
            // 
            timeoutTextBox.Location = new Point(371, 45);
            timeoutTextBox.Name = "timeoutTextBox";
            timeoutTextBox.Size = new Size(98, 35);
            timeoutTextBox.TabIndex = 5;
            timeoutTextBox.Text = "60000";
            // 
            // btnTestTransaction
            // 
            btnTestTransaction.Location = new Point(571, 209);
            btnTestTransaction.Name = "btnTestTransaction";
            btnTestTransaction.Size = new Size(180, 40);
            btnTestTransaction.TabIndex = 4;
            btnTestTransaction.Text = "Test";
            btnTestTransaction.UseVisualStyleBackColor = true;
            btnTestTransaction.Click += this.button3_Click;
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Location = new Point(263, 12);
            label9.Name = "label9";
            label9.Size = new Size(50, 30);
            label9.TabIndex = 3;
            label9.Text = "Port";
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Location = new Point(14, 12);
            label8.Name = "label8";
            label8.Size = new Size(56, 30);
            label8.TabIndex = 2;
            label8.Text = "Host";
            // 
            // portNumber
            // 
            portNumber.Location = new Point(263, 45);
            portNumber.Name = "portNumber";
            portNumber.Size = new Size(102, 35);
            portNumber.TabIndex = 1;
            portNumber.Text = "10009";
            // 
            // terminalIp
            // 
            terminalIp.Location = new Point(14, 45);
            terminalIp.Name = "terminalIp";
            terminalIp.Size = new Size(243, 35);
            terminalIp.TabIndex = 0;
            // 
            // tabPage2
            // 
            tabPage2.Location = new Point(4, 39);
            tabPage2.Name = "tabPage2";
            tabPage2.Padding = new Padding(3);
            tabPage2.Size = new Size(735, 157);
            tabPage2.TabIndex = 1;
            tabPage2.Text = "USB";
            tabPage2.UseVisualStyleBackColor = true;
            // 
            // button4
            // 
            button4.Location = new Point(14, 95);
            button4.Name = "button4";
            button4.Size = new Size(204, 40);
            button4.TabIndex = 7;
            button4.Text = "Test Connection";
            button4.UseVisualStyleBackColor = true;
            // 
            // testAmountTextbox
            // 
            testAmountTextbox.Location = new Point(571, 168);
            testAmountTextbox.Name = "testAmountTextbox";
            testAmountTextbox.Size = new Size(180, 35);
            testAmountTextbox.TabIndex = 5;
            testAmountTextbox.Text = "3";
            // 
            // label11
            // 
            label11.AutoSize = true;
            label11.Location = new Point(571, 135);
            label11.Name = "label11";
            label11.Size = new Size(160, 30);
            label11.TabIndex = 6;
            label11.Text = "Test Transaction";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(12F, 30F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(781, 800);
            Controls.Add(groupBox2);
            Controls.Add(groupBox1);
            Controls.Add(label3);
            Controls.Add(label2);
            Controls.Add(label1);
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
        private Label label7;
        private Label labelVolume;
        private ComboBox comboVolume;
        private GroupBox groupBox2;
        private ComboBox comboBox1;
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
    }
}
