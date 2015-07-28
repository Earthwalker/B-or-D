//-----------------------------------------------------------------------
// <copyright file="BoardTests.cs" company="Company">
//     Copyright (c) Ethan Vandersaul, Company. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace B_or_d.Tests
{
    using System;
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
            using (Program.Context = new BoardContext())
            {
                var board = new Board("test");

                Assert.IsFalse(board.HandleCommand(string.Empty, "user@mail.local"));
                Assert.IsFalse(board.HandleCommand("rules", string.Empty));
                Assert.IsTrue(board.HandleCommand("join", "user@mail.local"));
            }
        }
    }
}