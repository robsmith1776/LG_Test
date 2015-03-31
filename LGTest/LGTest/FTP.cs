using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinSCP;
using System.Data;
using System.IO;
using System.Collections;

using FreshAddress.Databases;
using FreshAddress.Logging;

namespace FreshAddress.FTP
{
    public class FTPConnection
    {
        public Credentials creds;
        public Protocol ftpProtocol;
        public SessionOptions connOptions;

        public FTPConnection(string FTPID)
        {
            //look it up
            DB ITS = FreshAddressDatabase.getDB("","");
            DataTable conInfo = ITS.dataQuery("SELECT * FROM FTPs WHERE FTPID = '" + FTPID + "'");
            if (conInfo.Rows.Count > 0)
            {
                creds = new Credentials(conInfo.Rows[0]["address"].ToString(), conInfo.Rows[0]["username"].ToString(), conInfo.Rows[0]["password"].ToString());

                //if has port
                if (conInfo.Rows[0]["port"].ToString().Trim() != "")
                {
                    creds.setPort(conInfo.Rows[0]["port"].ToString());
                }

                //protocol to use
                string proto = conInfo.Rows[0]["method"].ToString().Trim().ToLower();
                //SFTP
                if (proto == "sftp")
                {
                    ftpProtocol = Protocol.Sftp;
                    creds.setSFTPfingerprint(conInfo.Rows[0]["key"].ToString());
                    connOptions = new SessionOptions
                    {
                        Protocol = ftpProtocol,
                        HostName = creds.address,
                        UserName = creds.username,
                        Password = creds.password,
                        SshHostKeyFingerprint = creds.fingerprint
                    };
                }
                else
                {
                    ftpProtocol = Protocol.Ftp;
                    //FTPS
                    if (proto == "ftps")
                    {
                        creds.setFTPSfingerprint(conInfo.Rows[0]["key"].ToString());
                        connOptions = new SessionOptions
                        {
                            Protocol = ftpProtocol,
                            HostName = creds.address,
                            UserName = creds.username,
                            Password = creds.password,
                            FtpSecure = WinSCP.FtpSecure.ExplicitTls,
                            SslHostCertificateFingerprint = creds.FTPSfingerP
                        };
                    }
                    //normal FTP
                    else
                    {
                        connOptions = new SessionOptions
                        {
                            Protocol = ftpProtocol,
                            HostName = creds.address,
                            UserName = creds.username,
                            Password = creds.password
                        };
                    }
                }
            }
            ITS.disconnect();
        }
    }

    public class FAFTP
    {

        public Logger ftpLog;
        public string applicationName;
        public string applicationDir;
        public string WinSCPpath;

        public Credentials creds { private set; get; }
        public SessionOptions ftpOptions;
        private Protocol ftpProtocol;
        private string port;
        public Session session;
        public enum Direction { Upload, Download };

        public FAFTP(FTPConnection useConnection, string appName, string appDir, string winscpDir)
        {
            ftpLog = new Logger(appName, appDir);
            this.applicationName = appName;
            this.applicationDir = appDir;
            this.WinSCPpath = winscpDir;

            creds = useConnection.creds;
            ftpProtocol = useConnection.ftpProtocol;
            ftpOptions = useConnection.connOptions;
            startSession();
        }

        private void setup(Credentials loginWith)
        {
            creds = loginWith;

            ftpOptions = new SessionOptions
            {
                Protocol = ftpProtocol,
                HostName = creds.address,
                UserName = creds.username,
                Password = creds.password
            };

            if (ftpProtocol == Protocol.Sftp || ftpProtocol == Protocol.Scp)
            {
                //same as above, but with SSH
                ftpOptions = new SessionOptions
                {
                    Protocol = ftpProtocol,
                    HostName = creds.address,
                    UserName = creds.username,
                    Password = creds.password,
                    SshHostKeyFingerprint = creds.fingerprint
                };
            }

            startSession();
        }

        public bool startSession()
        {
            //if port defined
            if (creds.port > 0)
            {
                ftpOptions.PortNumber = creds.port;
            }

            try
            {
                session = new Session();

                session.ExecutablePath = this.WinSCPpath + "WinSCP.exe";

                // Connect
                session.Open(ftpOptions);

                Console.WriteLine("Connected to " + connectionInfo());

            }
            catch (Exception e)
            {
                Console.WriteLine("Could not connect to " + connectionInfo());
                Console.WriteLine(e.Message.ToString());
            }

            return session.Opened;
        }

