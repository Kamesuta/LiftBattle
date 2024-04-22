using FishNet;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using FishNet.Transporting;
using FishNet.Transporting.Bayou;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using System;
using System.Threading.Tasks;
using UnityEngine;

public class NetworkStart : MonoBehaviour
{
    public string[] addresses;

    void Start()
    {
        ServerManager serverManager = InstanceFinder.ServerManager;

        //This can be done easily using the TransportManager.
        Multipass multipass = InstanceFinder.TransportManager.GetTransport<Multipass>();

        //In this example if the build is a webGL build
        //then use Bayou, otherwise use Tugboat.
#if UNITY_WEBGL && !UNITY_EDITOR
        Connect(multipass, multipass.GetTransport<Bayou>());
#else
        serverManager.StartConnection();
        Connect(multipass, multipass.GetTransport<Tugboat>());
#endif
    }

    /**
     * Client: Connect to the server.
     */
    private async void Connect(Multipass multipass, Transport transport)
    {
        ClientManager clientManager = InstanceFinder.ClientManager;

        multipass.SetClientTransport(transport);

        // Wait for the connection to complete / fail.
        TaskCompletionSource<bool> taskCompletionSource = null;
        void func(ClientConnectionStateArgs arg)
        {
            if (arg.ConnectionState == LocalConnectionState.Started)
            {
                // Connection was successful.
                taskCompletionSource?.SetResult(true);
            }
            else if (arg.ConnectionState == LocalConnectionState.Stopped)
            {
                // Connection failed.
                taskCompletionSource?.SetResult(false);
            }
        }
        transport.OnClientConnectionState += func;

        // Try to connect in order of priority
        foreach (string address in addresses)
        {
            Debug.LogWarning($"Connecting to {address}...");
            
            transport.SetClientAddress(address);
            taskCompletionSource = new TaskCompletionSource<bool>();

            // Start the connection and wait for the result.
            bool result = false;
            if (clientManager.StartConnection())
            {
                result = await taskCompletionSource.Task;
            }

            Debug.LogWarning($"Connection to {address} {(result ? "successful" : "failed")}.");
            if (result)
            {
                // Connection was successful.
                break;
            }
        }

        transport.OnClientConnectionState -= func;
    }
}
