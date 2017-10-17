using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace WebServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var server = new Server(new Logger("Server").Log);
            var handler = new HttpHandler(new Logger("HttpHandler").Log);
            server.StartListening(8081, handler.HandleHttpConnection);
        }
    }

    class Logger
    {
        private string source;

        public Logger(string source)
        {
            this.source = source;
        }

        public void Log(string message) 
        {
            var timestamp = DateTime.Now;
            Console.WriteLine(message);
            using(var writer = new StreamWriter("logs/" + timestamp.Date + "/" + source + ".log"))
            {
                writer.WriteLine("{0}\n{1}", timestamp, message);
            }
        }
    }

    class Server 
    {
        private Action<string> logMessage;

        public Server(Action<string> logMessage)
        {
            this.logMessage = logMessage;            
        }

        public void StartListening(int port, Action<Socket> handleConnection) {
            logMessage("Web server started.");

            IPAddress ipAddress =  new IPAddress(new byte[] {127, 0, 0, 1});
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, port);

            Socket listener = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream, 
                ProtocolType.Tcp);

            try {
                listener.Bind(localEndPoint);
                listener.Listen(1);

                while (true) {
                    logMessage("Waiting for a connection...");
                    
                    using(var connectionHandle = listener.Accept())
                    {
                        try
                        {
                            handleConnection(connectionHandle);
                        }
                        catch(Exception e)
                        {
                            logMessage(e.ToString());
                        }
                    }
                }
            }
            catch (Exception e) 
            {
                logMessage(e.ToString());
            }
        }
    }

    class HttpHandler
    {
        private Action<string> logMessage;

        public HttpHandler(Action<string> logMessage)
        {
            this.logMessage = logMessage;            
        }

        public void HandleHttpConnection(Socket connectionHandle)
        {
            var requestText = ReadRequestText(connectionHandle);
            LogRequest(requestText);

            var request = ParseRequest(requestText);  
            var response = ProcessRequest(request);
            var responseHeader = BuildResponseHeader(response);

            WriteResponse(connectionHandle, responseHeader, response.Content);
            LogResponse(response);
        }

        private void LogRequest(string requestText) 
        {
            logMessage(string.Format("[Request]\n{0}", requestText == "" ? "Empty" : requestText));
        }
        
        private void LogResponse(HttpResponse response) 
        {
            var ommitPngContent = response.ContentType == HttpContentType.Png;
            var responseHeader = BuildResponseHeader(response);
            logMessage(string.Format("[Response]\n{0}", responseHeader.Length == 0 
                ? "Empty" 
                : responseHeader));
        }

        private HttpRequest ParseRequest(string request)
        {
            var regex = new Regex(@"(GET|POST) ([\S]+) HTTP/1.1");
            var matches = regex.Matches(request);
            if(matches.Count < 1) return null;

            var method = matches[0].Groups[1].Value;
            var uri = matches[0].Groups[2].Value;

            return new HttpRequest 
                {
                    Method = method,
                    Uri = uri
                }; 
        }



        private HttpResponse ProcessRequest(HttpRequest request)
        {
            try 
            {
                var siteRootPath = "Site";
                var uriPath = Path.Combine(siteRootPath, request.Uri.TrimStart('/'));
                if(!File.Exists(uriPath))
                {
                    if(uriPath.EndsWith(".ico"))
                    {
                        uriPath = uriPath.Replace(".ico", ".png");
                        if(!File.Exists(uriPath)) return new HttpResponse { Code =  HttpResponseCodes.NotFound};         
                    }
                    else
                    {
                        uriPath = Path.Combine(uriPath, "index.html");           
                        if(!File.Exists(uriPath)) return new HttpResponse { Code =  HttpResponseCodes.NotFound};            
                    }
                }

                var content = File.ReadAllBytes(uriPath);
                var contentType = HttpContentType.Html;

                switch(Path.GetExtension(uriPath))
                {
                    case ".png":
                        contentType = HttpContentType.Png;
                        break;
                    case ".css":
                        contentType = HttpContentType.Css;
                        break;
                }

                return new HttpResponse
                    {
                        Code = HttpResponseCodes.Ok,
                        Content = content,
                        ContentType = contentType                        
                    };
            }
            catch(Exception ex) 
            {
                logMessage(ex.ToString());
                return new HttpResponse
                    {
                        Code = HttpResponseCodes.InternalServerError     
                    };
            }            
        } 

        private static string ReadRequestText(Socket handle) 
        {
            using(var bufferStream = new MemoryStream())
            using(var requestReader = new StreamReader(bufferStream))
            {
                int bufferSize = 1024;
                do
                {
                    var inputBuffer = new byte[bufferSize];
                    var receivedBytes = handle.Receive(inputBuffer);
                    bufferStream.Write(inputBuffer, 0, receivedBytes);
                } while(handle.Available > 0);
                
                bufferStream.Position = 0;
                return requestReader.ReadToEnd();
            }
        }

        private static void WriteResponse(Socket connectionHandle, string responseHeader, byte[] content)
        {
            using(var buffer = new MemoryStream())
            {
                var headerBytes = Encoding.UTF8.GetBytes(responseHeader);
                buffer.Write(headerBytes, 0, headerBytes.Length);
                buffer.Write(content, 0, content.Length);
                var bytes = buffer.ToArray();
                connectionHandle.Send(bytes);
            }
        }

        private static string BuildResponseHeader(HttpResponse response)
        {
            var contentLength = response.Content?.Length ?? 0;

            var builder = new StringBuilder()
                .AppendFormat("HTTP/1.1 {0}\n", response.Code)
                .AppendFormat("Date: {0:R}\n", DateTime.Now)
                .AppendLine("Server: ToyWebServer")
                .AppendFormat("Last-Modified: {0:R}\n", DateTime.Now.AddSeconds(-1))
                .AppendFormat("Content-Length: {0}\n", contentLength)
                .AppendFormat("Content-Type: {0}\n", response.ContentType ?? HttpContentType.Html)
                .AppendLine("Connection: keep-alive")
                .AppendLine("Accept-Ranges: bytes");
                
            if(contentLength > 0)
            {
                builder.AppendLine();
            }

            return builder.ToString();
        }

            
        class HttpRequest 
        {
            public string Method {get; set;}
            public string Uri {get; set;}
        }

        class HttpResponse
        {
            public string Code {get; set;}
            public byte[] Content {get; set;}
            public string ContentType {get; set;}
        }

        static class HttpResponseCodes
        {
            public static readonly string Ok = "200 OK";
            public static readonly string NotFound = "404 Not found";
            public static readonly string InternalServerError = "500 Internal Server Error";
        }

        static class HttpContentType
        {
            public static readonly string Html = "text/html";
            public static readonly string Css = "text/css";
            public static readonly string Png = "image/png";
        }
    }
}
