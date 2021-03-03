using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EgoUdpClient.Protocol
{
    internal class Request
    {
        public string Path { get; set; }
        public Methods Method { get; set; }
        public string Id { get; set; }
        public string ContentType { get; set; }
        public byte[] Data { get; set; }

        public Request(string Path, Methods Method)
        {
            this.Path = Path;
            this.Method = Method;
        }

        public void SetData(string ContentType, byte[] Data)
        {
            this.ContentType = ContentType;
            this.Data = Data;
        }

        public override string ToString()
        {
            var data = "null";
            if (Data != null)
            {
                data = Data.ToString(); 
            }
            return $"Id: {Id}, path: {Path}, method: {Method}, type: {ContentType}, data: {data}";
        }
    }
}
