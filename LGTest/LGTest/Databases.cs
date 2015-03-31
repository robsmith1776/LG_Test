using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.IO;

using FreshAddress.Logging;
using FreshAddress.Files;
using FreshAddress.Email;


namespace FreshAddress.Databases
{
    public class FreshAddressDatabase
    {
        public static DB getDB(string appName, string appDir)
        {
            return new DB(new Credentials(Credentials.For.SqlITS), "FreshAddressSQL", appName, appDir);
        }
    }

    public class DB
    {
        public string Server { get; private set; }
        private string Username;
        private string Password;
        public string Database { get; private set; }
        SqlConnection connection = new SqlConnection();
        SqlCommand queryText = new SqlCommand();
        public int queryRows;

        public Logger dbLog;
        public string applicationName;
        public string applicationDir;


        public DB(string appName, string appDir)
        {
            init(new Credentials(Credentials.For.SqlITS), "FreshAddressSQL", appName, appDir);
        }
        public DB(Credentials C, string DB, string appName, string appDir)
        {
            init(C, DB, appName, appDir);
        }
        public void init(Credentials C, string DB, string appName, string appDir)
        {
            dbLog = new Logger(appName, appDir);
            this.applicationName = appName;
            this.applicationDir = appDir;

            this.Server = C.address;
            this.Username = C.username;
            this.Password = C.password;
            this.Database = DB;
            connect();
        }

        private string getConnectionString()
        {
            string ConnectionString = "Data Source=" + Server + ";";

            //if table has username and password
            if (Username.Trim() != "" && Password.Trim() != "")
            {
                ConnectionString += "User ID=" + Username + ";";
                ConnectionString += "Password=" + Password + ";";
            }
            else
            {
                ConnectionString += "Integrated Security=SSPI;";
            }

            ConnectionString += "Initial Catalog=" + Database;
            if (Server == "freshadd700.freshaddress.com")
            {
                ConnectionString += ";Connection Timeout=30";
            }
            return ConnectionString;
        }

        public SqlConnection getConnectionInUse() { return this.connection; }

        public bool connect()
        {

            bool c = false;

            try
            {
                //Console.WriteLine("Connecting with "+this.getConnectionString());

                connection.ConnectionString = this.getConnectionString();
                connection.Open();

                c = true;
                // You can get the server version 
                // SQLConnection.ServerVersion

                //Console.WriteLine("Connected");
            }
            catch (Exception Ex)
            {
                // Try to close the connection
                //if (connection != null)
                //    connection.Dispose();

                // Create a (useful) error message
                string ErrorMessage = "A error occurred while trying to connect to the server.";
                ErrorMessage += Environment.NewLine;
                ErrorMessage += Environment.NewLine;
                ErrorMessage += Ex.Message;

                // Show error message (this = the parent Form object)
                //System.Windows.Forms.MessageBox.Show(ErrorMessage);
                dbLog.logToFile(ErrorMessage, "");

                Console.WriteLine(ErrorMessage);
            }
            return c;
        }

