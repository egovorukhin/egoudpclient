using EgoUdpClient.Protocol;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EgoUdpClient
{
    public class Client : UdpClient
    {
        public string Host { get; set; } = "127.0.0.1";//"10.3.2.31";
        public int Port { get; set; }
        public int Timeout { get; set; } = 30;
        public int BufferSize { get; set; } = 256;

        public bool Started { get; set; }
        public bool Connected { get; set; }
        private Packet packet { get; set; }

        struct QItem
        {
            public Request Request { get; set; }
            public Response Response { get; set; }
            public bool Sended { get; set; }
            public bool Received { get; set; }
        }
        private ConcurrentDictionary<string, QItem> queue = new ConcurrentDictionary<string, QItem>();
        private IPEndPoint RemotePoint;
        private static readonly object locker = new object();

        //Флаг для остановки потока приема и отправки
        //private CancellationTokenSource cancellToken;

        public delegate void ConnectedHandler(Client client);
        public event ConnectedHandler OnConnected;

        public delegate void DisconnectedHandler(Client client);
        public event DisconnectedHandler OnDisconnected;

        public Client(string Host, int Port, int Timeout)
        {
            this.Host = Host;
            this.Port = Port;
            this.Timeout = Timeout;
        }

        public void Start(string hostname, string login, string domain, string version)
        {
            packet = new Packet(hostname, login, domain, version);
            packet.Header.Event = Events.Connected;

            Started = true;

            IPAddress remoteIpAddress;
            try
            {
                //Пытаемся получить адрес из строковой переменной
                if (!IPAddress.TryParse(Host, out remoteIpAddress))
                {
                    //Резолвим хост и получаем все ip адреса
                    var ipaddresses = Dns.GetHostAddresses(Host);
                    //Что то пошло не так
                    if (ipaddresses.Length == 0)
                    {
                        return;
                    }
                    //Берем первый попавшийся адрес
                    remoteIpAddress = ipaddresses[0];
                }
                //Формируем адресс для удаленной точки подключения
                RemotePoint = new IPEndPoint(remoteIpAddress, Port);
                //cancellToken = new CancellationTokenSource();
                //Запускаем бесконечную отправку пакетов на сервер
                sendAsync();
                //Запускаем слушатель
                receiveAsync();
            }
            catch
            {
                Started = false;
            }
        }

        public void StartAsync(string hostname, string login, string domain, string version)
        {
            Task.Run(() => Start(hostname, login, domain, version));
        }

        private void send()
        {
            QItem item;
            while (Started)
            {
                try
                {
                    foreach (var key in queue.Keys)
                    {
                        if (queue.TryGetValue(key, out item))
                        {
                            if (!item.Sended)
                            {
                                lock (locker)
                                {
                                    packet.Request = item.Request;
                                }
                                if (queue.TryUpdate(key,
                                    new QItem()
                                    {
                                        Request = item.Request,
                                        Sended = true
                                    }, item))
                                {
                                    break;
                                }
                            }
                        }
                    }

                    //Конвертируем в байты
                    byte[] data = packet.Serialize();
                    //Отправляем данные в порт
                    int n = base.Send(data, data.Length, RemotePoint);

                    packet.Request = null;
                }
                catch
                { 
                    continue;
                }
                finally
                {
                    //Задержка отправки сообщения
                    Thread.Sleep(1000);
                }
            }
        }

        private void sendAsync()
        {
            Task.Run(() => send());
        }

        private void receive()
        {
            //Бесконечный цикл для приема данных
            while (Started)
            {
                //Bind произойдет после отправки первого сообщения, 
                //если запустим прием без Bind, то будет ошибка
                if (!Client.IsBound)
                {
                    continue;
                }

                try
                {
                    var buffer = Receive(ref RemotePoint);
                    Console.WriteLine(Encoding.Default.GetString(buffer));
                    var resp = Response.Deserialize(buffer);
                    if (resp == null)
                    {
                        continue;
                    }

                    switch (resp.Event)
                    {
                        case Events.Connected:
                            Connected = true;
                            OnConnected?.Invoke(this);
                            break;
                        case Events.Disconnected:
                            Connected = false;
                            break;
                    }

                    lock (locker)
                    {
                        if (packet.Header.Event != Events.None)
                        {
                            packet.Header.Event = Events.None;
                        }
                    }

                    if (packet.Request != null)
                    {
                        Task.Run(() =>
                        {
                            QItem item;
                            if (queue.TryGetValue(resp.Id, out item))
                            {
                                if (queue.TryUpdate(resp.Id, new QItem()
                                {
                                    Request = item.Request,
                                    Sended = item.Sended,
                                    Response = resp,
                                    Received = true
                                }, item))
                                {

                                }
                            }
                        });
                    }
                }
                catch (SocketException ex)
                {
                    if (ex.NativeErrorCode == 10004)
                    {
                        return;
                    }
                }
            }
        }

        private void receiveAsync()
        {
            Task.Run(() => receive());
        }

        private Response wait(string id)
        {
            int count = 0;
            Response response = null;

            Task.Run(() =>
            {
                while (count < Timeout)
                {
                    count++;
                    Thread.Sleep(1000);
                }
            });

            var received = false;
            QItem item;
            while (count < Timeout && !received) {
                foreach (var key in queue.Keys)
                {                    
                    if (queue.TryGetValue(key, out item))
                    {
                        if (item.Received)
                        {
                            response = item.Response;
                            received = true;
                            break;
                        }
                    }
                }
            }
            queue.TryRemove(id, out item);
            return response;
        }

        public Response Send(Request request)
        {
            try
            {
                if (!Started || !Connected)
                {
                    return null;
                }

                request.Id = Guid.NewGuid().ToString().Replace("-", "");

                if (!queue.TryAdd(request.Id, new QItem() { Request = request }))
                {
                    return null;
                }

                return wait(request.Id);
            }
            catch
            {
                return null;
            }
        }

        public async Task<Response> SendAsync(Request request)
        {
            return await Task.Run(() => Send(request));
        }

        public void Stop()
        {
            OnDisconnected?.Invoke(this);
            Started = false;
            packet.Header.Event = Events.Disconnected;
            //Shutdown(SocketShutdown.Both);
            Close();
        }
    }
}
