using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EgoUdpClient.Protocol
{
    public class Response
    {
        public string Id { get; set; }
        public StatusCode StatusCode { get; set; }
        public Events Event { get; set; }
        public string ContentType { get; set; }
        public byte[] Data { get; set; }

        const char start = '^', end = '$';

        public Response() { }
        public Response(Request req, Events Event)
        {
            this.Event = Event;
            if (req != null) 
            {
                Id = req.Id;
                ContentType = req.ContentType;
            }
        }
        public Response(string Id, StatusCode StatusCode, Events Event, string ContentType, byte[] Data)
        {
            this.Id = Id;
            this.StatusCode = StatusCode;
            this.Event = Event;
            this.ContentType = ContentType;
            this.Data = Data;
        }

        public void SetData(StatusCode code, byte[] data)
        {
            StatusCode = code;
            Data = data;
        }

        public void OK(byte[] data)
        {
            SetData(StatusCode.OK, data);
        }

        public void Error(byte[] data)
        {
            SetData(StatusCode.Error, data);
        }

        public void SetContentType(string s)
        {
            ContentType = s;
        }

        public byte[] Serialize()
        {
            var id = $"{Id.Length}:{Id}";
            var code = $"{1}:{(int)StatusCode}";
            var events = $"{1}:{(int)Event}";
            var content_type = $"{ContentType.Length}:{ContentType}";
            var data = Data != null ? $"{Data.Length}:{Data}" : "";
            return Encoding.Default.GetBytes($"{start}{id}{code}{events}{content_type}{data}{end}");            
        }

        public static Response Deserialize(byte[] data)
        {
            if ( data[0] != start || data[data.Length - 1] != end)
            {
                return null;
            }

            Response response = new Response();
            (response.Id, data) = Packet.findField(data);
            string code;
            (code, data) = Packet.findField(data);
            response.StatusCode = (StatusCode)Convert.ToInt32(code);
            string events;
            (events, data) = Packet.findField(data);
            response.Event = (Events)Convert.ToInt32(events);
            (response.ContentType, data) = Packet.findField(data);
            string s;
            (s, data) = Packet.findField(data);
            response.Data = Encoding.Default.GetBytes(s);

            return response;
        }

        public override string ToString()
        {
            var data = "null";
            if (Data != null)
            {
                data = Data.ToString();
            }
            return $"id: {Id}, status_code: {StatusCode}, event: {Event}, content_type: {ContentType}, data: {data}";
        }
    }
}
