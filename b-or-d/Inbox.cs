//-----------------------------------------------------------------------
// <copyright file="Inbox.cs" company="Company">
//     Copyright (c) Ethan Vandersaul, Company. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace B_or_d
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Timers;
    using MailKit.Net.Pop3;
    using MailKit.Security;
    using MimeKit;

    /// <summary>
    /// Handles receiving mail messages.
    /// </summary>
    public class Inbox : IDisposable
    {
        /// <summary>
        /// Unique ids of the messages we've seen.
        /// </summary>
        private HashSet<string> seenUIDs = new HashSet<string>();

        /// <summary>
        /// Timer to check for new messages.
        /// </summary>
        private Timer timer;

        /// <summary>
        /// Initializes a new instance of the <see cref="Inbox"/> class.
        /// </summary>
        public Inbox()
        {
        }

        /// <summary>
        /// Event to signal we have new messages.
        /// </summary>
        public event EventHandler NewMessagesEvent;

        /// <summary>
        /// Gets the messages to be sent.
        /// </summary>
        /// <value>
        /// The messages to be sent.
        /// </value>
        public Queue<MimeMessage> Messages { get; } = new Queue<MimeMessage>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Inbox"/> class.
        /// </summary>
        /// <param name="frequency">Frequency to check messages.</param>
        public void StartTimer(int frequency)
        {
            // ensure the frequency is in the required range
            if (frequency <= 0 || frequency >= int.MaxValue)
                throw new ArgumentOutOfRangeException("frequency");

            // set the interval for the timer in seconds
            timer = new Timer(frequency * 1000);

            // we will manually start the timer each time
            timer.AutoReset = false;

            timer.Elapsed += ReceiveMessages;

            timer.Start();
        }

        /// <summary>
        /// Fetch messages not seen from the server.
        /// </summary>
        public void FetchNewMessages()
        {
            // the client disconnects from the server when being disposed
            using (Pop3Client client = new Pop3Client())
            {
                try
                {
                    client.Connect("pop." + Program.Host, Program.PopPort, SecureSocketOptions.SslOnConnect);
                    client.AuthenticationMechanisms.Remove("XOAUTH2"); // we don't have an OAuth2 token
                    client.Authenticate(Program.UserName + '@' + Program.Host, Program.Password);

                    // get message count
                    var messageUids = client.GetMessageUids();

                    for (int i = 0; i < messageUids.Count; i++)
                    {
                        // check if the message has been seen
                        if (!seenUIDs.Contains(messageUids[i]))
                        {
                            // add the message to our queue
                            Messages.Enqueue(client.GetMessage(i));

                            // delete the message from the server
                            client.DeleteMessage(i);

                            // add to the seen messages
                            seenUIDs.Add(messageUids[i]);
                        }
                    }

                    client.Disconnect(true);
                }
                catch (Exception e)
                {
                    Trace.TraceError(e.Message);
                }
            }
        }

        /// <summary>
        /// Implements <see cref="IDisposable"/>.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes resources.
        /// </summary>
        /// <param name="disposing">Whether to dispose managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            timer.Dispose();
        }

        /// <summary>
        /// Event to receive new messages.
        /// </summary>
        /// <param name="source">Source object.</param>
        /// <param name="e">Event arguments.</param>
        private void ReceiveMessages(object source, ElapsedEventArgs e)
        {
            // clear our message queue
            Messages.Clear();

            Console.WriteLine("Checking for new messages...");

            // fetch new messages
            FetchNewMessages();

            if (Messages.Count > 0)
            {
                NewMessagesEvent(this, e);
                Console.WriteLine(Messages.Count.ToString() + " new messages found");
            }
            else
                Console.WriteLine("No new messages found");

            // start the timer again
            timer?.Start();
        }
    }
}