        public bool isConnected()
        {
            if (this.connection.State == ConnectionState.Open)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool query(string Q)
        {
            bool ran = false;
            this.queryText = new SqlCommand(Q, this.connection);
            this.queryText.CommandTimeout = 60 * 60 * 24;   //wait up to a day
            try
            {
                if (!this.isConnected())
                {
                    this.connect();
                }
                //Console.WriteLine(this.Database + " QUERY: " + Q);
                this.queryRows = this.queryText.ExecuteNonQuery();
                ran = true;

            }
            catch (Exception error)
            {
                //System.Windows.Forms.MessageBox.Show(error.ToString());
                if (Q.Contains("BULK INSERT") && error.ToString().Contains("Operating system error code 5(Access is denied.)"))
                {
                    //don't log since bulk insert will try two methods of loading if bulk insert fails
                }
                else
                {
                    dbLog.logToFile(this.Database + " QUERY ERROR:" + Q + "\n" + error.ToString(), "");
                }
            }
            return ran;
        }

        public bool waitForQuery(string Q)
        {
            DateTime start = DateTime.Now;
            int attempts = 1;
            while (!this.query(Q))
            {
                int tryAgainIn = 5 * attempts;
                attempts++;

                string msg = "Will try again in " + tryAgainIn.ToString() + " seconds, problem running query: " + Q;

                Console.WriteLine(msg);
                dbLog.logToFile(msg, "");

                if (Q.Contains(" INTO ") && !Q.Contains("INSERT INTO"))
                {
                    string intoTable = Q.Substring(Q.IndexOf(" INTO "));
                    int b1 = intoTable.IndexOf("[");
                    int b2 = intoTable.IndexOf("]", b1 + 1);
                    if (b1 > 0 && b2 > b1)
                    {
                        intoTable = intoTable.Substring(b1 + 1, b2 - b1 - 1);
                        intoTable = intoTable.Replace("[", "");
                        intoTable = intoTable.Replace("]", "");
                    }
                    if (intoTable.EndsWith("_Ns") || intoTable.EndsWith("_RTC"))
                    {
                        //drop table
                        dbLog.logToFile("INTO query failed, dropping table " + intoTable, "");
                        this.query("DROP TABLE [" + intoTable + "]");
                    }
                }

                Thread.Sleep(1000 * tryAgainIn);
                TimeSpan elapsed = DateTime.Now.Subtract(start);
                if (elapsed.TotalHours > 24 || attempts > 5)
                {
                    MailerBase mb = new MailerBase("", "", applicationName, applicationDir);
                    mb.sendMessage(applicationName + " Query Failure", "This query gave up: " + Q, "ajordan@freshaddress.com", null, null, null);
                    dbLog.logToFile("This query gave up: " + Q, "");
                    return false;
                }
            }
            return true;
        }

        public DataTable dataQuery(string Q)
        {
            string SQLStatement = Q;

            // Create a new DataTable
            DataTable dtResult = new DataTable();

            try
            {
                if (!this.isConnected())
                {
                    this.connect();
                }

                // Create a SqlDataAdapter to get the results as DataTable
                SqlDataAdapter SQLDataAdapter = new SqlDataAdapter(SQLStatement, connection);
                //timeout
                SQLDataAdapter.SelectCommand.CommandTimeout = 60 * 60; //wait up to an hour

                //Console.WriteLine(this.Database + " QUERY: " + Q);

                // Fill the DataTable with the result of the SQL statement
                SQLDataAdapter.Fill(dtResult);

                // We don't need the data adapter any more
                SQLDataAdapter.Dispose();



            }
            catch (Exception error)
            {
                //System.Windows.Forms.MessageBox.Show(error.ToString());
                dbLog.logToFile(this.Database + " QUERY ERROR:" + Q + "\n" + error.ToString(), "");
            }

            return dtResult;
        }

        public bool changeDB(string db)
        {
            bool changed = false;
            try
            {
                //Console.WriteLine("Changing database to " + db);

                this.connection.ChangeDatabase(db);
                if (connection.Database == db)
                {
                    changed = true;
                    this.Database = db;
                }
            }
            catch (Exception Error)
            {
                //System.Windows.Forms.MessageBox.Show(Error.ToString());
                Console.WriteLine(Error.ToString());
            }
            return changed;
        }


        public void disconnect()
        {
            connection.Close();
        }


        public bool importFromFile(string file, bool hasHeader)
        {
            bool imported = false;

            FileInfo fi = new FileInfo(file);
            if (fi.Length == 0 || FileTools.IsFileLocked(fi))
            {
                return false;
            }

            StreamReader input = new StreamReader(file);
            string firstLine = input.ReadLine();
            input.Close();

            string CREATE_TABLE_SQL = "CREATE TABLE [" + fi.Name + "] (";
            string[] fields = firstLine.Split("\t".ToCharArray());
            int fieldCount = 0;
            foreach (string field in fields)
            {
                fieldCount++;
                //workaround for info file bug (has trailing extra field)
                if (fieldCount == fields.Length && file.Contains("-Info.txt") && firstLine.EndsWith("\t"))
                {

                }
                else
                {
                    string fieldName = "field" + fieldCount.ToString();
                    if (hasHeader && field.Trim() != "")
                    {
                        fieldName = field;
                        if (fieldName.Contains("[")) { fieldName = fieldName.Replace("[", ""); }
                        if (fieldName.Contains("]")) { fieldName = fieldName.Replace("]", ""); }
                    }
                    string fieldLen = "255";
                    if (fieldName == "FA INTERNAL REASON" || fieldName == "MX ERROR TYPE") //patch for upgrades to auditor
                    {
                        fieldLen = "2000";
                    }
                    CREATE_TABLE_SQL += "[" + fieldName + "] nvarchar(" + fieldLen + "),";

                    //patch for Listrak Audit logs
                    if (fi.Name.Contains("vmta-") && fieldName == "DSN Diagnostics")
                    {
                        CREATE_TABLE_SQL = CREATE_TABLE_SQL.Replace("[DSN Diagnostics] nvarchar(255),", "[DSN Diagnostics] nvarchar(2000),");
                    }
                }
            }
            //remove trailing ,
            CREATE_TABLE_SQL = CREATE_TABLE_SQL.Substring(0, CREATE_TABLE_SQL.Length - 1);

            CREATE_TABLE_SQL += ")";

            if (query(CREATE_TABLE_SQL))
            {
                string filename = new FileInfo(file).Name;
                string BULK_INSERT_SQL = "BULK INSERT [" + filename + "] FROM '" + file + "' WITH (FIELDTERMINATOR = '\\t'";
                if (hasHeader == true)
                {
                    BULK_INSERT_SQL += ",FIRSTROW=2)";
                }
                else
                {
                    BULK_INSERT_SQL += ")";
                }
                if (query(BULK_INSERT_SQL) && !filename.Contains("-Info.txt") && tableCountFast(filename) > 0)       //confirm records imported
                {
                    imported = true;
                }
                else
                {

                    //clear the table just in case some records did load
                    query("DELETE FROM [" + filename + "]");

                    //workaround for: http://support.microsoft.com/kb/944389
                    bool renamed = false;
                    string fileExt = "";
                    if (filename.Contains("."))
                    {
                        fileExt = filename.Substring(filename.IndexOf("."));
                        filename = filename.Replace(fileExt, "");
                        this.query("EXEC sp_rename '[" + filename + fileExt + "]', '" + filename + "'");
                        renamed = true;
                    }

                    SqlBulkCopy bulkCopy = new SqlBulkCopy(this.connection);
                    bulkCopy.DestinationTableName = "dbo.[" + filename + "]";
                    //if object owner is not dbo, change (ie freshsql)
                    DataTable owner = this.dataQuery("select * from information_schema.tables where table_schema <> 'dbo' and table_name = '" + filename + "'");
                    if (owner.Rows.Count > 0)
                    {
                        this.query("EXECUTE sp_changeobjectowner '" + filename + "', 'dbo'");
                    }
                    bulkCopy.BulkCopyTimeout = 60 * 60; //1hr timeout

                    //import using bulk copy
                    input = new StreamReader(file);
                    if (hasHeader)
                    {
                        input.ReadLine();
                    }

                    DataTable data = this.dataQuery("SELECT TOP 1 * FROM [" + filename + "]");
                    data.Clear();
                    string line;
                    while ((line = input.ReadLine()) != null)
                    {
                        DataRow dr = data.NewRow();
                        string[] values = line.Split("\t".ToCharArray());
                        int v = 0;
                        foreach (string value in values)
                        {
                            if (v <= dr.ItemArray.Length - 1)
                            {
                                dr[v] = value;
                                v++;
                                //if info file, header cannot be empty, fix if so
                                if (filename.Contains("-Info") && value == "")
                                {
                                    dr[v - 1] = "field" + v.ToString();
                                }
                            }
                        }
                        data.Rows.Add(dr);
                        //clear out memory, 500k at a time
                        if (data.Rows.Count == 500000)
                        {
                            bulkCopy.WriteToServer(data);
                            data.Clear();
                        }
                    }
                    //whatever is left load
                    bulkCopy.WriteToServer(data);
                    data.Clear();

                    if (renamed)
                    {
                        this.query("EXEC sp_rename '" + filename + "', '" + filename + fileExt + "'");
                    }
                    input.Close();

                    //confirm imported records
                    if (tableCountFast(filename + fileExt) > 0)
                    {
                        imported = true;
                    }
                }
            }

            return imported;
        }

        public bool exportToFile(string tableOrView, string file, bool includeHeader)
        {
            bool exported = false;
            string delim = "\t";
            if (!this.isConnected())
            {
                this.connect();
            }
            try
            {
                string Q = "SELECT * FROM [" + tableOrView + "]";
                SqlCommand sc = new SqlCommand(Q, connection);
                sc.CommandTimeout = 300; //default is 30 seconds, changed to 5 mins as for big datasets the dr.close() statement forces a complete read and times-out
                SqlDataReader dr = sc.ExecuteReader();
                if (dr.HasRows)
                {
                    //start file
                    StreamWriter output = new StreamWriter(file);
                    if (includeHeader)
                    {
                        dr.Close();
                        DataTable header = this.dataQuery("SELECT column_name from information_schema.columns where table_name = '" + tableOrView + "' order by ordinal_position");
                        dr = sc.ExecuteReader();
                        if (header.Rows.Count > 0)
                        {
                            int fCount = 0;
                            foreach (DataRow field in header.Rows)
                            {
                                output.Write(field["column_name"].ToString());
                                fCount++;
                                if (fCount != header.Rows.Count)
                                {
                                    output.Write(delim);
                                }
                                else
                                {
                                    output.Write(Environment.NewLine);
                                }
                            }
                        }
                    }
                    while (dr.Read())
                    {
                        int i = 0;
                        while (i < dr.FieldCount)
                        {
                            output.Write(dr.GetValue(i));
                            i++;
                            if (i != dr.FieldCount)
                            {
                                output.Write(delim);
                            }
                            else
                            {
                                output.Write(Environment.NewLine);
                            }
                        }
                    }
                    dr.Close();
                    output.Close();
                    exported = true;
                }
                else
                {
                    dr.Close();
                    dbLog.logToFile("No rows to export in " + tableOrView, "");
                    exported = true;
                }
            }
            catch (Exception exportError)
            {
                dbLog.logToFile("Could not export " + tableOrView + Environment.NewLine + exportError.ToString(), "");
            }
            return exported;
        }

        public bool fieldExists(string field, string table)
        {
            DataTable columns = this.dataQuery("SELECT column_name FROM information_schema.columns WHERE table_name = '" + table + "' and column_name = '" + field + "'");
            if (columns.Rows.Count == 1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool tableExists(string table)
        {
            DataTable tables = this.dataQuery("SELECT table_name FROM information_schema.tables WHERE table_name = '" + table + "'");
            if (tables.Rows.Count == 1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public void dropTableOrView(string table, bool isView)
        {
            if (tableExists(table))
            {
                string tmp = "TABLE";
                if (isView) { tmp = "VIEW"; }
                this.query("DROP " + tmp + " [" + table + "]");
            }
        }

        public bool dropTable(string table)
        {
            bool dropped = true;        //if doesn't exist, just return true
            if (tableExists(table))
            {
                if (!query("DROP TABLE [" + table + "]"))
                {
                    dropped = false;    //if exists, but can't drop
                }
            }
            return dropped;
        }

        public bool DBexists(string DB)
        {
            DataTable tables = this.dataQuery("select * from master..sysdatabases WHERE name = '" + DB + "'");
            if (tables.Rows.Count == 1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public void fieldStripQuotesAndTrim(string table, DataTable fields)
        {
            fieldTrim(table, fields);
            fieldStripQuotes(table, fields);
            fieldTrim(table, fields);
        }
        public void fieldStripQuotes(string table, DataTable fields)
        {
            foreach (DataRow fName in fields.Rows)
            {
                string f = fName["COLUMN_NAME"].ToString();
                //query
                this.query("UPDATE t SET t.[" + f + "] = substring(t.[" + f + "],2,len(t.[" + f + "])-2) FROM [" + table + "] as t WHERE t.[" + f + "] like '\"%\"'");
            }
        }
        public void fieldTrim(string table, DataTable fields)
        {
            foreach (DataRow fName in fields.Rows)
            {
                string f = fName["COLUMN_NAME"].ToString();
                //query
                this.query("UPDATE t SET t.[" + f + "] = rtrim(ltrim(t.[" + f + "])) FROM [" + table + "] as t");
            }
        }
        public void fieldReplace(string table, DataTable fields, string replace)
        {
            foreach (DataRow fName in fields.Rows)
            {
                string f = fName["COLUMN_NAME"].ToString();
                //query
                this.query("UPDATE t SET t.[" + f + "] = Replace(t.[" + f + "],'" + replace + "','') FROM [" + table + "] as t");
            }
        }
        public DataTable getAllFields(string table)
        {
            return dataQuery("SELECT column_name FROM information_schema.columns WHERE table_name = '" + table + "'");
        }

        public int tableCountFast(string table)
        {
            DataTable tmp = this.dataQuery("select row_count from (SELECT o.name, ddps.row_count FROM sys.indexes AS i INNER JOIN sys.objects AS o ON i.OBJECT_ID = o.OBJECT_ID INNER JOIN sys.dm_db_partition_stats AS ddps ON i.OBJECT_ID = ddps.OBJECT_ID AND i.index_id = ddps.index_id WHERE i.index_id < 2 AND o.is_ms_shipped = 0) as tmp where name = '" + table + "'");
            int res = 0;
            try
            {
                if (tmp.Rows.Count == 0)
                {
                    //use traditional select count method, maybe a view
                    tmp = this.dataQuery("select count(*) as row_count from [" + table + "]");
                }
                res = Convert.ToInt32(tmp.Rows[0]["row_count"].ToString());
            }
            catch (Exception bla) { }
            return res;
        }
    }

    public class RuntimeControl
    {
        private string appName;
        private string appDir;
        DB ITS;

        public RuntimeControl(string app, string dir)
        {
            this.appName = app;
            this.appDir = dir;
            ITS = FreshAddressDatabase.getDB(app, dir);
        }

        public bool isEnabled()
        {
            DataTable enabled = ITS.dataQuery("SELECT Enabled FROM RunTime_Control WHERE [Name] = '" + this.appName + "'");
            if (enabled.Rows.Count == 1)
            {
                if (enabled.Rows[0]["Enabled"].ToString().ToUpper() == "Y")
                {
                    ITS.disconnect();
                    return true;
                }
            }
            return false;
        }

        public void updateLastRun()
        {
            ITS.waitForQuery("UPDATE rc SET rc.Last_Run_Date = getdate() FROM RunTime_Control as rc WHERE rc.[Name] = '" + this.appName + "'");
        }

        public void updateError(string message)
        {
            ITS.waitForQuery("UPDATE rc SET rc.Error_Message = '" + message + "' FROM RunTime_Control as rc WHERE rc.[Name] = '" + this.appName + "'");
        }

        public void updateNote(string message)
        {
            ITS.waitForQuery("UPDATE rc SET rc.Notes = '" + message + "' FROM RunTime_Control as rc WHERE rc.[Name] = '" + this.appName + "'");
        }

        public void disable()
        {
            ITS.waitForQuery("UPDATE rc SET rc.Enabled = 'N' FROM Runtime_Control as rc where rc.[Name] = '" + this.appName + "'");
        }
        public void enable()
        {
            ITS.waitForQuery("UPDATE rc SET rc.Enabled = 'Y' FROM Runtime_Control as rc where rc.[Name] = '" + this.appName + "'");
        }
    }

    //static class so only connects once on app boot to get login data
    public static class CredData
    {
        private static bool init = false;
        public static DataTable logins;
        static CredData()
        {
            if (logins == null || logins.Rows.Count == 0)
            {
                Console.WriteLine("Connecting to Saab for login data");

                SqlConnection connection = new SqlConnection("Data Source=SAAB\\JOBS;Integrated Security=SSPI;");
                logins = new DataTable();
                connection.Open();
                if (connection.State == ConnectionState.Open)
                {
                    // Create a SqlDataAdapter to get the results as DataTable
                    SqlDataAdapter SQLDataAdapter = new SqlDataAdapter("SELECT *, apps.dbo.faDecrypt(EncryptedPassword) as DecryptedPass FROM Apps.dbo.Credentials", connection);
                    //timeout
                    SQLDataAdapter.SelectCommand.CommandTimeout = 60 * 60; //wait up to an hour

                    //Console.WriteLine(this.Database + " QUERY: " + Q);

                    // Fill the DataTable with the result of the SQL statement
                    SQLDataAdapter.Fill(logins);

                    // We don't need the data adapter any more
                    SQLDataAdapter.Dispose();
                }
            }
        }
    }

    public class Credentials
    {
        public For isFor { get; private set; }
        public string address { get; private set; }
        public string username { get; private set; }
        public string password { get; private set; }
        public string fingerprint { get; private set; } // for SFTP
        public string FTPSfingerP { get; set; } //let user set fingerprint
        public int port { get; private set; }

        public enum For
        {
            FtpDV, FtpLP, FtpBV, FtpBV2, FtpLT,
            SqlLuxor, SqlITS, SqlSaabActive, SqlJobsActive, SqlJobsComplete, SqlAuditor, SqlMatcher, SqlMasterEmails,
            SqlSaabASP, SqlRogueASP, SqlJourneyASP, SqlJourneySQLASP, SqlXzibitASP, SqlWonderland,
            FtpPS, FtpTD, FtpWKsend,
            SFTPAcxiomSend, SFTPAcxiomReceive, SFTPv12
        };

        public Credentials(For f)
        {
            isFor = f;
            fingerprint = ""; //only used in SFTP SSH
            FTPSfingerP = ""; //only used in FTPS
            port = 0;


            //connect
            try
            {
                DataTable data = CredData.logins;

                if (data.Rows.Count > 0)
                {
                    DataRow[] results = data.Select("LookupName = '" + f.ToString() + "'");
                    if (results.Length == 1)
                    {
                        this.address = results[0]["address"].ToString();
                        this.username = results[0]["username"].ToString();
                        this.password = results[0]["password"].ToString();

                        if (results[0]["FTPSfingerprint"].ToString() != "")
                        {
                            this.FTPSfingerP = results[0]["FTPSfingerprint"].ToString();
                        }
                        if (results[0]["fingerprint"].ToString() != "")
                        {
                            this.fingerprint = results[0]["fingerprint"].ToString();
                        }
                        if (results[0]["port"].ToString() != "")
                        {
                            try
                            {
                                this.port = Convert.ToInt32(results[0]["port"].ToString());
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception error)
            {
                Console.WriteLine("Error received from FALib.dll");
                Console.WriteLine(error.Message.ToString());
                Console.WriteLine(error.StackTrace.ToString());
            }




        }

        public Credentials(string ip, string user, string pass)
        {
            this.address = ip;
            this.username = user;
            this.password = pass;
            this.port = 0;
        }

        public void setPort(string port)
        {
            try
            {
                this.port = Convert.ToInt32(port);
            }
            catch { }
        }
        public void setSFTPfingerprint(string key)
        {
            this.fingerprint = key;
        }
        public void setFTPSfingerprint(string key)
        {
            this.FTPSfingerP = key;
        }

        public void setAdmin()
        {
            try
            {
                DataTable data = CredData.logins;

                if (data.Rows.Count > 0)
                {
                    DataRow[] results = data.Select("LookupName = 'LocalSqlAdmin'");
                    if (results.Length == 1)
                    {
                        this.username = results[0]["username"].ToString();
                        this.password = results[0]["DecryptedPass"].ToString();
                    }
                }
            }
            catch { }
        }
    }
}
