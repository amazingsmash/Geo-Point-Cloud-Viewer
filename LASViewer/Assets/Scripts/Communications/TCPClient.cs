using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Diagnostics;

public interface ITCPListener
{
    void OnMessageReceived(string msg);
    void OnStatusMessage(string msg);
}

public class TCPClient {  	
	#region private members 	
	private TcpClient socketConnection; 	
	private Thread clientReceiveThread;
    readonly ITCPListener listener = null;
    #endregion

    public bool IsListening
    {
        get { return socketConnection != null; }
    }

    public TCPClient(ITCPListener listener)
    {
        this.listener = listener;
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
			socketConnection = new TcpClient("localhost", 8052);  			
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
    public void SendMessage(string clientMessage) {         
		if (socketConnection == null) {             
			return;         
		}  		
		try { 			
			// Get a stream object for writing. 			
			NetworkStream stream = socketConnection.GetStream(); 			
			if (stream.CanWrite) {                 			
				// Convert string message to byte array.                 
				byte[] clientMessageAsByteArray = Encoding.ASCII.GetBytes(clientMessage); 				
				// Write byte array to socketConnection stream.                 
				stream.Write(clientMessageAsByteArray, 0, clientMessageAsByteArray.Length);
                listener.OnStatusMessage("Client sent his message - should be received by server");             
			}         
		} 		
		catch (SocketException socketException) {
            listener.OnStatusMessage("Socket exception: " + socketException);         
		}     
	} 
}