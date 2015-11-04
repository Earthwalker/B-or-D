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
    /// Handles sending mail messages.
    /// </summary>
    public class Outbox : IDisposable
    {
        /// <summary>
        /// Timer to send messages in the send queue.
        /// </summary>
        private Timer timer;

        /// <summary>
        /// Initializes a new instance of the <see cref="Outbox"/> class.
        /// </summary>
        public Outbox()
        {
        }

        /// <summary>
        /// Gets the messages to be sent.
        /// </summary>
        /// <value>
        /// The messages to be sent.
        /// </value>
        public Queue<MimeMessage> Messages { get; private set; } = new Queue<MimeMessage>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Outbox"/> class.
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

            timer.Elapsed += SendMessages;

            timer.Start();
        }

        /// <summary>
        /// Sends a message.
        /// </summary>
        /// <param name="message">Message to be sent.</param>
        public void SendMessage(MimeMessage message)
        {
            // add the message to the send queue
            Messages.Enqueue(message);
        }

        /// <summary>
        /// Forwards a message to one recipient.
        /// </summary>
        /// <param name="message">Message to send.</param>
        /// <param name="to">Recipient off the message.</param>
        public void ForwardMessage(MimeMessage message, MailboxAddress to)
        {
            ForwardMessage(message, new List<MailboxAddress>() { to });
        }

        /// <summary>
        /// Forwards a message to more than one recipient.
        /// </summary>
        /// <param name="message">Message to send.</param>
        /// <param name="to">Recipient off the message.</param>
        public void ForwardMessage(MimeMessage message, List<MailboxAddress> to)
        {
            // ensure the message and the to address aren't null
            if (message == null || to == null)
                return;

            // clear the original recipients
            message.To.Clear();
            message.Bcc.Clear();

            // set the recipient
            if (to.Count == 1)
                message.To.Add(to[0]);
            else
                message.Bcc.AddRange(to);

            // add the message to the send queue
            Messages.Enqueue(message);
        }

        /// <summary>
        /// Sends a reply from the selected recipients.
        /// </summary>
        /// <param name="message">Reply message.</param>
        /// <param name="from">From address of the reply.</param>
        public void SendReply(MimeMessage message, IEnumerable<MailboxAddress> from = null)
        {
            // ensure the message isn't null
            if (message == null)
                return;

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
            Messages.Enqueue(message);
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
        /// Event to send all messages.
        /// </summary>
        /// <param name="source">Source object.</param>
        /// <param name="e">Event arguments.</param>
        public void SendMessages(object source, ElapsedEventArgs e)
        {
            if (Messages.Count == 0)
            {
                // start the timer again
                timer?.Start();

                return;
            }

            var messageNum = Messages.Count;

            // send all messages in the send queue
            using (SmtpClient client = new SmtpClient())
            {
                client.Connect("smtp." + Program.Host, Program.SmtpPort, SecureSocketOptions.SslOnConnect);
                client.Authenticate(Program.UserName, Program.Password);

                // send each message in the queue
                for (int i = 0; i < messageNum; i++)
                    client.Send(Messages.Dequeue());

                client.Disconnect(true);
            }

            Console.WriteLine("Sent " + messageNum.ToString() + "s");

            // start the timer again
            timer?.Start();
        }

        /// <summary>
        /// Disposes resources.
        /// </summary>
        /// <param name="disposing">Whether to dispose managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            timer.Dispose();
        }
    }
}
