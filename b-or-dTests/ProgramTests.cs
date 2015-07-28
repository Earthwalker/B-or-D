//-----------------------------------------------------------------------
// <copyright file="ProgramTests.cs" company="Company">
//     Copyright (c) Ethan Vandersaul, Company. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace B_or_d.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Timers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using MimeKit;

    /// <summary>
    /// Program-wide tests
    /// </summary>
    [TestClass]
    public class ProgramTests
    {
        // holds the messages we receive
        private static Queue<MimeMessage> messages = new Queue<MimeMessage>();

        /// <summary>
        /// Tests sending and receiving mail
        /// </summary>
        [TestMethod]
        public void SendAndReceiveMailTest()
        {
            Program.LoadConfig();
            Program.PopulateWordlists(string.Empty, string.Empty);

            using (Program.Context = new BoardContext())
            {
                Program.StartInboxTimer(1);

                var message = SendMessage("Test Message " + DateTime.Now.ToString(), string.Empty, string.Empty);

                using (var timer = new Timer(10000))
                {
                    timer.Elapsed += Timer_Elapsed;
                    timer.Start();

                    while (timer.Enabled)
                    {
                        if (messages.Count > 0)
                        {
                            if (messages.Dequeue().Subject == message.Subject)
                                break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Tests using the join board command
        /// </summary>
        [TestMethod]
        public void JoinBoardTest()
        {
            Program.LoadConfig();
            Program.PopulateWordlists(string.Empty, string.Empty);

            using (Program.Context = new BoardContext())
            {
                Program.StartInboxTimer(1);

                var board = new Board("Test Board " + DateTime.Now.ToString());
                var message = SendMessage("join", string.Empty, board.Name);

                using (var timer = new Timer(10000))
                {
                    timer.Elapsed += Timer_Elapsed;
                    timer.Start();

                    while (timer.Enabled)
                    {
                        if (board.Users?.FirstOrDefault(u => u.Address == Program.GetAddress(message.From[0])) != null)
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Tests using the join command to create a board
        /// </summary>
        [TestMethod]
        public void CreateBoardTest()
        {
            Program.LoadConfig();
            Program.PopulateWordlists(string.Empty, string.Empty);

            using (Program.Context = new BoardContext())
            {
                Program.StartInboxTimer(1);

                var boardName = "Test Board " + DateTime.Now.ToString();
                SendMessage("join", string.Empty, boardName);

                using (var timer = new Timer(10000))
                {
                    timer.Elapsed += Timer_Elapsed;
                    timer.Start();

                    while (timer.Enabled)
                    {
                        if (Program.Context.Boards.Find(boardName) != null)
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Tests using the leave board command
        /// </summary>
        [TestMethod]
        public void LeaveBoardTest()
        {
            Program.LoadConfig();
            Program.PopulateWordlists(string.Empty, string.Empty);

            using (Program.Context = new BoardContext())
            {
                Program.StartInboxTimer(1);

                var mailboxAddress = Program.FormatMailboxAddress("user");

                var board = new Board("Test Board " + DateTime.Now.ToString());
                var user = board.AddUser(mailboxAddress.Address);
                Assert.IsNotNull(user, "Failed to add user");

                var message = SendMessage("leave", "user", board.Name);

                using (var timer = new Timer(10000))
                {
                    timer.Elapsed += Timer_Elapsed;
                    timer.Start();

                    while (timer.Enabled)
                    {
                        if (!board.Users.Contains(user))
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Tests using the leave command to delete a board
        /// </summary>
        [TestMethod]
        public void DeleteBoardTest()
        {
            Program.LoadConfig();
            Program.PopulateWordlists(string.Empty, string.Empty);

            using (Program.Context = new BoardContext())
            {
                Program.StartInboxTimer(1);

                var board = new Board("Test Board " + DateTime.Now.ToString());
                string owner = "owner@mail.local";
                Assert.IsNotNull(board.AddUser(owner), "Failed to add owner");
                Assert.IsNotNull(board.Users.FirstOrDefault(u => u.Address == owner).Role = UserRole.Owner, "Owner not found");

                SendMessage("leave", "owner", board.Name);

                using (var timer = new Timer(10000))
                {
                    timer.Elapsed += Timer_Elapsed;
                    timer.Start();

                    while (timer.Enabled)
                    {
                        if (Program.Context.Boards.Find(board.Name) == null)
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Tests using the admin command
        /// </summary>
        [TestMethod]
        public void AdminCommand()
        {
            Program.LoadConfig();
            Program.PopulateWordlists(string.Empty, string.Empty);

            using (Program.Context = new BoardContext())
            {
                Program.StartInboxTimer(1);

                var board = new Board("Test Board " + DateTime.Now.ToString());
                string owner = "owner@mail.local";
                Assert.IsNotNull(board.AddUser(owner), "Failed to add owner");
                Assert.IsNotNull(board.Users.FirstOrDefault(u => u.Address == owner).Role = UserRole.Owner, "Owner not found");

                SendMessage("admin", string.Empty, board.Name);

                using (var timer = new Timer(10000))
                {
                    timer.Elapsed += Timer_Elapsed;
                    timer.Start();

                    while (timer.Enabled)
                    {
                        if (messages.Count > 0)
                        {
                            if (messages.Dequeue().Bcc.Contains(Program.FormatMailboxAddress("admin")))
                                break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Tests using the join command
        /// </summary>
        [TestMethod]
        public void JoinCommandTest()
        {
            Program.LoadConfig();
            Program.PopulateWordlists(string.Empty, string.Empty);

            using (Program.Context = new BoardContext())
            {
                Program.StartInboxTimer(1);

                SendMessage("join", string.Empty, string.Empty);

                using (var timer = new Timer(10000))
                {
                    timer.Elapsed += Timer_Elapsed;
                    timer.Start();

                    while (timer.Enabled)
                    {
                        if (messages.Count > 0)
                        {
                            if (messages.Dequeue().Subject == "General Help")
                                break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Tests using the leave command
        /// </summary>
        [TestMethod]
        public void LeaveCommandTest()
        {
            Program.LoadConfig();
            Program.PopulateWordlists(string.Empty, string.Empty);

            using (Program.Context = new BoardContext())
            {
                Program.StartInboxTimer(1);

                var mailboxAddress = Program.FormatMailboxAddress("user");

                var board1 = new Board("Test Board1 " + DateTime.Now.ToString());
                var board1user = board1.AddUser(mailboxAddress.Address);
                Assert.IsNotNull(board1user, "Failed to add user to board1");

                var board2 = new Board("Test Board2 " + DateTime.Now.ToString());
                var board2user = board2.AddUser(mailboxAddress.Address);
                Assert.IsNotNull(board2user, "Failed to add user to board2");

                SendMessage("leave", "user", string.Empty);

                using (var timer = new Timer(10000))
                {
                    timer.Elapsed += Timer_Elapsed;
                    timer.Start();

                    while (timer.Enabled)
                    {
                        if (!board1.Users.Contains(board1user) && !board2.Users.Contains(board2user))
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Tests using the rules command
        /// </summary>
        [TestMethod]
        public void RulesCommandTest()
        {
            Program.LoadConfig();
            Program.PopulateWordlists(string.Empty, string.Empty);

            using (Program.Context = new BoardContext())
            {
                Program.StartInboxTimer(1);

                SendMessage("rules", string.Empty, string.Empty);

                using (var timer = new Timer(10000))
                {
                    timer.Elapsed += Timer_Elapsed;
                    timer.Start();

                    while (timer.Enabled)
                    {
                        if (messages.Count > 0)
                        {
                            if (messages.Dequeue().Subject == "General Rules")
                                break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Tests using the boards command
        /// </summary>
        [TestMethod]
        public void BoardsCommandTest()
        {
            Program.LoadConfig();
            Program.PopulateWordlists(string.Empty, string.Empty);

            using (Program.Context = new BoardContext())
            {
                Program.StartInboxTimer(1);

                // no tags. FAIL
                var board1 = new Board("Test Board1 " + DateTime.Now.ToString());

                // one of the tags. FAIL
                var board2 = new Board("Test Board2 " + DateTime.Now.ToString());
                board2.Tags.Add("tag1");

                // both tags. PASS
                var board3 = new Board("Test Board3 " + DateTime.Now.ToString());
                board3.Tags.Add("tag1");
                board3.Tags.Add("tag2");

                // both tags with an additional one. PASS
                var board4 = new Board("Test Board4 " + DateTime.Now.ToString());
                board4.Tags.Add("tag1");
                board4.Tags.Add("tag2");
                board4.Tags.Add("tag3");

                SendMessage("boards:tag1,tag2", string.Empty, string.Empty);

                using (var timer = new Timer(10000))
                {
                    timer.Elapsed += Timer_Elapsed;
                    timer.Start();

                    while (timer.Enabled)
                    {
                        if (messages.Count > 0)
                        {
                            var bodyText = messages.Dequeue().TextBody;

                            Assert.IsFalse(bodyText.Contains(board1.Name));
                            Assert.IsFalse(bodyText.Contains(board2.Name));

                            if (bodyText.Contains(board3.Name) && bodyText.Contains(board4.Name))
                                break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Tests the help command
        /// </summary>
        [TestMethod]
        public void HelpCommandTest()
        {
            Program.LoadConfig();
            Program.PopulateWordlists(string.Empty, string.Empty);

            using (Program.Context = new BoardContext())
            {
                Program.StartInboxTimer(1);

                SendMessage("help", string.Empty, string.Empty);

                using (var timer = new Timer(10000))
                {
                    timer.Elapsed += Timer_Elapsed;
                    timer.Start();

                    while (timer.Enabled)
                    {
                        if (messages.Count > 0)
                        {
                            if (messages.Dequeue().Subject == "General Help")
                                break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Timer to tell tests to end
        /// </summary>
        /// <param name="sender"> Object that called the event</param>
        /// <param name="e">Event arguments</param>
        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            ((Timer)sender).Stop();
            Assert.Fail("Time expired");
        }

        private static MimeMessage SendMessage(string subject, string from, string to)
        {
            var message = new MimeMessage();

            if (string.IsNullOrEmpty(from))
                message.From.Add(Program.FormatMailboxAddress("Test User " + DateTime.Now.ToString()));
            else
                message.From.Add(Program.FormatMailboxAddress(from));

            message.To.Add(Program.FormatMailboxAddress(to));
            message.Body = new TextPart("plain");
            message.Subject = subject;

            // send message
            Program.Outbox.SendMessage(message);
            Program.Outbox.SendMessages(null, null);

            return message;
        }
    }
}