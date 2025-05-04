import socket
import threading
from multiprocessing.dummy import Pool as ThreadPool
import socketserver

class SocksTCPHandler(socketserver.BaseRequestHandler):
    """
    The request handler class for our server.

    It is instantiated once per connection to the server, and must
    override the handle() method to implement communication to the
    client.
    """

   def handle():
       pass

def pipe(src, dst, label):
    print(f"[{label}] Pipe started between {src.getsockname()} → {dst.getsockname()}")
    try:
        while True:
            data = src.recv(4096)
            if not data:
                print(f"[{label}] No more data, breaking loop")
                break
            print(f"[{label}] Received {len(data)} bytes")
            dst.sendall(data)
            print(f"[{label}] Forwarded {len(data)} bytes")
    except Exception as e:
        print(f"[{label}] Pipe error: {e}")
    finally:
        print(f"[{label}] Closing src")
        try:
            src.shutdown(socket.SHUT_RDWR)
        except:
            pass
        src.close()

def handle_pair(client_socket, remote_socket, pool):
    print("[*] Launching bi-directional pipes...")
    pool.apply_async(pipe, (client_socket, remote_socket, 'Client→Remote'))
    pool.apply_async(pipe, (remote_socket, client_socket, 'Remote→Client'))

def accept_callback(callback_listener, callback_container):
    conn, addr = callback_listener.accept()
    print(f"[Callback] Connection accepted from {addr}")
    callback_container['socket'] = conn

def accept_socks(socks_listener, socks_container):
    conn, addr = socks_listener.accept()
    print(f"[SOCKS] Connection accepted from {addr}")
    socks_container['socket'] = conn

if __name__ == "__main__":
    port_callback = 443
    port_socks = 1080

    pool = ThreadPool(10)

    # Setup persistent listening sockets
    callback_listener = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    callback_listener.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    callback_listener.bind(('0.0.0.0', port_callback))
    callback_listener.listen(5)
    print(f"[Callback] Listening on 0.0.0.0:{port_callback}")

    socks_listener = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    socks_listener.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    socks_listener.bind(('0.0.0.0', port_socks))
    socks_listener.listen(5)
    print(f"[SOCKS] Listening on 0.0.0.0:{port_socks}")

    try:
        # Accept both connections in parallel
        callback_container = {}
        socks_container = {}

        t_callback = threading.Thread(target=accept_callback, args=(callback_listener, callback_container))
        t_socks = threading.Thread(target=accept_socks, args=(socks_listener, socks_container))

        t_callback.start()
        t_socks.start()

        t_callback.join()
        t_socks.join()

        remote_socket = callback_container['socket']
        client_socket = socks_container['socket']

        handle_pair(client_socket, remote_socket, pool)

        # Keep main thread alive while pool works
        pool.close()
        pool.join()
        print("[*] All tunnel tasks complete.")

    except KeyboardInterrupt:
        print("[!] Interrupted, shutting down...")
    finally:
        callback_listener.close()
        socks_listener.close()
