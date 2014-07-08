using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Management;
using System.Text.RegularExpressions;
using System.Net;

namespace ccfrontend
{
    public partial class Form1 : Form
    {
        const int MAX_LINES = 150;
        int serverPort = 8000;
        static bool serverRunning = true;
        String filename = "";
        static String args = "";
        
        const String ARGS_FILE = "CONFIG.DAT";
        const int RETRY_FAILED_AFTER = 30; // Time in minutes to wait before retrying the first pool after a failover
        const int FAILOVER_AFTER = 3; // number of failed attempts before failover

        MyHttpServer myHttp;

        String argsFile;
        String applicationDir;
        Process proc;
        int failedAttempts = 0;
        static bool inFailover = false;
        int lastRetryFirstPool = 0;
        static bool cudaClient = true;
        String cudaExe = "";
        String ccExe = "";
        
        static int thisMin = 0;
        static int yaysThisMin = 0;
        static int minutesRunning = 0;
        static int failovers = 0;
        static int yay = 0;
        static int boo = 0;
        static double percentAccepted = 0;
        static List<String> outputList = new List<String>();
        static List<String> ccArgs = new List<String>();
        static List<String> cudaArgs = new List<String>();
        static List<int> acceptedList = new List<int>();
        static bool running = false;
        static List<double> hashRateList = new List<double>();
        static List<int> minList = new List<int>();
        public Form1()
        {
            InitializeComponent();
        }



        private void TryNextPool()
        {
            if (comboBox1.SelectedIndex == -1)
            {
                comboBox1.SelectedIndex = 0;
                args = comboBox1.Text;
                Start();
            }
            else if (comboBox1.SelectedIndex < comboBox1.Items.Count - 1)
            {
                comboBox1.SelectedIndex = comboBox1.SelectedIndex + 1;
                args = comboBox1.Text;
                Start();
            }
            else
            {
                comboBox1.SelectedIndex = 0;
                args = comboBox1.Text;
                Start();
            }

        }
        private void Start()
        {
            try
            {
                proc = new Process();
                proc.StartInfo.FileName = filename;
                proc.StartInfo.Arguments = args;

                // set up output redirection
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.EnableRaisingEvents = true;
                proc.StartInfo.CreateNoWindow = true;
                proc.StartInfo.UseShellExecute = false;
                // see below for output handler
                proc.ErrorDataReceived += proc_DataReceived;
                proc.OutputDataReceived += proc_DataReceived;

                proc.Start();
                proc.BeginErrorReadLine();
                proc.BeginOutputReadLine();
                button1.Enabled = false;
                button2.Enabled = true;
                groupBox1.Enabled = false;
                comboBox1.Enabled = false;
                button1.Enabled = false;
                button3.Enabled = false;
                button4.Enabled = false;
                button5.Enabled = false;
                button6.Enabled = false;
                button7.Enabled = false;
                running = true;
                DateTime time = DateTime.Now;
                thisMin = time.Minute;
                if (inFailover)
                {
                    miningLabel.ForeColor = Color.OrangeRed;
                    miningLabel.Text = "In Failover";
                }
                else
                {
                    miningLabel.ForeColor = Color.DarkGreen;
                    miningLabel.Text = "Mining";
                }

                
                //proc.WaitForExit();
            }
            catch { }
        }

