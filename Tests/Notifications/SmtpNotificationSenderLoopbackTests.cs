using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using CourierService.Services.Notifications;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.Notifications
{
    /// <summary>
    /// The real SmtpClient path, against a tiny mail server that this test starts on 127.0.0.1. No smtp4dev or
    /// network needed: it checks the email really goes out over SMTP, and that a server that isn't there or turns
    /// the recipient away gives the right kind of failure.
    /// </summary>
    [TestClass]
    public class SmtpNotificationSenderLoopbackTests
    {
        /// <summary>
        /// Just enough of an SMTP server for one email: answers the commands SmtpClient sends and keeps what it received.
        /// </summary>
        private sealed class OneMessageSmtpServer : IDisposable
        {
            private readonly TcpListener _listener = new TcpListener(IPAddress.Loopback, 0);
            private readonly Thread _thread;
            private readonly string _rcptReply;

            public OneMessageSmtpServer(string rcptReply = "250 OK")
            {
                _rcptReply = rcptReply;
                _listener.Start();
                Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
                _thread = new Thread(Serve) { IsBackground = true };
                _thread.Start();
            }

            public int Port { get; }

            public List<string> Commands { get; } = new List<string>();

            public StringBuilder Data { get; } = new StringBuilder();

            public bool Finish() => _thread.Join(TimeSpan.FromSeconds(10));

            private void Serve()
            {
                try
                {
                    using (var client = _listener.AcceptTcpClient())
                    using (var stream = client.GetStream())
                    using (var reader = new StreamReader(stream, Encoding.ASCII))
                    using (var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true })
                    {
                        writer.WriteLine("220 localhost test SMTP");
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            Commands.Add(line);
                            var command = line.Length >= 4 ? line.Substring(0, 4).ToUpperInvariant() : line.ToUpperInvariant();

                            if (command == "EHLO" || command == "HELO") writer.WriteLine("250 localhost");
                            else if (command == "MAIL") writer.WriteLine("250 OK");
                            else if (command == "RCPT") writer.WriteLine(_rcptReply);
                            else if (command == "DATA")
                            {
                                writer.WriteLine("354 End data with <CR><LF>.<CR><LF>");
                                while ((line = reader.ReadLine()) != null && line != ".")
                                {
                                    Data.AppendLine(line);
                                }

                                writer.WriteLine("250 OK queued");
                            }
                            else if (command == "QUIT")
                            {
                                writer.WriteLine("221 Bye");
                                break;
                            }
                            else writer.WriteLine("250 OK");
                        }
                    }
                }
                catch (IOException)
                {
                    // The client hung up; the test checks what was received
                }
                catch (SocketException)
                {
                }
                finally
                {
                    _listener.Stop();
                }
            }

            public void Dispose()
            {
                _listener.Stop();
            }
        }

        private static SmtpSettings Settings(int port) => new SmtpSettings
        {
            Host = "127.0.0.1",
            Port = port,
            FromAddress = "courier-noreply@f20.local",
            TimeoutSeconds = 5
        };

        private static NotificationMessage Message() => new NotificationMessage
        {
            NotificationQueueId = 5,
            F20Identifier = "F20-0007",
            Channel = NotificationChannels.Email,
            TemplateKey = NotificationTemplateKeys.ReadyForCollection,
            To = "thandi@courier.test",
            Subject = "Your package is ready for collection",
            Body = "Your package F20-0007 is ready for collection at Shelf B-04."
        };

        [TestMethod]
        public void SendsTheEmailOverSmtp()
        {
            using (var server = new OneMessageSmtpServer())
            {
                var result = new SmtpNotificationSender(Settings(server.Port)).Send(Message());

                Assert.IsTrue(result.Success, result.Error);
                Assert.IsTrue(server.Finish(), "the test server did not finish");
                Assert.IsTrue(server.Commands.Exists(c => c.StartsWith("MAIL FROM:<courier-noreply@f20.local>", StringComparison.OrdinalIgnoreCase)), string.Join(" | ", server.Commands));
                Assert.IsTrue(server.Commands.Exists(c => c.StartsWith("RCPT TO:<thandi@courier.test>", StringComparison.OrdinalIgnoreCase)), string.Join(" | ", server.Commands));

                var data = server.Data.ToString();
                Assert.IsTrue(data.Contains("Subject:"), data);
                Assert.IsTrue(data.Contains("thandi@courier.test"), data);
            }
        }

        [TestMethod]
        public void RecipientRejectedByTheServer_IsPermanent()
        {
            using (var server = new OneMessageSmtpServer("550 5.1.1 <thandi@courier.test>: Recipient address rejected"))
            {
                var result = new SmtpNotificationSender(Settings(server.Port)).Send(Message());

                Assert.IsFalse(result.Success);
                Assert.IsTrue(result.Permanent, result.Error);
                Assert.IsFalse(result.Error.Contains("thandi"), "the error must not repeat the server's reply: " + result.Error);
            }
        }

        [TestMethod]
        public void NoServerListening_FailsQuickly_AndIsWorthARetry()
        {
            // Take a free port and close it again, so nothing is listening there
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            var timer = Stopwatch.StartNew();
            var result = new SmtpNotificationSender(Settings(port)).Send(Message());
            timer.Stop();

            Assert.IsFalse(result.Success);
            Assert.IsFalse(result.Permanent, result.Error);
            Assert.IsTrue(timer.Elapsed < TimeSpan.FromSeconds(10), "took " + timer.Elapsed);
        }
    }
}