        public bool reconnectFTPS(FtpSecure FTPSmode)
        {
            session.Dispose();

            ftpOptions = new SessionOptions
            {
                Protocol = ftpProtocol,
                HostName = creds.address,
                UserName = creds.username,
                Password = creds.password,
                FtpSecure = FTPSmode,
                SslHostCertificateFingerprint = creds.FTPSfingerP
            };

            return startSession();
        }

        /// <summary>
        /// Transfers files either via upload or download
        /// </summary>
        /// <param name="command">action to take</param>
        /// <param name="remotePath">path on remote server</param>
        /// <param name="localPath">path on local server</param>
        /// <param name="remove">remove files after transfer</param>
        /// <param name="mask">filename or mask</param>
        /// <returns>list of files transferred</returns>
        public ArrayList transferFiles(Direction command, string remotePath, string localPath, bool remove, string mask)
        {
            ArrayList transfers = new ArrayList();
            TransferOptions ops = new TransferOptions
            {
                //FileMask = mask,
                TransferMode = TransferMode.Automatic
            };

            TransferOperationResult tr = null;

            string transferText = "";
            if (command == Direction.Download)
            {
                if (!Directory.Exists(localPath)) { Directory.CreateDirectory(localPath); }

                string remoteMask = "";
                if (remotePath != "")
                {
                    remoteMask = remotePath + "/" + mask;
                }
                else
                {
                    remoteMask = mask;
                }
                transferText = localPath;

                //try to get
                try
                {
                    tr = session.GetFiles(remoteMask, localPath + "\\", false, ops);   //don't use built in remove (see below)
                }
                catch (Exception downloadError)
                {
                    string errorMessage = "Problem with " + command.ToString() + " " + remoteMask + " to " + transferText + " on " + connectionInfo();
                    errorMessage += " ERROR: " + downloadError.Message.ToString();
                    Console.WriteLine(errorMessage);
                    ftpLog.logToFile(errorMessage, "");
                    return transfers;
                }
            }
            else if (command == Direction.Upload)
            {
                string remoteMask = "*";
                if (remotePath != "") { remoteMask = remotePath + "/*"; }   //if passed a directory and not in root

                string localFiles = localPath;
                if (!localPath.EndsWith("\\")) { localFiles += "\\"; }  //if user doesn't end directory name
                localFiles += mask;

                //try to push
                try
                {
                    tr = session.PutFiles(localFiles, remoteMask, false, ops);
                }
                catch (Exception uploadError)
                {
                    string errorMessage = "Problem with " + command.ToString() + " " + remoteMask + " to " + transferText + " on " + connectionInfo();
                    errorMessage += " ERROR: " + uploadError.Message.ToString();
                    Console.WriteLine(errorMessage);
                    ftpLog.logToFile(errorMessage, "");
                    return transfers;
                }
                transferText = creds.address + remotePath;
            }

            if (tr.Transfers.Count > 0)
            {

                foreach (TransferEventArgs trFiles in tr.Transfers)
                {
                    //if path, remove
                    string transferFile = trFiles.FileName;
                    if (transferFile.Contains("/"))
                    {
                        int pos = transferFile.LastIndexOf("/");
                        transferFile = transferFile.Substring(pos + 1);
                    }
                    else if (transferFile.Contains("\\"))
                    {
                        int pos = transferFile.LastIndexOf("\\");
                        transferFile = transferFile.Substring(pos + 1);
                    }
                    //if was success
                    if (tr.IsSuccess)
                    {
                        transfers.Add(transferFile);
                        Console.WriteLine(command.ToString() + "ed " + trFiles.FileName + " to " + transferText);
                    }
                    else
                    {
                        //if failed, remove local file
                        if (command == Direction.Download)
                        {
                            if (File.Exists(localPath + transferFile) && new FileInfo(localPath + transferFile).Length == 0)
                            {
                                File.Delete(localPath + transferFile);
                            }
                        }
                    }
                }

                //confirm files
                if (remove == true && transfers.Count > 0)
                {
                    foreach (string fileTransfered in transfers)
                    {
                        if (command == Direction.Download)
                        {
                            string toRemove = fileTransfered;
                            if (remotePath != "")
                            {
                                toRemove = remotePath + "/" + toRemove;
                            }
                            session.RemoveFiles(toRemove);
                        }
                        else if (command == Direction.Upload)
                        {
                            File.Delete(localPath + "\\" + fileTransfered);
                        }
                    }
                }
            }
            if (tr.Failures.Count > 0)
            {
                foreach (SessionRemoteException ex in tr.Failures)
                {
                    Console.WriteLine(ex.Message.ToString() + ex.InnerException.ToString());
                    ftpLog.logToFile(ex.Message.ToString() + ex.InnerException.ToString(), "");
                }
            }

            return transfers;
        }

