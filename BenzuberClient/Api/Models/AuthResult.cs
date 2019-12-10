using System;
using System.Collections.Generic;
using System.Text;

namespace Benzuber.Api.Models
{
    public enum Results
    {
        OK, Error, Incorrect_HWID, Incorrect_Session, Incorrect_Sign, NeedLoad_Sign
    }

    public class AuthResult
    {
        public string SessionID { get; set; }
        public Results Result { get; set; }
        public string[] Servers { get; set; }
        public string[] FS_Servers { get; set; }
        public string[] UDP_Servers { get; set; }
        public string[] MB_Servers { get; set; }
        public string[] LogServers { get; set; }
        public int? SiTimeout { get; set; }
    }
}
