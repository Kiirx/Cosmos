﻿using System;
using System.Collections.Specialized;
using System.IO;
using System.Threading;
using Cosmos.Build.Common;
using Cosmos.Debug.Common;
using Cosmos.Debug.VSDebugEngine.Host;

namespace Cosmos.TestRunner.Core
{
    partial class Engine
    {
        private const int AllowedSecondsInKernel = 10;

        private void RunIsoInBochs(string iso)
        {
            var xBochsConfig = Path.Combine(mBaseWorkingDirectory, "Kernel.bochsrc");
            var xParams = new NameValueCollection();

            xParams.Add(BuildProperties.EnableBochsDebugString, "false");
            xParams.Add("ISOFile", iso);
            xParams.Add(BuildProperties.VisualStudioDebugPortString, "Pipe: Cosmos\\Serial");

            var xDebugConnector = new DebugConnectorPipeServer("Cosmos\\Serial");

            xDebugConnector.CmdChannel = ChannelPacketReceived;
            xDebugConnector.CmdStarted = () =>
                                         {
                                             OutputHandler.LogMessage("DC: Started");
                                             xDebugConnector.SendCmd(Vs2Ds.BatchEnd);
                                         };
            xDebugConnector.Error = e =>
                                    {
                                        OutputHandler.LogMessage("DC Error: " + e.ToString());
                                        mBochsRunning = false;
                                    };
            xDebugConnector.CmdText += s => OutputHandler.LogMessage("Text from kernel: " + s);
            xDebugConnector.CmdMessageBox = s => OutputHandler.LogMessage("MessageBox from kernel: " + s);

            var xBochs = new Bochs(xParams, false, new FileInfo(xBochsConfig));
            xBochs.OnShutDown = (a, b) =>
                                {
                                };

            mBochsRunning = true;
            xBochs.Start();
            try
            {
                var xStartTime = DateTime.Now;
                mKernelResultSet = false;
                Interlocked.Exchange(ref mSucceededAssertions, 0);

                Console.WriteLine("Bochs started");
                while (mBochsRunning)
                {
                    Thread.Sleep(50);

                    if (Math.Abs(DateTime.Now.Subtract(xStartTime).TotalSeconds) > AllowedSecondsInKernel)
                    {
                        OutputHandler.SetKernelTestResult(false, "Timeout exceeded");
                        mKernelResultSet = true;
                        break;
                    }
                }
                if (!mKernelResultSet)
                {
                    OutputHandler.SetKernelTestResult(true, null);
                }
                OutputHandler.SetKernelSucceededAssertionsCount(mSucceededAssertions);
                Console.WriteLine("Stopping bochs now");
            }
            finally
            {
                xBochs.Stop();
                xDebugConnector.Dispose();
                Thread.Sleep(50);
            }
        }

        private volatile bool mBochsRunning = true;
        private volatile bool mKernelResultSet;
        private int mSucceededAssertions;

        private void ChannelPacketReceived(byte arg1, byte arg2, byte[] arg3)
        {
            if (arg1 == 129)
            {
                // for now, skip
                return;
            }
            if (arg1 != TestController.TestChannel)
            {
                throw new Exception("Unhandled channel " + arg1);
            }

            switch (arg2)
            {
                case (byte)TestChannelCommandEnum.TestCompleted:
                    mBochsRunning = false;
                    break;
                case (byte)TestChannelCommandEnum.TestFailed:
                    OutputHandler.SetKernelTestResult(false, "Test failed");
                    mKernelResultSet = true;
                    mBochsRunning = false;
                    break;
                case (byte)TestChannelCommandEnum.AssertionSucceeded:
                    Interlocked.Increment(ref mSucceededAssertions);
                    break;
                default:
                    throw new NotImplementedException("TestChannel command " + arg2 + " is not implemented!");
            }
        }
    }
}
