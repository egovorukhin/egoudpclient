using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EgoUdpClient.Protocol
{
    public class Packet
    {
        public Header Header { get; set; }
        public Request Request { get; set; }

        const char start = '^', body = '#', end = '$';

        public Packet() { }

        public Packet(Header header, Request request)
        {
            Header = header;
            Request = request;            
        }

        public Packet(string hostname, string login, string domain, string version)
        {
            Header = new Header()
            {
                Hostname = hostname,
                Login = login,
                Domain = domain,
                Version = version,
                Event = Events.None
            };
        }

        public byte[] Serialize()
        {
            var host = $"{Header.Hostname.Length}:{Header.Hostname}";
            var login = $"{Header.Login.Length}:{Header.Login}";
            var domain = $"{Header.Domain.Length}:{Header.Domain}";
            var version = $"{Header.Version.Length}:{Header.Version}";
            var events = $"{1}:{(int)Header.Event}";
            string req = "";
            if (Request != null)
            {
                var path = $"{Request.Path.Length}:{Request.Path}";
                var id = $"{Request.Id.Length}:{Request.Id}";
                var method = $"{1}:{(int)Request.Method}";                
                var content_type = Request.ContentType != null ? $"{Request.ContentType.Length}:{Request.ContentType}" : "0:";
                var data = Encoding.Default.GetString(Request.Data);
                data = Request.Data != null ? $"{Request.Data.Length}:{data}" : "0:";
                req = $"{body}{path}{id}{method}{content_type}{data}";
            }

            return Encoding.Default.GetBytes($"{start}{host}{login}{domain}{version}{events}{req}{end}");
        }

        public static Packet Deserialize(byte[] data)
        {
            if (data[0] != start || data[data.Length - 1] != end)
            {
                return null;
            }

            Header header = new Header();
            (header.Hostname, data) = findField(data);
            (header.Login, data) = findField(data);
            (header.Domain, data) = findField(data);
            (header.Version, data) = findField(data);
            string events;
            (events, data) = findField(data);
            header.Event = (Events)Convert.ToInt32(events);

            Request request = null;
            if (data[0] == body)
            {
                request = new Request();
                (request.Path, data) = findField(data);
                (request.Id, data) = findField(data);
                string method;
                (method, data) = findField(data);
                request.Method = (Methods)Convert.ToInt32(method);
                (request.ContentType, data) = findField(data);
                string s;
                (s, data) = findField(data);
                request.Data = Encoding.Default.GetBytes(s);
            }

            return new Packet(header, request);
        }

        internal static (string, byte[]) findField(byte[] b)
        {

            using (var stream = new MemoryStream(b))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    var s = reader.ReadToEnd();
                    if (s[0] == start || s[0] == body)
                    {
                        s = s.Substring(1, s.Length - 1);
                    }
                    if (s[s.Length - 1] == end)
                    {
                        s = s.Substring(0, s.Length - 1);
                    }

                    var i = s.IndexOf(':');
                    int n = Convert.ToInt32(s.Substring(0, i));
                    /*if (n == 0)
                    {
                        return ("", Encoding.Default.GetBytes(s));
                    }*/

                    return (s.Substring(i + 1, n), Encoding.Default.GetBytes(s.Substring(i + n + 1)));
                }
            }
        }

        public override string ToString()
        {
            var req = "";
            if (Request != null)
            {
                req = Request.ToString();
            }
            return $"header: {Header}, request: {req}";
        }
    }
}
