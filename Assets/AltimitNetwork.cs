using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;

namespace Altimit {

	public class StateObject {
		//Client Socket
		public Socket workSocket = null;
		//Message Size
		public const int BufferSize = 256;
		//Message
		public byte[] buffer = new byte[BufferSize];
	}

	public class AltimitNetwork : MonoBehaviour {

		private static Socket sendSocket = null;


		public static void Connect(String ip, int port){
			try{
				IPHostEntry ipHostInfo = Dns.GetHostEntry (ip);
				IPAddress ipAddress = ipHostInfo.AddressList [0];
				IPEndPoint remoteEP = new IPEndPoint (ipAddress, port);

				Socket client = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

				client.BeginConnect (remoteEP, new AsyncCallback (ConnectCallBack), client);

			} catch (Exception e){
				Debug.LogError (e.ToString ());
			}
		}

		private static void ConnectCallBack(IAsyncResult ar){
			try{
				Socket client = (Socket) ar.AsyncState;
				client.EndConnect(ar);

				Debug.LogFormat("Client connected to {0}", client.RemoteEndPoint.ToString());

				Receive(client);
				sendSocket = client;

				Guid temp = new Guid ();
				AltimitNetwork.Send ("SetClientUUID", temp);

			} catch (Exception e){
				Debug.LogError (e.ToString ());
			}
		}

		private static void Receive(Socket client){
			try{
				StateObject state = new StateObject();
				state.workSocket = client;

				client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallBack), state);

			} catch (Exception e){
				Debug.LogError (e.ToString ());
			}
		}

		private static void ReceiveCallBack(IAsyncResult ar){
			byte[] key = { 5, 9, 0, 4 };

			int messageSize = 0;
			int messageOffset = 0;
			byte[] currentMessage;

			int fullMessageSize = 0;
			byte[] fullMessage = new byte[0];

			try{
				if (isConnected()){
					StateObject state = (StateObject) ar.AsyncState;
					Socket client = state.workSocket;

					int buffSize = client.EndReceive(ar);

					if(buffSize != 0 || fullMessage.Length != 0){
						if(fullMessage.Length != 0 && buffSize != 0){
							Array.Resize(ref fullMessage, fullMessage.Length + buffSize);
							byte[] newMessage = new byte[buffSize];
							newMessage = state.buffer;
							Array.ConstrainedCopy(newMessage, 0, fullMessage, fullMessageSize, newMessage.Length);
							messageSize = 0;
						} else if(buffSize != 0){
							fullMessage = new byte[buffSize];
							fullMessage = state.buffer;
							messageSize = 0;
						} else if(fullMessage.Length != 0){
							messageSize = 0;
						}

						if(messageSize == 0){
							messageSize = BitConverter.ToInt32(AltimitArray.copyOfRange(fullMessage, 0, 4), 0);
							byte[] messageKey = AltimitArray.copyOfRange(fullMessage, messageSize - 4, messageSize);
							if(Array.Equals(messageKey, key)){
								currentMessage = new byte[messageSize - 8];
								messageOffset = 4;
								Array.ConstrainedCopy(fullMessage, messageOffset, currentMessage, 0, currentMessage.Length);
								messageOffset = messageSize;

								List<object> sentMessage = AltimitConverter.ReceiveConversion(currentMessage);
								InvokeMessage(sentMessage);

								if(messageOffset != fullMessage.Length){
									fullMessage = AltimitArray.copyOfRange(fullMessage, messageOffset, fullMessage.Length);
									fullMessageSize = fullMessage.Length;
								} else {
									fullMessage = new byte[0];
									buffSize = 0;
								}
							} else {
								Debug.Log("Key was not found. Message will try to be completed in next read!");
							}
						}
					}
				} else {
					Disconnect();
				}

			} catch(Exception e){
				Debug.LogError (e.ToString ());
			} finally {
				Disconnect ();
			}
		}

		private static void InvokeMessage(List<object> sentMessage){
			Debug.Log ("got some message");
		}

		public static void Send(String MethodName, params object[] data){
			byte[] messageData = AltimitConverter.SendConversion (MethodName, data);

			sendSocket.BeginSend (messageData, 0, messageData.Length, 0, new AsyncCallback (SendCallBack), sendSocket);
		}

		private static void SendCallBack(IAsyncResult ar){
			try{
				Socket client = (Socket)ar.AsyncState;

				int bytesSent = client.EndSend (ar);
				Debug.LogFormat ("Sent {0} bytes to server.", bytesSent);
			} catch(Exception e){
				Debug.LogError (e.ToString());
			}
		}

		public static void Disconnect(){
			sendSocket.Disconnect (false);
		}

		public static bool isConnected(){
			return sendSocket.Connected;
		}
	}
}