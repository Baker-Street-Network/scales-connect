///*		
// * ===========================================================================================
// * = COPYRIGHT		                  	
// *          PAX Computer Technology (Shenzhen) Co., Ltd. PROPRIETARY INFORMATION	
// *   This software is supplied under the terms of a license agreement or nondisclosure 	
// *   agreement with PAX Computer Technology (Shenzhen) Co., Ltd. and may not be copied or 
// *   disclosed except in accordance with the terms in that agreement.   		
// *     Copyright (C) 2023 PAX Computer Technology (Shenzhen) Co., Ltd. All rights reserved.
// * ===========================================================================================
// */

//using System;
//using System.Collections.Generic;
//using System.ComponentModel;
//using System.Data;
//using System.Drawing;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using System.Windows.Forms;
//using static System.Windows.Forms.VisualStyles.VisualStyleElement;

//namespace POSLinkSemiIntegrationDemo
//{
//    public partial class Form1 : Form
//    {
//        public Form1()
//        {
//            InitializeComponent();
//        }

//        private void CreditButton_Click(object sender, EventArgs e)
//        {
//            CreditButton.Enabled = false;
//            IpTextBox.Enabled = false;
//            PortTextBox.Enabled = false;
//            TimeoutTextBox.Enabled = false;
//            AmountTextBox.Enabled = false;
//            EcrRefNumberTextBox.Enabled = false;
//            Task.Run(() => {
//                string ip = IpTextBox.Text;
//                int port;
//                bool isPortANumber = Int32.TryParse(PortTextBox.Text, out port);
//                if (!isPortANumber)
//                {
//                    this.Invoke((MethodInvoker)delegate
//                    {
//                        MessageBox.Show("Port is not a number.");
//                    });
//                    return;
//                }
//                int timeout;
//                bool isTimeoutANumber = Int32.TryParse(TimeoutTextBox.Text, out timeout);
//                if (!isTimeoutANumber)
//                {
//                    this.Invoke((MethodInvoker)delegate
//                    {
//                        MessageBox.Show("Timeout is not a number.");
//                    });
//                    return;
//                }
//                string amount = AmountTextBox.Text;
//                string ecrRefNumber = EcrRefNumberTextBox.Text;

//                POSLinkCore.CommunicationSetting.TcpSetting tcpSetting = new POSLinkCore.CommunicationSetting.TcpSetting();
//                tcpSetting.Ip = ip;
//                tcpSetting.Port = port;
//                tcpSetting.Timeout = timeout;

//                POSLinkSemiIntegration.POSLinkSemi poslinkSemi = POSLinkSemiIntegration.POSLinkSemi.GetPOSLinkSemi();
//                POSLinkSemiIntegration.Terminal terminal = poslinkSemi.GetTerminal(tcpSetting);

//                POSLinkSemiIntegration.Transaction.DoCreditRequest request = new POSLinkSemiIntegration.Transaction.DoCreditRequest();
//                POSLinkAdmin.Util.AmountRequest amountReq = new POSLinkAdmin.Util.AmountRequest();
//                amountReq.TransactionAmount = amount;
//                request.AmountInformation = amountReq;
//                POSLinkSemiIntegration.Util.TraceRequest traceReq = new POSLinkSemiIntegration.Util.TraceRequest();
//                traceReq.EcrReferenceNumber = ecrRefNumber;
//                request.TraceInformation = traceReq;
//                request.TransactionType = POSLinkAdmin.Const.TransactionType.Sale;

//                POSLinkSemiIntegration.Transaction.DoCreditResponse response;
//                POSLinkAdmin.ExecutionResult result = terminal.Transaction.DoCredit(request, out response);
//                if (result.GetErrorCode() == POSLinkAdmin.ExecutionResult.Code.Ok)
//                {
//                    this.Invoke((MethodInvoker)delegate
//                    {
//                        MessageBox.Show("Response code: " + response.ResponseCode + "\r\nResponse Message: " + response.ResponseMessage);
//                    });
//                }
//                else
//                {
//                    this.Invoke((MethodInvoker)delegate
//                    {
//                        MessageBox.Show("Error: " + result.GetErrorCode());
//                    });
//                }
//                this.Invoke((MethodInvoker)delegate
//                {
//                    Activate();
//                    CreditButton.Enabled = true;
//                    IpTextBox.Enabled = true;
//                    PortTextBox.Enabled = true;
//                    TimeoutTextBox.Enabled = true;
//                    AmountTextBox.Enabled = true;
//                    EcrRefNumberTextBox.Enabled = true;
//                });
//            });
//        }

//        private void ExitButton_Click(object sender, EventArgs e)
//        {
//            Close();
//            Dispose();
//        }
//    }
//}
