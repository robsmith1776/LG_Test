using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FreshAddress.Services
{
    public class Service
    {
        public enum Type { AppI, AppH, AppB2B, ECOA, Audit, AuditHygiene, RevApp, AutoAudit, AutoAuditHygiene, AutomatedHygiene, AutoAppI, AutoAppH, MPlus, ListRental }

        private Type serviceType;
        private string DBname = "";
        private string friendlyName = "";
        private bool isAuditType = false;

        public Service(string s)
        {
            DBname = s;
            switch (s)
            {
                case "Auto Audit":
                    serviceType = Type.AutoAudit;
                    friendlyName = s;
                    isAuditType = true;
                    break;
                case "Auto Audit & Hygiene":
                    serviceType = Type.AutoAuditHygiene;
                    friendlyName = s;
                    isAuditType = true;
                    break;
                case "Automated Hygiene":
                    serviceType = Type.AutomatedHygiene;
                    friendlyName = "SafeToSend Automated";
                    isAuditType = true;
                    break;
                case "Automated Append-I":
                    serviceType = Type.AutoAppI;
                    friendlyName = "Automated Individual Append";
                    break;
                case "Automated Append-H":
                    serviceType = Type.AutoAppH;
                    friendlyName = "Automated Household Append";
                    break;
                case "Append-H":
                    serviceType = Type.AppH;
                    friendlyName = "Household Append";
                    break;
                case "Append-I":
                    serviceType = Type.AppI;
                    friendlyName = "Individual Append";
                    break;
                case "Append-B2B":
                    serviceType = Type.AppB2B;
                    friendlyName = "B2B Append";
                    break;
                case "NCOA for Email":
                    serviceType = Type.ECOA;
                    friendlyName = "ECOA";
                    break;
                case "List Audit":
                    serviceType = Type.Audit;
                    friendlyName = "List Audit";
                    isAuditType = true;
                    break;
                case "List Audit + H":
                    serviceType = Type.AuditHygiene;
                    friendlyName = "SafeToSend";
                    isAuditType = true;
                    break;
                case "Reverse Append":
                    serviceType = Type.RevApp;
                    friendlyName = "Reverse Append";
                    break;
                case "Message+":
                    serviceType = Type.MPlus;
                    friendlyName = "Message+";
                    break;
                case "List Rental":
                    serviceType = Type.ListRental;
                    friendlyName = s;
                    break;
                default:
                    friendlyName = s;
                    break;
            }
        }

        public string getDBname() { return DBname; }
        public string getFriendlyName() { return friendlyName; }
        public Type getServiceType() { return serviceType; }
        public bool isAudit() { return isAuditType; }

    }
}
