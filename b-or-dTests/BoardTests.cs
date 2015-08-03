//-----------------------------------------------------------------------
// <copyright file="BoardTests.cs" company="Company">
//     Copyright (c) Ethan Vandersaul, Company. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace B_or_d.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Mail;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using MimeKit;

    /// <summary>
    /// Tests board methods
    /// </summary>
    [TestClass]
    public class BoardTests
    {
        /// <summary>
        /// Tests adding a new board
        /// </summary>
        [TestMethod]
        public void NewBoardTest()
        {
            Program.LoadConfig();
            Program.PopulateWordlists(string.Empty, string.Empty);

            using (Program.Context = new BoardContext())
            {
                string normalName = "Test Board " + DateTime.Now.ToString();

                new Board(normalName);
                new Board(normalName);
                new Board(string.Empty);

                Assert.IsNotNull(Program.Context.Boards.Local.FirstOrDefault(b => b.Name == normalName), "Normal name");
                Assert.IsTrue(Program.Context.Boards.Local.Count(b => b.Name == normalName) == 1, "Duplicate name");
                Assert.IsNull(Program.Context.Boards.Local.FirstOrDefault(b => string.IsNullOrEmpty(b.Name)), "Empty name");
            }
        }

        /// <summary>
        /// Tests adding a user
        /// </summary>
        [TestMethod]
        public void AddUserTest()
        {
            Program.LoadConfig();
            Program.PopulateWordlists(string.Empty, string.Empty);

            using (Program.Context = new BoardContext())
            {
                // create a new board for testing
                var board = new Board("Test Board " + DateTime.Now.ToString());

                Assert.IsNull(board.AddUser("invalid"), "Invalid address");
                Assert.IsNotNull(board.AddUser("valid@mail.local"), "Valid address");
                Assert.IsNull(board.AddUser("valid@mail.local"), "Duplicate address");
            }
        }

        /// <summary>
        /// Tests removing a user
        /// </summary>
        [TestMethod]
        public void RemoveUserTest()
        {
            Program.LoadConfig();
            Program.PopulateWordlists(string.Empty, string.Empty);

            using (Program.Context = new BoardContext())
            {
                // create a new board for testing
                var board = new Board("Test Board " + DateTime.Now.ToString());

                // add user
                Assert.IsNotNull(board.AddUser("valid@mail.local"), "Failed to add user");

                Assert.IsFalse(board.RemoveUser("invalid"), "Invalid address");
                Assert.IsTrue(board.RemoveUser("valid@mail.local"), "Valid member");
                Assert.IsFalse(board.RemoveUser("valid@mail.local"), "Non-member");

                // TODO: could be expanded upon in regards to owners
            }
        }

        /// <summary>
        /// Tests getting user of a certain role or higher
        /// </summary>
        [TestMethod]
        public void GetUsersOfRoleTest()
        {
            Program.LoadConfig();
            Program.PopulateWordlists(string.Empty, string.Empty);

            using (Program.Context = new BoardContext())
            {
                // create a board for testing
                var board = new Board("Test Board " + DateTime.Now.ToString());

                // create some users of each role
                string guest1 = "guest1@mail.local";
                Assert.IsNotNull(board.AddUser(guest1), "Failed to add guest1");
                board.Users.FirstOrDefault(u => u.Address == guest1).Role = UserRole.Guest;

                string guest2 = "guest2@mail.local";
                Assert.IsNotNull(board.AddUser(guest2), "Failed to add guest2");
                board.Users.FirstOrDefault(u => u.Address == guest2).Role = UserRole.Guest;

                string subscriber = "subscriber@mail.local";
                Assert.IsNotNull(board.AddUser(subscriber), "Failed to add mod");
                board.Users.FirstOrDefault(u => u.Address == subscriber).Role = UserRole.Subscriber;

                string mod = "mod@mail.local";
                Assert.IsNotNull(board.AddUser(mod), "Failed to add mod");
                board.Users.FirstOrDefault(u => u.Address == mod).Role = UserRole.Mod;

                string owner = "owner@mail.local";
                Assert.IsNotNull(board.AddUser(owner), "Failed to add owner");
                board.Users.FirstOrDefault(u => u.Address == owner).Role = UserRole.Owner;

                Assert.IsTrue(board.GetUsersOfRole(UserRole.Guest).SequenceEqual(Program.Context.Users.Local.Where(u => u.Role >= UserRole.Guest)), "User role guest");
                Assert.IsTrue(board.GetUsersOfRole(UserRole.Subscriber).SequenceEqual(Program.Context.Users.Local.Where(u => u.Role >= UserRole.Subscriber)), "User role subscriber");
                Assert.IsTrue(board.GetUsersOfRole(UserRole.Mod).SequenceEqual(Program.Context.Users.Local.Where(u => u.Role >= UserRole.Mod)), "User role mod");
                Assert.IsTrue(board.GetUsersOfRole(UserRole.Owner).SequenceEqual(Program.Context.Users.Local.Where(u => u.Role >= UserRole.Owner)), "User role owner");
            }
        }

        /// <summary>
        /// Tests posting
        /// </summary>
        [TestMethod]
        public void PostTest()
        {
            Program.LoadConfig();
            Program.PopulateWordlists(string.Empty, string.Empty);

            using (Program.Context = new BoardContext())
            {
                // create a board for testing
                var board = new Board("Test Board " + DateTime.Now.ToString());

                // add an owner
                Assert.IsNotNull(board.AddUser("owner@mail.local"), "Failed to add owner");

                // create new message
                var originalMessage = new MailMessage("user@mail.local", Program.FormatMailboxAddress(board.Name).Address, "Test", string.Empty);

                // passed  message
                MimeMessage message;

                try
                {
                    // null message
                    board.Post(null);
                }
                catch
                {
                    Assert.Fail("Null message");
                }

                // non-member sender
                message = MimeMessage.CreateFromMailMessage(originalMessage);
                board.Post(message);
                Assert.IsTrue(Program.Outbox.Messages.Dequeue().Subject == "PM: Test", "Non-member sender");

                // add user
                var user = board.AddUser("user@mail.local");
                Assert.IsNotNull(user, "Failed to add user");
                user.Role = UserRole.Guest;

                // guest post
                message = MimeMessage.CreateFromMailMessage(originalMessage);
                board.Post(message);
                Assert.IsTrue(Program.Outbox.Messages.Dequeue().Subject == "PM: Test", "Guest post");

                // unverified subscriber post
                user.Role = UserRole.Subscriber;
                message = MimeMessage.CreateFromMailMessage(originalMessage);
                board.Post(message);
                Assert.IsTrue(Program.GetAddress(Program.Outbox.Messages.Dequeue().To.First()) == "owner@mail.local", "Unverified subscriber post");

                // verified subscriber post
                user.Points = board.Points;
                message = MimeMessage.CreateFromMailMessage(originalMessage);
                board.Post(message);
                Assert.IsTrue(Program.Outbox.Messages.Dequeue().Subject == "Test", "Verified subscriber post");

                // mod post
                user.Role = UserRole.Mod;
                message = MimeMessage.CreateFromMailMessage(originalMessage);
                board.Post(message);
                Assert.IsTrue(Program.Outbox.Messages.Dequeue().Subject == "Test", "Mod post");

                // mod with incorrect sender
                message = MimeMessage.CreateFromMailMessage(originalMessage);
                message.Sender.Name = "bad";
                board.Post(message);
                Assert.IsTrue(Program.Outbox.Messages.Dequeue().Subject == "Test", "Mod with incorrect sender");

                // mod with correct sender with forwarded message
                message = MimeMessage.CreateFromMailMessage(originalMessage);
                message.Sender = new MailboxAddress(user.Name, string.Empty);
                board.Post(message);
                Assert.IsTrue(user.Points == board.Points + 1, "Mod with correct sender with forwarded message");

                // mod with correct sender with reply message
                message = MimeMessage.CreateFromMailMessage(originalMessage);
                message.ReplyTo.Add(message.From[0]);
                board.Post(message);
                Assert.IsTrue(user.Points == board.Points - 1, "Mod with correct sender with reply message");

                // owner post
                user.Role = UserRole.Owner;
                message = MimeMessage.CreateFromMailMessage(originalMessage);
                board.Post(message);
                Assert.IsTrue(Program.Outbox.Messages.Dequeue().Subject == "Test", "Owner post");
            }
        }

        /// <summary>
        /// Tests reporting a user
        /// </summary>
        [TestMethod]
        public void ReportTest()
        {
            Program.LoadConfig();
            Program.PopulateWordlists(string.Empty, string.Empty);

            using (Program.Context = new BoardContext())
            {
                // create a new board with normal name
                var board = new Board("Test Board " + DateTime.Now.ToString());

                Assert.IsNotNull(board.AddUser("member@mail.local"), "Failed to add user");

                Assert.IsTrue(board.Report("member@mail.local"), "Member");
                Assert.IsFalse(board.Report(string.Empty), "Empty");
                Assert.IsFalse(board.Report("nonmember@mail.local"), "Non-member");
            }
        }

        /// <summary>
        /// Tests message handling
        /// </summary>
        [TestMethod]
        public void HandleMessageTest()
        {
            Program.LoadConfig();
            Program.PopulateWordlists(string.Empty, string.Empty);

            using (Program.Context = new BoardContext())
            {
                string ownerAddress = "owner@mail.local";
                string subscriberAddress = "subscriber@mail.local";

                // create new board
                var board = new Board("Test Board " + DateTime.Now.ToString());

                // add an owner to the board
                var owner = board.AddUser(ownerAddress);
                Assert.IsNotNull(owner, "Failed to add owner");
                owner.Role = UserRole.Owner;

                Assert.IsFalse(board.HandleMessage(null), "Empty message not handled correctly");

                // create a new message
                var message = new MimeMessage();

                // join
                message.From.Add(new MailboxAddress(string.Empty, subscriberAddress));
                message.Subject = "join";
                Assert.IsTrue(board.HandleMessage(message), "Join command not handled correctly");
                Assert.IsNotNull(board.Users?.FirstOrDefault(u => u.Address == subscriberAddress), "Join command failed to add user");

                // rules
                message.From[0] = new MailboxAddress(string.Empty, subscriberAddress);
                message.Subject = "rules";
                Assert.IsTrue(board.HandleMessage(message), "Rules command not handled correctly");

                // leave
                message.From[0] = new MailboxAddress(string.Empty, subscriberAddress);
                message.Subject = "leave";
                Assert.IsTrue(board.HandleMessage(message), "Leave command not handled correctly");
                Assert.IsNull(board.Users?.FirstOrDefault(u => u.Address == subscriberAddress), "Leave command failed to remove user");

                // add user for testing
                var user = board.AddUser(subscriberAddress);
                Assert.IsNotNull(user, "Failed to add user");

                // points
                var points = board.Points;

                message.From[0] = new MailboxAddress(string.Empty, subscriberAddress);
                message.Subject = "points";
                Assert.IsTrue(board.HandleMessage(message), "Points command for non-owner not handled correctly");
                Assert.AreEqual(board.Points, points, "Points command for non-owner changed value");

                message.From[0] = new MailboxAddress(string.Empty, ownerAddress);
                message.Subject = "points";
                Assert.IsTrue(board.HandleMessage(message), "Points command for owner without value not handled correctly");
                Assert.AreEqual(board.Points, points, "Points command for owner without value changed value");

                message.From[0] = new MailboxAddress(string.Empty, ownerAddress);
                message.Subject = "points:non-number";
                Assert.IsTrue(board.HandleMessage(message), "Points command for owner with bad value not handled correctly");
                Assert.AreEqual(board.Points, points, "Points command for owner with bad value changed value");

                message.From[0] = new MailboxAddress(string.Empty, ownerAddress);
                message.Subject = "points:" + (points + 1).ToString();
                Assert.IsTrue(board.HandleMessage(message), "Points command for owner not handled correctly");
                Assert.AreEqual(board.Points, points + 1, "Points command for owner failed to change value");

                // defaultrole
                var defaultRole = board.DefaultUserRole;

                message.From[0] = new MailboxAddress(string.Empty, subscriberAddress);
                message.Subject = "defaultrole";
                Assert.IsTrue(board.HandleMessage(message), "Default Role command for non-owner not handled correctly");
                Assert.AreEqual(board.DefaultUserRole, defaultRole, "Default Role command for non-owner changed value");

                message.From[0] = new MailboxAddress(string.Empty, ownerAddress);
                message.Subject = "defaultrole";
                Assert.IsTrue(board.HandleMessage(message), "Default Role command for owner without value not handled correctly");
                Assert.AreEqual(board.DefaultUserRole, defaultRole, "Default Role command for owner without value changed value");

                message.From[0] = new MailboxAddress(string.Empty, ownerAddress);
                message.Subject = "defaultrole:none";
                Assert.IsTrue(board.HandleMessage(message), "Default Role command for owner with bad value not handled correctly");
                Assert.AreEqual(board.DefaultUserRole, defaultRole, "Default Role command for owner with bad value changed value");

                message.From[0] = new MailboxAddress(string.Empty, ownerAddress);
                message.Subject = "defaultrole:guest";
                Assert.IsTrue(board.HandleMessage(message), "Default Role command for owner not handled correctly");
                Assert.AreEqual(board.DefaultUserRole, UserRole.Guest, "Default Role command for owner failed to change value");

                // tags
                message.From[0] = new MailboxAddress(string.Empty, subscriberAddress);
                message.Subject = "tags:tag1,tag2";
                Assert.IsTrue(board.HandleMessage(message), "Tags command for non-owner not handled correctly");
                Assert.AreEqual(board.Tags.Count, 0, "Tags command for non-owner changed value");

                message.From[0] = new MailboxAddress(string.Empty, ownerAddress);
                message.Subject = "tags";
                Assert.IsTrue(board.HandleMessage(message), "Tags command for owner without value not handled correctly");
                Assert.AreEqual(board.Tags.Count, 0, "Tags command for owner without value changed value");

                message.From[0] = new MailboxAddress(string.Empty, ownerAddress);
                message.Subject = "tags:,";
                Assert.IsTrue(board.HandleMessage(message), "Tags command for owner with bad value not handled correctly");
                Assert.AreEqual(board.Tags.Count, 0, "Tags command for owner with bad value changed value");

                message.From[0] = new MailboxAddress(string.Empty, ownerAddress);
                message.Subject = "tags:tag1,tag2";
                Assert.IsTrue(board.HandleMessage(message), "Tags command for owner not handled correctly");
                Assert.IsTrue(board.Tags.SetEquals(new string[] { "TAG1", "TAG2" }), "Tags command for owner failed to change value");

                // guest
                user.Role = UserRole.Subscriber;
                message.From[0] = new MailboxAddress(string.Empty, subscriberAddress);
                message.Subject = "guest";
                Assert.IsTrue(board.HandleMessage(message), "Guest command for non-owner not handled correctly");
                Assert.AreNotEqual(user.Role, UserRole.Guest, "Guest command for non-owner changed value");

                user.Role = UserRole.Subscriber;
                message.From[0] = new MailboxAddress(string.Empty, ownerAddress);
                message.Subject = "guest";
                Assert.IsTrue(board.HandleMessage(message), "Guest command for owner without value not handled correctly");
                Assert.AreNotEqual(user.Role, UserRole.Guest, "Guest command for owner without value changed value");

                user.Role = UserRole.Subscriber;
                message.From[0] = new MailboxAddress(string.Empty, ownerAddress);
                message.Subject = "guest:none";
                Assert.IsTrue(board.HandleMessage(message), "Guest command for owner with bad value not handled correctly");
                Assert.AreNotEqual(user.Role, UserRole.Guest, "Guest command for owner with bad value changed value");

                user.Role = UserRole.Subscriber;
                message.From[0] = new MailboxAddress(string.Empty, ownerAddress);
                message.Subject = "guest:" + user.Name;
                Assert.IsTrue(board.HandleMessage(message), "Guest command for owner not handled correctly");
                Assert.AreEqual(user.Role, UserRole.Guest, "Guest command for owner failed to change value");

                // subscriber
                user.Role = UserRole.Guest;
                message.From[0] = new MailboxAddress(string.Empty, subscriberAddress);
                message.Subject = "subscriber";
                Assert.IsTrue(board.HandleMessage(message), "Subscriber command for non-owner not handled correctly");
                Assert.AreNotEqual(user.Role, UserRole.Subscriber, "Subscriber command for non-owner changed value");

                user.Role = UserRole.Guest;
                message.From[0] = new MailboxAddress(string.Empty, ownerAddress);
                message.Subject = "subscriber";
                Assert.IsTrue(board.HandleMessage(message), "Subscriber command for owner without value not handled correctly");
                Assert.AreNotEqual(user.Role, UserRole.Subscriber, "Subscriber command for owner without value changed value");

                user.Role = UserRole.Guest;
                message.From[0] = new MailboxAddress(string.Empty, ownerAddress);
                message.Subject = "subscriber:none";
                Assert.IsTrue(board.HandleMessage(message), "Subscriber command for owner with bad value not handled correctly");
                Assert.AreNotEqual(user.Role, UserRole.Subscriber, "Subscriber command for owner with bad value changed value");

                user.Role = UserRole.Guest;
                message.From[0] = new MailboxAddress(string.Empty, ownerAddress);
                message.Subject = "subscriber:" + user.Name;
                Assert.IsTrue(board.HandleMessage(message), "Subscriber command for owner not handled correctly");
                Assert.AreEqual(user.Role, UserRole.Subscriber, "Subscriber command for owner failed to change value");

                // mod
                user.Role = UserRole.Subscriber;
                message.From[0] = new MailboxAddress(string.Empty, subscriberAddress);
                message.Subject = "mod";
                Assert.IsTrue(board.HandleMessage(message), "Mod command for non-owner not handled correctly");
                Assert.AreNotEqual(user.Role, UserRole.Mod, "Mod command for non-owner changed value");

                user.Role = UserRole.Subscriber;
                message.From[0] = new MailboxAddress(string.Empty, ownerAddress);
                message.Subject = "mod";
                Assert.IsTrue(board.HandleMessage(message), "Mod command for owner without value not handled correctly");
                Assert.AreNotEqual(user.Role, UserRole.Mod, "Mod command for owner without value changed value");

                user.Role = UserRole.Subscriber;
                message.From[0] = new MailboxAddress(string.Empty, ownerAddress);
                message.Subject = "mod:none";
                Assert.IsTrue(board.HandleMessage(message), "Mod command for owner with bad value not handled correctly");
                Assert.AreNotEqual(user.Role, UserRole.Mod, "Mod command for owner with bad value changed value");

                user.Role = UserRole.Subscriber;
                message.From[0] = new MailboxAddress(string.Empty, ownerAddress);
                message.Subject = "mod:" + user.Name;
                Assert.IsTrue(board.HandleMessage(message), "Mod command for owner not handled correctly");
                Assert.AreEqual(user.Role, UserRole.Mod, "Mod command for owner failed to change value");

                // owner
                user.Role = UserRole.Subscriber;
                message.From[0] = new MailboxAddress(string.Empty, subscriberAddress);
                message.Subject = "owner";
                Assert.IsTrue(board.HandleMessage(message), "Owner command for non-owner not handled correctly");
                Assert.AreNotEqual(user.Role, UserRole.Owner, "Owner command for non-owner changed value");

                user.Role = UserRole.Subscriber;
                message.From[0] = new MailboxAddress(string.Empty, ownerAddress);
                message.Subject = "owner";
                Assert.IsTrue(board.HandleMessage(message), "Owner command for owner without value not handled correctly");
                Assert.AreNotEqual(user.Role, UserRole.Owner, "Owner command for owner without value changed value");

                user.Role = UserRole.Subscriber;
                message.From[0] = new MailboxAddress(string.Empty, ownerAddress);
                message.Subject = "owner:none";
                Assert.IsTrue(board.HandleMessage(message), "Owner command for owner with bad value not handled correctly");
                Assert.AreNotEqual(user.Role, UserRole.Owner, "Owner command for owner with bad value changed value");

                user.Role = UserRole.Subscriber;
                message.From[0] = new MailboxAddress(string.Empty, ownerAddress);
                message.Subject = "owner:" + user.Name;
                Assert.IsTrue(board.HandleMessage(message), "Owner command for owner not handled correctly");
                Assert.AreEqual(user.Role, UserRole.Owner, "Owner command for owner failed to change value");
            }
        }
    }
}