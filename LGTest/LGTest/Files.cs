using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace FreshAddress.Files
{
    public class FileTools
    {
        public static bool IsFileLocked(FileInfo file)
        {
            //if file doesn't exist, it's not locked
            if (!File.Exists(file.FullName))
            {
                return false;
            }

            FileStream stream = null;

            try
            {
                stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (Exception)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }

            //file is not locked
            return false;
        }

        public static int lineCount(string filename)
        {
            int records = 0;

            StreamReader inputCounter = new StreamReader(filename);
            string line;
            while ((line = inputCounter.ReadLine()) != null)
            {
                if (line != "")
                {
                    records++;
                }
            }
            inputCounter.Close();

            return records;
        }

        public static void appendHeader(string filename, string header)
        {
            StreamReader input = new StreamReader(filename);
            StreamWriter output = new StreamWriter(filename + ".JPtmp");
            output.WriteLine(header);
            string line;
            while ((line = input.ReadLine()) != null)
            {
                output.WriteLine(line);
            }
            input.Close();
            output.Close();
            File.Delete(filename);
            File.Move(filename + ".JPtmp", filename.Replace(".JPtmp", ""));
        }

        public static string biggestFileInDir(string dir)
        {
            DirectoryInfo di = new DirectoryInfo(dir);
            string fileToUse = ""; long size = 0;
            FileInfo[] files = di.GetFiles("*.txt");
            foreach (FileInfo fi in files)
            {
                //load biggest file
                if (fi.Length > size)
                {
                    fileToUse = fi.FullName;
                    size = fi.Length;
                }
            }
            return fileToUse;
        }
    }

    public class FieldMap
    {
        public enum FieldTypes { FNAME, MNAME, LNAME, ADD1, ADD2, CITY, STATE, ZIP, EMAIL, REGDATE, SOURCE, IP, OWNER, ID, Unknown }

        public string headerField;
        public FieldTypes type;

        public FieldMap(string fieldName)
        {
            headerField = fieldName;
            type = FieldTypes.Unknown;

            var types = Enum.GetValues(typeof(FieldMap.FieldTypes));
            foreach (FieldMap.FieldTypes ft in types)
            {
                if (fieldName.Trim().ToUpper() == ft.ToString())
                {
                    type = ft;
                }
            }
        }
    }

    //don't believe this is in use, need to check code though
    public class Paths
    {

        public const string JobsFolder = "\\\\freshdata\\data\\";
        public const string JobsSQLbackup = "\\\\saab\\SQL_BACKUP\\";

        public const string Auditor = "\\\\Journey\\Auditorv2\\";
        public const string AuditorClientInbox = "\\\\\\10.8.2.3\\Auditor\\Inbox\\";
        public const string AuditorErrors = "\\\\\\10.8.2.3\\Auditor\\Errors\\";

#if DEBUG
        public const string AutoAppends = "C:\\auto-append\\";
        public const string AutoStoS = "C:\\auto-stos\\";
#else
        public const string AutoAppends = "\\\\10.8.2.3\\auto-append\\";
        public const string AutoStoS = "C:\\auto-stos\\";
#endif
        public const string AutoAppClientInbox = AutoAppends + "inbox\\";
        public const string AutoAppErrors = AutoAppends + "errors\\";


        public const string LocalCF = "http://saab/web/";

        public const string MiscFilesDirectory = "\\\\saab\\files\\";
        public const string CloudFiles = MiscFilesDirectory + "cloud\\";
        public const string ListrakFiles = MiscFilesDirectory + "Listrak\\";

    }
}
