using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;

namespace Server
{
    class Program
    {
        static TcpListener listener;
  static object Locker = new object();

        /// <summary>
        /// порт сервера
        /// </summary>
        const int port = 8888;
        /// <summary>
        /// адрес сервера
        /// </summary>
        const string address = "127.0.0.1";


        /// <summary>
        /// интервал срабатывания таймера
        /// </summary>
        const int intervalTimer = 60000;

        /// <summary>
        /// задержка перед стартом отсчета
        /// </summary>
        const int waitTimer = 60000;

        /// <summary>
        /// путь и имя файла-лога
        /// </summary>
      static  string pathLogFile = "logfile.json";




        /// <summary>
        /// обработчик таймера
        /// </summary>
        /// <param name="obj"></param>
        public static void TimerSender(object obj)
        {

            LogProvider logprov = new LogProvider(Locker, pathLogFile);

            string result = logprov.RotationLog();
            Console.WriteLine("Ротация Лог-файла: {0} ", result);
        }

        static void Main(string[] args)
        {
            // устанавливаем метод обратного вызова
            TimerCallback tm = new TimerCallback(TimerSender);
            // создаем таймер
            Timer timer = new Timer(tm, null, waitTimer, intervalTimer);

            try
            {
                listener = new TcpListener(IPAddress.Parse(address), port);
                listener.Start();
                Console.WriteLine("Ожидание подключений...");

                //слушатель
                while (true)
                {

                 

                    TcpClient client = listener.AcceptTcpClient();
                    Console.WriteLine("Новое подключение");
                    Handler clientObject = new Handler(client, new JsonSerializationProvider(), new LogProvider(Locker, pathLogFile));


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
        /// обработка соединения с клиентом(получение сообщения-отпрaвка результата)
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


    /// <summary>
    /// Лог отправляемый клиентом
    /// </summary>
    [Serializable, DataContract]
    class Log
    {
        /// <summary>
        /// порядковый номер
        /// </summary>
        [DataMember]
        public int Id { get; set; }
        /// <summary>
        /// имя отправителя
        /// </summary>
        [DataMember]
        public string NameClient { get; set; }
        /// <summary>
        /// время и дата отправления
        /// </summary>
        [DataMember]
        public string Date { get; set; }

    }

    /// <summary>
    /// Ответ сервера
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
        /// сериализация ответа сервера
        /// </summary>
        /// <param name="data">запрос к серверу</param>
        /// <returns>массив byte[]</returns>
        byte[] Serialize(ResponseServer data);


        /// <summary>
        ///  десериализация запроса клиента
        /// </summary>
        /// <param name="data">массив byte[]-ответ сервера</param>
        /// <returns></returns>
        Log Deserialize(byte[] data);
    }

    interface ILogProvider
    {
        /// <summary>
        /// сохранение лога
        /// </summary>
        /// <param name="log">лог от клиента</param>
        /// <returns>результат операции</returns>
        string SaveLog(Log log);
        /// <summary>
        /// рокация лога
        /// </summary>
        /// <returns>результат операции</returns>
        string RotationLog();
    }

    class LogProvider : ILogProvider
    {

 private object Locker;
        /// <summary>
        /// путь к файлу лога
        /// </summary>
        string path;
        public LogProvider(object locker, string path)
        {
            Locker = locker;
            this.path = path;
        }
       
        
        public string RotationLog()
        {
            string result = "error";
            lock (Locker)
            {
                try
                {
                    File.Delete(path);
                    result = "rotation";
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                    result = "error";
                }
              
            }
            return result;
        }

        public string SaveLog(Log log)
        {
            string result = "NoSave";
            lock (Locker)
            {
                try {
                DataContractJsonSerializer jsonFormatter = new DataContractJsonSerializer(typeof(Log));

                using (FileStream fs = new FileStream(path, FileMode.Append, FileAccess.Write))
                {      
                   jsonFormatter.WriteObject(fs, log);
                }

                    result = "Save";
 }
                catch(Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                    result = "NoSave";
                }
            }

            return result;
        }
    }
}
