//-----------------------------------------------------------------------
// <copyright file="BoardTests.cs" company="Company">
//     Copyright (c) Ethan Vandersaul, Company. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace B_or_d.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

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
                string normalName = "test";

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
                var board = new Board("test");

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

        /*[TestMethod]
        public void PostTest()
        {


        }*/

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
                var board = new Board("test");

                Assert.IsNotNull(board.AddUser("member@mail.local"), "Failed to add user");

                Assert.IsTrue(board.Report("member@mail.local"), "Member");
                Assert.IsFalse(board.Report(string.Empty), "Empty");
                Assert.IsFalse(board.Report("nonmember@mail.local"), "Non-member");
            }
        }

        /// <summary>
        /// Tests command handling
        /// </summary>
        [TestMethod]
        public void HandleCommandTest()
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

                Assert.IsFalse(board.HandleCommand(string.Empty, subscriberAddress), "Empty command");
                Assert.IsFalse(board.HandleCommand("rules", string.Empty), "Empty user address");

                // join
                Assert.IsTrue(board.HandleCommand("join", subscriberAddress), "Join command");
                Assert.IsNotNull(board.Users?.FirstOrDefault(u => u.Address == subscriberAddress), "Join command failed to add user");

                // rules
                Assert.IsTrue(board.HandleCommand("rules", subscriberAddress), "Rules command");

                // leave
                Assert.IsTrue(board.HandleCommand("leave", subscriberAddress), "Leave command");
                Assert.IsNull(board.Users?.FirstOrDefault(u => u.Address == subscriberAddress), "Leave command failed to remove user");

                // add user for testing
                var user = board.AddUser(subscriberAddress);
                Assert.IsNotNull(user, "Failed to add user");

                // points
                Assert.IsFalse(board.HandleCommand("points", subscriberAddress), "Points command for non-owner");
                Assert.IsFalse(board.HandleCommand("points", ownerAddress), "Points command for owner without value");
                Assert.IsFalse(board.HandleCommand("points:non-number", ownerAddress), "Points command for owner with bad value");
                Assert.IsTrue(board.HandleCommand("points:99", ownerAddress), "Points command for owner");
                Assert.AreEqual(board.Points, 99, "Points command failed to change points");

                // defaultrole
                Assert.IsFalse(board.HandleCommand("defaultrole", subscriberAddress), "Default Role command for non-owner");
                Assert.IsFalse(board.HandleCommand("defaultrole", ownerAddress), "Default Role command for owner without value");
                Assert.IsFalse(board.HandleCommand("defaultrole:none", ownerAddress), "Default Role command for owner with bad value");
                Assert.IsTrue(board.HandleCommand("defaultrole:guest", ownerAddress), "Default Role command for owner");
                Assert.AreEqual(board.DefaultUserRole, UserRole.Guest, "Default Role command failed to change default role");

                // tags
                Assert.IsFalse(board.HandleCommand("tags", subscriberAddress), "Tags command for non-owner");
                Assert.IsFalse(board.HandleCommand("tags", ownerAddress), "Tags command for owner without value");
                Assert.IsFalse(board.HandleCommand("tags:,", ownerAddress), "Tags command for owner with bad value");
                Assert.IsTrue(board.HandleCommand("tags:tag1,tag2", ownerAddress), "Tags command for owner");
                Assert.AreEqual(board.Tags, new HashSet<string>(new string[]{ "tag1", "tag2" }), "Default Role command failed to change tags");

                // guest
                Assert.IsFalse(board.HandleCommand("guest", subscriberAddress), "Guest command for non-owner");
                Assert.IsFalse(board.HandleCommand("guest", ownerAddress), "Guest command for owner without value");
                Assert.IsFalse(board.HandleCommand("guest:none", ownerAddress), "Guest command for owner with bad value");
                Assert.IsTrue(board.HandleCommand("guest:" + user.Name, ownerAddress), "Guest command for owner");
                Assert.IsNull(board.Users?.FirstOrDefault(u => u.Address == subscriberAddress && u.Role == UserRole.Guest), "Guest command failed to change role");

                // subscriber
                Assert.IsFalse(board.HandleCommand("subscriber", subscriberAddress), "Subscriber command for non-owner");
                Assert.IsFalse(board.HandleCommand("subscriber", ownerAddress), "Subscriber command for owner without value");
                Assert.IsFalse(board.HandleCommand("subscriber:none", ownerAddress), "Subscriber command for owner with bad value");
                Assert.IsTrue(board.HandleCommand("subscriber:" + user.Name, ownerAddress), "Subscriber command for owner");
                Assert.IsNull(board.Users?.FirstOrDefault(u => u.Address == subscriberAddress && u.Role == UserRole.Subscriber), "Subscriber command failed to change role");

                // mod
                Assert.IsFalse(board.HandleCommand("mod", subscriberAddress), "Mod command for non-owner");
                Assert.IsFalse(board.HandleCommand("mod", ownerAddress), "Mod command for owner without value");
                Assert.IsFalse(board.HandleCommand("mod:none", ownerAddress), "Mod command for owner with bad value");
                Assert.IsTrue(board.HandleCommand("mod:" + user.Name, ownerAddress), "Mod command for owner");
                Assert.IsNull(board.Users?.FirstOrDefault(u => u.Address == subscriberAddress && u.Role == UserRole.Mod), "Mod command failed to change role");

                // owner
                Assert.IsFalse(board.HandleCommand("owner", subscriberAddress), "Owner command for non-owner");
                Assert.IsFalse(board.HandleCommand("owner", ownerAddress), "Owner command for owner without value");
                Assert.IsFalse(board.HandleCommand("owner:none", ownerAddress), "Owner command for owner with bad value");
                Assert.IsTrue(board.HandleCommand("owner:" + user.Name, ownerAddress), "Owner command for owner");
                Assert.IsNull(board.Users?.FirstOrDefault(u => u.Address == subscriberAddress && u.Role == UserRole.Owner), "Owner command failed to change role");

            }
        }
    }
}