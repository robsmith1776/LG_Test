using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Mail;
using System.Collections;
using System.Data;
using FreshAddress.Logging;

namespace FreshAddress.Email
{
    public class MailerBase
    {
        private SmtpClient smtp = new SmtpClient("mail.freshaddress.com", 587);
        private string from = "";
        private string friendlyFrom = "";
        private Logger mailerLog;


        public MailerBase(string fromEmail, string displayName, string appName, string appDir)
        {
            mailerLog = new Logger(appName, appDir);

            smtp.Credentials = new System.Net.NetworkCredential("automatedLP@freshaddress.com", "288newton");
            from = "listprocessing@freshaddress.com";
            friendlyFrom = "List Processing";
            if (fromEmail != "")
            {
                from = fromEmail;
            }
            if (displayName != "")
            {
                friendlyFrom = displayName;
            }
        }

        public void sendMessage(string subject, string body, ArrayList TOs, ArrayList CCs, ArrayList BCCs, ArrayList Attachments)
        {
            MailMessage M = new MailMessage();
            M.IsBodyHtml = true;

            MailAddress from = new MailAddress(this.from, this.friendlyFrom);
            M.From = from;

            M.Subject = subject;
            M.Body = body;

            foreach (string t in TOs) { M.To.Add(t); }
            if (CCs != null) { foreach (string c in CCs) { M.CC.Add(c); } }
            if (BCCs != null) { foreach (string b in BCCs) { M.Bcc.Add(b); } }
            if (Attachments != null) { foreach (Attachment a in Attachments) { M.Attachments.Add(a); } }
            try
            {
                smtp.Send(M);
            }
            catch (Exception sendingError)
            {

                mailerLog.logToFile("Error sending email: " + M.ToString(), "");
            }

        }

        public void sendMessage(string subject, string body, string TOs, string CCs, string BCCs, ArrayList Attachments)
        {
            ArrayList T = this.getEmailsFromString(TOs);
            ArrayList C = this.getEmailsFromString(CCs);
            ArrayList B = this.getEmailsFromString(BCCs);
            this.sendMessage(subject, body, T, C, B, Attachments);
        }

        private ArrayList getEmailsFromString(string s)
        {
            ArrayList emails = new ArrayList();

            if (s != null)
            {
                //remove any spaces
                s = s.Trim();
                s = s.Replace(" ", "");

                //change ; to ,
                s = s.Replace(";", ",");

                foreach (string e in s.Split(",".ToCharArray()))
                {
                    if (e.Contains("@"))
                    {
                        emails.Add(e);
                    }
                }
            }
            return emails;
        }
    }
}
