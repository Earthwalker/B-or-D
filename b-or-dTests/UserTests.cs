//-----------------------------------------------------------------------
// <copyright file="UserTests.cs" company="Company">
//     Copyright (c) Ethan Vandersaul, Company. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace B_or_d.Tests
{
    using System.Linq;
    using B_or_d;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests user methods
    /// </summary>
    [TestClass]
    public class UserTests
    {
        /// <summary>
        /// Tests adding a new user to a board
        /// </summary>
        [TestMethod]
        public void NewUserTest()
        {
            using (Program.Context = new BoardContext())
            {
                // create a board for testing
                var board = new Board("test");

                string normalAddress = "normal@mail.local";
                string badAddress = "bad";

                board.AddUser(normalAddress);
                Assert.IsNotNull(Program.Context.Users.Local.FirstOrDefault(u => u.Address == normalAddress), "Normal address");

                board.AddUser(normalAddress);
                Assert.IsTrue(Program.Context.Users.Local.Count == 1, "Duplicate address");

                board.AddUser(badAddress);
                Assert.IsNull(Program.Context.Users.Local.FirstOrDefault(u => u.Address == badAddress), "Bad address");

                board.AddUser(string.Empty);
                Assert.IsNull(Program.Context.Users.Local.FirstOrDefault(u => string.IsNullOrEmpty(u.Address)), "Blank  address");
            }
        }
    }
}