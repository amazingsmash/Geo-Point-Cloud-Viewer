using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Net;

public interface ITCPEndListener
{
    void OnStatusChanged(TCPEnd.Status status);
    void OnMessageReceived(string msg);
    void OnStatusMessage(string msg);
}

public abstract class TCPEnd
{
    public enum Status
    {
        READY, NOT_READY
    }

    protected readonly int port;
    protected readonly string ip;
    protected readonly ITCPEndListener listener = null;

    public TCPEnd(string ip, int port, ITCPEndListener listener)
    {
        this.ip = ip;
        this.port = port;
        this.listener = listener;
    }

    public abstract void SendMessage(string serverMessage);

    public abstract bool IsReady();
}


public class TCPServer: TCPEnd
{
    #region private members     
    /// <summary>   
    /// TCPListener to listen for incomming TCP connection  
    /// requests.   
    /// </summary>  
    private TcpListener tcpListener;
    /// <summary> 
    /// Background thread for TcpServer workload.   
    /// </summary>  
    private Thread tcpListenerThread;
    /// <summary>   
    /// Create handle to connected tcp client.  
    /// </summary>  
    private TcpClient connectedTcpClient;
    bool listenerHasStarted = false;
    #endregion

    // Use this for initialization
    public TCPServer(string ip, int port, ITCPEndListener listener):
        base(ip, port, listener)
    {
        // Start TcpServer background thread        
        tcpListenerThread = new Thread(new ThreadStart(ListenForIncommingRequests));
        tcpListenerThread.IsBackground = true;
        tcpListenerThread.Start();
    }

    /// <summary>   
    /// Runs in background TcpServerThread; Handles incomming TcpClient requests    
    /// </summary>  
    private void ListenForIncommingRequests()
    {
        try
        {
            // Create listener on localhost port 8052.          
            tcpListener = new TcpListener(IPAddress.Parse(ip), port);
            tcpListener.Start();
            listenerHasStarted = true;
            listener.OnStatusChanged(Status.READY);

            listener.OnStatusMessage("Server is listening");
            Byte[] bytes = new Byte[1024];
            while (true)
            {
                using (connectedTcpClient = tcpListener.AcceptTcpClient())
                {
                    // Get a stream object for reading                  
                    using (NetworkStream stream = connectedTcpClient.GetStream())
                    {
                        int length;
                        // Read incomming stream into byte arrary.                      
                        while ((length = stream.Read(bytes, 0, bytes.Length)) != 0)
                        {
                            var incommingData = new byte[length];
                            Array.Copy(bytes, 0, incommingData, 0, length);
                            // Convert byte array to string message.                            
                            string clientMessage = Encoding.ASCII.GetString(incommingData);
                            //Debug.Log("client message received as: " + clientMessage); 

                            if (listener != null)
                            {
                                listener.OnMessageReceived(clientMessage);
                            }
                        }
                    }
                }
            }
        }
        catch (SocketException socketException)
        {
            listener.OnStatusMessage("SocketException " + socketException.ToString());
        }
    }
    /// <summary>   
    /// Send message to client using socket connection.     
    /// </summary>  
    public override void SendMessage(string msg)
    {
        if (connectedTcpClient == null)
        {
            return;
        }

        try
        {
            // Get a stream object for writing.             
            NetworkStream stream = connectedTcpClient.GetStream();
            if (stream.CanWrite)
            {
                // Convert string message to byte array.                 
                byte[] serverMessageAsByteArray = Encoding.ASCII.GetBytes(msg);
                // Write byte array to socketConnection stream.               
                stream.Write(serverMessageAsByteArray, 0, serverMessageAsByteArray.Length);
                listener.OnStatusMessage("Server sent his message - should be received by client");
            }
        }
        catch (SocketException socketException)
        {
            listener.OnStatusMessage("Socket exception: " + socketException);
        }
    }

    public override bool IsReady()
    {
        return listenerHasStarted;
    }
}

public class TCPClient : TCPEnd
{  	
	#region private members 	
	private TcpClient socketConnection; 	
	private Thread clientReceiveThread;
    #endregion

    public bool IsListening
    {
        get { return socketConnection != null; }
    }

    public TCPClient(string ip, int port, ITCPEndListener listener) :
        base(ip, port, listener)
    {
    }

    /// <summary> 	
    /// Setup socket connection. 	
    /// </summary> 	
    public void ConnectToTCPServer () { 		
		try {  			
			clientReceiveThread = new Thread (new ThreadStart(ListenForData)); 			
			clientReceiveThread.IsBackground = true; 			
			clientReceiveThread.Start();  		
		} 		
		catch (Exception e) {
            listener.OnStatusMessage("On client connect exception " + e); 		
		} 	
	}  	
	/// <summary> 	
	/// Runs in background clientReceiveThread; Listens for incomming data. 	
	/// </summary>     
	private void ListenForData() { 		
		try { 			
			socketConnection = new TcpClient(ip, port);  			
			Byte[] bytes = new Byte[1024];             
			while (true) { 				
				// Get a stream object for reading 				
				using (NetworkStream stream = socketConnection.GetStream()) { 					
					int length; 					
					// Read incomming stream into byte arrary. 					
					while ((length = stream.Read(bytes, 0, bytes.Length)) != 0) { 						
						var incommingData = new byte[length]; 						
						Array.Copy(bytes, 0, incommingData, 0, length); 						
						// Convert byte array to string message. 						
						string serverMessage = Encoding.ASCII.GetString(incommingData);
                        if (listener != null)
                        {
                            listener.OnMessageReceived(serverMessage);
                        }				
					} 				
				} 			
			}         
		}         
		catch (SocketException socketException) {
            listener.OnStatusMessage("Socket exception: " + socketException);         
		}     
	}

    /// <summary> 	
    /// Send message to server using socket connection. 	
    /// </summary> 	
    public override void SendMessage(string msg) {         
		if (socketConnection == null) {             
			return;         
		}  		
		try { 			
			// Get a stream object for writing. 			
			NetworkStream stream = socketConnection.GetStream();
            listener.OnStatusChanged(Status.READY); 			
			if (stream.CanWrite) {                 			
				// Convert string message to byte array.                 
				byte[] clientMessageAsByteArray = Encoding.ASCII.GetBytes(msg); 				
				// Write byte array to socketConnection stream.                 
				stream.Write(clientMessageAsByteArray, 0, clientMessageAsByteArray.Length);
                listener.OnStatusMessage("Client sent his message - should be received by server");             
			}         
		} 		
		catch (SocketException socketException) {
            listener.OnStatusMessage("Socket exception: " + socketException);         
		}
    }

    public override bool IsReady()
    {
        return socketConnection != null;
    }
}