using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;

using System.Text;
using System.Threading;
using System.Threading.Tasks;

using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;

namespace Server
{
    class Program
    {
        const int port = 8888;
        const string address = "127.0.0.1";
        static TcpListener listener;

        const int intervalTimer = 60000;


        const int zaderjkatimera = 60000;

        public async static void Vizov(object obj)
        {


            Console.WriteLine("op");

        }

        static void Main(string[] args)
        {
            // устанавливаем метод обратного вызова
            TimerCallback tm = new TimerCallback(Vizov);
            // создаем таймер
            Timer timer = new Timer(tm, null, zaderjkatimera, intervalTimer);

            try
            {
                listener = new TcpListener(IPAddress.Parse(address), port);
                listener.Start();
                Console.WriteLine("Ожидание подключений...");

                //слушатель
                while (true)
                {

                    //+ojidanie vvoda command ы console

                    TcpClient client = listener.AcceptTcpClient();
                    Console.WriteLine("Новое подключение");
                    Handler clientObject = new Handler(client, new JsonSerializationProvider(), new LogProvider());


                    Task task = Task.Factory.StartNew(clientObject.Process);


                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                if (listener != null)
                    listener.Stop();
            }
        }
    }

    class Handler
    {

        public TcpClient client;

        ISerializationProvider provider;
        ILogProvider logprov;

        public Handler(TcpClient tcpClient, ISerializationProvider provider, ILogProvider logprov)
        {
            client = tcpClient;
            this.provider = provider;
            this.logprov = logprov;
        }

        /// <summary>
        /// обработка соединения с клиентом(получение сообщения, кодирование, отпревка результата)
        /// </summary>
        public void Process()
        {
            NetworkStream stream = null;
            try
            {
                stream = client.GetStream();
                byte[] data = new byte[64]; // буфер для получаемых данных



                byte[] result = new byte[0];
                int bytes = 0;
                do
                {

                    bytes = stream.Read(data, 0, data.Length);

                    //~~
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

                Log rec = provider.Deserialize(result);


                data = provider.Serialize(new ResponseServer(logprov.SaveLog(rec)));


                stream.Write(data, 0, data.Length);

            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error: " + ex.Message);
            }
            finally
            {
                if (stream != null)
                    stream.Close();

            }
        }


    }

   

        [Serializable, DataContract]
    class Log
    {
        [DataMember]
        public int Id { get; set; }

        [DataMember]
        public string NameClient { get; set; }

        [DataMember]
        public string Date { get; set; }

    }


    [Serializable, DataContract]
    class ResponseServer
    {
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


    class BinarySerializationProvider : ISerializationProvider
    {

        public byte[] Serialize(ResponseServer data)
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


        public Log Deserialize(byte[] data)
        {


            if (data != null && data.Length != 0)
                try
                {
                    Log resp;
                    using (MemoryStream stream = new MemoryStream(data))
                    {
                        resp = (Log)new BinaryFormatter().Deserialize(stream);
                    }
                    return resp;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());

                }


            return null;
        }


    }


    class JsonSerializationProvider:ISerializationProvider
    {
        public byte[] Serialize(ResponseServer data)
        {
            if (data != null)
            {
                try
                {
                    using (MemoryStream stream = new MemoryStream())
                    {
                        new DataContractJsonSerializer(typeof(ResponseServer)).WriteObject(stream, data);
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


        public Log Deserialize(byte[] data)
        {


            if (data != null && data.Length != 0)
                try
                {
                    Log resp;
                    using (MemoryStream stream = new MemoryStream(data))
                    { 
                         resp = (Log)new DataContractJsonSerializer(typeof(Log)).ReadObject(stream);
                    }
                    return resp;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());

                }


            return null;
        }

    }


    interface ISerializationProvider
    {
        /// <summary>
        /// сериализация запроса к серверу
        /// </summary>
        /// <param name="data">запрос к серверу</param>
        /// <returns>массив byte[]</returns>
        byte[] Serialize(ResponseServer data);
        /// <summary>
        ///  десериализация запроса от сервера
        /// </summary>
        /// <param name="data">массив byte[]-ответ сервера</param>
        /// <returns>ответ(от сервера)-type Response </returns>
        Log Deserialize(byte[] data);
    }

    interface ILogProvider
    {
        string SaveLog(Log log);
        string RotationLog();
    }

    class LogProvider : ILogProvider
    {
        public string RotationLog()
        {
            throw new NotImplementedException();
        }

        public string SaveLog(Log log)
        {
            return "OK";
        }
    }
}