        private void RewriteConfig()
        {
            StreamWriter writer = new StreamWriter(argsFile, false);
            try
            {
                writer.WriteLine("ccexe = " + ccExe);
                writer.WriteLine("cudaexe = " + cudaExe);
                writer.WriteLine("serverport = " + serverPort.ToString());
                foreach (String arg in cudaArgs)
                {
                    writer.WriteLine("cudaarg = " + arg);
                }
                foreach (String arg in ccArgs)
                {
                    writer.WriteLine("ccarg = " + arg);
                }
            }
            catch { }
            finally
            {
                writer.Close();
            }

        }
        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        if (cudaClient)
                        {
                            cudaExe = openFileDialog1.FileName;
                            filename = cudaExe;
                        }
                        else
                        {
                            ccExe = openFileDialog1.FileName;
                            filename = ccExe;
                        }
                        RewriteConfig();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
                }
            }
            catch { };
        }

        private double GetLastHashrate(String data)
        {
            int index = data.IndexOf(" khash/s");
            if (index > -1)
            {
                String bld = "";
                index--;
                char ch = data[index];

                do
                {
                    bld = ch + bld;
                    index--;
                    ch = data[index];

                } while (ch != ' ');
                try
                {
                    double resNum = double.Parse(bld);
                    return resNum;
                }
                catch (Exception) { return -1; }
            }
            return -1;

        }
        void proc_DataReceived(object sender, DataReceivedEventArgs e)
        {
            try
            {
                if (e.Data != null)
                {
                    Invoke(new MethodInvoker(
                          delegate
                          {
                              outputTextBox.AppendText(e.Data + Environment.NewLine);
                              int numOfLines = outputTextBox.Lines.Length - MAX_LINES;
                              if (numOfLines > 0)
                              {
                                  var lines = this.outputTextBox.Lines;
                                  var newLines = lines.Skip(numOfLines);
                                  
                                  this.outputTextBox.Lines = newLines.ToArray();
                                  outputList = this.outputTextBox.Lines.ToList();
                                  outputTextBox.Focus();
                                  outputTextBox.SelectionStart = outputTextBox.Text.Length;
                                  outputTextBox.SelectionLength = 0;
                                  outputTextBox.ScrollToCaret();
                                  outputTextBox.Refresh();

                              }

                              
                          }
                        ));
                    String data = e.Data;
                    
                    if (data.Contains("retry after")) {
                        failedAttempts++;
                    }
                    DateTime time = DateTime.Now;
                    int min = time.Minute;

                    double hr = GetLastHashrate(data);
                    if (hr > -1)
                    {
                        hashRateList.Add(hr);
                        minList.Add(min);

                    }
            
                    if (data.Contains("yay"))
                    {
                        yay++;
                        yaysThisMin++;
                    }
                    
                    if (data.Contains("boo"))
                    {
                        boo++;
                    }
                    int totalTried = yay + boo;
                    if (totalTried > 0) {
                        percentAccepted = ((double)yay / (double)totalTried) * 100;
                    }
                    int ind = -1;
                    foreach (int minute in minList)
                    {
                        int curMin = min;
                        if (minute > curMin)
                        {
                            curMin += 60;
                        }
                        if (curMin - minute > 5)
                        {
                            ind++;
                        }
                        else
                        {
                            break;
                        }

                    }
                    for (int i = 0; i <= ind; i++)
                    {
                        minList.RemoveAt(0);
                        hashRateList.RemoveAt(0);
                    }
                    if (hashRateList.Count > 0)
                    {
                        double avg = hashRateList.Average();
                        Invoke(new MethodInvoker(
                                delegate { hashrateLabel.Text = avg.ToString("0.00") + " kh/s";

                                double acceptAvg = 0;
                                if (acceptedList.Count > 0)
                                {
                                   acceptAvg = acceptedList.Average();
                                }
                                acceptLabel.Text = percentAccepted.ToString("0.00") + "% Accepted (" + acceptAvg.ToString("0.00") + " per min)";
                                           
                                }
                         ));

                    }
                }
            }
            catch { };
    }
        

    
        

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                proc.Kill();
                ClearMiningVariables();
                running = false;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            
            try
            {
                applicationDir = Path.GetDirectoryName(Application.ExecutablePath);
                argsFile = applicationDir + "\\" + ARGS_FILE;
                if (!File.Exists(argsFile))
                {
                    File.Create(argsFile);
                }

                StreamReader reader = new StreamReader(argsFile);
                String line = reader.ReadLine();
                while (line != null)
                {
                    line = line.Trim();
                    if (line == "")
                    {
                        line = reader.ReadLine();
                        continue;
                    }
                    if (line[0] == '#') {
                        line = reader.ReadLine();
                        continue;
                    }
                    if (!line.Contains("="))
                    {
                        line = reader.ReadLine();
                        continue;
                    }
                    int equalIndex = line.IndexOf("=");
                    if (equalIndex == line.Length - 1)
                    {
                        line = reader.ReadLine();
                        continue;
                    }
                    String command = line.Substring(0, equalIndex).Trim();
                    String arg = line.Substring(equalIndex + 1);
                    if (command == "ccexe")
                    {
                        ccExe = arg;
                    }
                    else if (command == "cudaexe")
                    {
                        cudaExe = arg;
                    }
                    else if (command == "ccarg")
                    {
                        ccArgs.Add(arg);
                    }
                    else if (command == "cudaarg") {
                        cudaArgs.Add(arg);
                    }
                    else if (command == "serverport")
                    {
                        serverPort = int.Parse(arg);
                        
                    }
                    line = reader.ReadLine();
                }
                reader.Close();
                filename = cudaExe;
                foreach (String arg in cudaArgs)
                {
                    comboBox1.Items.Add(arg);
                }
                if (comboBox1.Items.Count > 0)
                {
                    comboBox1.SelectedIndex = 0;
                }
                Application.ApplicationExit += new EventHandler(this.OnApplicationExit);
                startServer();
                portTextBox.Text = serverPort.ToString();
            }


                
            catch { }

            
        }

        public void startServer() {
            try
            {
                string localIP = "?";
                IPHostEntry host;
                host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (IPAddress ip in host.AddressList)
                {
                    if (ip.AddressFamily.ToString() == "InterNetwork")
                    {
                        localIP = ip.ToString();
                    }
                }

                HTTPGet req = new HTTPGet();
                req.Request("http://checkip.dyndns.org");
                string[] res = req.ResponseBody.Split(':');
                string[] finalRes = res[1].Split('<');
                string extIP = finalRes[0].Trim();

                serverIPLabel.Text = "HTTP Monitor Server: Running\n\nhttp://" + localIP + ":" + serverPort.ToString() + "\nhttp://" + extIP + ":" + serverPort.ToString();

                String paramString = "http://*:" + serverPort.ToString() + "/";
                myHttp = new MyHttpServer(SendResponse, paramString);
                myHttp.Run();
            }
            catch { }

        }
        public static string SendResponse(HttpListenerRequest request)
        {
            String outputString = String.Format("<HTML><HEAD><TITLE>CCMiner/CudaMiner Frontend</TITLE></HEAD><BODY><p>{0}<p>", DateTime.Now);
            if (!running)
            {
                outputString += "Not Running.";
            }
            else
            {
                String acceptRate = "0.00";
                if (acceptedList.Count > 0)
                {
                    acceptRate = acceptedList.Average().ToString("0.00");
                }
                outputString += "Running ";
                if (cudaClient)
                {
                    outputString += "CudaMiner<p>";
                }
                else
                {
                    outputString += "CCMiner<p>";
                }
                outputString += "Args: " + args + "<p>";
                outputString += "5 Minute Average: " + hashRateList.Average().ToString("0.00") + " KHash/Second <p> " +  acceptRate + " shares per minute (" + percentAccepted.ToString("0.00") + " % Accepted)<p>";
                foreach (String line in outputList)
                {
                    outputString += line + "<br>";
                }
               
                
                outputString += "</BODY></HTML>";

            }
            return outputString;

        }

        private void OnApplicationExit(object sender, EventArgs e)
        {
            if (running)
            {
                try
                {
                    proc.Kill();
                }
                catch { }
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            try
            {
                if (cudaClient)
                {
                    cudaArgs.Add(comboBox1.Text);
                }
                else
                {
                    ccArgs.Add(comboBox1.Text);
                }
                RewriteConfig();
                comboBox1.Items.Add(comboBox1.Text);
            }
            catch { };
        }

        private void filenameTextBox_TextChanged(object sender, EventArgs e)
        {
            
        }

        private void button4_Click(object sender, EventArgs e)
        {

        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            cudaClient = true;
            comboBox1.Items.Clear();
            comboBox1.Text = "";
            foreach (String arg in cudaArgs)
            {
                comboBox1.Items.Add(arg);
            }
            if (comboBox1.Items.Count > 0)
            {
                comboBox1.SelectedIndex = 0;
            }
            filename = cudaExe;
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            cudaClient = false;
            comboBox1.Items.Clear();
            comboBox1.Text = "";
            foreach (String arg in ccArgs)
            {
                comboBox1.Items.Add(arg);
            }
            if (comboBox1.Items.Count > 0)
            {
                comboBox1.SelectedIndex = 0;
            }
            
            filename = ccExe;
                 
        }

        private void button4_Click_1(object sender, EventArgs e)
        {
            if (cudaClient)
            {
                cudaArgs.RemoveAt(comboBox1.SelectedIndex);
            }
            else
            {
                ccArgs.RemoveAt(comboBox1.SelectedIndex);
            }
            comboBox1.Items.RemoveAt(comboBox1.SelectedIndex);
            //comboBox1.Text = "";

            RewriteConfig();
        }

        private void button5_Click(object sender, EventArgs e)
        {
            if (filename != "")
            {
                args = comboBox1.Text;
                Start();
            }
            else
            {
                MessageBox.Show("No executable has been selected.");
            }

        }
        private void ClearMiningVariables()
        {

            hashRateList.Clear();
            minList.Clear();
            acceptedList.Clear();
            button1.Enabled = true;
            button2.Enabled = false;
            groupBox1.Enabled = true;
            comboBox1.Enabled = true;
            button1.Enabled = true;
            button3.Enabled = true;
            button4.Enabled = true;
            button5.Enabled = true;
            button6.Enabled = true;
            button7.Enabled = true;
            
            failedAttempts = 0;
            inFailover = false;
            lastRetryFirstPool = 0;
            running = false;
            minutesRunning = 0;
            yaysThisMin = 0;
            boo = 0;
            yay = 0;
            runtimeLabel.Text = "-";
            hashrateLabel.Text = "-";
            acceptLabel.Text = "-";
            miningLabel.ForeColor = Color.Red;
            miningLabel.Text = "Not Mining";
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (running && proc.HasExited)
            {
                ClearMiningVariables();
            }
            if (running)
            {
                if (failedAttempts >= FAILOVER_AFTER)
                {
                    if (!proc.HasExited)
                    {
                        proc.Kill();
                    }
                    failedAttempts = 0;
                    lastRetryFirstPool = 0;
                    failoversLabel.Text = failovers.ToString();
                    ClearMiningVariables();
                    inFailover = true;
                    failovers++;
                    TryNextPool();
                    return;
                }
     
                DateTime time = DateTime.Now;
                int min = time.Minute;
                if (min != thisMin)
                {
                    if (inFailover)
                    {
                        lastRetryFirstPool++;
                    }
                    thisMin = min;
                    acceptedList.Add(yaysThisMin);
                    if (acceptedList.Count > 5)
                    {
                        acceptedList.RemoveAt(0);
                    }
                    yaysThisMin = 0;
                    minutesRunning++;
                }

                if (inFailover && lastRetryFirstPool >= RETRY_FAILED_AFTER)
                {
                    if (!proc.HasExited)
                    {
                        proc.Kill();

                    }
                    ClearMiningVariables();
                    comboBox1.SelectedIndex = 0;
                    args = comboBox1.Text;
                    Start();

                }
                int hours = (int)(minutesRunning / 60);
                int minutes = minutesRunning % 60;
                runtimeLabel.Text = "Running for: " + hours + " hour(s) and " + minutes + " minute(s)";
                    

            }
        }

        private void outputTextBox_TextChanged(object sender, EventArgs e)
        {

        }

        public static void Swap(List<String> list, int index1, int index2)
        {
            String temp = list[index1];
            list[index1] = list[index2];
            list[index2] = temp;
        }
        private void button7_Click(object sender, EventArgs e)
        {
            if (comboBox1.SelectedIndex > 0)
            {
                int index = comboBox1.SelectedIndex;
                if (cudaClient)
                {
                    Swap(cudaArgs, index, index - 1);
                    comboBox1.Items.Clear();
                    foreach (String arg in cudaArgs)
                    {
                        comboBox1.Items.Add(arg);
                    }
                }
                else
                {
                    Swap(ccArgs, index, index - 1);
                    comboBox1.Items.Clear();
                    foreach (String arg in ccArgs)
                    {
                        comboBox1.Items.Add(arg);
                    }

                }
                comboBox1.SelectedIndex = index - 1;
                RewriteConfig();
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            if (comboBox1.SelectedIndex < comboBox1.Items.Count - 1 && comboBox1.SelectedIndex != -1)
            {
                int index = comboBox1.SelectedIndex;
                if (cudaClient)
                {
                    
                    Swap(cudaArgs, index, index + 1);
                    comboBox1.Items.Clear();
                    foreach (String arg in cudaArgs)
                    {
                        comboBox1.Items.Add(arg);
                    }
                }
                else
                {
                    Swap(ccArgs, index, index + 1);
                    comboBox1.Items.Clear();
                    foreach (String arg in ccArgs)
                    {
                        comboBox1.Items.Add(arg);
                    }

                }
                comboBox1.SelectedIndex = index + 1;
                RewriteConfig();
            }

        }

        private void button8_Click(object sender, EventArgs e)
        {
            myHttp.Stop();
            try
            {
                serverPort = int.Parse(portTextBox.Text);
                RewriteConfig();
                startServer();
            }
            catch { }

        }

        private void button9_Click(object sender, EventArgs e)
        {
            serverRunning = !serverRunning;
            if (!serverRunning)
            {
                try
                {
                    myHttp.Stop();
                    serverIPLabel.Text = "HTTP Monitor Server: Not Running";
                    button9.Text = "Start Server";
                }
                catch { }
            }
            else
            {
                try
                {
                    button9.Text = "Stop Server";
                    startServer();
                }
                catch { }
            }
        }
    }
 
}
