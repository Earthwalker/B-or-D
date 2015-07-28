//-----------------------------------------------------------------------
// <copyright file="Outbox.cs" company="Company">
//     Copyright (c) Ethan Vandersaul, Company. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace B_or_d
{
    using System;
    using System.Collections.Generic;
    using System.Timers;
    using MailKit.Net.Smtp;
    using MailKit.Security;
    using MimeKit;

    /// <summary>
    /// Handles sending mail messages
    /// </summary>
    public class Outbox : IDisposable
    {
        /// <summary>
        /// Timer to send messages in the send queue
        /// </summary>
        private Timer timer;

        /// <summary>
        /// Messages to be sent
        /// </summary>
        private Queue<MimeMessage> messages = new Queue<MimeMessage>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Outbox"/> class.
        /// </summary>
        public Outbox()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Outbox"/> class.
        /// </summary>
        /// <param name="frequency">Frequency to check messages</param>
        public void StartTimer(int frequency)
        {
            if (frequency <= 0 || frequency >= int.MaxValue)
                throw new ArgumentOutOfRangeException("frequency");

            // set the interval for the timer in seconds
            timer = new Timer(frequency * 1000);

            // we will manually start the timer each time
            timer.AutoReset = false;

            timer.Elapsed += SendMessages;

            timer.Start();
        }

        /// <summary>
        /// Sends a message
        /// </summary>
        /// <param name="message">Message to be sent</param>
        public void SendMessage(MimeMessage message)
        {
            // add the message to the send queue
            messages.Enqueue(message);
        }

        /// <summary>
        /// Forwards a message to one recipient
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="to">Recipient off the message</param>
        /// <param name="bcc">Private recipients</param>
        public void ForwardMessage(MimeMessage message, MailboxAddress to, IEnumerable<MailboxAddress> bcc = null)
        {
            if (message == null)
                throw new ArgumentNullException("message");

            // clear the original recipients
            message.To.Clear();

            // set the recipient
            message.To.Add(to);

            // clear the bcc
            message.Bcc.Clear();

            // set the bcc if any
            if (bcc != null)
                message.Bcc.AddRange(bcc);

            // add the message to the send queue
            messages.Enqueue(message);
        }

        /// <summary>
        /// Sends a reply from the selected recipients
        /// </summary>
        /// <param name="message">Reply message</param>
        /// <param name="from">From address of the reply</param>
        public void SendReply(MimeMessage message, IEnumerable<MailboxAddress> from = null)
        {
            if (message == null)
                throw new ArgumentNullException("message");

            // clear the original senders
            message.From.Clear();

            // if we don't specify a sender, use the original message's recipients
            if (from == null)
                message.From.AddRange(message.To);
            else
                message.From.AddRange(from);

            // clear the original recipients
            message.To.Clear();

            // set the recipients
            message.To.AddRange(message.From);

            // add the message to the send queue
            messages.Enqueue(message);
        }

        /// <summary>
        /// Implements IDisposable.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        /// <param name="disposing">Whether to dispose managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            timer.Dispose();
        }

        /// <summary>
        /// Event to send all messages
        /// </summary>
        /// <param name="source">Source object</param>
        /// <param name="e">Event arguments</param>
        public void SendMessages(object source, ElapsedEventArgs e)
        {
            if (messages.Count == 0)
            {
                // start the timer again
                timer?.Start();

                return;
            }

            var messageNum = messages.Count;

            // send all messages in the send queue
            using (SmtpClient client = new SmtpClient())
            {
                client.Connect("smtp." + Program.Host, Program.SmtpPort, SecureSocketOptions.SslOnConnect);
                client.Authenticate(Program.UserName, Program.Password);

                // send each message in the queue
                for (int i = 0; i < messageNum; i++)
                    client.Send(messages.Dequeue());

                client.Disconnect(true);
            }

            Console.WriteLine("Sent " + messageNum.ToString() + "s");

            // start the timer again
            timer?.Start();
        }
    }
}
