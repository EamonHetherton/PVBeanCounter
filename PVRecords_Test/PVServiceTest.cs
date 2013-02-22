/*
* Copyright (c) 2010 Dennis Mackay-Fisher
*
* This file is part of PV Scheduler
* 
* PV Scheduler is free software: you can redistribute it and/or 
* modify it under the terms of the GNU General Public License version 2 as published 
* by the Free Software Foundation.
* 
* PV Scheduler is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
* GNU General Public License for more details.
* 
* You should have received a copy of the GNU General Public License
* along with PV Scheduler.
* If not, see <http://www.gnu.org/licenses/>.
*/

using PVService;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System;

namespace PVRecords_Test
{
    
    
    /// <summary>
    ///This is a test class for PVServiceTest and is intended
    ///to contain all PVServiceTest Unit Tests
    ///</summary>
    [TestClass()]
    public class PVServiceTest
    {
        private PVService_Accessor target;


        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        // 
        //You can use the following additional attributes as you write your tests:
        //
        //Use ClassInitialize to run code before running the first test in the class
        //[ClassInitialize()]
        //public static void MyClassInitialize(TestContext testContext)
        //{
        //}
        //
        //Use ClassCleanup to run code after all tests in a class have run
        //[ClassCleanup()]
        //public static void MyClassCleanup()
        //{
        //}
        //
        //Use TestInitialize to run code before running each test
        //[TestInitialize()]
        //public void MyTestInitialize()
        //{
        //}
        //
        //Use TestCleanup to run code after each test has run
        //[TestCleanup()]
        //public void MyTestCleanup()
        //{
        //}
        //
        #endregion


        /// <summary>
        ///A test for OnStart
        ///</summary>
        [TestMethod()]
        [DeploymentItem("PVService.exe")]
        public void OnStartTest()
        {
            target = new PVService_Accessor(); // TODO: Initialize to an appropriate value
            string[] args = new string[4];
            args[0] = "localhost";
            args[1] = "pvhistory";
            args[2] = "PVRecords";
            args[3] = "jethrotull";

            Thread thread = new Thread(StartThread);
            thread.Start();

            Thread.Sleep(100000000);
            target.OnStop();

            //Assert.Inconclusive("A method that does not return a value cannot be verified.");
        }

        public void StartThread()
        {
            //target = new PVService_Accessor(); // TODO: Initialize to an appropriate value
            string[] args = new string[4];
            args[0] = "localhost";
            args[1] = "pvhistory";
            args[2] = "PVRecords";
            args[3] = "jethrotull";
            target.OnStart(args);
        }
    }
}
