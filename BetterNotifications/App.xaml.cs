using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;

namespace BetterNotifications
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        List<Client> clients = new List<Client>();
        NamedPipeServerStream pipe = new NamedPipeServerStream(
            "BetterNotifications",
            PipeDirection.In,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Message,
            PipeOptions.Asynchronous
        );

        Thread? readerThread;

        private List<NotificationWindow> ActiveNotifications = new();

        public void ShowNotification(string title, string message)
        {
            var screen = SystemParameters.WorkArea;
            const double margin = 20;
            const double notificationHeight = 100;

            var offsetY = ActiveNotifications.Count * (notificationHeight + margin);
            var position = new Point(screen.Right - 310, screen.Top + offsetY);

            var notification = new NotificationWindow(title, message);
            notification.OnClosedExternally = OnNotificationClosed;
            ActiveNotifications.Add(notification);
            notification.ShowAt(position);
        }

        private void OnNotificationClosed(NotificationWindow closed)
        {
            ActiveNotifications.Remove(closed);
            Reposition();
        }

        private void Reposition()
        {
            const double margin = 20;
            const double notificationHeight = 100;
            var screen = SystemParameters.WorkArea;

            for (int i = 0; i < ActiveNotifications.Count; i++)
            {
                var top = screen.Top + ((i) * (notificationHeight + margin));
                ActiveNotifications[i].Top = top;
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            CancellationTokenSource source = new CancellationTokenSource();
            CancellationToken token = source.Token;

            // task for listening for connections
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        Task connectionTask = pipe.WaitForConnectionAsync();
                        await connectionTask.WaitAsync(token);

                        Console.WriteLine("Client connected");

                        var pipeCopy = pipe;

                        int processPid = 0;
                        GetNamedPipeClientProcessId(
                            pipe.SafePipeHandle.DangerousGetHandle(),
                            out processPid
                        );

                        Process process = Process.GetProcessById(processPid);
                        var processName = process.ProcessName;

                        var client = clients.Find(x => x.name == processName);

                        if (client == null)
                        {
                            var exePath = process.MainModule?.FileName;
                            if (exePath == null)
                                continue;

                            clients.Add(new Client(exePath, process.ProcessName, pipeCopy));
                            client = clients[clients.Count - 1];
                        }

                        if (!client.isAllowed)
                        {
                            Console.WriteLine("Client not allowed");
                            pipe.Disconnect();
                        }

                        pipe = new NamedPipeServerStream(
                            "BetterNotifications",
                            PipeDirection.In,
                            NamedPipeServerStream.MaxAllowedServerInstances,
                            PipeTransmissionMode.Message,
                            PipeOptions.Asynchronous
                        );
                    }
                    catch (OperationCanceledException)
                    {
                        pipe.WaitForPipeDrain();
                        if (pipe.IsConnected)
                        {
                            pipe.Disconnect();
                        }

                        Console.WriteLine("Waiting for connection was cancelled.");
                    }
                    catch (Exception ex)
                    {
                        pipe.WaitForPipeDrain();
                        if (pipe.IsConnected)
                        {
                            pipe.Disconnect();
                        }

                        Console.WriteLine(ex.Message);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            });

            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        for (int i = clients.Count - 1; i >= 0; i--)
                        {
                            var client = clients[i];

                            if (!client.pipe.IsConnected)
                            {
                                if (client.isActive)
                                {
                                    client.isActive = false;
                                    client.pipe.Dispose();
                                }
                                //Console.WriteLine("Not connected");

                                clients.Remove(client);
                                continue;
                            }

                            using var reader = new StreamReader(client.pipe);
                            string json = await reader.ReadToEndAsync();

                            var jsonMessage = JsonSerializer.Deserialize<Message>(json);

                            this.Dispatcher.Invoke(() =>
                                ShowNotification(jsonMessage?.title, jsonMessage?.content)
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            });

            ShowNotification("test 12", "testes2312");
            ShowNotification("test 12", "testes2312123123");
            ShowNotification("estset", "teststes");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            if (pipe.IsConnected)
            {
                pipe.WaitForPipeDrain();
                pipe.Disconnect();
            }

            pipe.Close();
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetNamedPipeClientProcessId(
            IntPtr hNamedPipe,
            out int clientProcessId
        );
    }

    public class Client
    {
        public string exe = "";
        public string name = "";
        public bool isAllowed = true;
        public NamedPipeServerStream pipe;
        public bool isActive = true;

        public Client(string exe, string name, NamedPipeServerStream pipe)
        {
            this.exe = exe;
            this.name = name;
            this.pipe = pipe;
        }
    }

    public class Message
    {
        public string title { get; set; }
        public string content { get; set; }
    }
}
