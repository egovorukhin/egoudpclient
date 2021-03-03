using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EgoUdpClient.Protocol
{
    internal class Header
    {
        public string Hostname { get; set; }
        public string Login { get; set; }
        public string Domain { get; set; }
        public string Version { get; set; }
        public Events Event { get; set; }

        public bool IsNil()
        {
            if (string.IsNullOrEmpty(Hostname) ||
                string.IsNullOrEmpty(Login) ||
                string.IsNullOrEmpty(Domain) ||
                string.IsNullOrEmpty(Version))
            {
                return true;
            }
            return false;
        }

        public override string ToString()
        {
            return $"hostname: {Hostname}, login: {Login}, domain: {Domain}, version: {Version}, event: {Event}";
        }

    }
}
