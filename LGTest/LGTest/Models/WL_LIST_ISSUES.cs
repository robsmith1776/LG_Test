//------------------------------------------------------------------------------
// <auto-generated>
//    This code was generated from a template.
//
//    Manual changes to this file may cause unexpected behavior in your application.
//    Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace LGTest.Models
{
    using System;
    using System.Collections.Generic;
    
    public partial class WL_LIST_ISSUES
    {
        public int Issue_ID { get; set; }
        public System.DateTime Issue_Added { get; set; }
        public Nullable<System.DateTime> Issue_Removed { get; set; }
        public int Inc_ID { get; set; }
        public System.DateTime Last_Check { get; set; }
        public string Source { get; set; }
        public string Finding { get; set; }
        public string Comment { get; set; }
        public Nullable<System.DateTime> StoS_Last { get; set; }
        public string Stos_JobID { get; set; }
    }
}
