//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="Company">
//     Copyright (c) Ethan Vandersaul, Company. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace B_or_d
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using MimeKit;
    using SharpConfig;

    /// <summary>
    /// Main program.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Path to our config file.
        /// </summary>
        private static readonly string ConfigPath = "config.ini";

        /// <summary>
        /// Path to our adjectives list file.
        /// </summary>
        private static readonly string AdjectivesListPath = "adjectives.txt";

        /// <summary>
        /// Path to our nouns list file.
        /// </summary>
        private static readonly string NounsListPath = "nouns.txt";

        /// <summary>
        /// Messages to be processed.
        /// </summary>
        private static Queue<MimeMessage> messages = new Queue<MimeMessage>();

        /// <summary>
        /// Whether we should have boards use the +something format.
        /// </summary>
        private static bool useAlias = true;

        /// <summary>
        /// Inbox interval loaded from config.
        /// </summary>
        private static int inboxInterval = 0;

        /// <summary>
        /// Outbox interval loaded from config.
        /// </summary>
        private static int outboxInterval = 0;

        /// <summary>
        /// Random number generator.
        /// </summary>
        private static Random rng = new Random();

        /// <summary>
        /// Gets the adjectives used in user name generation.
        /// </summary>
        /// <value>
        /// The adjectives.
        /// </value>
        public static Collection<string> Adjectives { get; private set; }

        /// <summary>
        /// Gets the nouns used in user name generation.
        /// </summary>
        /// <value>
        /// The nouns.
        /// </value>
        public static Collection<string> Nouns { get; private set; }

        /// <summary>
        /// Gets or sets the context for the underlying database
        /// </summary>
        /// <value>
        /// The database context.
        /// </value>
        public static BoardContext Context { get; set; }

        /// <summary>
        /// Gets the inbox handling incoming messages.
        /// </summary>
        /// <value>
        /// The inbox.
        /// </value>
        public static Inbox Inbox { get; } = new Inbox();

        /// <summary>
        /// Gets the outbox handling outgoing messages.
        /// </summary>
        /// <value>
        /// The outbox.
        /// </value>
        public static Outbox Outbox { get; } = new Outbox();

        /// <summary>
        /// Gets or sets the user name of the email account.
        /// </summary>
        /// <value>
        /// The user name.
        /// </value>
        public static string UserName { get; set; }

        /// <summary>
        /// Gets or sets the host name of the email account.
        /// </summary>
        /// <value>
        /// The host name.
        /// </value>
        public static string Host { get; set; }

        /// <summary>
        /// Gets or sets the pop port of the host.
        /// </summary>
        /// <value>
        /// The pop port of the host.
        /// </value>
        public static int PopPort { get; set; }

        /// <summary>
        /// Gets or sets the SMTP port of the host.
        /// </summary>
        /// <value>
        /// The SMTP port of the host.
        /// </value>
        public static int SmtpPort { get; set; }

        /// <summary>
        /// Gets or sets the password of the email account.
        /// </summary>
        /// <value>
        /// The password.
        /// </value>
        public static string Password { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="Program" /> is running.
        /// </summary>
        /// <value>
        /// <c>true</c> if running; otherwise, <c>false</c>.
        /// </value>
        public static bool Running { get; set; } = false;

        /// <summary>
        /// Defines the program entry point. 
        /// </summary>
        public static void Main()
        {
            Console.WriteLine("Welcome to b-or-d");

            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
            Trace.AutoFlush = true;

            // create new context
            using (Context = new BoardContext())
            {
                // create the database if it doesn't exist
                Context.Database.CreateIfNotExists();

                // load config
                LoadConfig();

                // load word lists
                PopulateWordlists(NounsListPath, AdjectivesListPath);

                // start timers for sending and receiving messages
                StartInboxTimer(inboxInterval);
                StartOutboxTimer(outboxInterval);

                Running = true;

                while (Running)
                {
                    switch (Console.ReadLine().ToUpperInvariant())
                    {
                        case "QUIT":
                            Running = false;
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Load config file.
        /// </summary>
        /// <param name="fileName">File name.</param>
        public static void LoadConfig(string fileName = null)
        {
            if (string.IsNullOrEmpty(fileName))
                fileName = ConfigPath;

            // make sure the config file exists
            if (!File.Exists(fileName))
                throw new FileNotFoundException("fileName");

            // load config file
            var config = Configuration.LoadFromFile(fileName);

            // load server settings
            UserName = config["Server"]["UserName"].StringValue;
            Host = config["Server"]["Host"].StringValue;
            PopPort = config["Server"]["PopPort"].IntValue;
            SmtpPort = config["Server"]["SmtpPort"].IntValue;
            Password = config["Server"]["Password"].StringValue;

            // load inbox setting
            inboxInterval = config["Inbox"]["InboxInterval"].IntValue;

            // load outbox settings
            outboxInterval = config["Outbox"]["OutboxInterval"].IntValue;

            Console.WriteLine("Loaded settings from " + fileName);
        }

        /// <summary>
        /// Loads word lists used for generating user names.
        /// </summary>
        /// <param name="adjectivesFileName">File containing adjectives.</param>
        /// <param name="nounsFileName">File containing nouns.</param>
        public static void PopulateWordlists(string adjectivesFileName, string nounsFileName)
        {
            if (string.IsNullOrEmpty(adjectivesFileName))
                adjectivesFileName = AdjectivesListPath;

            if (string.IsNullOrEmpty(nounsFileName))
                nounsFileName = NounsListPath;

            // make sure the adjectives file exists
            if (!File.Exists(adjectivesFileName))
                throw new FileNotFoundException("File containing adjectives", "adjectivesFileName");

            // make sure the nouns file exists
            if (!File.Exists(nounsFileName))
                throw new FileNotFoundException("File containing nouns", "nounsFileName");

            // read from the files
            Adjectives = new Collection<string>(File.ReadAllLines(adjectivesFileName).ToList());
            Nouns = new Collection<string>(File.ReadAllLines(nounsFileName).ToList());

            Console.WriteLine("Loaded word lists from " + adjectivesFileName + " and " + nounsFileName);
        }

        /// <summary>
        /// Loads a saved message from a file.
        /// </summary>
        /// <param name="messageName">Message name.</param>
        /// <returns>The loaded message.</returns>
        public static MimeMessage LoadMailMessage(string messageName)
        {
            if (string.IsNullOrWhiteSpace(messageName))
            {
                Trace.TraceError("Message name empty");
                return null;
            }

            if (!File.Exists(messageName))
            {
                Trace.TraceError("File " + messageName + " does not exist");
                return null;
            }

            // load the message
            var message = MimeMessage.Load(messageName);

            // clear to and from fields
            message.From.Clear();
            message.To.Clear();

            // return the loaded message
            return message;
        }

        /// <summary>
        /// Gets the julian day number.
        /// </summary>
        /// <param name="date">Date to convert.</param>
        /// <returns>The julian day.</returns>
        public static int GetDayNumber(DateTime date)
        {
            return (int)(date - new DateTime(1, 1, 1)).TotalDays + 1;
        }

        /// <summary>
        /// Formats the mailbox address.
        /// </summary>
        /// <param name="sender">Sender of the mail.</param>
        /// <param name="displayName">Name to display.</param>
        /// <returns>The formatted <see cref="MailboxAddress"/>.</returns>
        public static MailboxAddress FormatMailboxAddress(string sender, string displayName = null)
        {
            if (string.IsNullOrEmpty(displayName))
                displayName = UserName;

            if (!string.IsNullOrEmpty(sender))
                displayName = sender + " at " + displayName;

            if (string.IsNullOrEmpty(sender))
                return new MailboxAddress(displayName, UserName + '@' + Host);

            if (useAlias)
                return new MailboxAddress(displayName, UserName + '+' + sender + '@' + Host);
            else
                return new MailboxAddress(displayName, sender + '@' + Host);
        }

        /// <summary>
        /// Gets the sender from an <see cref="InternetAddress"/>.
        /// </summary>
        /// <param name="address"><see cref="InternetAddress"/> to parse.</param>
        /// <returns>The sender of an <see cref="InternetAddress"/>.</returns>
        public static string GetSender(InternetAddress address)
        {
            if (useAlias)
            {
                var sender = GetAddress(address).Split('@')[0].Split('+');

                if (sender.Length > 1)
                    return sender[1];
                else
                    return sender[0];
            }
            else
                return GetAddress(address).Split('@')[0];
        }

        /// <summary>
        /// Gets the address of an <see cref="InternetAddress"/>.
        /// </summary>
        /// <param name="address"><see cref="InternetAddress"/> to parse.</param>
        /// <returns>Address of the <see cref="InternetAddress"/>.</returns>
        public static string GetAddress(InternetAddress address)
        {
            return (address as MailboxAddress)?.Address ?? string.Empty;
        }

        /// <summary>
        /// Starts the timer for the inbox to check for messages.
        /// </summary>
        /// <param name="intervalSeconds">Interval time in seconds.</param>
        public static void StartInboxTimer(int intervalSeconds)
        {
            // set up inbox events
            Inbox.NewMessagesEvent += Inbox_NewMessagesEvent;

            Inbox.StartTimer(intervalSeconds);
        }

        /// <summary>
        /// Starts the timer for the outbox to empty itself.
        /// </summary>
        /// <param name="intervalSeconds">Interval time in seconds.</param>
        public static void StartOutboxTimer(int intervalSeconds)
        {
            Outbox.StartTimer(intervalSeconds);
        }

        /// <summary>
        /// Shuffles a list based off of the Fisher-Yates shuffle.
        /// </summary>
        /// <typeparam name="T">List type.</typeparam>
        /// <param name="list">List to shuffle.</param>
        public static void Shuffle<T>(this List<T> list)
        {
            if (list == null)
                return;

            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        /// <summary>
        /// Save changes to the underlying database.
        /// </summary>
        private static void SaveDB()
        {
            // only save if the program is running and not just testing
            if (Running)
                Context.SaveChanges();
        }

        /// <summary>
        /// Handles messages.
        /// </summary>
        private static void HandleMessages()
        {
            // loop through each message in the queue
            while (messages.Count > 0)
            {
                // dequeue the message
                var message = messages.Dequeue();

                foreach (var to in message.To)
                {
                    var board = Context.Boards?.Find(GetSender(to));

                    if (board == null)
                    {
                        HandleCommand(message.Subject, GetAddress(message.From[0]));
                        SaveDB();
                    }
                    else
                    {
                        if (board.HandleMessage(message))
                            SaveDB();
                    }
                }
            }
        }

        /// <summary>
        /// Handle general commands.
        /// </summary>
        /// <param name="command">User command.</param>
        /// <param name="userAddress">User address calling the command.</param>
        private static void HandleCommand(string command, string userAddress)
        {
            var commands = command.ToUpperInvariant().Split(':');

            switch (commands[0])
            {
                case "JOIN":
                    // receives a list of popular boards and main rules
                    Outbox.SendReply(LoadMailMessage("rules"));

                    // TODO: append a list popular boards to the reply
                    return;
                case "LEAVE":
                    // leaves every board
                    Context.Users.RemoveRange(Context.Users.Where(u => u.Address == userAddress));
                    return;
                case "RULES":
                    // receives the general rules
                    Outbox.SendReply(LoadMailMessage("rules"));
                    return;
                case "BOARDS":
                    // receives a list of boards with the given tags
                    if (commands.Length == 1)
                        break;

                    var tags = commands[1].Split(',');
                    var matches = Context.Boards.Where(b => b.Tags.IsSupersetOf(tags));

                    // create the message
                    var newMessage = new MimeMessage();

                    // set the subject
                    newMessage.Subject = "Boards with tags " + commands[1];

                    // build the body of the message
                    var builder = new BodyBuilder();

                    // TODO: add new lines after each match
                    foreach (var match in matches)
                        builder.TextBody += match.Name + "\r\n";

                    newMessage.Body = builder.ToMessageBody();

                    // send the reply
                    Outbox.SendReply(newMessage);
                    return;
            }

            // receives help
            Outbox.SendReply(LoadMailMessage("help"));
        }

        /// <summary>
        /// New messages event.
        /// </summary>
        /// <param name="sender">Inbox that called the event.</param>
        /// <param name="e">Event arguments.</param>
        private static void Inbox_NewMessagesEvent(object sender, EventArgs e)
        {
            var newMessages = ((Inbox)sender).Messages;
            var newMessagesNum = newMessages.Count;

            // enqueue all the new messages
            for (int i = 0; i < newMessagesNum; i++)
                messages.Enqueue(newMessages.Peek());

            // if the new messages are the only ones in the queue, process them right away
            if (newMessagesNum == messages.Count)
                HandleMessages();
        }
    }
}