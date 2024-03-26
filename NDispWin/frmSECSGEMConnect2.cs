﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace NDispWin
{
    public partial class frmSECSGEMConnect2 : Form
    {
        public SECSGEMConnect2 sgc2;

        public frmSECSGEMConnect2()
        {
            InitializeComponent();
            GControl.LogForm(this);
        }

        private void frmSECSGEMConnect2_Load(object sender, EventArgs e)
        {
            GControl.UpdateFormControl(this);
            this.Text = "SECSGEMConnect2";

            sgc2.LoggedEvent += new SECSGEMConnect2.OnLogged(OnLoggedEvent);
            sgc2.MapDownloadEvent += new SECSGEMConnect2.OnMapDownload(OnMapDownloadEvent);

            lbxLog.DataSource = sgc2.LogList;

            tbxIPAddress.Text = sgc2.client.IPAddress;// "127.0.0.1";// ipHostInfo.AddressList[0].ToString();
            tbxPort.Text = sgc2.client.Port.ToString();// "9000";

            cbEnableSECSGEMConnect2.Checked = sgc2.EnableSECSGEMConnect2;
            cbEnableAlarm.Checked = sgc2.EnableAlarm;
            cbEnableEvent.Checked = sgc2.EnableEvent;
            cbEnableRMS.Checked = sgc2.EnableRMS;
            cbEnableE142.Checked = sgc2.EnableStripMapE142;
            cbUseFreshMap.Checked = sgc2.UseFreshMap;
            tbxTimeOut.Text = sgc2.TimeOut.ToString();
        }
        private void frmSECSGEMConnect2_FormClosed(object sender, FormClosedEventArgs e)
        {
            sgc2.LoggedEvent -= new SECSGEMConnect2.OnLogged(OnLoggedEvent);
            sgc2.MapDownloadEvent -= new SECSGEMConnect2.OnMapDownload(OnMapDownloadEvent);
        }

        private void OnLoggedEvent()
        {
            Invoke(new Action(() =>
            {
                lbxLog.DataSource = null;//Dirty method to force update
                lbxLog.DataSource = sgc2.LogList;
            }));
        }
        private void OnMapDownloadEvent()
        {
            Invoke(new Action(() =>
            {
                lbxDownloadedMap.Items.Clear();
                rtbxInternalMap.Clear();
                tbxStripID.Text = sgc2.stripID;
            }));
            for (int r = 0; r < sgc2.cr.Y; r++)
            {
                if (r == 0)
                {
                    string header = "    ";
                    for (int c = sgc2.cr.X; c > 0; c--)
                    {
                        header = header + " [" + c.ToString("00") + "]";
                    }
                    Invoke(new Action(() =>
                    {
                        lbxDownloadedMap.Items.Add(header);
                        rtbxInternalMap.Text = header;
                    }));
                }

                string data = "[" + (r + 1).ToString("00") + "]";
                for (int c = 0; c < sgc2.cr.X; c++)
                {
                    data = data + " " + sgc2.dnLoadMap[c, r];
                }
                Invoke(new Action(() =>
                {
                    lbxDownloadedMap.Items.Add(data);
                }));

                data = "[" + (r + 1).ToString("00") + "]";
                for (int c = 0; c < sgc2.cr.X; c++)
                {
                    data = data + " " + sgc2.map[c, r].ToString("0000");
                }
                Invoke(new Action(() =>
                {
                    rtbxInternalMap.Text = rtbxInternalMap.Text + '\n' + data;
                }));
            }
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (sgc2.client.IsConnected)
            {
                sgc2.Disconnect();
                return;
            }

            try
            {
                int.TryParse(tbxPort.Text, out int port);
                sgc2.Connect(tbxIPAddress.Text, port);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message.ToString());
            }
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            sgc2.Send(rtbxOutMsg.Text);
        }

        private void btnDownloadMap_Click(object sender, EventArgs e)
        {
            if (cbFUseFile.Checked)
            {
                string fileName = @"c:\Map\" + tbxStripID.Text + ".xml";

                if (!File.Exists(fileName))
                {
                    MessageBox.Show("Strip Map not found.");
                    return;
                }

                using (var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var textReader = new StreamReader(fileStream))
                {
                    string content = textReader.ReadToEnd();
                    sgc2.UpdateXMLtoLocal(content);
                }
            }
            else
            {
                sgc2.SendDownload(tbxStripID.Text);
            }
        }
        private void btnClearMap_Click(object sender, EventArgs e)
        {
            sgc2.stripID = "";
            sgc2.xmlContent = "";
            sgc2.dnLoadMap = new string[100, 100];
            sgc2.map = new int[100, 100];
            sgc2.cr = new Point(0, 0);

            lbxDownloadedMap.Items.Clear();
            rtbxInternalMap.Clear();
        }
        private void btnUploadMap_Click(object sender, EventArgs e)
        {
            string data = rtbxInternalMap.Text;
            string[] lines = data.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int r = 0; r < lines.Length - 1; r++)
            {
                string[] bin = lines[r + 1].Split(new[] { " " }, StringSplitOptions.None);
                for (int c = 0; c < bin.Length - 1; c++)
                    int.TryParse(bin[c + 1], out sgc2.map[c, r]);
            }

            if (cbFUseFile.Checked)
            {
                string fileName = @"c:\Map\" + tbxStripID.Text + "_Upload.xml";
                sgc2.stripID = tbxStripID.Text;
                sgc2.UploadXMLString(fileName);
            }
            else
            {
                sgc2.UploadXMLString("");
            }
        }

        private void btnSelectRecipe_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.InitialDirectory = @"c:\Program Files\NSWAutomation\NDisp3Win\Recipe";
            if (ofd.ShowDialog() != DialogResult.OK) return;
            rtbxRecipeFilename.Text = ofd.FileName;
        }

        private void btnPPSend_Click(object sender, EventArgs e)
        {
            string fileName = rtbxRecipeFilename.Text;

            if (!File.Exists(fileName))
            {
                MessageBox.Show("Recipe File not found."); return;
            }

            sgc2.SendPPSend(fileName);
        }
        private void btnAlarmSet_Click(object sender, EventArgs e)
        {
            sgc2.SendAlarmSet(tbxAlarm.Text);
        }
        private void btnAlarmReset_Click(object sender, EventArgs e)
        {
            sgc2.SendAlarmClear(tbxAlarm.Text);
        }
        private void btnEvent_Click(object sender, EventArgs e)
        {
            sgc2.SendEvent(tbxEvent.Text);
        }

        private void tmr500ms_Tick(object sender, EventArgs e)
        {
            if (!Visible) return;

            if (sgc2.EnableSECSGEMConnect2)
            {
                lblIPPort.Text = sgc2.client.IPAddress + " : " + sgc2.client.Port.ToString() + (sgc2.client.IsConnected ? " Connected" : " ");
                lblIPPort.BackColor = sgc2.client.IsConnected ? Color.Lime : Color.Red;
            }
            else
            {
                lblIPPort.Text = sgc2.client.IPAddress + " : " + sgc2.client.Port.ToString() + " Disabled";
                lblIPPort.BackColor = this.BackColor;
            }
            btnConnect.Text = sgc2.client.IsConnected ? "Disconnect" : "Connect";
            btnConnect.Enabled = sgc2.EnableSECSGEMConnect2;
        }
        private void lbxLog_DataSourceChanged(object sender, EventArgs e)
        {
        }

        private void cbEnableAlarm_Click(object sender, EventArgs e)
        {
            Log.OnSet("EnableAlarm", sgc2.EnableAlarm, !sgc2.EnableAlarm);
            sgc2.EnableAlarm = cbEnableAlarm.Checked;
        }

        private void cbEnableEvent_Click(object sender, EventArgs e)
        {
            Log.OnSet("EnableEvent", sgc2.EnableEvent, !sgc2.EnableEvent);
            sgc2.EnableEvent = cbEnableEvent.Checked;
        }

        private void cbEnableRMS_Click(object sender, EventArgs e)
        {
            Log.OnSet("EnableRMS", sgc2.EnableRMS, !sgc2.EnableRMS);
            sgc2.EnableRMS = cbEnableRMS.Checked;
        }

        private void cbEnableE142_Click(object sender, EventArgs e)
        {
            Log.OnSet("EnableStripMapE142", sgc2.EnableStripMapE142, !sgc2.EnableStripMapE142);
            sgc2.EnableStripMapE142 = cbEnableE142.Checked;
        }

        private void cbEnableSECSGEMConnect2_Click(object sender, EventArgs e)
        {
            Log.OnSet("EnableSECSGEMConnect2", sgc2.EnableSECSGEMConnect2, !sgc2.EnableSECSGEMConnect2);
            sgc2.EnableSECSGEMConnect2 = (sender as CheckBox).Checked;
        }

        private void cbUseFreshMap_Click(object sender, EventArgs e)
        {
            Log.OnSet("UseFreshMap", sgc2.UseFreshMap, !sgc2.UseFreshMap);
            sgc2.UseFreshMap = cbUseFreshMap.Checked;
        }

        private void tbxTimeOut_TextChanged(object sender, EventArgs e)
        {
            int i = 0;
            int.TryParse(tbxTimeOut.Text, out i);
            sgc2.TimeOut = i;
        }
    }
    public class TClient
    {
        public string IPAddress = "127.0.0.1";
        public int Port = 9000;

        public string RxData = "";
        public string TxData = "";

        private static Mutex mutexRX = new Mutex();
        internal class SocketPacket
        {
            public System.Net.Sockets.Socket m_currentSocket;
            public byte[] dataBuffer = new byte[1];
        }

        private AsyncCallback pfnCallBack;
        private Socket socket;

        public TClient()
        {
            this.ConnectedEvent += new OnConnected(ConnectedToServerEvent);
            this.DisconnectedEvent += new OnDisconnected(DisconnectedFromServerEvent);

            this.FrameSendEvent += new OnFrameSend(OnFrameSendEvent);
            this.FrameStartReceivedEvent += new OnFrameStartReceived(Server_FrameStartReceivedEvent);
            this.FrameEndReceivedEvent += new OnFrameEndReceived(Server_FrameEndReceivedEvent);
        }

        // Start waiting for data from the TClient
        private void WaitForData(Socket Socket)
        {
            try
            {
                if (pfnCallBack == null)
                {
                    // Specify the call back function which is to be 
                    // invoked when there is any write activity by the 
                    // connected TClient
                    pfnCallBack = new AsyncCallback(OnDataReceived);
                }
                SocketPacket SocPkt = new SocketPacket();
                SocPkt.m_currentSocket = Socket;
                // Start receiving any data written by the connected TClient asynchronously
                Socket.BeginReceive(SocPkt.dataBuffer, 0, SocPkt.dataBuffer.Length, SocketFlags.None, pfnCallBack, SocPkt);
            }
            catch (SocketException se)
            {
                //MessageBox.Show(se.Message);
                throw;
            }
        }

        public delegate void OnConnected();
        public event OnConnected ConnectedEvent;
        private void ConnectedToServerEvent() { }

        public delegate void OnDisconnected();
        public event OnDisconnected DisconnectedEvent;
        private void DisconnectedFromServerEvent() { }

        public delegate void OnFrameSend();
        public event OnFrameSend FrameSendEvent;
        private void OnFrameSendEvent() { }

        public delegate void OnFrameStartReceived();
        public event OnFrameStartReceived FrameStartReceivedEvent;
        private void Server_FrameStartReceivedEvent() { }
        public delegate void OnFrameEndReceived();
        public event OnFrameEndReceived FrameEndReceivedEvent;
        private void Server_FrameEndReceivedEvent() { }

        // This the call back function which will be invoked when the socket
        // detects any TClient writing of data on the stream
        private void OnDataReceived(IAsyncResult asyn)
        {
            if (socket != null && !socket.Connected)
            {
                DisconnectedEvent();
                return;
            }

            mutexRX.WaitOne();

            try
            {
                SocketPacket SocPkt = (SocketPacket)asyn.AsyncState;

                // Complete the BeginReceive() asynchronous call by EndReceive() method
                // which will return the number of characters written to the stream 
                // by the TClient
                int iRx = 0;
                iRx = SocPkt.m_currentSocket.EndReceive(asyn);

                char[] chars = new char[iRx + 1];
                System.Text.Decoder d = System.Text.Encoding.UTF8.GetDecoder();
                int charLen = d.GetChars(SocPkt.dataBuffer, 0, iRx, chars, 0);
                System.String szData = new System.String(chars);
                string sData = szData.Remove(charLen);

                if (sData.Contains((char)0x02)) 
                    FrameStartReceivedEvent();
                RxData = RxData + sData;
                if (RxData.Contains((char)0x03)) 
                    FrameEndReceivedEvent();

                // Continue the waiting for data on the Socket
                WaitForData(SocPkt.m_currentSocket);
            }
            catch (ObjectDisposedException)
            {
                System.Diagnostics.Debugger.Log(0, "1", "\nOnDataReceived: Socket has been closed\n");
            }
            catch (SocketException se)
            {
                //MessageBox.Show(se.Message);
                DisconnectedEvent();
            }
            finally
            {
                mutexRX.ReleaseMutex();
            }
        }

        public void Connect(string ServerIPAddress, int Port)
        {
            if (ServerIPAddress == "" || Port == 0)
            {
                throw new Exception("IP Address and Port Number are required to connect to the Server\n");
            }
            try
            {
                // Create the socket instance
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // Cet the remote IP address
                IPAddress ip = System.Net.IPAddress.Parse(ServerIPAddress);
                //int iPortNo = System.Convert.ToInt16(tbox_Port.Text);
                // Create the end point 
                IPEndPoint ipEnd = new IPEndPoint(ip, Port);
                // Connect to the remote host
                socket.Connect(ipEnd);
                if (socket.Connected)
                {
                    ConnectedEvent();
                    //Wait for data asynchronously 
                    WaitForData(socket);
                }
            }
            catch (SocketException se)
            {
                string str;
                str = "\nConnection failed, is the server running?\n" + se.Message;
                //MessageBox.Show(str);
                throw new Exception(str);
            }
        }
        public void Disconnect()
        {
            if (socket != null)
            {
                socket.Close();
                socket = null;
            }
        }

        public bool IsConnected
        {
            get
            {
                if (socket == null) return false;
                return socket.Connected;
            }
        }

        public int Send(string Data)
        {
            Object objData = Data;
            byte[] byData = System.Text.Encoding.ASCII.GetBytes(objData.ToString());
            try
            {
                if (socket != null)
                {
                    if (socket.Connected)
                    {
                        socket.Send(byData);
                        return Data.Length;
                    }
                }
                return -1;
            }
            catch { throw; }
        }
        public int SendFrame(string Data)
        {
            if (Data.Length == 0) return 0;
            TxData = Data;
            Data = (char)0x02 + Data + (char)0x03;

            FrameSendEvent();
            return Send(Data);
        }

        public int BufferFrameCount
        {
            get { return RxData.Split((char)0x03).Length - 1; }
        }
        public int ReceiveFrame(ref string Data)
        {
            mutexRX.WaitOne();
            try
            {
                if (RxData.Length == 0) return 0;
                if (!RxData.Contains((char)0x03)) return 0;

                string Temp = RxData;
                Temp = Temp.Remove(0, 1);
                if (Temp.Length > Temp.IndexOf((char)0x03))
                {
                    Temp = Temp.Remove(RxData.IndexOf((char)0x03) - 1);
                }
                RxData = RxData.Remove(0, RxData.IndexOf((char)0x03) + 1);

                Data = Temp;
            }
            catch { throw; }
            finally { mutexRX.ReleaseMutex(); }
            return Data.Length;
        }
    }
    public class SECSGEMConnect2
    {
        public TClient client = new TClient();
        public List<string> LogList = new List<string>();

        public bool EnableSECSGEMConnect2 = false;
        public bool EnableAlarm = false;
        public bool EnableEvent = false;
        public bool EnableRMS = false;
        public bool EnableStripMapE142 = false;
        public bool UseFreshMap = false;
        public int TimeOut = 30000;

        #region Map Variables
        public enum EMapState { None, Requested, Loaded, NoFound, DecodeError, Uploaded, UploadFail}
        public EMapState MapState = EMapState.None;
        public string stripID = "";
        public string xmlContent = "";
        public string[,] dnLoadMap = new string[400, 400];
        public int[,] map = new int[400, 400];
        public Point cr = new Point(0, 0);
        #endregion

        public SECSGEMConnect2()
        {
            client.ConnectedEvent += new TClient.OnConnected(OnConnectedEvent);
            client.DisconnectedEvent += new TClient.OnDisconnected(OnDisconnectedEvent);

            client.FrameSendEvent += new TClient.OnFrameSend(OnFrameSendEvent);
            client.FrameEndReceivedEvent += new TClient.OnFrameEndReceived(OnFrameEndReceivedEvent);

            MapDownloadEvent += new OnMapDownload(OnMapDownloadEvent);
            LoggedEvent += new OnLogged(OnLoggedvent);
        }

        private void AddLog(string s)
        {
            if (LogList.Count > 100) LogList.RemoveAt(100);
            LogList.Insert(0, s);
            LoggedEvent();
        }

        private void OnConnectedEvent()
        {
            AddLog("Connected.");
        }
        private void OnDisconnectedEvent()
        {
            AddLog("Disconnected.");
        }
        private void OnFrameSendEvent()
        {
            AddLog("< " + client.TxData);
        }
        private void OnFrameEndReceivedEvent()
        {
            if (!EnableSECSGEMConnect2) return;

            string rxData = "";
            while (client.BufferFrameCount > 0)
            {
                if (client.ReceiveFrame(ref rxData) != 0)
                {
                    AddLog("> " + rxData);
                }

                string[] data = rxData.Split(new[] { "," }, StringSplitOptions.None);
                string data0 = data[0].ToUpper();
                switch (data0)
                {
                    case "DOWNLOAD":
                        if (!EnableStripMapE142) break;
                        if (data.Length == 1) AddLog("Download data incomplete.");
                        string content = rxData;
                        content = content.Remove(0, content.IndexOf("<?"));
                        UpdateXMLtoLocal(content);
                        break;
                    case "PPSELECT":
                        if (!EnableRMS) break;
                        if (data.Length == 1) AddLog("PPSELECT data incomplete.");
                        PPSelect(data[1]);
                        break;
                    case "START":
                        if (!EnableEvent) break;
                        Define_Run.TR_StartRun();
                        //if (Define_Run.BetaAutoRun) AutoRun();
                        break;
                    case "STOP":
                        if (!EnableEvent) break;
                        Define_Run.TR_StopRun();
                        break;
                    case "SERVER_DISCONNECTED":
                        Msg MsgBox = new Msg();
                        EMsgRes res = MsgBox.Show("Server Disconnected. Pls check Gemtaro status.", "", TEMessage.EType.Notification, EMsgBtn.smbOK);
                        break;
                    default:
                        break;
                }
            }
        }

        public delegate void OnMapDownload();
        public event OnMapDownload MapDownloadEvent;
        private void OnMapDownloadEvent() { }

        public delegate void OnLogged();
        public event OnLogged LoggedEvent;
        private void OnLoggedvent() { }

        public void Connect(string ipAddress, int port)
        {
            try
            {
                AddLog("Connecting to " + ipAddress + " : " + port.ToString());
                client.Connect(ipAddress, port);
            }
            catch
            {
                AddLog("Connect fail.");
            }
        }
        public void Connect()
        {
            client.Connect(client.IPAddress, client.Port);
        }
        public void Disconnect()
        {
            try
            {
                client.Disconnect();
            }
            catch
            {
                AddLog("Disconnect fail.");
            }
        }

        public void Send(string outMsg)
        {
            if (!EnableSECSGEMConnect2) return;

            string data = outMsg;
            client.SendFrame(data);
        }
        public void SendAlarmSet(string alarmInfo)
        {
            if (EnableAlarm) Send("ALARM,SET," + alarmInfo);
        }
        public void SendAlarmClear(string alarmInfo)
        {
            if (EnableAlarm) Send("ALARM,CLEAR," + alarmInfo);
        }
        public void SendEvent(string eventInfo)
        {
            if (EnableEvent) Send("EVENT," + eventInfo);
        }
        public void SendPPSend(string recipeFileName)
        {
            if (EnableRMS)
            {
                Send("PPSEND," + recipeFileName);
                Event.PPSEND_E2H.Set("FileName", recipeFileName);
            }
        }
        public bool PPSelect(string fileName)
        {
            if (!EnableRMS) return false;

            if (!File.Exists(fileName))
            {
                AddLog("Recipe file not found.");
                return false;
            }

            bool rdy = GDefine.Status == EStatus.Ready || GDefine.Status == EStatus.Stop || GDefine.Status == EStatus.EndStop;

            if (!rdy)
            {
                AddLog("System is not ready to PPSelect.");
                return false;
            }

            if (DispProg.TR_IsBusy())
            {
                AddLog("System busy.");
                return false;
            }

            if (!DispProg.loadXML2(fileName, true))
            {
                AddLog("Load recipe fail.");
                return false;
            }

            Event.PPSELECT.Set("FileName", fileName);
            AddLog("Load recipe success.");

            Msg MsgBox = new Msg();
            EMsgRes res = MsgBox.Show("Recipe changed. Pls Initialize", "", TEMessage.EType.Notification, EMsgBtn.smbOK);
            //Intf.CmdSend("ForceInit", 0);
            Define_Run.InitSystem(true);

            return true;
        }

        public void SendDownload(string stripID)
        {
            if (EnableStripMapE142)
            {
                Event.MAP_REQUEST.Set("StripID", stripID);
                MapState = EMapState.Requested;
                Send("DOWNLOAD," + stripID);
            }
        }
        public void UpdateXMLtoLocal(string xml)
        {
            bool bDownnLoadData = false;
            bool bBinCode = false;

            List<string> rowData = new List<string>();
            XmlReader reader = XmlReader.Create(new StringReader(xml));
            try
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "SubstrateMap")
                    {
                        //string substrateID 
                        stripID = reader.GetAttribute("SubstrateId");
                    }
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "Overlay")
                    {
                        bDownnLoadData = reader.GetAttribute("MapName") == "DownloadBinCodeMap";
                    }
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "BinCodeMap")
                    {
                        if (bDownnLoadData) bBinCode = true;
                    }
                    if (reader.NodeType == XmlNodeType.EndElement && reader.Name == "BinCodeMap")
                    {
                        if (bDownnLoadData) bBinCode = false;
                    }
                    if (bDownnLoadData && bBinCode)
                    {
                        if (reader.NodeType == XmlNodeType.Text)
                        {
                            rowData.Add(reader.Value);
                        }
                    }
                }

                cr = new Point(rowData[0].Length / 4, rowData.Count);

                for (int r = 0; r < rowData.Count; r++)
                {
                    for (int c = 0; c < rowData[r].Length / 4; c++)
                    {
                        dnLoadMap[c, r] = rowData[r].Substring(c * 4, 4);
                        map[c, r] = (dnLoadMap[c, r] == "0100") ? 0 : 210;
                    }
                }

                xmlContent = xml;
                MapDownloadEvent();
                Event.MAP_DOWNLOADED.Set("StripID", stripID);
                MapState = EMapState.Loaded;
                AddLog("Decode XML Success.");
            }
            catch (Exception ex)
            {
                MapState = EMapState.DecodeError;
                AddLog("Update XML Error." + ex.Message.ToString());
            }
        }
        public string UploadXMLString(string fileName = "")
        {
            try
            {
                if (!EnableStripMapE142) return "";

                XDocument doc = XDocument.Parse(xmlContent);
                string ns = doc.Root.GetDefaultNamespace().NamespaceName;

                var xNameKey = XName.Get("SubstrateMap", ns);

                XElement ele = new XElement("BinCodeMap", new XAttribute("BinType", "Integer2"), new XAttribute("NullBin", "0000"));
                for (int r = 0; r < cr.Y; r++)
                {
                    string mapLine = "";
                    for (int c = 0; c < cr.X; c++)
                    {
                        int bin = map[c, r];
                        string binCode = "FFFF";

                        if (bin < 100) binCode = "0A0F";//Unprocessed
                                                        //None = 0, BinNG = 100,
                                                        //MapOK = 1, MapNG = 101,
                                                        //RefOK = 2, RefNG = 102,
                                                        //HeightOK = 3, HeightNG = 103,
                                                        //UnitMarkOK = 4, UnitMarkNG = 104,
                                                        //VVIOK = 6, VVING = 106,
                        if (bin >= 100 && bin < 200) binCode = "0E0F";//Complete = 200
                        if (bin == (int)EMapBin.VVING/*106*/) binCode = "0F0F";//VVING = 106
                        if (bin == 200) binCode = "0C00";//Complete = 200
                        if (bin == 210) binCode = "0C0F";//InMapNG = 210,
                        if (bin == 220) binCode = "0AFF";//Bypass = 220,
                        if (bin == 255) binCode = "0A0F";//PreMapNG = 255

                        mapLine = mapLine + binCode;
                    }
                    ele.Add(new XElement("BinCode", mapLine));
                }

                var b = doc.Descendants(XName.Get("Overlay", ns)).Last();
                b.Parent.Add(new XElement("Overlay", new XAttribute("MapName", "UploadBinCodeMap"), ele));

                foreach (var node in doc.Root.Descendants())
                {
                    // If we have an empty namespace...
                    if (node.Name.NamespaceName == "")
                    {
                        // Remove the xmlns='' attribute. Note the use of
                        // Attributes rather than Attribute, in case the
                        // attribute doesn't exist (which it might not if we'd
                        // created the document "manually" instead of loading
                        // it from a file.)
                        node.Attributes("xmlns").Remove();
                        // Inherit the parent namespace instead
                        node.Name = node.Parent.Name.Namespace + node.Name.LocalName;
                    }
                }

                if (fileName.Length > 0)
                {
                    doc.Save(fileName);
                    MapState = EMapState.Uploaded;
                }
                else
                //if (fileName == null || fileName.Length == 0)
                {
                    Send("UPLOAD," + stripID + "," + doc.ToString());
                    Event.MAP_UPLOADED.Set("StripID", stripID);
                    MapState = EMapState.Uploaded;
                }
                return doc.ToString();
            }
            catch (Exception ex)
            {
                AddLog("Update XML Error." + ex.Message.ToString());
                MapState = EMapState.UploadFail;
                return "";
            }
        }
    }
}
