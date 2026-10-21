using MCPForUnity.Editor.Services;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class McpAutoReconnect
{
    static McpAutoReconnect()
    {
        EditorApplication.delayCall += ReconnectOnce;
    }

    private static async void ReconnectOnce()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ReconnectOnce;
            return;
        }
        if (!MCPServiceLocator.Server.IsLocalHttpServerReachable())
        {
            MCPServiceLocator.Server.StartLocalHttpServer(quiet: true);
            for (int i = 0; i < 20 && !MCPServiceLocator.Server.IsLocalHttpServerReachable(); i++)
                await Task.Delay(250);
        }

        if (MCPServiceLocator.Bridge.IsRunning) return;

        bool connected = await MCPServiceLocator.Bridge.StartAsync();
        if (connected)
        {
            Debug.Log("[Thanh Giong] MCP for Unity session restored automatically.");
        }
    }
}
