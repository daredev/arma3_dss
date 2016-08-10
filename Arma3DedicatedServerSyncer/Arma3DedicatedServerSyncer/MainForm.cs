using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using DLog.NET;
using Microsoft.Win32;
using WinSCP;
using System.Resources;
using System.Reflection;

namespace Arma3DedicatedServerSyncer
{
    public partial class MainForm : Form
    {
        private DLogger logger;
        private Session scpSession;
        private SessionOptions scpSessionOptions;
        private Settings mySettings;

        private string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "A3DSM.xml");

        public MainForm()
        {


            InitializeComponent();
            InitComponents();
            GetInfo();
            Strings();
        }

        private void Strings()
        {
            this.Text = string.Format("{0} v.{1}",Properties.Resources.s_MainFormText, Assembly.GetExecutingAssembly().GetName().Version);
        }

        private void InitComponents()
        {
            if (logger == null)
                logger = new DLogger();

            logger.AddTargetTextBox(tbLog);
            logger.AddProgressBar(toolStripProgressBar1.ProgressBar);


            if (scpSession == null)
                scpSession = new Session();
            if (backgroundWorker1 == null)
                backgroundWorker1 = new BackgroundWorker();

            if (mySettings == null)
                mySettings = new Settings();

            LoadFromXML(ref mySettings, new FileInfo(path));
        }

