using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading.Tasks;
namespace reverse_tunnel
{

    
    internal class Program


    {

        private static IPEndPoint listenEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 443);
        private static IPAddress destinationAddress;
        private static int destinationPort;

        public static async Task StartServer()
        {
            while (true)
            {
                try
                {
                    Socket conn = new Socket(SocketType.Stream, ProtocolType.Tcp);
                    await conn.ConnectAsync(listenEP);
                    Console.WriteLine("[+] Connected, starting tunnel...");
                    await HandleTunnelAsync(conn);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[!] Connection error: {ex.Message}, retrying in 5 seconds...");
                    await Task.Delay(5000);
                }
            }
        }

        private static async Task HandleTunnelAsync(Socket clientSocket)
        {
            Console.WriteLine($"[+] Connected to {clientSocket.RemoteEndPoint}");

            Socket remoteSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await remoteSocket.ConnectAsync(destinationAddress, destinationPort);
                Console.WriteLine($"[+] Tunneling to {destinationAddress}:{destinationPort}");

                var clientStream = new NetworkStream(clientSocket, ownsSocket: true);
                var remoteStream = new NetworkStream(remoteSocket, ownsSocket: true);

                var upload = CopyStreamAsync(clientStream, remoteStream);
                var download = CopyStreamAsync(remoteStream, clientStream);

                var completed = await Task.WhenAny(upload, download);

                if (completed == upload)
                    Console.WriteLine("[*] Upload (client → remote) finished first.");
                else if (completed == download)
                    Console.WriteLine("[*] Download (remote → client) finished first.");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] Tunnel error: {ex.Message}");
            }
            finally
            {
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
                    Console.WriteLine($"[DEBUG] Read {read} bytes from {source}");
                    if (read == 0)
                    {
                        Console.WriteLine("[DEBUG] Remote side closed (read 0 bytes).");
                        break;
                    }
                    await destination.WriteAsync(buffer, 0, read);
                    Console.WriteLine($"[DEBUG] Wrote {read} bytes to {destination}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Exception in CopyStreamAsync: {ex.Message}");
            }
        }


        static async Task Main(string[] args)
        {

            Console.WriteLine("******** Tunnel Proxy ********");
            Console.Write("Destination IP: ");
            destinationAddress = IPAddress.Parse(Console.ReadLine().Replace(" ", ""));

            Console.Write("Destination port: ");
            destinationPort = int.Parse(Console.ReadLine());

            await StartServer();
        }
    }
}
