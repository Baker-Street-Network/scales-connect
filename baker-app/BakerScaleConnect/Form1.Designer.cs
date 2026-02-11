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
            groupBox1.SuspendLayout();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Segoe UI", 14F);
            label1.Location = new Point(172, 29);
            label1.Name = "label1";
            label1.Size = new Size(313, 45);
            label1.TabIndex = 0;
            label1.Text = "Baker Scale Connect";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(113, 88);
            label2.Name = "label2";
            label2.Size = new Size(449, 30);
            label2.TabIndex = 1;
            label2.Text = "This helper connects the Odoo PoS system to a";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(127, 118);
            label3.Name = "label3";
            label3.Size = new Size(421, 30);
            label3.TabIndex = 2;
            label3.Text = "locally connected Zebra embedded scanner.";
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
            groupBox1.Location = new Point(12, 185);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(645, 317);
            groupBox1.TabIndex = 3;
            groupBox1.TabStop = false;
            groupBox1.Text = "Configuration";
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
            button2.Location = new Point(475, 174);
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
            button1.Location = new Point(475, 128);
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
            // labelVolume
            // 
            labelVolume.AutoSize = true;
            labelVolume.Location = new Point(8, 225);
            labelVolume.Name = "labelVolume";
            labelVolume.Size = new Size(175, 30);
            labelVolume.TabIndex = 6;
            labelVolume.Text = "🔊 Beeper Volume:";
            // 
            // comboVolume
            // 
            comboVolume.DropDownStyle = ComboBoxStyle.DropDownList;
            comboVolume.FormattingEnabled = true;
            comboVolume.Items.AddRange(new object[] { "Low", "Medium", "High" });
            comboVolume.Location = new Point(475, 222);
            comboVolume.Name = "comboVolume";
            comboVolume.Size = new Size(160, 38);
            comboVolume.TabIndex = 7;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(12F, 30F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(676, 530);
            Controls.Add(groupBox1);
            Controls.Add(label3);
            Controls.Add(label2);
            Controls.Add(label1);
            MaximizeBox = false;
            Name = "Form1";
            Text = "Baker Scale Connect";
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
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
    }
}