        private void GetInfo()
        {
            logger.Write("Application started");
            logger.Write("Searching for Arma 3 install path in registry...");
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey("Software\\Wow6432Node\\bohemia interactive\\arma 3"))
                {
                    if (key != null)
                    {
                        Object o = key.GetValue("main");
                        if (o != null)
                        {
                            logger.Write(string.Format("Found installation at {0}", o as string));
                            tbLocalArmaPath.InvokeIfRequired(delegate
                            {
                                tbLocalArmaPath.Text = (o as String);
                            });
                        }
                    }
                }
            }
            catch (Exception ex)  //just for demonstration...it's always best to handle specific exceptions
            {
                //react appropriately
                logger.Write(string.Format("ERROR: {0}", ex.Message));
            }
        }

        private void toolStripStatusLabel1_Click(object sender, EventArgs e)
        {
            splitContainerMain.Panel2Collapsed = !splitContainerMain.Panel2Collapsed;
        }

        private void btnTestConnection_Click(object sender, EventArgs e)
        {
            backgroundWorker1 = new BackgroundWorker();
            backgroundWorker1.WorkerSupportsCancellation = true;
            backgroundWorker1.DoWork += delegate
            {
                TestConnection();
                btnCancel.Enabled = false;
            };
            backgroundWorker1.RunWorkerCompleted += delegate(object o, RunWorkerCompletedEventArgs args)
            {
                CloseSession();
                logger.Write("Finished testing", 100);
            };
            btnCancel.Enabled = true;
            backgroundWorker1.RunWorkerAsync();
        }

        private void CloseSession()
        {
            if (scpSession.Opened)
                scpSession.Close();
        }

        private void PrepareConnection()
        {
            if (scpSession.Opened)
                scpSession.Close();
            scpSessionOptions = new SessionOptions
            {
                Protocol = Protocol.Scp,
                HostName = tbServerHostname.Text,
                UserName = tbSSHLogin.Text,
                PortNumber = int.Parse(tbServerSSHPort.Text),
                Password = tbSSHPassword.Text,
                SshHostKeyFingerprint = tbSSHKeyFingerprint.Text
            };
            scpSession.FileTransferProgress += scpSession_FileTransferProgress;
        }

        private void TestConnection()
        {
            if (ValidateServerData())
            {
                logger.Write("Testing connection...", 5);
                PrepareConnection();

                try
                {
                    logger.Write("Connecting...");
                    scpSession.Open(scpSessionOptions);
                    logger.Write("Connected!");
                    RemoteDirectoryInfo rdi = scpSession.ListDirectory(".");
                    foreach (RemoteFileInfo remoteFileInfo in rdi.Files)
                    {
                        if (remoteFileInfo.Name == "arma3server")
                        {
                            var result = scpSession.ExecuteCommand("pwd");
                            tbServerArmaPath.InvokeIfRequired(delegate
                            {
                                tbServerArmaPath.Text = result.Output;
                            });
                        }
                        logger.Write(string.Format("File: {0}", remoteFileInfo.Name));
                    }
                }
                catch (Exception ex)
                {
                    logger.Write(string.Format("ERROR: {0}", ex.Message));
                }
            }
        }

        private bool ValidateServerData()
        {
            bool result = true;

            if (string.IsNullOrEmpty(tbServerHostname.Text))
            {
                logger.Write("Server hostname missing...");
                result = false;
            }
            if (string.IsNullOrEmpty(tbServerSSHPort.Text))
            {
                logger.Write("Server SSH port missing...");
                result = false;
            }
            if (string.IsNullOrEmpty(tbSSHLogin.Text))
            {
                logger.Write("Server SSH Login missing...");
                result = false;
            }
            if (string.IsNullOrEmpty(tbSSHPassword.Text))
            {
                logger.Write("Server SSH Password missing...");
                result = false;
            }

            if (string.IsNullOrEmpty(tbSSHKeyFingerprint.Text))
            {
                logger.Write("Server SSH Key Fingerprint missing... Visit: https://winscp.net/eng/docs/ui_fsinfo to get instruction on getting the key");
                result = false;
            }

            return result;
        }

        private bool ValidateLocalData()
        {
            bool result = true;

            if (string.IsNullOrEmpty(tbLocalArmaPath.Text))
            {
                logger.Write("Local Arma 3 installation path is missing...");
                result = false;
            }
            if (clbLocalFolders.CheckedItems.Count == 0)
            {
                logger.Write("No local directories checked for Sync...");
                result = false;
            }

            return result;
        }

        private void tbLocalArmaPath_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (string.IsNullOrEmpty(tbLocalArmaPath.Text))
                    MessageBox.Show(@"You have to provide path to refresh local folders", @"Error",
                        MessageBoxButtons.RetryCancel, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
                else
                {
                    try
                    {
                        DirectoryInfo di = new DirectoryInfo(tbLocalArmaPath.Text);
                        var subfolders = di.GetDirectories();
                        foreach (DirectoryInfo directoryInfo in subfolders)
                        {
                            clbLocalFolders.Items.Add(directoryInfo.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Write(string.Format("ERROR: {0}", ex.Message));
                    }
                }
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            backgroundWorker1.CancelAsync();
            btnCancel.Enabled = false;
        }

        private void btnSync_Click(object sender, EventArgs e)
        {
            if (!backgroundWorker1.CancellationPending)
            {
                try
                {
                    backgroundWorker1.CancelAsync();
                    backgroundWorker1 = new BackgroundWorker { WorkerSupportsCancellation = true };
                    backgroundWorker1.DoWork += backgroundWorker1_DoWork;
                    backgroundWorker1.RunWorkerCompleted += backgroundWorker1_RunWorkerCompleted;
                    backgroundWorker1.RunWorkerAsync();
                }
                catch (Exception ex)
                {
                    logger.Write(string.Format("ERROR: {0}", ex.Message));
                }
            }
        }

        void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            CloseSession();
            logger.Write("Finished process...", 100);
            btnCancel.Enabled = false;
        }

        void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            logger.Write("Validating data...");
            if (ValidateLocalData() && ValidateServerData())
            {
                try
                {
                    if (!string.IsNullOrEmpty(tbServerArmaPath.Text))
                    {
                        btnCancel.InvokeIfRequired(delegate
                        {
                            btnCancel.Enabled = false;
                        });
                        PrepareConnection();
                        if (!scpSession.Opened)
                            scpSession.Open(scpSessionOptions);

                        foreach (string checkedItem in clbLocalFolders.CheckedItems)
                        {

                            string localPath = Path.Combine(mySettings.Arma3LocalPath, checkedItem);

                            IEnumerable<FileSystemInfo> fileInfos =
                                new DirectoryInfo(localPath).EnumerateFileSystemInfos("*", SearchOption.AllDirectories);

                            foreach (FileSystemInfo fileInfo in fileInfos)
                            {
                                string remoteFilePath = scpSession.TranslateLocalPathToRemote(fileInfo.FullName, localPath, mySettings.RemoteBaseDirectory, checkedItem);

                                if (fileInfo.Attributes.HasFlag(FileAttributes.Directory))
                                {
                                    if (cbLowercase.Checked)
                                        remoteFilePath = remoteFilePath.ToLower();
                                    // Create remote subdirectory, if it does not exist yet
                                    if (!scpSession.FileExists(remoteFilePath))
                                    {
                                        scpSession.CreateDirectory(remoteFilePath);
                                        logger.Write(string.Format("Directory to create: {0}",remoteFilePath));
                                    }
                                }
                                else
                                {
                                    Console.WriteLine(string.Format("Moving file {0}...", fileInfo.FullName));
                                    // Upload file and remove original
                                    if (cbLowercase.Checked)
                                        remoteFilePath = remoteFilePath.ToLower();

                                    logger.Write(string.Format("Local file: {0} upload to: {1}", fileInfo.FullName, remoteFilePath));
                                    scpSession.PutFiles(fileInfo.FullName, remoteFilePath, false, new TransferOptions{TransferMode = TransferMode.Binary}).Check();

                                }
                            }

                        }
                    }
                    else
                    {
                        logger.Write("Server Remote Path missing...");
                    }
                }
                catch (Exception ex)
                {
                    tbLog.InvokeIfRequired(delegate
                    {
                        logger.Write(string.Format("ERROR: {0}",ex.Message));
                    });
                }
            }
        }

        void scpSession_FileTransferProgress(object sender, FileTransferProgressEventArgs e)
        {
            logger.Write(string.Format("\r{0} ({1:P0})", e.FileName, e.FileProgress));
        }

        private void btnSaveSettings_Click(object sender, EventArgs e)
        {


            if (File.Exists(path))
                File.Copy(path, path + "_backup", true);
            SaveToXML(mySettings, new FileInfo(path));
        }

        private void SaveToXML(Settings settings, FileInfo path)
        {
            XmlSerializer xsSubmit = new XmlSerializer(typeof(Settings));

            using (XmlWriter writer = XmlWriter.Create(path.FullName, new XmlWriterSettings { Indent = true })) //XmlWriter.Create(sww))
            {
                xsSubmit.Serialize(writer, settings);
                logger.Write(string.Format("Settings saved to file: {0}", path.FullName));
            }
        }

        private void LoadFromXML(ref Settings settings, FileInfo path)
        {
            if (path.Exists)
            {
                XmlSerializer xsRead = new XmlSerializer(typeof(Settings));

                using (StreamReader strr = new StreamReader(path.FullName))
                {
                    settings = (xsRead.Deserialize(strr) as Settings);
                }

                if (settings != null)
                {

                    if (!string.IsNullOrEmpty(settings.Arma3LocalPath))
                        tbLocalArmaPath.Text = settings.Arma3LocalPath;

                    if (!string.IsNullOrEmpty(settings.RemoteHost))
                        tbServerHostname.Text = settings.RemoteHost;

                    if (!string.IsNullOrEmpty(settings.RemotePort))
                        tbServerSSHPort.Text = settings.RemotePort;

                    if (!string.IsNullOrEmpty(settings.RemoteLogin))
                        tbSSHLogin.Text = settings.RemoteLogin;

                    if (!string.IsNullOrEmpty(settings.RemotePassword))
                        tbSSHPassword.Text = settings.RemotePassword;

                    if (!string.IsNullOrEmpty(settings.RemoteKeyFingerprint))
                        tbSSHKeyFingerprint.Text = settings.RemoteKeyFingerprint;

                    if (!string.IsNullOrEmpty(settings.RemoteBaseDirectory))
                        tbServerArmaPath.Text = settings.RemoteBaseDirectory;

                    logger.Write(string.Format("Settings loaded from file: {0}", path));
                }
            }
            else
                logger.Write("Settings file doesn't exist... Save settings first...");
        }

        private void btnLoad_Click(object sender, EventArgs e)
        {
            LoadFromXML(ref mySettings, new FileInfo(path));
        }

        private void tbLocalArmaPath_TextChanged_1(object sender, EventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null) mySettings.Arma3LocalPath = textBox.Text;
        }

        private void tbServerHostname_TextChanged(object sender, EventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null) mySettings.RemoteHost = textBox.Text;
        }

        private void tbServerSSHPort_TextChanged(object sender, EventArgs e)
        {
            var textBox = sender as MaskedTextBox;
            if (textBox != null) mySettings.RemotePort = textBox.Text;
        }

        private void tbSSHLogin_TextChanged(object sender, EventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null) mySettings.RemoteLogin = textBox.Text;
        }

        private void tbSSHPassword_TextChanged(object sender, EventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null) mySettings.RemotePassword = textBox.Text;
        }

        private void tbServerArmaPath_TextChanged(object sender, EventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null) mySettings.RemoteBaseDirectory = textBox.Text;
        }

        private void tbSSHKeyFingerprint_TextChanged(object sender, EventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null) mySettings.RemoteKeyFingerprint = textBox.Text;
        }
    }
}
