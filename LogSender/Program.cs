using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LogSender
{
    class Program
    {

        const int port = 8888;
        const string address = "127.0.0.1";

        static string nameClient = "None";

        const int intervalTimer = 10000;


        const int zaderjkatimera = 0;




        static void Main(string[] args)
        {
            int numberResponse = 0;


            Console.WriteLine("Vvedite nazvanie clienta");
            nameClient = Console.ReadLine();


            Console.WriteLine("Запуст отправки сообщений на сервер {0} с периодом в {1}", address + ":" + port, intervalTimer.ToString());

            // устанавливаем метод обратного вызова
            TimerCallback tm = new TimerCallback(Vizov);
            // создаем таймер
            Timer timer = new Timer(tm, numberResponse, zaderjkatimera, intervalTimer);

            while (true)
                Console.ReadLine();


        }

        public async static void Vizov(object obj)
        {


            int x = (int)obj;
            Handler hand = new Handler(address, port, new BinarySerializationProvider());
            Tuple<Log, ResponseServer> result = await hand.Connect(new Log(x, nameClient, DateTime.Now));

            Printer p = new Printer();
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
        /// соединение с сервером
        /// </summary>
        /// <param name="request">запрос к серверу</param>
        /// <param name="serializ">класс сериализатор\десериализатор</param>
        /// <returns>DeEncryptionResult</returns>
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




    [Serializable]
    class Log
    {
        public int Id { get; private set; }

        public string NameClient { get; private set; }

        [NonSerialized]
        private DateTime _date;
        public string Date
        {
            get
            {
                return _date.ToString("0:dd/MM/yyyy H:mm:ss");
            }
        }

        public Log(int id, string nameclient, DateTime date)
        {
            Id = id;
            NameClient = nameclient;
            _date = date;
        }

    }

    [Serializable]
    class ResponseServer
    {
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
        /// <returns>ответ(от сервера)-type Response </returns>
        ResponseServer Deserialize(byte[] data);
    }

    class BinarySerializationProvider : ISerializationProvider
    {

        public byte[] Serialize(Log data)
        {
            if (data != null)
            {
                try
                {
                    using (MemoryStream stream = new MemoryStream())
                    {
                        new BinaryFormatter().Serialize(stream, data);
                        return Convert.FromBase64String(Convert.ToBase64String(stream.ToArray()));
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
                        resp = (ResponseServer)new BinaryFormatter().Deserialize(stream);
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



    interface IPrinter
    {
        void Print(Log log, ResponseServer response);
    }

    class Printer : IPrinter
    {
        public void Print(Log log, ResponseServer response)
        {
        if (log != null && response != null)
            Console.WriteLine(log.ToString());
        }
    }
}