        public string connectionInfo()
        {
            string conInfo = "";

            string ftpMode = "FTP";
            if (creds.FTPSfingerP != null && creds.FTPSfingerP.ToString() != "")
            {
                ftpMode = "FTPS";
            }
            else if (creds.fingerprint != null && creds.fingerprint.ToString() != "")
            {
                ftpMode = "SFTP";
            }

            conInfo = ftpMode + ": " + creds.address;

            if (creds.port != 0)
            {
                conInfo += ":" + creds.port;
            }
            conInfo += " (" + creds.username + ") ";

            return conInfo;
        }

        public DataTable dirListingAsDataTable(string directoryToCheck)
        {
            if (directoryToCheck == string.Empty || directoryToCheck.Trim() == "") { directoryToCheck = "."; }

            DataTable table = new DataTable();
            DataColumn name = new DataColumn("name", System.Type.GetType("System.String"));
            DataColumn isDirectory = new DataColumn("isDirectory", System.Type.GetType("System.String"));
            DataColumn length = new DataColumn("length", System.Type.GetType("System.Int64"));
            DataColumn lastwrite = new DataColumn("lastwrite", System.Type.GetType("System.DateTime"));

            table.Columns.Add(name);
            table.Columns.Add(isDirectory);
            table.Columns.Add(length);
            table.Columns.Add(lastwrite);

            /*attempt at patch for issue: in case disconnected, reconnect
            System.InvalidOperationException: Session is not opened
            at WinSCP.Session.CheckOpened()
            at WinSCP.Session.ListDirectory(String path)
            at Processor.FTPer.dirListingAsDataTable(String directoryToCheck) in c:\Users\AJordan\Desktop\Coding\Processor\FTPer.cs:line 286
            at Processor.FTPer.mostRecentDVUpload(FileType uploadType, String jobnumber) in c:\Users\AJordan\Desktop\Coding\Processor\FTPer.cs:line 305
            at Processor.ToDo.queueFiles() in c:\Users\AJordan\Desktop\Coding\Processor\ToDo.cs:line 305 
             */
            if (!session.Opened) { session.Dispose(); this.startSession(); }

            RemoteDirectoryInfo dir = this.session.ListDirectory(directoryToCheck);

            foreach (RemoteFileInfo ftpFile in dir.Files)
            {
                DataRow row = table.NewRow();
                row["name"] = ftpFile.Name;
                row["isDirectory"] = ftpFile.IsDirectory;
                row["length"] = ftpFile.Length;
                row["lastwrite"] = ftpFile.LastWriteTime;
                table.Rows.Add(row);
            }

            return table;
        }

        /*
        public string mostRecentDVUpload(DVftp.FileType uploadType, string jobnumber)
        {
            string file = "";

            DataTable uploads = this.dirListingAsDataTable("/uploads/" + jobnumber);

            string selectLike = "%";

            if (uploadType == DVftp.FileType.Input)
            {
                selectLike = "%FileForProcessing%";
            }
            else if (uploadType == DVftp.FileType.Suppression)
            {
                selectLike = "%SuppressionList%";
            }
            else if (uploadType == DVftp.FileType.Unsub)
            {
                selectLike = "%UnsubscribeList%";
            }
            else if (uploadType == DVftp.FileType.Rejects)
            {
                selectLike = "%UndeliverableList%";
            }

            DataRow[] sortedUploads = uploads.Select("isDirectory = 'False' AND name LIKE '" + selectLike + "'", "lastwrite DESC");

            //since select can't have wildcard in the middle, ie: %UnsubscribeList%-Info.txt, filter here
            foreach (DataRow upload in sortedUploads)
            {
                if (upload["name"].ToString().EndsWith("-Info.txt"))
                {
                    return upload["name"].ToString();
                }
            }

            return file;
        }
        */
    }
}
