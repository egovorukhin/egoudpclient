using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace EgoUdpClient
{
    public class Client : UdpClient
    {
        public string Host { get; set; } = "127.0.0.1";//"10.3.2.31";
        public int Port { get; set; }
        public int Timeout { get; set; } = 5;

        private IPEndPoint RemoteAddress;

        internal bool IsConnected = false;

        public delegate void ConnectedHandler();
        public event ConnectedHandler OnConnected;

        public delegate void DisconnectedHandler();
        public event DisconnectedHandler OnDisconnected;

        public int Interval { get; set; } = 1000;

        //Список сообщений для отправки
        //List<Message> Messages = new List<Message>();
        internal DevLocService.Server.Buffer Buffer = new DevLocService.Server.Buffer();
        //Сообщение
        SendMessage sendMessage = new SendMessage();
        //Флаг для остановки потока приема и отправки
        CancellationTokenSource cancellToken = new CancellationTokenSource();
        //Сериализаторы сообщений
        DataContractJsonSerializer SendJson = new DataContractJsonSerializer(typeof(SendMessage));
        DataContractJsonSerializer ReceiveJson = new DataContractJsonSerializer(typeof(Response));

        public Client(string Hostname, int LocalPort, int RemotePort)
        {
            this.Hostname = Hostname;
            this.LocalPort = LocalPort;
            this.RemotePort = RemotePort;
        }

        public Client(string Hostname, int LocalPort, int RemotePort, int Timeout)
        {
            this.Hostname = Hostname;
            this.LocalPort = LocalPort;
            this.RemotePort = RemotePort;
            this.Timeout = Timeout;
        }

        public void Start()
        {
            //Переменная для удаленного адреса которую будем заполнять
            IPAddress remoteIpAddress;
            try
            {
                //Пытаемся получить адрес из строковой переменной
                if (!IPAddress.TryParse(Hostname, out remoteIpAddress))
                {
                    //Резолвим хост и получаем все ip адреса
                    var ipaddresses = Dns.GetHostAddresses(Hostname);
                    //Что то пошло не так
                    if (ipaddresses.Length == 0)
                    {
                        return;
                    }
                    //Берем первый попавшийся адрес
                    remoteIpAddress = ipaddresses[0];
                }
                //Формируем адресс для удаленной точки подключения
                RemoteAddress = new IPEndPoint(remoteIpAddress, RemotePort);
                //this.Connect(Hostname, Port);
                //Запускаем слушатель
                ReceiveMessageAsync();
                //Запускаем бесконечную отправку пакетов на сервер
                ConnectAsync();
            }
            catch (Exception ex)
            {
                Logger.Error("Server.Client.Start", ex);
                Thread.Sleep(3000);
                Start();
            }
        }

        public async void StartAsync()
        {
            await Task.Run(() => Start());
        }

        public void Stop()
        {
            Send(new Request(Commands.Disconnected, Methods.None));
            cancellToken.Cancel();
            Close();
        }

        //Зацикливаем отправку сообщения на сервер, учитывая интервал
        private void Connect()
        {
            while (!cancellToken.Token.IsCancellationRequested)
            {
                try
                {
                    if (IsWrite)
                    {
                        continue;
                    }

                    //Проверяем и вставляем прицеп
                    if (IsConnected)
                    {
                        sendMessage.Body = Buffer.GetRequestForSend();
                    }
                    else
                    {
                        sendMessage.Body = new Request(Commands.Connected, Methods.None);
                    }

                    using (MemoryStream stream = new MemoryStream())
                    {
                        //Сериализуем структуру в JSON
                        SendJson.WriteObject(stream, sendMessage);
                        //Возвращаем корретку в начало
                        stream.Position = 0;
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            //Конвертируем в байты
                            byte[] data = Encoding.UTF8.GetBytes(reader.ReadToEnd());
                            //Отправляем данные в порт
                            int n = Send(data, data.Length, RemoteAddress);
                            //WaitResponseAsync(sendMessage.Body.Id);
                            sendMessage.Body = null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Server.Client.Connect", ex);
                    continue;
                }
                finally
                {
                    //Задержка отправки сообщения
                    Thread.Sleep(Interval);
                }
            }
        }

        //Асинхрон
        private async void ConnectAsync()
        {
            await Task.Run(() => Connect());
        }

        private Response WaitResponse(string id)
        {
            int count = 0;
            Response response = null;
            while (count < Timeout && IsConnected)
            {
                if (Buffer.CheckResponse(id))
                {
                    Buffer.SetRequestIsReceived(id, true);
                    response = Buffer.GetResponse(id);
                    break;
                }
                count++;
                Thread.Sleep(1000);
            }
            Buffer.Delete(id);
            return response;
        }

        public Response Send(Request request)
        {
            try
            {
                if (!IsConnected)
                {
                    return null;
                }
                IsWrite = true;
                var id = Buffer.SetRequest(request);
                IsWrite = false;
                return WaitResponse(id);
                //return WaitResponseAsync(id).Result;
            }
            catch (Exception ex)
            {
                Logger.Error("Server.Client.Send", ex);
                return null;
            }
        }

        public Response Send(Commands command, Methods method, Data data)
        {
            return Send(new Request(command, method, data));
        }

        private void ReceiveMessage()
        {
            try
            {
                IPEndPoint localAddress = new IPEndPoint(IPAddress.Any, LocalPort);
                if (!Client.IsBound)
                {
                    //Connect(localAddress);
                    Client.Bind(localAddress);
                }
                //Бесконечный цикл для приема данных
                while (!cancellToken.Token.IsCancellationRequested)
                {
                    try
                    {
                        //Возвращаем набор байт
                        byte[] data = this.Receive(ref localAddress);
                        //Logger.Info("Server.Client.ReceiveMessage", Encoding.UTF8.GetString(data));
                        //Создаём поток для преобразования байт
                        using (MemoryStream stream = new MemoryStream(data))
                        {
                            try
                            {
                                //Пытаемся поток конвертиорвать в структуру
                                //var message = (Message)ReceiveJson.ReadObject(stream);
                                var response = (Response)ReceiveJson.ReadObject(stream);
                                if (response == null)
                                {
                                    continue;
                                }
                                //Парсим полученные данные
                                DoAsync(response);
                            }
                            catch (Exception ex)
                            {
                                Logger.Error("Server.Client.ReceiveMessage", ex);
                                Thread.Sleep(1000);
                            }
                        }
                    }
                    catch (SocketException ex)
                    {
                        if (ex.NativeErrorCode == 10004)
                        {
                            return;
                        }
                        if (IsConnected)
                        {
                            //Buffer.SetRequestsIsSended(false);
                            OnDisconnected?.Invoke();
                            Logger.Error("Server.Client.ReceiveMessage", ex);
                            IsConnected = false;
                        }
                        Thread.Sleep(1000);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Server.Client.ReceiveMessage", ex);
                Thread.Sleep(3000);
                ReceiveMessageAsync();
            }
        }

        private async void ReceiveMessageAsync()
        {
            await Task.Run(() => ReceiveMessage());
        }

        public void Do(Response response)
        {
            try
            {
                Buffer.SetResponse(response);
                if (response != null)
                {
                    Logger.Info("Server.Client.Do", response.ToString());
                }

                //Раскидываем по действиям
                switch (response.Command)
                {
                    case Commands.Connected:
                        IsConnected = true;
                        OnConnected?.Invoke();
                        break;
                    case Commands.Disconnected:
                        IsConnected = false;
                        OnDisconnected?.Invoke();
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Server.Client.Do", ex);
            }
        }

        public async void DoAsync(Response response)
        {
            await Task.Run(() => Do(response));
        }

        public void SetUsername(string Username, string DomainName)
        {
            sendMessage.Header.Login = Username;
            sendMessage.Header.Domain = DomainName;
        }
    }
}
