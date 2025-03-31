using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace TunnelServer
{
    internal class Program
    {
        private static IPEndPoint listenEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 443);
        private static IPAddress destinationAddress;
        private static int destinationPort;

        public static async Task StartServer()
        {
            Socket listener = new Socket(SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(listenEP);
            listener.Listen(100);
            Console.WriteLine($"[+] Listening on {listenEP}");

            while (true)
            {
                Socket clientSocket = await listener.AcceptAsync();
                _ = Task.Run(() => HandleTunnelAsync(clientSocket));
            }
        }

        private static async Task HandleTunnelAsync(Socket clientSocket)
        {
            Console.WriteLine($"[+] Accepted connection from {clientSocket.RemoteEndPoint}");

            Socket remoteSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await remoteSocket.ConnectAsync(destinationAddress, destinationPort);
                Console.WriteLine($"[+] Connected to {destinationAddress}:{destinationPort}");

                var clientStream = new NetworkStream(clientSocket, ownsSocket: true);
                var remoteStream = new NetworkStream(remoteSocket, ownsSocket: true);

                var upload = CopyStreamAsync(clientStream, remoteStream);
                var download = CopyStreamAsync(remoteStream, clientStream);

                await Task.WhenAny(upload, download);
                Console.WriteLine("[*] One side closed. Tunnel done.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] Tunnel error: {ex.Message}");
                clientSocket.Close();
                remoteSocket.Close();
            }
        }

        private static async Task CopyStreamAsync(Stream source, Stream destination)
        {
            byte[] buffer = new byte[81920];
            try
            {
                while (true)
                {
                    int read = await source.ReadAsync(buffer, 0, buffer.Length);
                    if (read == 0) break;
                    await destination.WriteAsync(buffer, 0, read);
                }
            }
            catch
            {
                // Expected: other side closed
            }
        }

        static async Task Main(string[] args)
        {
            Console.WriteLine("******** Tunnel Proxy ********");
            Console.Write("Destination IP: ");
            destinationAddress = IPAddress.Parse(Console.ReadLine().Replace(" ",""));

            Console.Write("Destination port: ");
            destinationPort = int.Parse(Console.ReadLine());

            await StartServer();
        }
    }
}
