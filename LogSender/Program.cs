using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LogSender
{
    class Program
    {

        /// <summary>
        /// адрес сервера
        /// </summary>
        const string address = "127.0.0.1";
        /// <summary>
        /// порт сервера
        /// </summary>
        const int port = 8888;

        /// <summary>
        /// название отправителя лога
        /// </summary>
        static string nameClient = "None";

        /// <summary>
        /// интервал срабатывания таймера
        /// </summary>
        const int intervalTimer = 10000;

        /// <summary>
        /// задержка перед стартом отсчета
        /// </summary>
        const int delayTimer = 10000;

        /// <summary>
        /// номер отправленного запроса к серверу
        /// </summary>
 static int numberResponse = 1;

        static void Main(string[] args)
        {
          


            Console.WriteLine("Введите имя клиента");
            nameClient = Console.ReadLine();


            Console.WriteLine("Запуст отправки сообщений на сервер {0} с периодом в {1}", address + ":" + port, intervalTimer.ToString());

            // устанавливаем метод обратного вызова
            TimerCallback tm = new TimerCallback(Send);
            // создаем таймер
            Timer timer = new Timer(tm, numberResponse, delayTimer, intervalTimer);

            
            while (true)
                Console.ReadLine();


        }

        /// <summary>
        /// отправление лога и получение ответа от сервера
        /// </summary>
        /// <param name="obj"></param>
        public async static void Send(object obj)
        {

            Console.WriteLine("Отправка {0}", numberResponse);

            Handler hand = new Handler(address, port, new JsonSerializationProvider());
            Printer p = new Printer();

            Tuple<Log, ResponseServer> result = await hand.Connect(new Log(numberResponse, nameClient, DateTime.Now));
            numberResponse++;


            p.Print(result.Item1, result.Item2);

        }

    }


    class Handler
    {

        TcpClient client;

        string address;
        int port;
        ISerializationProvider serializator;


        public Handler(string address, int port, ISerializationProvider serializator)
        {

            this.serializator = serializator;

            this.address = address;
            this.port = port;


        }







     /// <summary>
     /// TCP соединение с сервером
     /// </summary>
     /// <param name="request">сообщение лог отправляемое серверу</param>
     /// <returns></returns>
        public async Task<Tuple<Log, ResponseServer>> Connect(Log request)
        {

            try
            {
                client = new TcpClient(address, port);
                NetworkStream stream = client.GetStream();



                byte[] data = serializator.Serialize(request);
                // отправка сообщения
                await stream.WriteAsync(data, 0, data.Length);

                // получаем ответ
                data = new byte[1024]; // буфер для получаемых данных

                byte[] result = new byte[0];


                int bytes = 0;
                do
                {

                    bytes = await stream.ReadAsync(data, 0, data.Length);
                    if (bytes < data.Length)
                    {
                        byte[] newdata = new byte[bytes];
                        Array.Copy(data, 0, newdata, 0, bytes);
                        result = result.Concat(newdata).ToArray();
                    }
                    else
                        result = result.Concat(data).ToArray();

                }
                while (stream.DataAvailable);
                return new Tuple<Log, ResponseServer>(request, serializator.Deserialize(result));

            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return new Tuple<Log, ResponseServer>(request, new ResponseServer("Error "));
            }
            finally
            {
                if (client != null)
                    client.Close();
            }
        }



    }




    /// <summary>
    /// Лог отправляемый на сервер
    /// </summary>
    [Serializable, DataContract]
    class Log
    {
        /// <summary>
        /// порядковый номер
        /// </summary>
        [DataMember]
        public int Id { get; private set; }

        /// <summary>
        /// имя отправителя
        /// </summary>
        [DataMember]
        public string NameClient { get; private set; }

        [NonSerialized]
        private DateTime _date;

        /// <summary>
        /// время и дата отправления
        /// </summary>
        [DataMember]
        public string Date
        {
            get
            {
                return _date.ToString("dd/MM/yyyy H:mm:ss");
            }
            set
            {
                Date = value;
            }
        }

        public Log(int id, string nameclient, DateTime date)
        {
            Id = id;
            NameClient = nameclient;
            _date = date;
        }

    }

    /// <summary>
    /// Ответ от сервера
    /// </summary>
    [Serializable, DataContract]
    class ResponseServer
    {
        /// <summary>
        /// результат операции с логом на сервере
        /// </summary>
        [DataMember]
        public string Result { get; set; }

        public ResponseServer()
        {
        }

        public ResponseServer(string result)
        {
            Result = result;
        }
    }



    interface ISerializationProvider
    {
        /// <summary>
        /// сериализация запроса к серверу
        /// </summary>
        /// <param name="data">запрос к серверу</param>
        /// <returns>массив byte[]</returns>
        byte[] Serialize(Log data);
        /// <summary>
        ///  десериализация запроса от сервера
        /// </summary>
        /// <param name="data">массив byte[]-ответ сервера</param>
        /// <returns>ответ(от сервера)-type ResponseServer </returns>
        ResponseServer Deserialize(byte[] data);
    }

   

    class JsonSerializationProvider : ISerializationProvider
    {

        public byte[] Serialize(Log data)
        {
            if (data != null)
            {
                try
                {
                    using (MemoryStream stream = new MemoryStream())
                    {
                        new DataContractJsonSerializer(typeof(Log)).WriteObject(stream, data);
                        return stream.ToArray();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                }
            }

            return new byte[0];
        }


        public ResponseServer Deserialize(byte[] data)
        {

            if (data != null && data.Length != 0)
            {
                try
                {


                    ResponseServer resp;
                    using (MemoryStream stream = new MemoryStream(data))
                    {
                        resp = (ResponseServer)new DataContractJsonSerializer(typeof(ResponseServer)).ReadObject(stream);
                    }


                    return resp;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                }
            }

            return null;
        }

    }



    class Printer 
    {
        /// <summary>
        /// выведение лога и ответа 
        /// </summary>
        /// <param name="log">лог отправленный на сервер</param>
        /// <param name="response">ответ сервера</param>
        public void Print(Log log, ResponseServer response)
        {
            if (log != null)
            {

                Console.Write("{0} запрос №{1} отправлен на сервер и ", log.Date, log.Id.ToString());

                if (response != null)
                {
                    switch (response.Result)
                    {
                       

                        case "NoSave":
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Ответ от сервера: {0} ", "Лог не сохранен на сервере");
                            Console.ResetColor();
                            break;
                        case "Save":
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("Ответ от сервера: {0} ", "Лог сохранен на сервере");
                            Console.ResetColor();
                            break;

                        default:
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("Ответ от сервера: {0} ", response.Result);
                            Console.ResetColor();
                            break;
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Неудалось прочитать ответ от сервера");
                    Console.ResetColor();
                }
            }

        }
    }
}
